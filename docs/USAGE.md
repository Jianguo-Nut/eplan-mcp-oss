# Usage

This guide covers the tools `eplan-mcp` exposes and shows how to use them from
an MCP client.

Before you start, make sure:

- EPLAN P8 is running with the add-in loaded, and
- the server is registered with your MCP client.

See [`INSTALL.md`](INSTALL.md) if you have not set this up yet.

## How tools are invoked

`eplan-mcp` serves its tools over MCP. In an assistant such as Claude Code you
describe what you want in natural language and the assistant selects and calls
the appropriate tool. You can also call the tools directly through any MCP
client that supports stdio servers.

A typical session: ensure EPLAN is ready, open a project, perform one or more
operations, save, and (optionally) export.

## Tools

### Status and lifecycle

| Tool | Description |
|------|-------------|
| `eplan_status` | Check whether EPLAN is running and the add-in responds. |
| `eplan_ensure_ready` | Start EPLAN if needed and wait until it is ready. |
| `eplan_watchdog` | Dismiss pending modal dialogs once. |

### Projects

| Tool | Description |
|------|-------------|
| `eplan_open_project` | Open a project by path. |
| `eplan_close_project` | Close a project. |
| `eplan_new_project` | Create a project from a template (.zw9/.zw1). |
| `eplan_delete_project` | Remove a project. |
| `eplan_save` | Save the active project. |
| `eplan_export_project` | Export a project: `type` `backup` (default) or `pxf`. |
| `eplan_import_project` | Import a project: `type` `pxf` (default) or `backup`. |
| `eplan_open_backup` | Open a project backup file. |

### Pages

| Tool | Description |
|------|-------------|
| `eplan_list_pages` | List the page names of a project. |
| `eplan_create_page` | Create a page (name like `=A1+B1/3`, `page_type` like `Circuit`). |
| `eplan_delete_pages` | Delete the given pages from a project. |
| `eplan_copy_page` | Copy a page to the same or another project. |
| `eplan_export_pages` | Export pages: `format` `pdf` (default), `dxf`, `dwg`, `png`, `tif`, `jpg`. |
| `eplan_import_pages` | Import a DXF/DWG file into a page. |

### Devices and wiring

| Tool | Description |
|------|-------------|
| `eplan_list_devices` | List devices (tags, symbols, pin counts) of a page or project. |
| `eplan_list_pins` | List the pins (index, designation, position) of a device. |
| `eplan_place_device` | Place one device (by `symbol`, `macro`, or `function_definition`). |
| `eplan_place_devices` | Place several devices from a JSON array. |
| `eplan_place_macro` | Place a `.ema` macro file on a page. |
| `eplan_connect` | Create an electrical connection between two pins (`-K1:1` → `-M1:2`). |
| `eplan_connect_many` | Create several connections from a JSON array. |
| `eplan_draw_line` | Draw a line between two points (`start`/`end` as `x,y`). |
| `eplan_draw_polyline` | Draw a polyline (`points` as `x,y;x,y;...`). |

### Data and exports

| Tool | Description |
|------|-------------|
| `eplan_rename_tag` | Rename a device tag. |
| `eplan_export_pdf` | Export the project (or selected pages) to PDF. |
| `eplan_export_partslist` | Export the parts list to a file. |
| `eplan_export_devicelist` | Export the device list to a file. |

## Common workflows

### Check status and prepare EPLAN

Ask the assistant to confirm EPLAN is reachable:

> Check whether EPLAN is ready.

This calls `eplan_status`. If EPLAN is not running, `eplan_ensure_ready` can
start it and wait until it is available.

### Open a project and list its pages

> Open the project at `C:\projects\Demo.elk`, then list its pages.

This calls `eplan_open_project` followed by `eplan_list_pages`. The page list
is returned so you can reference specific pages in later operations.

### Delete pages

> Delete pages `=A1+B1/3` and `=A1+B1/4` from the open project.

This calls `eplan_delete_pages` with the page names to remove. Follow up with a
save when you are done.

### Rename a device tag

> Rename device tag `-M1` to `-P1`.

This calls `eplan_rename_tag`.

### Save your work

> Save the project.

This calls `eplan_save`.

### Export PDF, parts list, or device list

> Export the project to a PDF.

> Export the parts list.

> Export the device list.

These call `eplan_export_pdf`, `eplan_export_partslist`, and
`eplan_export_devicelist` respectively. Each writes the output to a file.

### Create a new project and a page

> Create a project from the IEC template at `C:\templates\IEC_bas003.zw9`,
> then create page `=A1+B1/3`.

This calls `eplan_new_project` followed by `eplan_create_page`.

### Export a project or pages

> Export the project to an EPLAN exchange file at `C:\out\Demo.epj`.

> Export pages `=A1+B1/1` and `=A1+B1/2` as PDF to `C:\out\pages.pdf`.

> Export page `=A1+B1/1` to DXF.

These call `eplan_export_project`, `eplan_export_pages`, and
`eplan_export_pages` (format `dxf`) respectively.

### Import a project or pages

> Import the EPLAN exchange file `C:\in\Demo.epj`.

> Import the DXF file `C:\in\layout.dxf` into page `=A1+B1/5`.

These call `eplan_import_project` and `eplan_import_pages`.

### Place a device

> Place a circuit breaker on page `=A1+B1/3` at position (80, 100).

The assistant looks up a suitable symbol or function definition and calls
`eplan_place_device`. You can also place by macro file or by explicit symbol:

> Place the macro `C:\macros\motor.ema` at (120, 80) on page `=A1+B1/3`.

This calls `eplan_place_macro`.

### Wire devices together

> Connect `-K1:1` to `-M1:2` in the project.

This calls `eplan_connect`. List pins first when you are unsure of indices:

> List the pins of `-K1`.

This calls `eplan_list_pins`. For several wires at once use
`eplan_connect_many` with a JSON array like
`[{"from":"-K1:1","to":"-M1:2"},{"from":"-K1:2","to":"-M2:1"}]`.

### Draw on a page

> Draw a line from (10, 10) to (85, 120) on page `=A1+B1/3`.

> Draw a rectangle outline as a closed polyline through (20,20);(80,20);(80,60);(20,60).

These call `eplan_draw_line` and `eplan_draw_polyline`.

### Open a backup

> Open the backup file `C:\backups\Demo-backup`.

This calls `eplan_open_backup`.

### Clearing a stuck dialog

If an operation leaves EPLAN waiting on a modal dialog, `eplan_watchdog`
dismisses pending dialogs once so automation can continue. This usually runs
automatically as part of long operations, but you can invoke it explicitly:

> Dismiss any open dialogs.

## A worked example

A short end-to-end flow:

1. *Ensure EPLAN is ready.* → `eplan_ensure_ready`
2. *Open `C:\projects\Demo.elk`.* → `eplan_open_project`
3. *List the pages.* → `eplan_list_pages`
4. *Create page `=A1+B1/3`.* → `eplan_create_page`
5. *Place a motor `-M1` and a contactor `-K1` on the new page.* →
   `eplan_place_devices`
6. *Connect `-K1:1` to `-M1:2`.* → `eplan_connect`
7. *Rename `-M1` to `-P1`.* → `eplan_rename_tag`
8. *Save.* → `eplan_save`
9. *Export to PDF.* → `eplan_export_pdf`
10. *Close the project.* → `eplan_close_project`

## Extending the tool set

The tools above are the framework core. To add operations specific to your
work, implement a handler in your add-in and add a matching tool in the MCP
server. See [`ARCHITECTURE.md`](ARCHITECTURE.md) for how the layers fit
together and [`../addin/README.md`](../addin/README.md) for
the message format.
