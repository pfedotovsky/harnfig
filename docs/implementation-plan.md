# Harnfig v0.1 implementation plan

## Summary

Build harnfig as a small, open-source C# utility:

```text
harnfig.yaml → harnfig install → Cursor / Codex / Claude
```

- Repository: `github.com/pfedotovsky/harnfig`; the name was available when the project was created.
- NuGet package: `harnfig`; the package ID was available when the project was created.
- License: Apache-2.0.
- Runtime: .NET 10 LTS.
- Universal distribution: self-contained NativeAOT executables that do not require .NET to be installed.
- .NET distribution: `dotnet tool install --global harnfig`.
- Additional channels: GitHub Releases, Homebrew, and Scoop.
- Credit [gaal](https://github.com/getgaal/gaal) as the broader existing solution; position harnfig as a deliberately narrow, educational implementation.

## Public interface

### Configuration

```yaml
version: 1
scope: project

agents:
  - cursor
  - codex
  - claude

skills:
  - repo: mattpocock/skills
    path: skills/productivity/grill-me
    ref: main

  - repo: nicobailon/visual-explainer
    path: plugins/visual-explainer
    ref: main
```

Definitions:

- `repo`: the GitHub repository containing the skill.
- `path`: the directory inside that repository whose entry point is `SKILL.md`.
- Use `path: .` when the repository root is itself the skill.
- `ref`: branch, tag, or commit; omitted means the repository’s default branch.

Accepted `repo` forms:

```yaml
repo: mattpocock/skills
repo: https://github.com/mattpocock/skills.git
repo: git@github.com:mattpocock/skills.git
```

Validation:

- `version: 1` is required.
- `scope` is `project` or `user`; default is `project`.
- `agents` is a non-empty subset of `cursor`, `codex`, and `claude`.
- `repo` and `path` are required.
- Unknown YAML fields are errors.
- Duplicate entries and duplicate resulting skill names are errors.
- The skill name comes from `SKILL.md`; harnfig does not rename or transform skills.

### CLI

```text
harnfig install
harnfig install --dry-run
harnfig install --frozen
harnfig install --update
harnfig install --force
harnfig version
harnfig help
```

Options:

- `--config <path>`: explicit configuration; otherwise search from the current directory to the Git repository root.
- `--scope project|user`: override YAML scope.
- `--dry-run`: fetch, resolve, and validate without writing anything.
- `--frozen`: require an exact, current lockfile.
- `--update`: resolve mutable refs again and update the lockfile.
- `--force`: explicitly overwrite unmanaged or locally modified destinations.
- `--frozen` and `--update` cannot be combined.

Exit codes:

- `0`: success or idempotent no-op.
- `1`: Git, authentication, filesystem, or installation failure.
- `2`: invalid arguments or configuration.

### Installation paths

Project paths are relative to the directory containing `harnfig.yaml`.

| Agent | Project scope | User scope |
|---|---|---|
| Cursor | `.cursor/skills/<name>` | `~/.cursor/skills/<name>` |
| Codex | `.agents/skills/<name>` | `~/.agents/skills/<name>` |
| Claude | `.claude/skills/<name>` | `~/.claude/skills/<name>` |

Validate the portable core against the [Agent Skills specification](https://agentskills.io/specification). Preserve agent-specific frontmatter extensions without interpreting them.

### Lockfile

Generate deterministic `harnfig.lock` YAML:

```yaml
version: 1
skills:
  - name: grill-me
    repo: mattpocock/skills
    path: skills/productivity/grill-me
    ref: main
    commit: 0123456789abcdef0123456789abcdef01234567
    digest: sha256:...
```

The lockfile contains no credentials or machine-specific absolute paths. Commit `harnfig.yaml` and `harnfig.lock`.

## C# implementation

### Project foundation

Use one production console project and separate unit/integration test projects:

```text
src/Harnfig/
tests/Harnfig.Tests/
tests/Harnfig.IntegrationTests/
docs/
```

Production settings:

- Target `net10.0`, supported through November 2028 under the [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
- Enable nullable reference types, implicit usings, deterministic builds, and warnings as errors.
- Enable NativeAOT and its trimming/AOT analyzers.
- Use `System.CommandLine` 2.x; it is trim-friendly and suitable for AOT CLI applications. See the [System.CommandLine documentation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/).
- Use YamlDotNet 18.1.0 through its representation model.
- Parse YAML nodes manually rather than reflection-based object serialization, keeping NativeAOT behavior predictable.
- Use `System.Diagnostics.Process` for Git; do not add a Git client library.
- Use xUnit for tests.

Keep the architecture intentionally small:

```text
CLI
 ↓
Config and lockfile
 ↓
Git resolver
 ↓
Skill validator
 ↓
Installation planner
 ↓
Transactional filesystem writer
```

Introduce interfaces only around the Git process runner and filesystem transaction boundary.

### Installation pipeline

1. Locate and strictly parse `harnfig.yaml`.
2. Load and compare `harnfig.lock`.
3. Group skills by repository so each repository is fetched once.
4. Fetch the requested ref or locked commit into a temporary directory using system Git.
5. Validate the selected skill:
   - `SKILL.md` exists;
   - valid `name` and `description`;
   - frontmatter name matches its source directory;
   - `path` cannot escape the checkout;
   - only regular files and directories are accepted;
   - symlinks and special files are rejected in v0.1.
6. Calculate a stable SHA-256 directory digest.
7. Build the complete destination plan.
8. Detect unmanaged or locally modified destinations.
9. Stage all new directories beside their destinations.
10. Replace destinations and lockfile as one rollback-capable transaction.
11. Print a short installed/unchanged/failed summary.

Behavior:

- A second identical install is a no-op.
- Normal installation uses locked commits when available.
- `--update` refreshes refs.
- `--frozen` fails rather than modifying the lock.
- A failed multi-agent installation restores every previous destination.
- Removing an agent from YAML does not delete its old directory in v0.1.
- Harnfig never changes `.gitignore`.

### Security and private repositories

- Never execute downloaded scripts, hooks, or commands.
- Copy the complete skill directory unchanged, including its license and executable file permissions.
- Reject path traversal and symlink escapes.
- Reject HTTPS URLs with embedded credentials.
- Redact credentials from process output and errors.
- Delegate private-repository authentication to the user’s Git credential helper or SSH agent.
- Do not read, store, or manage GitHub tokens.

## Distribution

### NativeAOT executables

NativeAOT produces platform-specific, self-contained executables that run without an installed .NET runtime. See the [NativeAOT documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/).

Publish these runtime identifiers:

- `win-x64`
- `win-arm64`
- `linux-x64`
- `linux-arm64`
- `osx-x64`
- `osx-arm64`

Use native GitHub-hosted build environments where NativeAOT cross-compilation is unsupported. Fail the release on trimming or AOT warnings.

Release archives:

```text
harnfig-v0.1.0-win-x64.zip
harnfig-v0.1.0-linux-x64.tar.gz
harnfig-v0.1.0-osx-arm64.tar.gz
SHA256SUMS
```

GitHub Actions will:

1. Build and test the regular .NET application.
2. Publish every NativeAOT runtime.
3. Run each produced executable with `harnfig version` where the runner architecture permits it.
4. Archive binaries and calculate SHA-256 checksums.
5. Create a GitHub Release from a `v*` tag.
6. Generate GitHub build-provenance attestations.
7. Publish the NuGet tool.
8. Update Homebrew and Scoop manifests.

### NuGet/.NET tool

Configure the console project as a tool package:

```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>harnfig</ToolCommandName>
<PackageId>harnfig</PackageId>
```

Installation:

```bash
dotnet tool install --global harnfig
harnfig install
```

The .NET 10 one-off execution path is:

```bash
dnx harnfig -- install
```

The NuGet tool is framework-dependent; NativeAOT releases remain the default recommendation for users without .NET.

### Homebrew and Scoop

- Extend the existing `pfedotovsky/homebrew-tap` with `Formula/harnfig.rb`.
- Create `pfedotovsky/scoop-bucket` containing `bucket/harnfig.json`.
- Use a fine-grained GitHub token scoped only to those two repositories for manifest updates.
- Never overwrite unrelated files in the existing Homebrew tap.

Commands:

```bash
brew install pfedotovsky/tap/harnfig
```

```powershell
scoop bucket add pfedotovsky https://github.com/pfedotovsky/scoop-bucket
scoop install harnfig
```

Defer curl and PowerShell bootstrap scripts until signature verification is designed.

## Repository documentation

Create:

- README with the two-skill example and a 60-second talk demo.
- `docs/implementation-plan.md` containing this plan.
- `docs/alternatives.md` comparing gaal, Vercel `skills`, `gh skill install`, and OpenSkills.
- `docs/architecture.md` explaining the compact processing pipeline.
- `LICENSE`, `CONTRIBUTING.md`, and `SECURITY.md`.
- Example configuration and lockfile fixtures.

README positioning:

- Harnfig is a teaching-quality, usable implementation of declarative Agent Skill installation.
- It is not a universal agent configuration manager.
- Gaal already offers broader configuration covering skills, MCP, repositories, content, and hooks.
- Harnfig stays skills-only so the complete implementation can be understood during a .NET talk.

Explicitly defer:

- Non-GitHub hosts and GitHub Enterprise.
- Registry, search, ratings, and marketplace features.
- MCP, hooks, plugins, and arbitrary content synchronization.
- Automatic agent detection.
- Uninstall, pruning, self-update, and telemetry.
- Skill transformation or conversion between agent-specific formats.

## Test plan

Unit tests:

- Strict YAML parsing and defaults.
- `repo`, `path`, and `ref` validation.
- GitHub HTTPS, shorthand, and SSH normalization.
- Agent Skills frontmatter validation.
- Deterministic lockfile generation.
- Directory digest stability.
- Target-path mapping.
- Credential redaction.

Integration tests use temporary local Git repositories and temporary home directories:

- Install one and multiple skills.
- Install two skills from one repository with one fetch.
- Install for each agent and both scopes.
- Repeat installation as a no-op.
- Update a moved branch.
- Reject stale locks under `--frozen`.
- Reject path traversal, symlinks, and special files.
- Reject unmanaged and modified targets.
- Verify `--force`.
- Roll back a partially failed multi-target commit.
- Verify Windows path and replacement behavior.

Release tests:

- Build all six NativeAOT targets without AOT warnings.
- Run every generated executable where the runner architecture permits it.
- Install and execute the packed NuGet tool from a temporary local feed.
- Validate Homebrew formula and Scoop manifest.
- Download release assets and verify checksums.

Definition of done:

- The sample YAML installs `grill-me` and `visual-explainer` for Cursor, Codex, and Claude through one `harnfig install`.
- The command works as both a NativeAOT executable and a NuGet global tool.
- Private GitHub repositories work through existing Git credentials.
- Installation is reproducible, idempotent, and never executes downloaded code.
- `v0.1.0` is available through GitHub Releases, NuGet, Homebrew, and Scoop.
