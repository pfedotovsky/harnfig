# 60-second Harnfig demo

This walkthrough demonstrates the complete idea behind Harnfig: strict YAML, ordinary Git, `SKILL.md` validation, deterministic hashing, and safe filesystem replacement.

1. Show the two entries in [`examples/harnfig.yaml`](../examples/harnfig.yaml).
2. Run `harnfig install --config examples/harnfig.yaml` from the repository root.
3. Open `.cursor/skills`, `.agents/skills`, and `.claude/skills`. Both skills are present in each directory.
4. Show the resolved commits and content digests in [`examples/harnfig.lock`](../examples/harnfig.lock).
5. Run the same command again. The result is `0 installed, 6 unchanged`.

The first run installs two skills across three agents. The second demonstrates that an unchanged, locked configuration is a no-op.
