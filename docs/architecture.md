# Architecture

Harnfig has one linear pipeline:

```text
CLI
 ↓
Config and lockfile
 ↓
Git resolver
 ↓
Agent Skills validator
 ↓
Installation planner
 ↓
Transactional filesystem writer
```

## Configuration and locking

The YAML reader uses YamlDotNet's representation model instead of reflection-based object serialization. Every key is checked explicitly, which provides strict errors and keeps the NativeAOT build predictable.

`harnfig.lock` records the normalized repository, path, requested ref, resolved commit, skill name, and content digest. Normal installs reuse matching commits. `--update` resolves refs again; `--frozen` rejects any difference.

## Git boundary

Harnfig delegates fetching and authentication to the system Git executable. Arguments are passed through `ProcessStartInfo.ArgumentList`, never through a shell. Repositories using the same URL and revision share one checkout per invocation.

Private repository credentials stay in the user's configured credential helper or SSH agent. URLs containing embedded HTTPS credentials are rejected.

## Validation

The validator checks the portable Agent Skills contract: `SKILL.md`, YAML frontmatter, `name`, `description`, and the directory-name rule. Unknown agent-specific frontmatter is preserved. Paths, symlinks, and filesystem entries are checked before anything is copied.

Harnfig hashes sorted relative paths and file bytes. The resulting digest is platform-independent and detects content changes without putting absolute paths in the lockfile.

## Installation transaction

All target directories and the new lockfile are staged beside their destinations. Existing managed destinations move to unique backups, staged content moves into place, and backups are removed only after every commit succeeds. Disposal rolls committed moves back in reverse order after a failure.

The implementation introduces interfaces only at the Git checkout boundary. Tests use real temporary filesystems and either a fixture checkout provider or an actual local Git repository.
