# EPLAN MCP Add-In

An EPLAN P8 add-in that gives the MCP framework its counterpart inside EPLAN.
It receives `id`/`op` requests from the bridge (`src/eplan_bridge.py`),
performs the operation through the EPLAN API, and returns an `id`/`op`/`ok`/
`detail` reply. Verified to compile against **EPLAN Platform 2.9.4**.

## What it does

| Operation | Parameters | Result |
|-----------|------------|--------|
| `ping` | — | status string |
| `list_pages` | `project` | page names of the project |
| `list_devices` | `project`, `page`? | tags, symbols, pin counts |
| `list_pins` | `project`, `tag` | pin index / designation / position |
| `open_project` | `project` | opens a project |
| `close_project` | `project` | closes a project |
| `new_project` | `project` (.elk), `template` (.zw9/.zw1) | creates a project |
| `delete_project` | `project` | removes a project |
| `save` | — | saves the active project (EPLAN action `XES`) |
| `export_project` | `out`, `project`?, `type`=backup\|pxf | backup archive or EPLAN exchange format |
| `import_project` | `src`, `project`?, `type`=pxf\|backup | imports a project |
| `open_backup` / `restore_backup` | `archive`, `name`?, `force`? | restores a project backup |
| `create_page` | `project`, `name` (=A1+B1/3), `page_type`? | creates a page |
| `delete_pages` | `project`, `pages` (pipe separated) | deletes pages |
| `copy_page` | `project`, `page`, `to_project`?, `new_name`? | copies a page |
| `export_pages` | `project`, `pages`, `out`, `format`=pdf\|dxf\|dwg\|png\|tif\|jpg, `scheme`? | exports pages |
| `import_pages` | `project`, `file` (DXF/DWG), `page`? | imports into a page |
| `place_device` | `project`, `page`?, `tag`?, one of `symbol`/`macro`/`function_definition`, `x`, `y`, `variant`?, `part`? | places one device |
| `place_devices` | `project`?, `payload` (base64 JSON array) | places several devices |
| `place_macro` | `project`, `page`, `macro` (.ema), `x`, `y` | places a macro |
| `connect` | `project`, `from`=TAG[:PIN], `to`=TAG[:PIN] | creates an electrical connection |
| `connect_many` | `project`?, `payload` (base64 JSON array) | several connections at once |
| `draw_line` | `project`, `page`, `payload` {start,end,pen} | draws a line |
| `draw_polyline` | `project`, `page`, `payload` {points,closed,pen} | draws a polyline |
| `rename_tag` | `project`, `old`, `new` | renames a device tag |
| `export_pdf` | `out`, `project`?, `pages`?, `scheme`? | PDF export |
| `export_partslist` | `out`, `project`?, `fmt`? | parts list export (action) |
| `export_devicelist` | `out`, `project`?, `fmt`? | device list export (action) |
| `run_actions` | `actions` | executes an EPLAN command-line action |

## Message format

Requests and replies are flat `key=value` lines. A request carries an `id`, an
`op`, and any operation parameters; the reply echoes the `id` and reports `ok`
and `detail`. Structured results (page/device/pin lists) and batch payloads are
transported as **base64(JSON)** in the optional `data` / `payload` fields, so
the flat contract stays intact. Pairing replies to requests by `id` is the
only part of the exchange that callers depend on.

```
id=1
op=place_devices
project=C:\projects\Demo.elk
payload=W3sidGFnIjoiLUsxIiwicGFnZSI6Ij1BMStCMS8xIiwieCI6NTAsInkiOjYwfV0=
```

```
id=1
op=place_devices
ok=true
detail=placed 1/1
data=eyJkZXZpY2VzIjpbeyJ0YWciOiItSzEifV19
```

The bridge handles base64 encoding/decoding automatically; MCP tool users never
see these fields.

## Coordinates

Device placement and drawing use EPLAN page coordinates in **millimetres**
(`Eplan.EplApi.Base.PointD`), origin at the top-left of the drawing frame.
Typical schematic coordinates are tens to hundreds of mm.

## Device placement

`place_device` accepts exactly one of three sources:

- `symbol` — a symbol library name such as `IEC_...` (plus `variant`, default 1).
  The symbol is looked up in the project's first symbol library.
- `macro` — a path to an `.ema` macro file, placed via
  `Function.PlaceFunctionalMacro`.
- `function_definition` — a function definition identifier such as
  `CON_SWITCH_...`, resolved through `FunctionDefinitionLibrary`; the
  definition's base symbol is used.

Optionally `tag` sets the device tag (`-K1`) and `part` attaches an article
reference.

## Building

The physical API assembly files in EPLAN Platform 2.9.x carry a trailing `u`
(`Eplan.EplApi.AFu.dll`, `Baseu.dll`, `DataModelu.dll`, `HEServicesu.dll`);
the namespaces inside keep the classic `Eplan.EplApi.*` form. `build.ps1`
already references the correct physical files:

```powershell
.\build.ps1 -EplanApiDir "C:\Program Files\EPLAN\Platform\2.9.4\Bin"
```

The compiled DLL is written to `.\build\ReferenceAddIn.dll`.

## Registering the add-in

1. Copy `build\ReferenceAddIn.dll` to the EPLAN add-in location (for example
   `C:\Program Files\EPLAN\Platform\2.9.4\Bin\Addins\`, creating the folder).
2. Start EPLAN; it loads IEplAddIn implementations found in the add-in folder.
3. Verify with `python src\eplan_bridge.py ping` (or ask your MCP client to
   run `eplan_status`).
4. After rebuilding the add-in, **restart EPLAN** so the new DLL is loaded.

## Transport

How messages get from the host to the add-in is a separate, swappable concern,
handled by the `Transport` class. The reference build carries them through
files in a shared working directory (`C:\eplan_mcp`, overridable with the
`EPLAN_MCP_BASE_DIR` environment variable). Replace `Transport` to carry the
same messages over any other channel without touching the operation handlers.
