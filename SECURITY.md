# Security policy

## Reporting a vulnerability

Do not open a public issue for a vulnerability that could expose credentials, execute downloaded code, escape configured directories, or overwrite unrelated files. Use GitHub's private vulnerability reporting for `pfedotovsky/harnfig`.

Include the harnfig version, operating system, configuration shape, and a minimal reproduction without real credentials.

## Supported versions

Until 1.0, only the latest published version receives security fixes.

## Trust boundary

Harnfig downloads skill contents but does not establish that those contents are trustworthy. Review skill sources before installation. Harnfig never runs downloaded skill scripts during installation, but an agent may later run them when using the installed skill.
