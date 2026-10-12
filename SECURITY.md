# Security Policy

## Supported versions

Security fixes are provided for the latest released version of eplan-mcp.
Please make sure you are on the most recent release before reporting an
issue.

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub
issues, discussions, or pull requests.**

Instead, report them privately through one of the following channels:

- Email: **security@eplan-mcp.dev**
- Or use the repository's **"Report a vulnerability"** feature under the
  *Security* tab (GitHub private vulnerability reporting), if enabled.

To help us triage quickly, please include:

- a description of the issue and its impact,
- the version or commit affected,
- steps to reproduce or a proof of concept,
- any suggested mitigation, if known.

## What to expect

- **Acknowledgement:** within **3 business days** of your report.
- **Initial assessment:** within **10 business days**, including whether
  the report is accepted and an expected timeline.
- **Resolution:** we aim to release a fix or mitigation for confirmed,
  high-severity issues as quickly as is practical, and will keep you
  informed of progress.

We practice coordinated disclosure. Please give us a reasonable amount of
time to address the issue before any public disclosure. We are happy to
credit reporters who wish to be acknowledged.

## Scope

This policy covers the code in this repository: the MCP server, the bridge,
and the reference add-in stub.

Note that this framework automates a separate desktop application (EPLAN
P8) on the user's machine and reads and writes files on the local
filesystem. Operators are responsible for running it in a trusted
environment and for the security of the systems and data it touches.
Vulnerability reports about third-party software should be directed to the
respective vendor.
