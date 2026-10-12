"""MCP server exposing EPLAN automation tools over stdio."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from mcp.server.fastmcp import FastMCP
import eplan_bridge as eb

mcp = FastMCP("eplan")


@mcp.tool()
def eplan_status() -> dict:
    """Check whether EPLAN is running and the add-in responds."""
    running = eb.is_running()
    if not running:
        return {"ok": False, "running": False, "detail": "EPLAN is not running; call eplan_ensure_ready"}
    p = eb.ping(timeout=15)
    return {"ok": p.get("ok", False), "running": True, "ping": p}


@mcp.tool()
def eplan_ensure_ready(project: str = "") -> dict:
    """Start EPLAN if needed and wait until it is ready."""
    return eb.ensure_ready(project=(project or None))


@mcp.tool()
def eplan_open_project(project: str) -> dict:
    """Open a project by path."""
    return eb.open_project(project)


@mcp.tool()
def eplan_close_project(project: str) -> dict:
    """Close a project."""
    return eb.close_project(project)


@mcp.tool()
def eplan_list_pages(project: str) -> dict:
    """List the page names of a project."""
    return eb.list_pages(project)


@mcp.tool()
def eplan_delete_pages(pages: str, project: str) -> dict:
    """Delete the given pages from a project."""
    pg = [p.strip() for p in pages.split(";") if p.strip()]
    return eb.delete_pages(pg, project=project)


@mcp.tool()
def eplan_save() -> dict:
    """Save the active project."""
    return eb.save()


@mcp.tool()
def eplan_rename_tag(old_tag: str, new_tag: str, project: str) -> dict:
    """Rename a device tag."""
    return eb.rename_tag(old_tag, new_tag, project)


@mcp.tool()
def eplan_export_pdf(out_path: str, scheme: str = "", pages: str = "") -> dict:
    """Export the project to PDF."""
    pg = [p.strip() for p in pages.split(";") if p.strip()] if pages else None
    return eb.export_pdf(out_path, scheme=(scheme or None), pages=pg)


@mcp.tool()
def eplan_export_partslist(out_path: str, fmt: str = "") -> dict:
    """Export the parts list to a file."""
    return eb.export_partslist(out_path, fmt=(fmt or None))


@mcp.tool()
def eplan_export_devicelist(out_path: str, fmt: str = "") -> dict:
    """Export the device list to a file."""
    return eb.export_devicelist(out_path, fmt=(fmt or None))


@mcp.tool()
def eplan_new_project(project: str, template: str) -> dict:
    """Create a new project from a template. project = .elk link file path,
    template = .zw9/.zw1 template path."""
    return eb.new_project(project, template)


@mcp.tool()
def eplan_delete_project(project: str) -> dict:
    """Remove a project."""
    return eb.delete_project(project)


@mcp.tool()
def eplan_export_project(out_path: str, project: str = "", type: str = "backup") -> dict:
    """Export a project. type 'backup' (default, EPLAN backup archive) or 'pxf'
    (EPLAN exchange format)."""
    return eb.export_project(out_path, project=(project or None), type=type)


@mcp.tool()
def eplan_import_project(src: str, project: str = "", type: str = "pxf") -> dict:
    """Import a project. type 'pxf' (exchange format, default) or 'backup'."""
    return eb.import_project(src, project=(project or None), type=type)


@mcp.tool()
def eplan_create_page(project: str, name: str, page_type: str = "Circuit") -> dict:
    """Create a new page. name is a page name like '=A1+B1/3';
    page_type: Circuit (default), CircuitSingleLine, Overview, Graphics, ..."""
    return eb.create_page(project, name, page_type=page_type)


@mcp.tool()
def eplan_copy_page(project: str, page: str, to_project: str = "", new_name: str = "") -> dict:
    """Copy a page to the same or another project."""
    return eb.copy_page(project, page, to_project=(to_project or None), new_name=(new_name or None))


@mcp.tool()
def eplan_export_pages(project: str, pages: str, out_path: str,
                       format: str = "pdf", scheme: str = "") -> dict:
    """Export pages (semicolon separated names) to pdf|dxf|dwg|png|tif|jpg."""
    pg = [p.strip() for p in pages.split(";") if p.strip()]
    return eb.export_pages(pg, out_path, project=project, format=format, scheme=(scheme or None))


@mcp.tool()
def eplan_import_pages(project: str, file: str, page: str = "") -> dict:
    """Import a DXF/DWG file into a page of a project."""
    return eb.import_pages(file, project, page=(page or None))


@mcp.tool()
def eplan_list_devices(project: str, page: str = "") -> dict:
    """List devices of a page (or the whole project)."""
    return eb.list_devices(project, page=(page or None))


@mcp.tool()
def eplan_list_pins(project: str, tag: str) -> dict:
    """List the pins of a device (index, designation, position)."""
    return eb.list_pins(project, tag)


@mcp.tool()
def eplan_list_connections(project: str) -> dict:
    """List all connections in the project (placed state, start/end pin, page)."""
    return eb.list_connections(project)


@mcp.tool()
def eplan_list_symbols(project: str, page: str = "") -> dict:
    """List all SymbolReferences (connection symbols) on a page: type, x, y."""
    return eb.send_command("list_symbols", project=project, page=page)


@mcp.tool()
def eplan_list_texts(project: str, page: str = "") -> dict:
    """List all graphic texts on a page: x, y and content (Chinese-safe)."""
    return eb.send_command("list_texts", project=project, page=page)


@mcp.tool()
def eplan_place_text(project: str, page: str, content: str,
                     x: float = 0, y: float = 0) -> dict:
    """Place a graphic text on a page at (x, y) in mm."""
    return eb.send_command("place_text", project=project, page=page,
                           content=content, x=x, y=y)


@mcp.tool()
def eplan_set_texts(project: str, page: str, from_text: str, to_text: str,
                    xmin: float = -1e18, xmax: float = 1e18,
                    ymin: float = -1e18, ymax: float = 1e18) -> dict:
    """Replace graphic texts whose content equals from_text with to_text,
    optionally limited to the x/y window (mm)."""
    return eb.send_command("set_texts", project=project, page=page,
                           **{"from": from_text, "to": to_text},
                           xmin=xmin, xmax=xmax, ymin=ymin, ymax=ymax)


@mcp.tool()
def eplan_remove_symbols(project: str, page: str,
                         xmin: float = -1e18, xmax: float = 1e18,
                         ymin: float = -1e18, ymax: float = 1e18) -> dict:
    """Remove SymbolReferences (connection symbols) inside the area (mm)."""
    return eb.send_command("remove_symbols", project=project, page=page,
                           xmin=xmin, xmax=xmax, ymin=ymin, ymax=ymax)


@mcp.tool()
def eplan_exec_cmd(command: str) -> dict:
    """Execute an EPLAN CLI command (e.g. 'generate /TYPE:CONNECTIONS ...')."""
    return eb.send_command("exec_cmd", command=command)


@mcp.tool()
def eplan_place_device(project: str, tag: str = "", page: str = "",
                       symbol: str = "", macro: str = "", function_definition: str = "",
                       x: float = 100, y: float = 100, variant: int = 1,
                       part: str = "", angle: float = 0) -> dict:
    """Place one device on a page. Provide exactly one of:
    symbol (symbol library name, e.g. 'IEC_...'), macro (path to an .ema
    macro file), or function_definition (e.g. 'CON_SWITCH_...'). x/y are page
    coordinates in mm; variant is the symbol variant number; part is an
    optional part number; tag is an optional device tag like '-K1';
    angle is the insertion angle in degrees (0/90/180/270)."""
    return eb.place_device(project, page=(page or None), tag=(tag or None),
                           symbol=(symbol or None), macro=(macro or None),
                           function_definition=(function_definition or None),
                           x=x, y=y, variant=variant, part=(part or None),
                           angle=angle)


@mcp.tool()
def eplan_place_devices(project: str, devices: str) -> dict:
    """Place several devices at once. devices is a JSON array string of
    objects with the same fields as eplan_place_device, e.g.
    '[{"tag":"-K1","page":"=A1+B1/1","symbol":"IEC_...","x":50,"y":50}]'."""
    try:
        import json
        items = json.loads(devices)
    except Exception:
        return {"ok": False, "detail": "devices must be a JSON array"}
    return eb.place_devices(items, project=project)


@mcp.tool()
def eplan_place_macro(project: str, page: str, macro: str,
                      x: float = 100, y: float = 100) -> dict:
    """Place a macro file (.ema) on a page at (x, y) in mm."""
    return eb.place_macro(project, page, macro, x=x, y=y)


@mcp.tool()
def eplan_remove_device(project: str, tag: str) -> dict:
    """Remove a placed device by its tag."""
    return eb.remove_device(project, tag)


@mcp.tool()
def eplan_connect(project: str, from_spec: str, to_spec: str) -> dict:
    """Create an electrical connection between two device pins.
    Specs are 'TAG' or 'TAG:PININDEX', e.g. '-K1:1' -> '-M1:2'."""
    return eb.connect(project, from_spec, to_spec)


@mcp.tool()
def eplan_connect_many(project: str, wires: str) -> dict:
    """Create several connections at once. wires is a JSON array string of
    {'from': 'TAG:PIN', 'to': 'TAG:PIN'} objects."""
    try:
        import json
        items = json.loads(wires)
    except Exception:
        return {"ok": False, "detail": "wires must be a JSON array"}
    return eb.connect_many(items, project=project)


@mcp.tool()
def eplan_draw_line(project: str, page: str, start: str, end: str,
                    color_id: int = 0, style_id: int = 0, width: int = 1) -> dict:
    """Draw a line between two points on a page. start/end are 'x,y' strings
    (page coordinates in mm)."""
    def pt(s):
        x, y = s.split(",")
        return [float(x), float(y)]
    return eb.draw_line(project, page, pt(start), pt(end),
                        pen={"colorId": color_id, "styleId": style_id, "width": width})


@mcp.tool()
def eplan_draw_polyline(project: str, page: str, points: str,
                        closed: bool = False,
                        color_id: int = 0, style_id: int = 0, width: int = 1) -> dict:
    """Draw a polyline through points. points is a 'x,y;x,y;...' string."""
    pts = []
    for s in points.split(";"):
        if not s.strip():
            continue
        x, y = s.split(",")
        pts.append([float(x), float(y)])
    return eb.draw_polyline(project, page, pts, closed=closed,
                            pen={"colorId": color_id, "styleId": style_id, "width": width})


@mcp.tool()
def eplan_open_backup(archive: str, dest_name: str = "", force: bool = False) -> dict:
    """Open a project backup file."""
    return eb.open_backup(archive, dest_name=(dest_name or None), force=force)


@mcp.tool()
def eplan_watchdog() -> dict:
    """Dismiss pending modal dialogs once."""
    return {"ok": True, "cleared": eb.dismiss_dialogs(verbose=False)}


# ---------------------------------------------------------------------------
# Experience prompts (learned the hard way against EPLAN Platform 2.9.4 /
# GB template). Invoke these before automating EPLAN work.
# ---------------------------------------------------------------------------


@mcp.prompt()
def eplan_wiring_connections() -> str:
    """How to create VISIBLE electrical connection lines between device pins.
    Read this before using eplan_connect / eplan_place_device for wiring."""
    return """# EPLAN 接线/连线经验

完整实测经验维护在项目文档 docs/EXPERIENCE.md
（相对本文件 src/mcp_server.py 为 ../docs/EXPERIENCE.md）。
**不要在此处复制经验内容**——请先用 Read 工具阅读该文件。

重点章节：
- 第 1 节：常用元件代号表（symbol 代号 -> 元件对应）
- 第 2 节：接线/连线（API 无法直接画线；CO 转角 + T 形分支正确做法）
- 第 5 节：批量放置防崩溃策略
"""

@mcp.prompt()
def eplan_known_pitfalls() -> str:
    """Known EPLAN API pitfalls (exceptions, read-only properties, locking).
    Read this before writing or debugging add-in / bridge code."""
    return """# EPLAN API 已知坑

完整实测经验维护在项目文档 docs/EXPERIENCE.md
（相对本文件 src/mcp_server.py 为 ../docs/EXPERIENCE.md）。
**不要在此处复制经验内容**——请先用 Read 工具阅读该文件。

重点章节：
- 第 1 节：常用元件代号表
- 第 2 节：接线/连线（CO 转角 + T 形分支）
- 第 3 节：已知 API 坑（LockingStep、功能定义库、设备标签等）
- 第 4 节：add-in 部署（DLL 只能软件内手动安装）
- 第 5 节：批量放置防崩溃
- 第 6 节：add-in 修改清单
"""


if __name__ == "__main__":
    mcp.run()