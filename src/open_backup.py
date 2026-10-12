"""Command-line utility: open a project backup and load it into a running EPLAN instance.

Usage:
  python open_backup.py <backup-file>
  python open_backup.py <backup-file> --name MyProject --force
  python open_backup.py <backup-file> --restore-only

Prints the result as JSON. Exits 0 on success, 1 on failure.
"""
import os, sys, json, argparse

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import eplan_bridge as eb


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass

    ap = argparse.ArgumentParser(
        description="Open a project backup and load it into a running EPLAN instance.")
    ap.add_argument("archive", help="path to the project backup file")
    ap.add_argument("--name", "--dest-name", dest="name", default=None,
                    help="destination project name (defaults to the backup file name)")
    ap.add_argument("--force", action="store_true",
                    help="overwrite an existing destination (default: skip if already present)")
    ap.add_argument("--restore-only", dest="restore_only", action="store_true",
                    help="restore to disk only, without loading into EPLAN")
    ap.add_argument("--timeout", type=int, default=600,
                    help="restore timeout in seconds (default 600)")
    args = ap.parse_args()

    if args.restore_only:
        if not os.path.exists(args.archive) or os.path.isdir(args.archive):
            res = {"ok": False, "op": "restore_backup",
                   "detail": "backup file not found: %s" % args.archive}
        else:
            rdy = eb.ensure_ready()
            if not rdy.get("ok"):
                res = {"ok": False, "op": "restore_backup",
                       "detail": "EPLAN not ready: %s" % rdy.get("detail")}
            else:
                res = eb.send_command("restore_backup", timeout=args.timeout,
                                      archive=args.archive, name=args.name,
                                      force=args.force)
    else:
        res = eb.open_backup(args.archive, dest_name=args.name, force=args.force,
                             timeout=args.timeout)

    print(json.dumps(res, ensure_ascii=False, indent=2))
    sys.exit(0 if res.get("ok") else 1)


if __name__ == "__main__":
    main()
