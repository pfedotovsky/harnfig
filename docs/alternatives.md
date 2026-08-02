# Alternatives

Harnfig is not the first Agent Skills installer. It is intentionally the smallest declarative option in this comparison.

| Tool | Configuration model | Scope |
|---|---|---|
| [gaal](https://github.com/getgaal/gaal) | Declarative `gaal.yaml` and `gaal sync` | Skills, MCP, repositories, content, and hooks across multiple agents |
| [Vercel skills](https://github.com/vercel-labs/skills) | Imperative CLI | Discover and install skills for many agents |
| [`gh skill`](https://cli.github.com/manual/gh_skill) | GitHub CLI commands | GitHub-hosted skill installation |
| [OpenSkills](https://github.com/numman-ali/openskills) | Imperative CLI | Universal skill loading and installation |
| harnfig | Declarative `harnfig.yaml` and `harnfig install` | GitHub skills for Cursor, Codex, and Claude |

Use gaal when the repository needs one tool for the broader harness. Use Vercel skills or `gh skill` when an imperative installer is enough. Use harnfig when the desired contract is specifically “these skills, at these commits, for these three agents” and a small C# implementation is valuable.

Harnfig v0.1 deliberately excludes registries, ratings, MCP configuration, hooks, plugins, arbitrary content synchronization, and non-GitHub sources.
