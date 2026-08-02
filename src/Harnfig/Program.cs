using System.CommandLine;
using System.Reflection;

namespace Harnfig;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "help", StringComparison.Ordinal))
        {
            args = ["--help", .. args.Skip(1)];
        }

        var configOption = new Option<string?>("--config")
        {
            Description = "Path to harnfig.yaml.",
        };
        var scopeOption = new Option<string?>("--scope")
        {
            Description = "Override installation scope: project or user.",
        };
        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Resolve and validate without writing files.",
        };
        var frozenOption = new Option<bool>("--frozen")
        {
            Description = "Require harnfig.lock to match without changes.",
        };
        var updateOption = new Option<bool>("--update")
        {
            Description = "Refresh mutable refs and rewrite harnfig.lock.",
        };
        var forceOption = new Option<bool>("--force")
        {
            Description = "Replace unmanaged or locally modified destinations.",
        };

        var install = new Command("install", "Install configured skills for the selected agents.")
        {
            configOption,
            scopeOption,
            dryRunOption,
            frozenOption,
            updateOption,
            forceOption,
        };
        install.SetAction(async (parseResult, cancellationToken) =>
        {
            try
            {
                var scopeValue = parseResult.GetValue(scopeOption);
                InstallScope? scope = scopeValue is null ? null : ConfigParser.ParseScope(scopeValue);
                var request = new InstallRequest(
                    parseResult.GetValue(configOption),
                    scope,
                    parseResult.GetValue(dryRunOption),
                    parseResult.GetValue(frozenOption),
                    parseResult.GetValue(updateOption),
                    parseResult.GetValue(forceOption),
                    Environment.CurrentDirectory);
                var installer = CreateInstaller();
                await installer.InstallAsync(request, Console.Out, cancellationToken);
                return 0;
            }
            catch (ConfigurationException exception)
            {
                Console.Error.WriteLine($"error: {exception.Message}");
                return 2;
            }
            catch (Exception exception) when (exception is HarnfigException or IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"error: {exception.Message}");
                return 1;
            }
        });

        var version = new Command("version", "Print the harnfig version.");
        version.SetAction(_ =>
        {
            Console.WriteLine(GetVersion());
            return 0;
        });

        var root = new RootCommand("Declaratively install Agent Skills from GitHub.")
        {
            install,
            version,
        };

        var parseResult = root.Parse(args);
        var exitCode = await parseResult.InvokeAsync();
        return parseResult.Errors.Count > 0 ? 2 : exitCode;
    }

    private static Installer CreateInstaller()
        => new(new ConfigParser(), new LockFileStore(), new GitCheckoutService(new GitRunner()), new SkillValidator());

    private static string GetVersion()
    {
        var informational = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return informational?.Split('+')[0] ?? "0.0.0";
    }
}
