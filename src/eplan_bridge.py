"""Bridge between the MCP server and a running EPLAN P8 add-in.

The bridge submits an ``id``/``op`` request to the add-in and returns the
matching reply. EPLAN lifecycle (launch / detect / close) is handled here; each
operation itself is performed by the add-in.

Requests and replies travel over a small, swappable transport (see ``Transport``
below). The reference build marshals them through a shared working directory
because that needs no extra dependencies; the directory and the file names it
uses are configurable defaults, not part of the message contract. Only the
id/op request and the result fields are stable across implementations.

Modal dialog handling is intentionally left as an extension point: the public
framework ships a no-op ``dismiss_dialogs`` hook. If your EPLAN configuration can
raise modal dialogs during unattended runs, implement the hook for your own
environment (see ``dismiss_dialogs`` below) or run EPLAN with prompts suppressed.
"""
import os, time, uuid, json, subprocess, importlib, base64


# ---------- Configuration ----------
def _load_config():
    cfg = {
        "eplan_exe": r"C:\Program Files\EPLAN\Platform\2.9.4\Bin\EPLAN.exe",
        "variant": "Electric P8",
        "base_dir": r"C:\eplan_mcp",
    }
    path = os.environ.get("EPLAN_MCP_CONFIG")
    if not path:
        cand = os.path.join(os.path.dirname(os.path.abspath(__file__)), "config.json")
        if os.path.exists(cand):
            path = cand
    if path and os.path.exists(path):
        try:
            with open(path, "r", encoding="utf-8") as f:
                cfg.update({k: v for k, v in json.load(f).items() if v})
        except Exception:
            pass
    for key, env in (("eplan_exe", "EPLAN_EXE"), ("variant", "EPLAN_VARIANT"),
                     ("base_dir", "EPLAN_MCP_BASE_DIR")):
        if os.environ.get(env):
            cfg[key] = os.environ[env]
    return cfg


_CFG        = _load_config()
EPLAN_EXE   = _CFG["eplan_exe"]
VARIANT     = _CFG["variant"]
BASE_DIR    = _CFG["base_dir"]
OUT_DIR     = os.path.join(BASE_DIR, "out")


# ---------- Transport ----------
# Swappable carrier for id/op requests and their replies. The reference build
# exchanges them through files in the working directory; the directory and the
# endpoint names below are configurable defaults, not part of the contract.
# Replace this object to carry the same messages over any other channel.
class _DirTransport:
    def __init__(self, base_dir):
        self.base_dir = base_dir
        self.request_path = os.path.join(base_dir, "command.txt")
        self.reply_path = os.path.join(base_dir, "result.txt")
        self.signal_path = os.path.join(base_dir, "trigger.flag")

    def submit(self, lines):
        try:
            if os.path.exists(self.reply_path):
                os.remove(self.reply_path)
        except Exception:
            pass
        try:
            os.makedirs(self.base_dir, exist_ok=True)
        except Exception:
            pass
        with open(self.request_path, "w", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
        open(self.signal_path, "w").close()

    def read_reply(self):
        if not os.path.exists(self.reply_path):
            return None
        d = {}
        try:
            with open(self.reply_path, "r", encoding="utf-8", errors="replace") as f:
                for line in f:
                    if "=" in line:
                        k, v = line.split("=", 1)
                        d[k.strip()] = v.strip()
        except Exception:
            return None
        return d

    def read_reply_b64(self):
        """Like read_reply, but also decodes an optional base64(JSON) 'data'
        field into a dict, so the bridge can return structured results."""
        d = self.read_reply()
        if not d or "data" not in d:
            return d
        try:
            d["data"] = json.loads(base64.b64decode(d["data"]).decode("utf-8"))
        except Exception:
            pass
        return d


_TRANSPORT = _DirTransport(BASE_DIR)


# ---------- Dialog handling (extension point) ----------
# Whether an operation raises blocking modal prompts depends entirely on the
# local EPLAN configuration and deployment, so the public framework leaves this
# as a pluggable extension point. The default handler does nothing, so the bridge
# runs and stays valid out of the box. Supply your own handler for unattended runs
# either by calling ``register_dialog_handler(obj)`` or by pointing the
# ``EPLAN_MCP_DIALOG_HANDLER`` environment variable at an importable module that
# defines ``get_dialog_handler()`` returning such an object.
#
# A handler is any object exposing one optional callable:
#     clear(verbose=False) -> list      # dismiss pending prompts once

class _NoopDialogHandler:
    """Default handler that takes no action. Replace it for unattended runs."""
    def clear(self, verbose=False):
        return []


def _load_dialog_handler():
    spec = os.environ.get("EPLAN_MCP_DIALOG_HANDLER")
    if spec:
        try:
            mod = importlib.import_module(spec)
            factory = getattr(mod, "get_dialog_handler", None)
            if callable(factory):
                h = factory()
                if h is not None:
                    return h
        except Exception:
            pass
    return _NoopDialogHandler()


_DIALOG_HANDLER = _load_dialog_handler()


def register_dialog_handler(handler):
    """Install a custom dialog handler. Pass None to restore the no-op default."""
    global _DIALOG_HANDLER
    _DIALOG_HANDLER = handler if handler is not None else _NoopDialogHandler()
    return _DIALOG_HANDLER


def dismiss_dialogs(*args, **kwargs):
    """Dismiss any pending modal prompts so unattended automation can continue.

    Delegates to the active dialog handler (a no-op by default) and returns the
    list of actions it reports. The rest of the bridge calls this opportunistically
    and works whether or not it does anything.
    """
    try:
        fn = getattr(_DIALOG_HANDLER, "clear", None)
        if callable(fn):
            return fn(verbose=kwargs.get("verbose", False)) or []
    except Exception:
        pass
    return []


# ---------- EPLAN lifecycle ----------
def _tasklist_pids(image="EPLAN.exe"):
    try:
        out = subprocess.run(["tasklist", "/FI", f"IMAGENAME eq {image}", "/FO", "CSV", "/NH"],
                             capture_output=True, text=True, timeout=15,
                             encoding="utf-8", errors="ignore").stdout or ""
    except Exception:
        return []
    pids = []
    for ln in out.splitlines():
        if image.lower() not in ln.lower():
            continue
        for p in ln.split('","'):
            q = p.strip().strip('"').replace(",", "")
            if q.isdigit():
                pids.append(q)
                break
    return pids


def is_running():
    return bool(_tasklist_pids())


def launch(project=None, minimized=True):
    args = [EPLAN_EXE, f'/Variant:{VARIANT}']
    if project:
        args.append(f'/Project:{project}')
    si = subprocess.STARTUPINFO()
    if minimized:
        si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        si.wShowWindow = 7
    subprocess.Popen(args, startupinfo=si)


def close(graceful=True, timeout=40):
    """Close EPLAN; force-kill if it does not exit within the timeout."""
    if graceful:
        deadline = time.time() + timeout
        while time.time() < deadline:
            dismiss_dialogs()
            if not is_running():
                return True
            time.sleep(2)
    for pid in _tasklist_pids():
        subprocess.run(["taskkill", "/PID", pid, "/F"], capture_output=True)
    time.sleep(2)
    return not is_running()


# ---------- Request / reply ----------
def _write_request(op, **params):
    """Build an id/op request and hand it to the transport.

    ``payload`` (a JSON-serializable object) is carried as base64 so batch
    operations (place_devices, connect_many, draw_polyline) fit the flat
    key=value contract.
    """
    cid = uuid.uuid4().hex[:8]
    lines = [f"id={cid}", f"op={op}"]
    payload = params.pop("payload", None)
    for k, v in params.items():
        if v is not None:
            lines.append(f"{k}={v}")
    if payload is not None:
        raw = json.dumps(payload, ensure_ascii=False)
        lines.append("payload=" + base64.b64encode(raw.encode("utf-8")).decode("ascii"))
    _TRANSPORT.submit(lines)
    return cid


def send_command(op, timeout=40, poll=0.2, **params):
    """Send one request to the add-in and wait for the matching reply.

    Waits until the reply with the matching id arrives or the timeout elapses,
    giving the dialog hook a chance to run while waiting. A structured result
    (if the add-in sent one) is returned under ``data``.
    """
    cid = _write_request(op, **params)
    deadline = time.time() + timeout
    while time.time() < deadline:
        dismiss_dialogs()
        r = _TRANSPORT.read_reply_b64()
        if r and r.get("id") == cid:
            r["ok"] = (r.get("ok", "").lower() == "true")
            return r
        time.sleep(poll)
    return {"ok": False, "op": op, "detail": "timeout waiting for result", "id": cid}


# ---------- High-level operations ----------
def ping(timeout=20):
    return send_command("ping", timeout=timeout)


def ensure_ready(project=None, restart=False, init_wait=60):
    """Start EPLAN if needed and wait until the add-in responds."""
    if restart and is_running():
        close()
    if not is_running():
        launch(project=project)
        deadline = time.time() + init_wait
        while time.time() < deadline:
            dismiss_dialogs()
            r = ping(timeout=8)
            if r.get("ok"):
                return {"ok": True, "detail": "EPLAN ready", "ping": r}
            time.sleep(2)
        return {"ok": False, "detail": "EPLAN launched but the add-in did not respond"}
    else:
        dismiss_dialogs()
        r = ping(timeout=15)
        return {"ok": r.get("ok", False), "detail": "EPLAN already running", "ping": r}


def open_project(project, timeout=60):
    """Open a project by path."""
    return send_command("open_project", timeout=timeout, project=project)


def close_project(project, timeout=60):
    """Close an open project."""
    return send_command("close_project", timeout=timeout, project=project)


def list_pages(project, timeout=60):
    """List the page names of a project."""
    return send_command("list_pages", timeout=timeout, project=project)


def delete_pages(pages, project=None, timeout=120):
    """Delete one or more pages from a project."""
    if isinstance(pages, (list, tuple)):
        pages = "|".join(pages)
    return send_command("delete_pages", timeout=timeout, pages=pages, project=project)


def save(timeout=40):
    """Save the active project."""
    return send_command("save", timeout=timeout)


def rename_tag(old_tag, new_tag, project, timeout=120):
    """Rename a device tag in a project."""
    return send_command("rename_tag", timeout=timeout, old=old_tag, new=new_tag, project=project)


def _verify_output(res, out_file, min_bytes):
    if out_file:
        exists = os.path.exists(out_file)
        size = os.path.getsize(out_file) if exists else 0
        res["output_file"] = out_file
        res["output_exists"] = exists
        res["output_size"] = size
        res["ok"] = bool(res.get("ok")) and bool(exists and size >= min_bytes)
    return res


def export_pdf(out_path, scheme=None, pages=None, project=None, timeout=360):
    """Export a project to PDF. The add-in performs the export."""
    if isinstance(pages, (list, tuple)):
        pages = ";".join(pages)
    try:
        os.remove(out_path)
    except Exception:
        pass
    res = send_command("export_pdf", timeout=timeout, out=out_path,
                       scheme=scheme, pages=pages, project=project)
    return _verify_output(res, out_path, 1000)


def export_partslist(out_path, fmt=None, timeout=300):
    """Export the parts list to a file. The add-in performs the export."""
    try:
        os.remove(out_path)
    except Exception:
        pass
    res = send_command("export_partslist", timeout=timeout, out=out_path, fmt=fmt)
    return _verify_output(res, out_path, 10)


def export_devicelist(out_path, fmt=None, timeout=300):
    """Export the device list to a file. The add-in performs the export."""
    try:
        os.remove(out_path)
    except Exception:
        pass
    res = send_command("export_devicelist", timeout=timeout, out=out_path, fmt=fmt)
    return _verify_output(res, out_path, 10)


# ---------- Project creation / import / export ----------
def new_project(project, template, timeout=180):
    """Create a new project from a template (.zw9/.zw1)."""
    return send_command("new_project", timeout=timeout, project=project, template=template)


def delete_project(project, timeout=60):
    """Remove a project."""
    return send_command("delete_project", timeout=timeout, project=project)


def export_project(out_path, project=None, type="backup", timeout=600):
    """Export a project. type: 'backup' (EPLAN backup, default) or 'pxf'
    (EPLAN exchange format)."""
    return send_command("export_project", timeout=timeout, out=out_path,
                        project=project, type=type)


def import_project(src, project=None, type="pxf", timeout=600):
    """Import a project. type: 'pxf' (exchange format) or 'backup'."""
    return send_command("import_project", timeout=timeout, src=src,
                        project=project, type=type)


# ---------- Pages ----------
def create_page(project, name, page_type="Circuit", timeout=60):
    """Create a page. name is a page name like '=A1+B1/3'."""
    return send_command("create_page", timeout=timeout, project=project,
                        name=name, page_type=page_type)


def copy_page(project, page, to_project=None, new_name=None, timeout=60):
    """Copy a page to the same or another project."""
    return send_command("copy_page", timeout=timeout, project=project, page=page,
                        to_project=to_project, new_name=new_name)


def export_pages(pages, out_path, project=None, format="pdf", scheme=None, timeout=600):
    """Export pages to PDF, DXF/DWG, or a raster format (png/tif/jpg/gif)."""
    if isinstance(pages, (list, tuple)):
        pages = "|".join(pages)
    return send_command("export_pages", timeout=timeout, project=project,
                        pages=pages, out=out_path, format=format, scheme=scheme)


def import_pages(file, project, page=None, timeout=120):
    """Import a DXF/DWG file into a page."""
    return send_command("import_pages", timeout=timeout, project=project,
                        page=page, file=file)


# ---------- Devices / placement ----------
def list_devices(project, page=None, timeout=60):
    """List devices (tags, symbols, pin counts) of a page or the whole project."""
    return send_command("list_devices", timeout=timeout, project=project, page=page)


def list_pins(project, tag, timeout=60):
    """List the pins (index, designation, position) of a device."""
    return send_command("list_pins", timeout=timeout, project=project, tag=tag)


def list_connections(project, timeout=60):
    """List all connections in the project."""
    return send_command("list_connections", timeout=timeout, project=project)


def place_device(project, page=None, tag=None, symbol=None, macro=None,
                 function_definition=None, x=100, y=100, variant=1, part=None,
                 angle=0, timeout=180):
    """Place one device. Provide one of symbol (symbol library name, e.g.
    'IEC_...'), macro (path to an .ema macro), or function_definition
    (e.g. 'CON_SWITCH_...'). angle: insertion angle in degrees (0/90/180/270)."""
    return send_command("place_device", timeout=timeout, project=project, page=page,
                        tag=tag, symbol=symbol, macro=macro,
                        function_definition=function_definition,
                        x=x, y=y, variant=variant, part=part, angle=angle)


def place_devices(devices, project=None, timeout=900):
    """Place several devices at once. devices: list of dicts with the same
    fields as place_device (page/tag/symbol/macro/function_definition/x/y/variant/part)."""
    return send_command("place_devices", timeout=timeout, project=project,
                        payload=devices)


def place_macro(project, page, macro, x=100, y=100, timeout=180):
    """Place a macro file (.ema) on a page at (x, y)."""
    return send_command("place_macro", timeout=timeout, project=project,
                        page=page, macro=macro, x=x, y=y)


def remove_device(project, tag, timeout=60):
    """Remove a placed device by its tag."""
    return send_command("remove_device", timeout=timeout, project=project, tag=tag)


# ---------- Wiring ----------
def connect(project, from_spec, to_spec, timeout=120):
    """Create an electrical connection between two device pins.
    Specs are 'TAG' or 'TAG:PININDEX', e.g. '-K1:1' -> '-M1:2'."""
    return send_command("connect", timeout=timeout, project=project,
                        **{"from": from_spec, "to": to_spec})


def connect_many(wires, project=None, timeout=900):
    """Create several connections at once. wires: list of dicts with
    {'from': 'TAG:PIN', 'to': 'TAG:PIN'}."""
    return send_command("connect_many", timeout=timeout, project=project,
                        payload=wires)


# ---------- Drawing ----------
def draw_line(project, page, start, end, pen=None, timeout=60):
    """Draw a line between two points (page coordinates in mm).
    pen: optional dict with colorId/styleId/width."""
    return send_command("draw_line", timeout=timeout, project=project, page=page,
                        payload={"start": start, "end": end, "pen": pen or {}})


def draw_polyline(project, page, points, closed=False, pen=None, timeout=60):
    """Draw a polyline through points (page coordinates in mm)."""
    return send_command("draw_polyline", timeout=timeout, project=project, page=page,
                        payload={"points": points, "closed": closed, "pen": pen or {}})


# ---------- Project backups ----------
def open_backup(archive, dest_name=None, force=False, timeout=600):
    """Open a project backup file. The add-in restores it and loads it."""
    if not archive or not os.path.exists(archive) or os.path.isdir(archive):
        return {"ok": False, "op": "open_backup", "detail": "backup file not found: %s" % archive}
    rdy = ensure_ready()
    if not rdy.get("ok"):
        return {"ok": False, "op": "open_backup", "detail": "EPLAN not ready: %s" % rdy.get("detail")}
    return send_command("open_backup", timeout=int(timeout),
                        archive=archive, name=dest_name, force=force)


def run_actions(actions, timeout=60):
    """Run a sequence of add-in operations passed through to the add-in."""
    return send_command("run_actions", actions=actions, timeout=timeout)


# ---------- CLI ----------
if __name__ == "__main__":
    import sys
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    cmd = sys.argv[1] if len(sys.argv) > 1 else "ping"
    arg = sys.argv[2] if len(sys.argv) > 2 else None
    if cmd == "watchdog":
        print(json.dumps(dismiss_dialogs(), ensure_ascii=False))
    elif cmd == "running":
        print("running:", is_running(), _tasklist_pids())
    elif cmd == "ready":
        print(json.dumps(ensure_ready(project=arg), ensure_ascii=False, indent=2))
    elif cmd == "ping":
        print(json.dumps(ping(), ensure_ascii=False, indent=2))
    elif cmd == "open":
        print(json.dumps(open_project(arg), ensure_ascii=False, indent=2))
    elif cmd == "list_pages":
        print(json.dumps(list_pages(arg), ensure_ascii=False, indent=2))
    elif cmd == "save":
        print(json.dumps(save(), ensure_ascii=False, indent=2))
    elif cmd == "close":
        print("closed:", close())
    else:
        print("unknown cmd:", cmd)
