# Contributing

Harnfig is intentionally small. A change should make the current skills-only workflow clearer, safer, or more reliable before it adds a new abstraction.

## Local checks

Install the .NET 10 SDK and Git, then run:

```bash
dotnet restore Harnfig.slnx
dotnet format Harnfig.slnx --verify-no-changes
dotnet build Harnfig.slnx --no-restore
dotnet test Harnfig.slnx --no-build
```

Changes to configuration or lockfile behavior need unit tests. Changes to installation behavior need an integration test using a temporary directory. Do not make tests depend on a public GitHub repository.

Keep downloaded content inert: installation must never execute scripts from a skill repository.
