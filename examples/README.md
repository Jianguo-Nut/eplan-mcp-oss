# Examples

A generic, end-to-end example that proves the framework works against a running
EPLAN P8 instance. The example opens a sample project, lists its pages, and
exports a PDF — nothing more.

These examples are intentionally domain-neutral. They use a placeholder project
named `sample_project` and do not assume any particular kind of drawing.

## Files

| File | What it does |
|------|--------------|
| `quickstart.py` | Runnable script: `ping`, then list pages, then export PDF, over the command channel. |
| `mcp_tool_calls.md` | The same workflow expressed as a sequence of MCP tool calls. |
| `config.sample.json` | Sample paths you can copy and edit. |
| `sample_project/` | Placeholder describing how to point the example at your own project. |

## Prerequisites

1. EPLAN P8 is installed and running.
2. The reference add-in in `../addin/` is built and loaded (it implements
   `ping`, `list_pages`, and `export_pdf` among other operations).
3. Python 3.8 or newer.

## Run it

From this folder:

```bash
python quickstart.py --project "C:\projects\sample_project.elk"
```

Optional flags:

```bash
python quickstart.py \
  --base-dir "C:\eplan_mcp" \
  --project  "C:\projects\sample_project.elk" \
  --out      "C:\out\sample_project.pdf" \
  --timeout  60
```

You can also set the command-channel directory through an environment variable:

```bash
set EPLAN_MCP_BASE_DIR=C:\eplan_mcp
python quickstart.py
```

## Expected output

The script prints one line per step: a `ping` health check, the page listing,
and the PDF export, followed by a short JSON summary of the paths it used. The
`ok` flag on each step reflects whether the add-in handled it.

## How it talks to EPLAN

The example speaks the public message format described in `../addin/README.md`:
each request carries an `id` and an `op`, and the reply echoes the `id` and
reports `ok` and `detail`. That message format is the only thing the example
relies on. How the messages are carried is handled by a swappable transport;
the example uses the same default as the reference add-in so the two
interoperate without configuration.

`quickstart.py` contains a small client and a transport class. You can use
either as a starting point for your own scripts.

## A note on the export step

The reference add-in implements `export_pdf` (project or selected pages), so
the example's export step runs against it. If your own add-in build does not
implement `export_pdf`, the step reports "not implemented" — add a handler and
re-run the example.

## License

Apache License 2.0. See the `LICENSE` file in the repository root.
