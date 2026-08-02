namespace Harnfig;

internal sealed class Installer(
    ConfigParser configParser,
    LockFileStore lockFileStore,
    IGitCheckoutService checkoutService,
    SkillValidator skillValidator)
{
    public async Task<InstallSummary> InstallAsync(
        InstallRequest request,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        if (request.Frozen && request.Update)
        {
            throw new ConfigurationException("--frozen and --update cannot be used together.");
        }

        var configPath = ConfigLocator.Find(request.WorkingDirectory, request.ConfigPath);
        var projectDirectory = Path.GetDirectoryName(configPath)!;
        var config = configParser.Parse(await File.ReadAllTextAsync(configPath, cancellationToken), configPath);
        var scope = request.ScopeOverride ?? config.Scope;
        var homeDirectory = request.HomeDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (scope == InstallScope.User && string.IsNullOrWhiteSpace(homeDirectory))
        {
            throw new HarnfigException("Could not determine the user home directory.");
        }

        var lockPath = Path.Combine(projectDirectory, "harnfig.lock");
        var existingLock = lockFileStore.Load(lockPath);
        var normalizedSpecs = config.Skills.Select(spec => new NormalizedSpec(spec, RepoReferenceParser.Parse(spec.Repo))).ToArray();
        ValidateFrozenInputs(request.Frozen, existingLock, normalizedSpecs);

        using var workspace = new TemporaryDirectory();
        var resolved = await ResolveSkillsAsync(normalizedSpecs, existingLock, request.Update, workspace.Path, cancellationToken);
        ValidateUniqueNames(resolved);

        var desiredLock = new HarnfigLock(1, resolved.Select(skill => new LockEntry(
            skill.Name,
            skill.Repo,
            skill.Path,
            skill.Ref,
            skill.Commit,
            skill.Digest)).ToArray());
        ValidateFrozenContent(request.Frozen, existingLock, desiredLock);

        var operations = BuildOperations(config.Agents, scope, projectDirectory, homeDirectory, resolved, existingLock, request.Force);
        var changed = operations.Where(operation => !operation.Unchanged).ToArray();
        var unchanged = operations.Length - changed.Length;
        var lockContents = lockFileStore.Serialize(desiredLock);
        var lockChanged = !File.Exists(lockPath) || !string.Equals(File.ReadAllText(lockPath), lockContents, StringComparison.Ordinal);

        foreach (var operation in operations)
        {
            var verb = operation.Unchanged ? "unchanged" : request.DryRun ? "would install" : "install";
            await output.WriteLineAsync($"{verb,-13} {operation.Skill.Name} -> {operation.Destination}");
        }

        if (!request.DryRun && (changed.Length > 0 || lockChanged))
        {
            using var transaction = new FileTransaction();
            var stagedOperations = changed.Select(operation => new
            {
                Operation = operation,
                Staged = transaction.StageDirectory(operation.Skill.SourceDirectory, operation.Destination),
            }).ToArray();
            var stagedLock = lockChanged ? transaction.StageFile(lockContents, lockPath) : null;

            foreach (var stagedOperation in stagedOperations)
            {
                transaction.Commit(stagedOperation.Staged, stagedOperation.Operation.Destination);
            }

            if (stagedLock is not null)
            {
                transaction.Commit(stagedLock, lockPath);
            }

            transaction.Complete();
        }

        var installed = changed.Length;
        await output.WriteLineAsync(request.DryRun
            ? $"dry run: {installed} change(s), {unchanged} unchanged"
            : $"done: {installed} installed, {unchanged} unchanged");
        return new InstallSummary(installed, unchanged);
    }

    private async Task<IReadOnlyList<ValidatedSkill>> ResolveSkillsAsync(
        IReadOnlyList<NormalizedSpec> specs,
        HarnfigLock? existingLock,
        bool update,
        string workspace,
        CancellationToken cancellationToken)
    {
        var requests = specs.Select((item, index) =>
        {
            var locked = FindMatchingLock(existingLock, item.Repository.Normalized, item.Spec.Path, item.Spec.Ref);
            var revision = !update && locked is not null ? locked.Commit : item.Spec.Ref;
            return new ResolveRequest(index, item, revision);
        }).ToArray();

        var checkouts = new Dictionary<string, GitCheckout>(StringComparer.Ordinal);
        foreach (var group in requests.GroupBy(item => $"{item.Spec.Repository.CloneUrl}\n{item.Revision}", StringComparer.Ordinal))
        {
            var first = group.First();
            var destination = Path.Combine(workspace, $"repo-{checkouts.Count}");
            checkouts[group.Key] = await checkoutService.CheckoutAsync(first.Spec.Repository, first.Revision, destination, cancellationToken);
        }

        var skills = new ValidatedSkill[requests.Length];
        foreach (var request in requests)
        {
            var key = $"{request.Spec.Repository.CloneUrl}\n{request.Revision}";
            var checkout = checkouts[key];
            var validated = skillValidator.Validate(checkout.Directory, request.Spec.Repository.Normalized, request.Spec.Spec.Path);
            skills[request.Index] = new ValidatedSkill(
                validated.Name,
                validated.Directory,
                request.Spec.Repository.Normalized,
                request.Spec.Spec.Path,
                request.Spec.Spec.Ref,
                checkout.Commit,
                DirectoryDigest.Calculate(validated.Directory));
        }

        return skills;
    }

    private static InstallOperation[] BuildOperations(
        IReadOnlyList<AgentKind> agents,
        InstallScope scope,
        string projectDirectory,
        string homeDirectory,
        IReadOnlyList<ValidatedSkill> skills,
        HarnfigLock? existingLock,
        bool force)
    {
        var operations = new List<InstallOperation>();
        foreach (var agent in agents)
        {
            var root = TargetMapper.GetSkillsRoot(agent, scope, projectDirectory, homeDirectory);
            foreach (var skill in skills)
            {
                var destination = Path.Combine(root, skill.Name);
                var oldEntry = existingLock?.Skills.SingleOrDefault(entry => string.Equals(entry.Name, skill.Name, StringComparison.Ordinal));
                var unchanged = false;

                if (FileTree.Exists(destination))
                {
                    if (!Directory.Exists(destination))
                    {
                        if (!force)
                        {
                            throw new HarnfigException($"Destination is not a directory: {destination}. Use --force to replace it.");
                        }
                    }
                    else
                    {
                        var currentDigest = DirectoryDigest.Calculate(destination);
                        var managed = oldEntry is not null && string.Equals(currentDigest, oldEntry.Digest, StringComparison.Ordinal);
                        if (!managed && !force)
                        {
                            throw new HarnfigException($"Destination is unmanaged or locally modified: {destination}. Use --force to replace it.");
                        }

                        unchanged = managed && string.Equals(currentDigest, skill.Digest, StringComparison.Ordinal);
                    }
                }

                operations.Add(new InstallOperation(agent, skill, destination, unchanged));
            }
        }

        return operations.ToArray();
    }

    private static void ValidateFrozenInputs(bool frozen, HarnfigLock? lockFile, IReadOnlyList<NormalizedSpec> specs)
    {
        if (!frozen)
        {
            return;
        }

        if (lockFile is null)
        {
            throw new ConfigurationException("--frozen requires an existing harnfig.lock.");
        }

        var matches = specs.All(spec => FindMatchingLock(lockFile, spec.Repository.Normalized, spec.Spec.Path, spec.Spec.Ref) is not null);
        if (!matches || lockFile.Skills.Count != specs.Count)
        {
            throw new ConfigurationException("harnfig.lock does not match harnfig.yaml and --frozen was specified.");
        }
    }

    private static void ValidateFrozenContent(bool frozen, HarnfigLock? existing, HarnfigLock desired)
    {
        if (!frozen || existing is null)
        {
            return;
        }

        var store = new LockFileStore();
        if (!string.Equals(store.Serialize(existing), store.Serialize(desired), StringComparison.Ordinal))
        {
            throw new HarnfigException("Resolved skill content does not match harnfig.lock and --frozen was specified.");
        }
    }

    private static LockEntry? FindMatchingLock(HarnfigLock? lockFile, string repo, string path, string @ref)
        => lockFile?.Skills.SingleOrDefault(entry =>
            string.Equals(entry.Repo, repo, StringComparison.Ordinal) &&
            string.Equals(entry.Path, path, StringComparison.Ordinal) &&
            string.Equals(entry.Ref, @ref, StringComparison.Ordinal));

    private static void ValidateUniqueNames(IReadOnlyList<ValidatedSkill> skills)
    {
        var duplicate = skills.GroupBy(skill => skill.Name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ConfigurationException($"Multiple configured skills resolve to the name '{duplicate.Key}'.");
        }
    }

    private sealed record NormalizedSpec(SkillSpec Spec, RepoReference Repository);

    private sealed record ResolveRequest(int Index, NormalizedSpec Spec, string Revision);

    private sealed record InstallOperation(AgentKind Agent, ValidatedSkill Skill, string Destination, bool Unchanged);
}
