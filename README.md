# harnfig

Install the same [Agent Skills](https://agentskills.io) for Cursor, Codex, and Claude from one small YAML file.

```text
harnfig.yaml → harnfig install → Cursor / Codex / Claude
```

Harnfig is deliberately narrow. It installs skills from GitHub and does not try to manage every part of an agent harness. That keeps the implementation small enough to read, explain, and change during a .NET talk.

## Quick start

Create `harnfig.yaml` in your repository:

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

Then run:

```bash
harnfig install
```

`repo` is the GitHub repository. `path` is the directory containing the skill's `SKILL.md` inside that repository. Use `path: .` when the repository root is the skill.

Harnfig resolves Git refs to commits, validates each skill, installs it in every selected agent directory, and writes `harnfig.lock`. Commit the YAML and lockfile for reproducible setup.

## 60-second talk demo

1. Show the two entries in [`examples/harnfig.yaml`](examples/harnfig.yaml).
2. Run `harnfig install --config examples/harnfig.yaml`.
3. Open `.cursor/skills`, `.agents/skills`, and `.claude/skills`: both skills are present in each.
4. Show the resolved commits and content digests in [`examples/harnfig.lock`](examples/harnfig.lock).
5. Run the same command again: the result is `0 installed, 6 unchanged`.

That is the complete idea: strict YAML, ordinary Git, `SKILL.md` validation, deterministic hashing, and safe filesystem replacement.

## Installation

The first release will support four channels:

```bash
# Any platform with the .NET 10 SDK
dotnet tool install --global harnfig

# One-off execution with .NET 10
dnx harnfig -- install

# macOS or Linux
brew install pfedotovsky/tap/harnfig

# Windows
scoop bucket add pfedotovsky https://github.com/pfedotovsky/scoop-bucket
scoop install harnfig
```

NativeAOT archives on [GitHub Releases](https://github.com/pfedotovsky/harnfig/releases) run without an installed .NET runtime.

## Commands

```text
harnfig install [--config <path>] [--scope project|user]
                [--dry-run] [--frozen] [--update] [--force]
harnfig version
harnfig help
```

- `--dry-run` resolves and validates without writing.
- `--frozen` requires the lockfile to match exactly.
- `--update` refreshes mutable refs.
- `--force` replaces unmanaged or locally modified destinations.

Project installations are relative to `harnfig.yaml`:

| Agent | Project | User |
|---|---|---|
| Cursor | `.cursor/skills/<name>` | `~/.cursor/skills/<name>` |
| Codex | `.agents/skills/<name>` | `~/.agents/skills/<name>` |
| Claude | `.claude/skills/<name>` | `~/.claude/skills/<name>` |

Private GitHub repositories use your existing Git credential helper or SSH agent. Harnfig does not read or store tokens.

## Safety model

- Downloaded scripts, hooks, and commands are never executed during installation.
- Repository paths cannot escape the checkout.
- Symlinks and special filesystem entries are rejected in v0.1.
- Existing unmanaged or locally changed skills are not overwritten without `--force`.
- Multi-target writes are staged and rolled back if the transaction fails.
- The installed skill is copied unchanged, including its own license.

## Why another tool?

[gaal](https://github.com/getgaal/gaal) already solves a broader version of this problem: skills, MCP servers, repositories, content, and hooks. [Vercel skills](https://github.com/vercel-labs/skills), [`gh skill`](https://cli.github.com/manual/gh_skill), and [OpenSkills](https://github.com/numman-ali/openskills) are useful alternatives too.

Harnfig exists to show .NET developers that the useful core is approachable: parse one file, resolve Git commits, validate `SKILL.md`, and copy directories safely. See [the alternatives](docs/alternatives.md) and [architecture](docs/architecture.md) for the boundaries.

## Development

Requires the .NET 10 SDK and Git.

```bash
dotnet restore Harnfig.slnx
dotnet build Harnfig.slnx --no-restore
dotnet test Harnfig.slnx --no-build
dotnet publish src/Harnfig/Harnfig.csproj -c Release -r osx-arm64
```

The code is licensed under Apache-2.0. Downloaded skills retain their original licenses.
