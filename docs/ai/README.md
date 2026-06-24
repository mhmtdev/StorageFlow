# StorageFlow AI Agent Guides

This directory contains versioned instructions for coding agents that help
build applications with StorageFlow NuGet packages.

These are consumer instructions. They are separate from the repository root
`AGENTS.md`, which governs development of the StorageFlow framework itself.

## Recommended setup

Copy the three canonical documents into the consuming repository:

```text
docs/storageflow/STORAGEFLOW.md
docs/storageflow/examples.md
docs/storageflow/troubleshooting.md
```

Then copy the adapter for the coding agent used by that repository:

| Agent | Template | Destination in consumer repository |
|---|---|---|
| OpenAI Codex | `templates/codex/AGENTS.md` | `AGENTS.md` or the nearest scoped `AGENTS.md` |
| Claude Code | `templates/claude/CLAUDE.md` | `CLAUDE.md` |
| GitHub Copilot | `templates/github-copilot/copilot-instructions.md` | `.github/copilot-instructions.md` |
| GitHub Copilot path rules | `templates/github-copilot/storageflow.instructions.md` | `.github/instructions/storageflow.instructions.md` |
| Cursor | `templates/cursor/storageflow.mdc` | `.cursor/rules/storageflow.mdc` |
| Windsurf | `templates/windsurf/storageflow.md` | `.windsurf/rules/storageflow.md` |
| Cline | `templates/cline/storageflow.md` | `.clinerules/storageflow.md` |
| Roo Code | `templates/roo/storageflow.md` | `.roo/rules/storageflow.md` |
| Gemini CLI | `templates/gemini/GEMINI.md` | `GEMINI.md` |

Do not copy every adapter into one repository. Use the canonical documents plus
the adapter files for the agents that actually work on that codebase. For
Copilot, the repository-wide file and path-specific file can be used together;
the path-specific file keeps StorageFlow rules focused on relevant files.

## Updating the guide

Pin the copied guide to the same StorageFlow release used by the application.
Review it whenever the application upgrades StorageFlow. Package APIs and this
guide are versioned together; instructions from a newer major version must not
be applied to an older package installation.

The canonical guide contains the behavioral contract. Adapter files are kept
small so agent-specific formats do not become independent documentation forks.

- [`STORAGEFLOW.md`](STORAGEFLOW.md): mandatory API and architecture rules
- [`examples.md`](examples.md): focused application examples
- [`troubleshooting.md`](troubleshooting.md): common failures and fixes
