# eplan-mcp

An MCP server that lets an AI assistant (or any
[Model Context Protocol](https://modelcontextprotocol.io) client) drive a
running EPLAN P8 instance through a small, well-defined automation surface.

`eplan-mcp` exposes everyday project operations — open, create, close, save,
import and export projects; create, list, delete, copy, import and export
pages; place devices (by symbol, macro or function definition); create
electrical connections between device pins; draw lines and polylines; export
PDF / parts list / device list; rename device tags; and open project backups —
as MCP tools. A lightweight bridge relays each request to a companion add-in
loaded inside EPLAN, which carries out the work against the EPLAN API and
reports the result back.

The package is a **framework**: it ships a usable core plus a reference add-in
and a documented message format, so you can add the operations your own
workflow needs without rewriting the plumbing.

## Highlights

- **Talk to EPLAN from any MCP client.** Tools are served over stdio, so they
  plug straight into Claude Code and other MCP-aware assistants.
- **Full project and page lifecycle.** Open/create/close/save/import/export
  projects and pages, all from natural language.
- **Automatic device placement and wiring.** Place one or many devices on a
  page (by symbol, `.ema` macro, or function definition) and create electrical
  connections between device pins with `eplan_place_devices` /
  `eplan_connect_many`.
- **Drawing support.** Draw lines and polylines with configurable pens through
  the EPLAN Graphics API.
- **Clear three-layer architecture.** MCP server, Python bridge, and an EPLAN
  add-in are decoupled by a small message contract over a swappable transport.
- **Extensible by design.** A reference add-in and a documented request /
  reply message format let you implement additional operations yourself.
- **Configuration over hard-coding.** Paths and the EPLAN executable location
  are read from a small config file or environment variables.

## Architecture at a glance

```
  MCP client (e.g. Claude Code)
        │  MCP over stdio
        ▼
  MCP server  (mcp_server.py)        — exposes the tools
        │  in-process call
        ▼
  Bridge      (eplan_bridge.py)      — relays requests, waits for results
        │  id/op messages over a swappable transport
        ▼
  Add-in      (inside EPLAN P8)      — runs the operation against the EPLAN API
```

The MCP server registers the tools and forwards each call to the bridge. The
bridge submits a request and waits for the matching reply. The add-in runs
inside EPLAN and performs the actual work. The layers share only a small
message contract, so each can be developed and replaced independently; how the
messages are carried between processes is a separate, swappable transport.

See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for a fuller, conceptual
overview.

## Requirements

- Windows with EPLAN P8 installed.
- Python 3.11 or newer.
- An MCP-capable client (for example, Claude Code).

## Installation

See [`docs/INSTALL.md`](docs/INSTALL.md) for step-by-step setup, including
building and registering the reference add-in and registering the server with
your MCP client.

In short:

```powershell
# from the package root: install the MCP runtime the server uses
pip install "mcp[cli]"
# copy the config template and edit it for your machine
Copy-Item src/config.template.json config.json
# build and register the reference add-in (see docs/INSTALL.md), then
# register mcp_server.py with your MCP client (see docs/INSTALL.md)
```

## Quickstart

Once the server is registered with your MCP client and EPLAN is running with
the add-in loaded, you can ask the assistant to perform operations such as:

- *"Open the project at `C:\projects\Demo.elk`."*
- *"List the pages in the current project."*
- *"Create a new page `=A1+B1/3` in the project."*
- *"Place a motor `-M1` at (100, 100) on page `=A1+B1/3`."*
- *"Connect `-K1:1` to `-M1:2`."*
- *"Export the project to a PDF."*
- *"Rename device tag `-M1` to `-P1`."*

Under the hood these map to MCP tools such as `eplan_open_project`,
`eplan_list_pages`, `eplan_create_page`, `eplan_place_device`,
`eplan_connect`, `eplan_export_pdf`, and `eplan_rename_tag`. The full tool
reference and worked examples are in [`docs/USAGE.md`](docs/USAGE.md).

## The reference add-in

The companion EPLAN add-in under [`addin/`](addin/) implements the full
operation surface: project lifecycle (open/create/close/save/import/export),
pages (create/delete/copy/import/export), device placement by symbol, macro or
function definition, electrical connections between device pins, and drawing
through the Graphics API. It is verified to compile against EPLAN Platform
2.9.4 (`Eplan.EplApi.AFu/Baseu/DataModelu/HEServicesu`). See
[`addin/README.md`](addin/README.md) for the message format and build notes.

## Documentation

- [`docs/INSTALL.md`](docs/INSTALL.md) — installation and setup.
- [`docs/USAGE.md`](docs/USAGE.md) — tools and usage examples.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — high-level architecture.

## License

Licensed under the Apache License, Version 2.0. See [`LICENSE`](LICENSE) and
[`NOTICE`](NOTICE).
