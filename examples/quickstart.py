#!/usr/bin/env python3
"""End-to-end example: open a project, list its pages, and export a PDF.

This script demonstrates a typical session against a running EPLAN instance.
It speaks only the public message format (the `id`/`op` request and the result
fields), so it works with the reference add-in as well as your own
implementation.

Requests and replies travel over a small, swappable transport (see Transport
below). This example uses the same shared-directory default as the reference
add-in so the two interoperate out of the box; the endpoint names are
configurable defaults, not part of the message contract.

Run it after EPLAN is running with the add-in loaded. See examples/README.md
for setup and for which operations the reference add-in supports.

Usage:
    python quickstart.py
    python quickstart.py --project "C:\\projects\\sample_project.elk"
    python quickstart.py --base-dir "C:\\eplan_mcp" --out "C:\\out\\sample_project.pdf"
"""

import argparse
import json
import os
import time
import uuid


# --- Configuration -----------------------------------------------------------
# These defaults can be overridden by command-line flags, the EPLAN_MCP_BASE_DIR
# environment variable, or a config.json placed next to this script.

DEFAULT_BASE_DIR = os.environ.get("EPLAN_MCP_BASE_DIR", r"C:\eplan_mcp")
DEFAULT_PROJECT = r"C:\projects\sample_project.elk"
DEFAULT_OUT = r"C:\out\sample_project.pdf"

POLL_INTERVAL = 0.2          # seconds between checks for a reply
DEFAULT_TIMEOUT = 60.0       # seconds to wait for a single operation


# --- Swappable transport -----------------------------------------------------

class Transport:
    """Carries id/op requests and their replies.

    This implementation marshals them through a shared working directory. The
    directory and the endpoint names below are configurable defaults; replace
    this class to carry the same messages over any other channel.
    """

    def __init__(self, base_dir):
        self.base_dir = base_dir
        self.request_path = os.path.join(base_dir, "command.txt")
        self.reply_path = os.path.join(base_dir, "result.txt")
        self.signal_path = os.path.join(base_dir, "trigger.flag")
        os.makedirs(base_dir, exist_ok=True)

    def submit(self, lines):
        try:
            os.remove(self.reply_path)
        except OSError:
            pass
        with open(self.request_path, "w", encoding="utf-8") as fh:
            fh.write("\n".join(lines))
        open(self.signal_path, "w").close()

    def read_reply(self):
        if not os.path.exists(self.reply_path):
            return None
        try:
            with open(self.reply_path, "r", encoding="utf-8") as fh:
                text = fh.read()
        except OSError:
            return None
        resp = {}
        for raw in text.splitlines():
            line = raw.strip()
            if not line or "=" not in line:
                continue
            k, v = line.split("=", 1)
            resp[k.strip()] = v.strip()
        return resp


# --- Minimal client over the public message format ---------------------------

class Client:
    """A small client that sends one operation and waits for its reply."""

    def __init__(self, base_dir, timeout=DEFAULT_TIMEOUT):
        self.timeout = timeout
        self.transport = Transport(base_dir)

    def call(self, op, **params):
        """Send an operation and return the parsed reply dictionary."""
        req_id = uuid.uuid4().hex[:8]

        lines = ["id=%s" % req_id, "op=%s" % op]
        for key, value in params.items():
            lines.append("%s=%s" % (key, value))
        self.transport.submit(lines)

        deadline = time.time() + self.timeout
        while time.time() < deadline:
            resp = self.transport.read_reply()
            if resp is not None and resp.get("id") == req_id:
                resp["ok"] = resp.get("ok", "").lower() == "true"
                return resp
            time.sleep(POLL_INTERVAL)
        raise TimeoutError("no reply for op=%s within %ss" % (op, self.timeout))


# --- Example workflow --------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--base-dir", default=DEFAULT_BASE_DIR,
                        help="directory used for the command channel files")
    parser.add_argument("--project", default=DEFAULT_PROJECT,
                        help="path to the project to open")
    parser.add_argument("--out", default=DEFAULT_OUT,
                        help="output path for the exported PDF")
    parser.add_argument("--timeout", type=float, default=DEFAULT_TIMEOUT,
                        help="per-operation timeout in seconds")
    args = parser.parse_args()

    client = Client(args.base_dir, timeout=args.timeout)

    # 1. Health check: confirm EPLAN is up and the add-in answers.
    print("[1/3] ping ...")
    resp = client.call("ping")
    print("      ->", resp.get("detail"))
    if not resp["ok"]:
        print("      add-in did not respond; is EPLAN running with the add-in loaded?")
        return 1

    # 2. List the pages of the sample project.
    print("[2/3] list_pages project=%s ..." % args.project)
    resp = client.call("list_pages", project=args.project)
    print("      ok=%s" % resp["ok"])
    print("      ->", resp.get("detail"))
    if not resp["ok"]:
        return 1

    # 3. Export the project to PDF.
    #    The reference add-in does not implement export; provide your own
    #    export handler (see examples/README.md) to complete this step.
    print("[3/3] export_pdf project=%s out=%s ..." % (args.project, args.out))
    resp = client.call("export_pdf", project=args.project, out=args.out)
    print("      ok=%s" % resp["ok"])
    print("      ->", resp.get("detail"))

    print("\nDone. Summary:")
    print(json.dumps({
        "base_dir": args.base_dir,
        "project": args.project,
        "pdf_out": args.out,
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
