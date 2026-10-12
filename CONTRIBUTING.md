# Contributing to eplan-mcp

Thanks for your interest in improving eplan-mcp. This document explains
what the project accepts, how to propose changes, and the few rules that
keep contributions easy to review and merge.

## Project scope

eplan-mcp is an **open framework**: a generic MCP (Model Context Protocol)
server and bridge for automating an EPLAN P8 instance, plus a minimal
reference add-in. It deliberately stays small and general-purpose.

Contributions are welcome for the **open framework** only — for example:

- bug fixes and reliability improvements in the MCP server and bridge,
- portability fixes (paths, platform handling, configuration),
- tests, CI, and developer tooling,
- improvements to the user-facing documentation,
- new **generic** tools that operate on a project at the framework level
  (for example: open/close a project, list or delete pages, save, export
  PDF / parts list / device list, rename a tag, open a backup).

## Out of scope

To keep the framework generic and maintainable, the following areas are
**not accepted** as community contributions. Pull requests that add or
extend them will be closed:

- domain-specific or vendor-specific automation built on top of the
  framework, rather than the generic framework itself,
- bundled asset libraries or end-to-end workflow packages,
- changes that hard-code vendor part numbers, customer data, project
  names, or site-specific identifiers,
- expansion of the reference add-in beyond its two read-only operations
  (`ping`, `list_pages`).

Such higher-level capabilities are expected to live in separate projects
that build on this framework, not in this repository. The reference add-in
is intentionally a minimal stub; provide your own command handlers for any
additional operations your workflow needs.

If you are unsure whether an idea is in scope, open an issue to discuss it
**before** writing code.

## How to contribute

1. **Open an issue first** for anything larger than a trivial fix, so the
   approach can be agreed before you invest time.
2. **Fork** the repository and create a topic branch.
3. Keep changes focused: one logical change per pull request.
4. Add or update **tests** where practical, and make sure existing tests
   pass.
5. Keep documentation **generic and English** — no customer names,
   project names, site paths, or vendor-specific data in code, comments,
   docs, or examples.
6. Open a **pull request** with a clear description of what changed and why.

## Contributor License Agreement (CLA)

This project requires a signed **Contributor License Agreement** before
your first contribution can be merged. The CLA confirms that you have the
right to submit your contribution and grants the project the rights it
needs to maintain and relicense the combined work. A CLA assistant will
present the current terms and guide you through signing on your first pull
request.

A `Signed-off-by` line (DCO) alone is **not** sufficient for this project.

## Review process

Every pull request is reviewed by a maintainer. In addition to normal code
review, the project runs an automated check that rejects non-generic content
such as personal names, customer or project names, and machine- or
site-specific paths. Please keep your changes generic so the check passes
cleanly.

## Code style

- Python: follow PEP 8; keep functions small and names descriptive.
- Comments and docstrings describe **what** a function does, briefly. Avoid
  long narratives.
- Prefer configuration over hard-coded paths.

## Building a release

Release archives are produced **only** with `git archive`, never by zipping a
working folder. `git archive` serializes the committed tree, so it honors both
`.gitignore` (a fail-closed whitelist) and the `export-ignore` rules in
`.gitattributes`. Local, untracked build artifacts and caches in a developer's
working directory are therefore structurally excluded from the archive.

```powershell
./package.ps1            # package HEAD
./package.ps1 -Ref v1.0.0
```

Do not hand-zip the checkout: doing so can sweep in untracked, ignored
artifacts that the release process is designed to keep out.

## Reporting bugs

Use the issue tracker for functional bugs and feature discussion. For
anything security-sensitive, do **not** open a public issue — follow
`SECURITY.md` instead.

## License of contributions

By contributing, you agree that your contributions are licensed under the
Apache License 2.0 (see `LICENSE`) and subject to the terms of the CLA.
