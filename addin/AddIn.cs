using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Eplan.EplApi.ApplicationFramework;
using Eplan.EplApi.Base;
using Eplan.EplApi.DataModel;
using Eplan.EplApi.DataModel.MasterData;
using Eplan.EplApi.HEServices;
using Eplan.EplApi.DataModel.Graphics;
using PPage = Eplan.EplApi.DataModel.Properties.Page;
using PFunction = Eplan.EplApi.DataModel.Properties.Function;

namespace EplanMcp.Reference
{
    public class AddIn : IEplAddIn
    {
        private const string VERSION = "mcp-1.0";

        private readonly Transport _transport = new Transport();
        private static System.Threading.Timer _timer;

        public bool OnRegister(ref bool bLoadOnStart) { bLoadOnStart = true; return true; }
        public bool OnUnregister() { return true; }
        public bool OnInit() { return true; }

        public bool OnInitGui()
        {
            _timer = new System.Threading.Timer(delegate { Tick(); }, null,
                                                 Transport.PeriodMs, Transport.PeriodMs);
            return true;
        }

        public bool OnExit()
        {
            if (_timer != null) _timer.Dispose();
            return true;
        }

        private static void RunInHostContext(System.Action work)
        {
            work();
        }

        private void Tick()
        {
            try
            {
                if (!_transport.TryTakeRequest()) return;
                RunInHostContext(delegate { OnRequest(); });
            }
            catch { }
        }

        private void OnRequest()
        {
            string id = "", op = "";
            try
            {
                var req = _transport.ReadRequest();
                if (req == null) return;
                req.TryGetValue("id", out id);
                req.TryGetValue("op", out op);

                string o = (op ?? "").ToLowerInvariant();
                if (NeedsLockingStep(o))
                {
                    using (LockingStep ls = new LockingStep())
                    {
                        Dispatch(id, o, req);
                    }
                }
                else
                {
                    Dispatch(id, o, req);
                }
            }
            catch (Exception ex)
            {
                Reply(id, op, false, "error: " + ex.Message);
            }
        }

        private static bool NeedsLockingStep(string op)
        {
            switch (op)
            {
                case "open_project": case "close_project": case "new_project": case "save":
                case "delete_project": case "import_project": case "open_backup":
                case "restore_backup":
                case "create_page": case "delete_pages": case "copy_page": case "import_pages":
                case "place_device": case "place_devices": case "place_macro":
                case "connect": case "connect_many": case "rename_tag":
                case "remove_device":
                case "place_text": case "set_texts": case "remove_symbols":
                case "exec_cmd": case "list_symbols": case "list_texts":
                case "draw_line": case "draw_polyline":
                case "export_project": case "export_pages": case "export_pdf":
                case "export_partslist": case "export_devicelist":
                case "list_pages": case "list_devices": case "list_pins":
                case "list_connections":
                    return true;
                default:
                    return false;
            }
        }

        private void Dispatch(string id, string op, Dictionary<string, string> req)
        {
            switch (op)
            {
                // ---- health & navigation ----
                case "ping":                       Reply(id, op, true, "pong " + VERSION); break;
                case "list_pages":                 ListPages(id, op, req); break;
                case "list_devices":               ListDevices(id, op, req); break;
                case "list_pins":                  ListPins(id, op, req); break;
                case "list_connections":           ListConnections(id, op, req); break;

                // ---- project lifecycle ----
                case "open_project":               OpenProject(id, op, req); break;
                case "close_project":              CloseProject(id, op, req); break;
                case "new_project":                NewProject(id, op, req); break;
                case "save":                       SaveProject(id, op, req); break;
                case "delete_project":             DeleteProject(id, op, req); break;
                case "export_project":             ExportProject(id, op, req); break;
                case "import_project":             ImportProject(id, op, req); break;
                case "open_backup":                OpenBackup(id, op, req); break;
                case "restore_backup":             RestoreBackup(id, op, req); break;

                // ---- pages ----
                case "create_page":                CreatePage(id, op, req); break;
                case "delete_pages":               DeletePages(id, op, req); break;
                case "copy_page":                  CopyPage(id, op, req); break;
                case "export_pages":               ExportPages(id, op, req); break;
                case "import_pages":               ImportPages(id, op, req); break;

                // ---- devices / placement ----
                case "place_device":               PlaceDevice(id, op, req); break;
                case "place_devices":              PlaceDevices(id, op, req); break;
                case "place_macro":                PlaceMacro(id, op, req); break;

                // ---- wiring ----
                case "connect":                    Connect(id, op, req); break;
                case "connect_many":               ConnectMany(id, op, req); break;
                case "draw_line":                  DrawLine(id, op, req); break;
                case "draw_polyline":              DrawPolyline(id, op, req); break;

                // ---- data edits & exports ----
                case "rename_tag":                 RenameTag(id, op, req); break;
                case "remove_device":              RemoveDevice(id, op, req); break;

                // ---- atomic text / symbol ops (used by scripts / MCP) ----
                case "list_symbols":               ListSymbols(id, op, req); break;
                case "list_texts":                 ListTexts(id, op, req); break;
                case "place_text":                 PlaceText(id, op, req); break;
                case "set_texts":                  SetTexts(id, op, req); break;
                case "remove_symbols":             RemoveSymbols(id, op, req); break;
                case "exec_cmd":                   ExecCmd(id, op, req); break;
                case "export_pdf":                 ExportPdf(id, op, req); break;
                case "export_partslist":           ExportPartsList(id, op, req); break;
                case "export_devicelist":          ExportDeviceList(id, op, req); break;

                // ---- misc ----
                case "run_actions":                RunActions(id, op, req); break;

                default:
                    Reply(id, op, false, "operation '" + op + "' is not implemented");
                    break;
            }
        }


        private void Reply(string id, string op, bool ok, string detail)
        {
            ReplyWithData(id, op, ok, detail, null);
        }

        private void ReplyWithData(string id, string op, bool ok, string detail, object data)
        {
            var fields = new Dictionary<string, string>
            {
                { "id", id },
                { "op", op },
                { "ok", ok ? "true" : "false" },
                { "detail", (detail ?? "").Replace("\r", " ").Replace("\n", " ") },
                { "ts", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
            };
            if (data != null)
            {
                try
                {
                    fields["data"] = JsonB64.Encode(data);
                }
                catch { }
            }
            _transport.WriteReply(fields);
        }

        // =====================================================================
        // Health & navigation
        // =====================================================================

        private void ListPages(string id, string op, Dictionary<string, string> req)
        {
            string path; req.TryGetValue("project", out path);
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
            var list = new List<Dictionary<string, object>>();
            foreach (Page p in prj.Pages)
                list.Add(new Dictionary<string, object> { { "name", p.Name }, { "type", p.PageTypeName } });
            ReplyWithData(id, op, true,
                "[" + prj.ProjectName + "] " + prj.Pages.Length + " pages",
                new Dictionary<string, object> { { "pages", list } });
        }

        private void ListDevices(string id, string op, Dictionary<string, string> req)
        {
            string path, pageName; req.TryGetValue("project", out path); req.TryGetValue("page", out pageName);
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
            Page page = FindPage(prj, pageName);
            if (page == null && !string.IsNullOrEmpty(pageName))
            { Reply(id, op, false, "page not found: " + pageName); return; }

            var devs = new List<Dictionary<string, object>>();
            foreach (Function fn in page != null ? page.Functions : AllFunctions(prj))
            {
                var d = new Dictionary<string, object>
                {
                    { "tag", DeviceTagOf(fn) },
                    { "page", fn.Page != null ? fn.Page.Name : "" },
                    { "symbol", fn.SymbolVariant != null ? fn.SymbolVariant.SymbolName : "" },
                    { "pins", fn.Pins.Length },
                    { "x", (int)fn.Location.X },
                    { "y", (int)fn.Location.Y },
                };
                devs.Add(d);
            }
            ReplyWithData(id, op, true, "devices: " + devs.Count,
                new Dictionary<string, object> { { "devices", devs } });
        }

        private void ListPins(string id, string op, Dictionary<string, string> req)
        {
            string path, tag; req.TryGetValue("project", out path); req.TryGetValue("tag", out tag);
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
            Function fn = FindFunction(prj, tag);
            if (fn == null) { Reply(id, op, false, "device not found: " + tag); return; }
            var pins = new List<Dictionary<string, object>>();
            double fx = 0, fy = 0;
            try { fx = fn.Location.X; fy = fn.Location.Y; } catch { }
            foreach (Pin p in fn.Pins)
            {
                pins.Add(new Dictionary<string, object>
                {
                    { "index", p.Index },
                    { "designation", p.Designation },
                    { "x", p.Location.X },
                    { "y", p.Location.Y },
                    { "abs_x", fx + p.Location.X },
                    { "abs_y", fy + p.Location.Y },
                    { "dir", p.Direction.ToString() },
                });
            }
            ReplyWithData(id, op, true, "device " + tag + " [" + fx + "," + fy + "]: " + pins.Count + " pins",
                new Dictionary<string, object> { { "pins", pins } });
        }

        private void ListConnections(string id, string op, Dictionary<string, string> req)
        {
            string path; req.TryGetValue("project", out path);
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
            var seen = new HashSet<string>();
            var conns = new List<Dictionary<string, object>>();
            foreach (Function f in AllFunctions(prj))
            {
                foreach (Connection c in f.Connections)
                {
                    string key = c.ObjectIdentifier.ToString();
                    if (!seen.Add(key)) continue;
                    var pts = new List<string>();
                    foreach (var cdp in c.ConnectionDefPoints)
                    {
                        string extra2 = "";
                        try
                        {
                            if (cdp is ConnectionDefinitionPoint)
                            {
                                var pp = cdp.ConnectionPointPositions;
                                extra2 = " pts:" + (pp != null ? pp.Length.ToString() : "0");
                            }
                        }
                        catch { }
                        try { pts.Add(((int)cdp.Location.X) + "," + ((int)cdp.Location.Y) + extra2); }
                        catch { }
                    }
                    string owner = "";
                    try
                    {
                        if (c.StartSymbolReference is Function) owner = ((Function)c.StartSymbolReference).Name;
                        else owner = c.StartSymbolReference.GetType().Name;
                    }
                    catch { }
                    conns.Add(new Dictionary<string, object>
                    {
                        { "placed", c.IsPlaced },
                        { "template", c.IsTemplate },
                        { "start_pin", c.StartPin != null ? c.StartPin.Index.ToString() : "?" },
                        { "end_pin", c.EndPin != null ? c.EndPin.Index.ToString() : "?" },
                        { "page", c.Page != null ? c.Page.Name : "" },
                        { "owner_start", owner },
                        { "defpts", c.ConnectionDefPoints.Length },
                        { "cdp_pts", string.Join(" ", pts) },
                    });
                }
            }
            ReplyWithData(id, op, true, "connections: " + conns.Count,
                new Dictionary<string, object> { { "connections", conns } });
        }

        // =====================================================================
        // Project lifecycle
        // =====================================================================

        private void OpenProject(string id, string op, Dictionary<string, string> req)
        {
            string path; req.TryGetValue("project", out path);
            if (string.IsNullOrEmpty(path)) { Reply(id, op, false, "missing project path"); return; }
            var pm = new ProjectManager();
            Project existing = null;
            try { existing = FindOpenProject(pm, path); } catch { }
            if (existing != null) { Reply(id, op, true, "project already open: " + existing.ProjectName); return; }
            try { pm.OpenProject(path); Reply(id, op, true, "project opened: " + path); }
            catch (Exception e) { Reply(id, op, false, "open failed: " + e.Message); }
        }

        private void CloseProject(string id, string op, Dictionary<string, string> req)
        {
            string path; req.TryGetValue("project", out path);
            var pm = new ProjectManager();
            Project prj = FindOpenProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not open: " + path); return; }
            try
            {
                using (LockingStep ls = new LockingStep())
                {
                    prj.Close();
                }
                Reply(id, op, true, "project closed: " + prj.ProjectName);
            }
            catch (Exception e)
            {
                try
                {
                    Project gone = null;
                    try { gone = FindOpenProject(new ProjectManager(), path); } catch { }
                    if (gone == null)
                    {
                        Reply(id, op, true, "project closed: " + path + " (verified)");
                        return;
                    }
                }
                catch { }
                Reply(id, op, false, "close failed: [" + e.GetType().Name + "] " + e.Message);
            }
        }

        private void NewProject(string id, string op, Dictionary<string, string> req)
        {
            string project, template; req.TryGetValue("project", out project); req.TryGetValue("template", out template);
            if (string.IsNullOrEmpty(project)) { Reply(id, op, false, "missing project path (.elk link file)"); return; }
            if (string.IsNullOrEmpty(template)) { Reply(id, op, false, "missing template path (.zw9/.zw1)"); return; }
            try
            {
                var pm = new ProjectManager();
                using (LockingStep ls = new LockingStep())
                {
                    Project prj = pm.CreateProject(project, template);
                    Reply(id, op, true, "project created: " + (prj != null ? prj.ProjectName : project));
                }
            }
            catch (Exception e) { Reply(id, op, false, "create failed: " + e.Message); }
        }

        private void DeleteProject(string id, string op, Dictionary<string, string> req)
        {
            string path; req.TryGetValue("project", out path);
            var pm = new ProjectManager();
            try
            {
                Project prj = FindOpenProject(pm, path);
                if (prj != null)
                {
                    using (LockingStep ls = new LockingStep())
                    {
                        prj.Close();
                    }
                }
                pm.RemoveProject(path);
                Reply(id, op, true, "project removed: " + path);
            }
            catch (Exception e) { Reply(id, op, false, "remove failed: [" + e.GetType().Name + "] " + e.Message); }
        }

        private Project FindOpenProject(ProjectManager pm, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                try { return pm.CurrentProject; } catch { return null; }
            }
            foreach (Project p in pm.OpenProjects)
            {
                try
                {
                    if (p.ProjectLinkFilePath.Equals(path, StringComparison.OrdinalIgnoreCase))
                        return p;
                }
                catch { }
                if (p.ProjectName.Equals(path, StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }

        private void SaveProject(string id, string op, Dictionary<string, string> req)
        {
            try
            {
                var cli = new CommandLineInterpreter();
                bool ok = cli.Execute("XES");
                Reply(id, op, ok, ok ? "project saved (XES)" : "XES action returned false");
            }
            catch (Exception e) { Reply(id, op, false, "save failed: " + e.Message); }
        }

        private void ExportProject(string id, string op, Dictionary<string, string> req)
        {
            string path, outPath, type; req.TryGetValue("project", out path); req.TryGetValue("out", out outPath); req.TryGetValue("type", out type);
            if (string.IsNullOrEmpty(type)) type = "backup";
            try
            {
                type = type.ToLowerInvariant();
                if (type == "pxf")
                {
                    var pm = new ProjectManager();
                    Project prj = ResolveProject(pm, path);
                    if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
                    new Export().PXFProject(prj, outPath);
                    Reply(id, op, true, "project exported to PXF: " + outPath);
                }
                else
                {
                    // backup action: /TYPE:PROJECT /DESTINATIONPATH:<dir> /ARCHIVENAME:<file>
                    string dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
                    string name = Path.GetFileName(outPath);
                    var cli = new CommandLineInterpreter();
                    string cmd = "backup /TYPE:PROJECT /DESTINATIONPATH:" + Quote(dir) +
                                 " /ARCHIVENAME:" + Quote(name) + " /AUTOCOPYREFDATA:0";
                    bool ok = cli.Execute(cmd);
                    Reply(id, op, ok, ok ? "project backed up: " + outPath : "backup action failed");
                }
            }
            catch (Exception e) { Reply(id, op, false, "export failed: " + e.Message); }
        }

        private void ImportProject(string id, string op, Dictionary<string, string> req)
        {
            string src, project, type; req.TryGetValue("src", out src); req.TryGetValue("project", out project); req.TryGetValue("type", out type);
            if (string.IsNullOrEmpty(src)) { Reply(id, op, false, "missing src file"); return; }
            if (string.IsNullOrEmpty(type)) type = "pxf";
            try
            {
                switch (type.ToLowerInvariant())
                {
                    case "pxf":
                        new Import().PXFProject(src, project);
                        Reply(id, op, true, "project imported (PXF): " + src);
                        break;
                    case "backup":
                    case "restore":
                        var coll = new System.Collections.Specialized.StringCollection();
                        coll.Add(src);
                        new Restore().Project(coll, project, "", true, false);
                        Reply(id, op, true, "project restored: " + src);
                        break;
                    default:
                        Reply(id, op, false, "unknown import type: " + type + " (use pxf|backup)");
                        break;
                }
            }
            catch (Exception e) { Reply(id, op, false, "import failed: " + e.Message); }
        }

        private void OpenBackup(string id, string op, Dictionary<string, string> req)
        {
            string archive, name, forceStr; req.TryGetValue("archive", out archive); req.TryGetValue("name", out name); req.TryGetValue("force", out forceStr);
            if (string.IsNullOrEmpty(archive)) { Reply(id, op, false, "missing archive path"); return; }
            try
            {
                var coll = new System.Collections.Specialized.StringCollection();
                coll.Add(archive);
                bool del = !forceStr.Equals("true", StringComparison.OrdinalIgnoreCase);
                // Restore.Project(StringCollection backups, string projectName,
                //                 string destPath, bool openAfterRestore, bool deleteExisting)
                new Restore().Project(coll, name, "", true, del);
                Reply(id, op, true, "backup restored: " + archive);
            }
            catch (Exception e) { Reply(id, op, false, "restore failed: " + e.Message); }
        }

        private void RestoreBackup(string id, string op, Dictionary<string, string> req)
        {
            OpenBackup(id, op, req);
        }

        // =====================================================================
        // Pages
        // =====================================================================

        private void CreatePage(string id, string op, Dictionary<string, string> req)
        {
            string path, name, pageType; req.TryGetValue("project", out path); req.TryGetValue("name", out name); req.TryGetValue("page_type", out pageType);
            if (string.IsNullOrEmpty(name)) { Reply(id, op, false, "missing page name (e.g. =A1+B1/3)"); return; }
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
            try
            {
                DocumentTypeManager.DocumentType dt = PageTypeOf(pageType);
                var props = new PagePropertyList();
                ParsePageName(name, props);
                var page = new Page();
                page.Create(prj, dt, props);
                Reply(id, op, true, "page created: " + page.Name + " (type " + dt + ")");
            }
            catch (Exception e) { Reply(id, op, false, "create page failed: " + e.Message); }
        }

        private void DeletePages(string id, string op, Dictionary<string, string> req)
        {
            string path, pagesStr; req.TryGetValue("project", out path); req.TryGetValue("pages", out pagesStr);
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
            string[] names = (pagesStr ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            int removed = 0;
            var errors = new List<string>();
            foreach (string n in names)
            {
                Page p = FindPage(prj, n);
                if (p == null) { errors.Add("page not found: " + n); continue; }
                try { p.Remove(); removed++; }
                catch (Exception e) { errors.Add("remove " + n + ": [" + e.GetType().Name + "] " + e.Message); }
            }
            string detail = "pages removed: " + removed + "/" + names.Length;
            if (errors.Count > 0) detail += " | errors: " + string.Join("; ", errors);
            Reply(id, op, removed > 0, detail);
        }

        private void CopyPage(string id, string op, Dictionary<string, string> req)
        {
            string path, pageName, toProject, newName; req.TryGetValue("project", out path); req.TryGetValue("page", out pageName);
            req.TryGetValue("to_project", out toProject); req.TryGetValue("new_name", out newName);
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
            Page page = FindPage(prj, pageName);
            if (page == null) { Reply(id, op, false, "page not found: " + pageName); return; }
            try
            {
                Project dest = string.IsNullOrEmpty(toProject) ? prj : ResolveProject(pm, toProject);
                if (dest == null) { Reply(id, op, false, "target project not found: " + toProject); return; }
                var props = new PagePropertyList();
                if (!string.IsNullOrEmpty(newName)) ParsePageName(newName, props);
                Page copy = page.CopyTo(dest, props, PageMacro.Enums.NumerationMode.Number, false);
                Reply(id, op, true, "page copied to " + (copy != null ? copy.Name : dest.ProjectName));
            }
            catch (Exception e) { Reply(id, op, false, "copy failed: " + e.Message); }
        }

        private void ExportPages(string id, string op, Dictionary<string, string> req)
        {
            string path, pagesStr, outPath, format, scheme; req.TryGetValue("project", out path); req.TryGetValue("pages", out pagesStr);
            req.TryGetValue("out", out outPath); req.TryGetValue("format", out format); req.TryGetValue("scheme", out scheme);
            if (string.IsNullOrEmpty(format)) format = "pdf";
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
            string[] names = (pagesStr ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            var pages = new ArrayList();
            foreach (string n in names)
            {
                Page p = FindPage(prj, n);
                if (p != null) pages.Add(p);
            }
            if (pages.Count == 0) { Reply(id, op, false, "no matching pages"); return; }
            try
            {
                var ex = new Export();
                switch (format.ToLowerInvariant())
                {
                    case "pdf":
                        ex.PdfPages(pages, string.IsNullOrEmpty(scheme) ? "" : scheme, outPath,
                                    Export.DegreeOfColor.BlackAndWhite, false, "", false);
                        break;
                    case "dxf":
                    case "dwg":
                        ex.DxfDwgPages(pages, outPath, string.IsNullOrEmpty(scheme) ? "" : scheme);
                        break;
                    case "png": case "tif": case "jpg": case "gif": case "bmp":
                        {
                            Export.Format fmt = (Export.Format)Enum.Parse(typeof(Export.Format), format.ToUpperInvariant());
                            foreach (Page p in pages)
                            {
                                string fn = Path.GetDirectoryName(outPath) + "\\" +
                                            Sanitize(p.Name) + "." + format.ToLowerInvariant();
                                ex.GraphicPageEx(p, fn, fmt, 150, 150, Export.Compression.None, false);
                            }
                        }
                        break;
                    default:
                        Reply(id, op, false, "unknown format: " + format + " (use pdf|dxf|dwg|png|tif|jpg)");
                        return;
                }
                Reply(id, op, true, "exported " + pages.Count + " page(s) to " + outPath);
            }
            catch (Exception e) { Reply(id, op, false, "export failed: " + e.Message); }
        }

        private void ImportPages(string id, string op, Dictionary<string, string> req)
        {
            string path, pageName, file; req.TryGetValue("project", out path); req.TryGetValue("page", out pageName); req.TryGetValue("file", out file);
            if (string.IsNullOrEmpty(file)) { Reply(id, op, false, "missing file (DXF/DWG)"); return; }
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
            Page page = null;
            if (!string.IsNullOrEmpty(pageName)) page = FindPage(prj, pageName);
            try
            {
                new Import().DXFPage(file, page, new Transformation(), "");
                Reply(id, op, true, "imported " + file + (page != null ? " into " + page.Name : ""));
            }
            catch (Exception e) { Reply(id, op, false, "import failed: " + e.Message); }
        }

        // =====================================================================
        // Device placement
        // =====================================================================

        private void PlaceDevice(string id, string op, Dictionary<string, string> req)
        {
            var data = new Dictionary<string, object>();
            foreach (var kv in req)
            {
                if (kv.Key == "id" || kv.Key == "op") continue;
                data[kv.Key] = kv.Value;
            }
            PlaceDevices(id, op, req, new[] { data });
        }

        private void PlaceDevices(string id, string op, Dictionary<string, string> req)
        {
            // batch payload: base64 JSON array of device descriptors
            string payload; req.TryGetValue("payload", out payload);
            List<Dictionary<string, object>> items = JsonB64.DecodeArray(payload);
            if (items == null) { Reply(id, op, false, "missing or invalid payload"); return; }
            PlaceDevices(id, op, req, items);
        }

        private void PlaceDevices(string id, string op, Dictionary<string, string> req, IEnumerable<Dictionary<string, object>> items)
        {
            string path; req.TryGetValue("project", out path);
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }

            int placed = 0;
            var errors = new List<string>();
            // Device placement modifies project data, so it must run inside a
            // LockingStep; otherwise EPLAN throws NoLockingStepException.
            using (LockingStep ls = new LockingStep())
            {
            foreach (var d in items)
            {
                string tag = Str(d, "tag"), pageName = Str(d, "page");
                string symbol = Str(d, "symbol"), macro = Str(d, "macro"), funcDef = Str(d, "function_definition");
                double x = Num(d, "x", 100), y = Num(d, "y", 100);
                int variant = (int)Num(d, "variant", 1);
                double angle = Num(d, "angle", 0);      // insertion angle in deg (0/90/180/270)
                string part = Str(d, "part");

                try
                {
                    Page page = null;
                    if (!string.IsNullOrEmpty(pageName)) page = FindPage(prj, pageName);
                    if (page == null && !string.IsNullOrEmpty(pageName))
                    { errors.Add("page not found: " + pageName); continue; }

                    var pos1 = new PointD(x, y);
                    double rad = angle * Math.PI / 180.0;
                    var pos2 = new PointD(x + 10 * Math.Cos(rad), y + 10 * Math.Sin(rad));   // orientation reference

                    Function fn = null;
                    if (!string.IsNullOrEmpty(symbol) && symbol.StartsWith("REMOVE_SR:", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] rp = symbol.Substring(10).Split(';');
                        double rx0 = double.MinValue, rx1 = double.MaxValue, ry0 = double.MinValue, ry1 = double.MaxValue;
                        if (rp.Length >= 4)
                        {
                            try
                            {
                                rx0 = double.Parse(rp[0]); rx1 = double.Parse(rp[1]);
                                ry0 = double.Parse(rp[2]); ry1 = double.Parse(rp[3]);
                            }
                            catch { }
                        }
                        int removed = 0;
                        var toRemove = new List<SymbolReference>();
                        foreach (Placement p in page.AllPlacements)
                        {
                            if (p is SymbolReference)
                            {
                                var sr = (SymbolReference)p;
                                try
                                {
                                    PointD loc = sr.Location;
                                    if (loc.X >= rx0 && loc.X <= rx1 && loc.Y >= ry0 && loc.Y <= ry1)
                                        toRemove.Add(sr);
                                }
                                catch { }
                            }
                        }
                        foreach (SymbolReference sr in toRemove)
                        {
                            try { sr.Remove(); removed++; }
                            catch { }
                        }
                        if (removed == 0)
                        {
                            foreach (Placement p in page.AllPlacements)
                            {
                                if (p is SymbolReference)
                                {
                                    var sr = (SymbolReference)p;
                                    try
                                    {
                                        PointD loc = sr.Location;
                                        errors.Add("DEBUG SR '" + p.GetType().Name + "' loc=(" +
                                                   loc.X.ToString("0.0") + "," + loc.Y.ToString("0.0") + ")");
                                    }
                                    catch { }
                                }
                            }
                        }
                        errors.Add("DEBUG REMOVE_SR area removed " + removed);
                        if (removed > 0) placed++;
                        continue;
                    }
                    if (!string.IsNullOrEmpty(symbol) && symbol.StartsWith("CMD:", StringComparison.OrdinalIgnoreCase))
                    {
                        string cmd = symbol.Substring(4);
                        try
                        {
                            var cli = new CommandLineInterpreter();
                            bool ok = cli.Execute(cmd);
                            errors.Add("DEBUG CMD '" + cmd + "' -> " + ok);
                            if (ok) placed++;
                        }
                        catch (Exception e) { errors.Add("CMD fail: " + e.GetType().Name + ": " + e.Message); }
                        continue;
                    }
                    if (!string.IsNullOrEmpty(symbol) && symbol.StartsWith("ADD_TEXT:", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] ap = symbol.Substring(9).Split(';');
                        string content = ap[0];
                        double tx = 0, ty = 0;
                        try { tx = double.Parse(ap[1]); ty = double.Parse(ap[2]); } catch { }
                        try
                        {
                            var txt = new Eplan.EplApi.DataModel.Graphics.Text();
                            txt.Create(page, content, 2.5);
                            txt.Location = new PointD(tx, ty);
                            errors.Add("DEBUG ADD_TEXT '" + content + "' at " + tx + "," + ty);
                            placed++;
                        }
                        catch (Exception e) { errors.Add("ADD_TEXT fail: " + e.GetType().Name + ": " + e.Message); }
                        continue;
                    }
                    if (!string.IsNullOrEmpty(symbol) && symbol.StartsWith("TEXT:", StringComparison.OrdinalIgnoreCase))
                    {
                        string spec = symbol.Substring(5);
                        string[] parts = spec.Split(';');
                        string from = parts[0], to = parts.Length > 1 ? parts[1] : "";
                        double xmin = double.MinValue, xmax = double.MaxValue, ymin = double.MinValue, ymax = double.MaxValue;
                        if (parts.Length >= 4)
                        {
                            try { xmin = double.Parse(parts[2]); xmax = double.Parse(parts[3]); } catch { }
                        }
                        if (parts.Length >= 6)
                        {
                            try { ymin = double.Parse(parts[4]); ymax = double.Parse(parts[5]); } catch { }
                        }
                        int changed = 0;
                        foreach (Placement p in page.AllPlacements)
                        {
                            if (p is Eplan.EplApi.DataModel.Graphics.Text)
                            {
                                var txt = (Eplan.EplApi.DataModel.Graphics.Text)p;
                                try
                                {
                                    PointD loc = txt.Location;
                                    if (loc.X < xmin || loc.X > xmax || loc.Y < ymin || loc.Y > ymax)
                                        continue;
                                    if (txt.Contents.ToString().Contains("@" + from + ";"))
                                    {
                                        var mls = new MultiLangString();
                                        mls.SetAsString(to);
                                        txt.Contents = mls;
                                        changed++;
                                    }
                                }
                                catch { }
                            }
                        }
                        errors.Add("DEBUG text '" + from + "'->'" + to + "' changed " + changed);
                        if (changed == 0)
                        {
                            var types = new Dictionary<string, int>();
                            foreach (Placement p in page.AllPlacements)
                            {
                                string tn = p.GetType().Name;
                                types[tn] = types.ContainsKey(tn) ? types[tn] + 1 : 1;
                                if (p is Eplan.EplApi.DataModel.Graphics.Text)
                                {
                                    try { errors.Add("DEBUG text obj '" + ((Eplan.EplApi.DataModel.Graphics.Text)p).Contents.ToString() + "'"); }
                                    catch { }
                                }
                            }
                            foreach (var kv in types)
                                errors.Add("DEBUG placement type " + kv.Key + ": " + kv.Value);
                        }
                        if (changed > 0) placed++;
                        continue;
                    }
                    if (!string.IsNullOrEmpty(macro) && File.Exists(macro))
                    {
                        var tmp = new Function();
                        var placedObjs = tmp.PlaceFunctionalMacro(page, pos1, macro,
                                                                  WindowMacro.Enums.RepresentationType.MultiLine, 0);
                        if (placedObjs != null && placedObjs.Length > 0 && placedObjs[0] is Function)
                            fn = (Function)placedObjs[0];
                        else fn = null;
                    }
                    else if (!string.IsNullOrEmpty(symbol))
                    {
                        SymbolVariant sv = null;
                        try { sv = FindSymbolVariant(prj, symbol, variant); }
                        catch (Exception e) { errors.Add("symbol '" + symbol + "': " + e.GetType().Name + ": " + e.Message + " | libraries: " + ListSymbolLibraries(prj)); continue; }
                        if (sv == null) { errors.Add("symbol variant not found: " + symbol + "/" + variant + " | libraries: " + ListSymbolLibraries(prj)); continue; }
                        fn = PlaceBySymbol(prj, page, sv, pos1, pos2);
                        if (fn == null)
                        {
                            errors.Add("DEBUG fn null for '" + symbol + "' v" + variant + " -> SymbolReference");
                            try
                            {
                                var sr = new SymbolReference();
                                sr.Create(page, sv);
                                sr.Location = pos1;
                                placed++;
                                errors.Add("DEBUG SymbolReference placed ok");
                                continue;
                            }
                            catch (Exception e2)
                            {
                                errors.Add("symbol '" + symbol + "' place fail: " + e2.GetType().Name + ": " + e2.Message);
                                continue;
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(funcDef))
                    {
                        FunctionDefinition fd = null;
                        try { fd = FindFunctionDefinition(prj, funcDef); }
                        catch (Exception e) { errors.Add("find function def '" + funcDef + "': " + e.GetType().Name + ": " + e.Message); continue; }
                        if (fd == null) { errors.Add("function definition not found: " + funcDef + " | relay-like candidates: " + RelayCandidates(prj)); continue; }
                        SymbolVariant sv = null;
                        try { sv = fd.BaseSymbol; }
                        catch (Exception e) { errors.Add("base symbol of '" + funcDef + "': " + e.GetType().Name + ": " + e.Message); continue; }
                        if (sv != null) fn = PlaceBySymbol(prj, page, sv, pos1, pos2);
                        else { errors.Add("function definition has no base symbol: " + funcDef + " | " + FdBaseSymbolInfo(prj, funcDef)); continue; }
                    }
                    else
                    {
                        errors.Add("need one of: symbol, macro, function_definition");
                        continue;
                    }

                    if (fn == null) { errors.Add("placement failed for " + (tag ?? symbol)); continue; }
                    if (!string.IsNullOrEmpty(tag))
                    {
                        SetDeviceTag(fn, tag);
                        double tagDx = 6;
                        switch (symbol)
                        {
                            case "S": case "O": case "SSA": case "SOA":
                            case "SSAR": case "SOAR": case "W2":
                                tagDx = 4;
                                break;
                        }
                        try
                        {
                            var txt = new Eplan.EplApi.DataModel.Graphics.Text();
                            txt.Create(page, tag, 2.5);
                            txt.Justification = Eplan.EplApi.DataModel.Graphics.TextBase.JustificationType.MiddleRight;
                            txt.Location = new PointD(x - tagDx, y);
                        }
                        catch { }
                    }
                    if (!string.IsNullOrEmpty(part)) { try { fn.AddArticleReference(part); } catch { } }
                    placed++;
                }
                catch (Exception e) { errors.Add((tag ?? symbol ?? "?") + ": [" + e.GetType().Name + "] " + e.Message); }
            }
            } // LockingStep
            string detail = "placed " + placed + "/" + Count(items);
            if (errors.Count > 0) detail += " | errors: " + string.Join("; ", errors);
            Reply(id, op, placed > 0, detail);
        }

        private void PlaceMacro(string id, string op, Dictionary<string, string> req)
        {
            string path, pageName, macro; req.TryGetValue("project", out path); req.TryGetValue("page", out pageName); req.TryGetValue("macro", out macro);
            double x = 100, y = 100;
            try { x = double.Parse(Get(req, "x", "100")); y = double.Parse(Get(req, "y", "100")); } catch { }
            if (string.IsNullOrEmpty(macro)) { Reply(id, op, false, "missing macro path"); return; }
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
            Page page = FindPage(prj, pageName);
            if (page == null) { Reply(id, op, false, "page not found: " + pageName); return; }
            try
            {
                var fn = new Function();
                var objs = fn.PlaceFunctionalMacro(page, new PointD(x, y), macro,
                                                   WindowMacro.Enums.RepresentationType.MultiLine, 0);
                Reply(id, op, objs != null && objs.Length > 0, "macro placed, objects: " + (objs != null ? objs.Length : 0));
            }
            catch (Exception e) { Reply(id, op, false, "macro placement failed: " + e.Message); }
        }

        // =====================================================================
        // Wiring
        // =====================================================================

        private void Connect(string id, string op, Dictionary<string, string> req)
        {
            string path, from, to; req.TryGetValue("project", out path); req.TryGetValue("from", out from); req.TryGetValue("to", out to);
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            { Reply(id, op, false, "need from and to (e.g. -K1:1 -> -M1:2)"); return; }
            var res = CreateConnection(path, from, to);
            Reply(id, op, res.ok, res.detail);
        }

        private void ConnectMany(string id, string op, Dictionary<string, string> req)
        {
            string payload; req.TryGetValue("payload", out payload);
            List<Dictionary<string, object>> items = JsonB64.DecodeArray(payload);
            if (items == null) { Reply(id, op, false, "missing or invalid payload"); return; }
            string path; req.TryGetValue("project", out path);
            int ok = 0; var errors = new List<string>();
            foreach (var d in items)
            {
                string from = Str(d, "from"), to = Str(d, "to");
                var r = CreateConnection(path, from, to);
                if (r.ok) ok++; else errors.Add(r.detail);
            }
            string detail = "connected " + ok + "/" + items.Count;
            if (errors.Count > 0) detail += " | errors: " + string.Join("; ", errors);
            Reply(id, op, ok > 0, detail);
        }

        private Result CreateConnection(string path, string from, string to)
        {
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) return Result.Fail("project not found: " + path);
            try
            {
                string fTag, tTag; int fIdx = -1, tIdx = -1;
                SplitTagPin(from, out fTag, out fIdx);
                SplitTagPin(to, out tTag, out tIdx);
                Function f = FindFunction(prj, fTag);
                Function t = FindFunction(prj, tTag);
                if (f == null) return Result.Fail("device not found: " + fTag);
                if (t == null) return Result.Fail("device not found: " + tTag);
                Pin p1 = fIdx >= 0 ? FindPin(f, fIdx) : FirstPin(f);
                Pin p2 = tIdx >= 0 ? FindPin(t, tIdx) : FirstPin(t);
                if (p1 == null) return Result.Fail("no pin " + fIdx + " on " + fTag);
                if (p2 == null) return Result.Fail("no pin " + tIdx + " on " + tTag);
                string extra = "";
                try
                {
                    Page pg = null;
                    if (p1.ParentFunction != null && p1.ParentFunction.Page != null) pg = p1.ParentFunction.Page;
                    else if (p2.ParentFunction != null && p2.ParentFunction.Page != null) pg = p2.ParentFunction.Page;
                    if (pg == null) return Result.Fail("no page for pins");

                    double fx1 = p1.ParentFunction != null ? p1.ParentFunction.Location.X : 0;
                    double fy1 = p1.ParentFunction != null ? p1.ParentFunction.Location.Y : 0;
                    double fx2 = p2.ParentFunction != null ? p2.ParentFunction.Location.X : 0;
                    double fy2 = p2.ParentFunction != null ? p2.ParentFunction.Location.Y : 0;
                    PointD start = new PointD(fx1 + p1.Location.X, fy1 + p1.Location.Y);
                    PointD end = new PointD(fx2 + p2.Location.X, fy2 + p2.Location.Y);

                    var cs = new ConnectionService();
                    var src = new ArrayList(); src.Add(p1);
                    var tgt = new ArrayList(); tgt.Add(p2);
                    Connection[] conns = cs.InterconnectDevices(src, tgt);
                    if (conns == null || conns.Length == 0)
                        return Result.Fail("interconnect returned no connections");
                    extra += " [interconnect:" + conns.Length + " placed:" + conns[0].IsPlaced + "]";
                    foreach (var c in conns)
                    {
                        try { c.MakePersistent(); }
                        catch (Exception e) { extra += " [persist err: " + e.Message + "]"; }
                    }
                    extra += " [after placed:" + conns[0].IsPlaced + "]";

                    var cli = new CommandLineInterpreter();
                    string cmd = "generate /TYPE:CONNECTIONS /REBUILDALLCONNECTIONS:1 /PROJECTNAME:\"" + path + "\"";
                    bool ok = cli.Execute(cmd);
                    extra += ok ? " [rebuild:ok]" : " [rebuild:false]";
                }
                catch (Exception e)
                {
                    extra = " [conn err: " + e.GetType().Name + ": " + e.Message + "]";
                }
                return Result.Ok("connected " + fTag + ":" + p1.Index + " -> " + tTag + ":" + p2.Index + extra);
            }
            catch (Exception e) { return Result.Fail("connect failed: " + e.Message); }
        }

        // =====================================================================
        // Drawing (Graphics API)
        // =====================================================================

        private void DrawLine(string id, string op, Dictionary<string, string> req)
        {
            string path, pageName, payload; req.TryGetValue("project", out path); req.TryGetValue("page", out pageName); req.TryGetValue("payload", out payload);
            var d = JsonB64.DecodeObject(payload);
            if (d == null) { Reply(id, op, false, "missing or invalid payload"); return; }
            double[] a = PointsOf(d, "start"), b = PointsOf(d, "end");
            if (a == null || b == null) { Reply(id, op, false, "need start/end points"); return; }
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
            Page page = FindPage(prj, pageName);
            if (page == null) { Reply(id, op, false, "page not found: " + pageName); return; }
            try
            {
                var line = new Line();
                line.Create(page, new PointD(a[0], a[1]), new PointD(b[0], b[1]));
                ApplyPen(line, d);
                Reply(id, op, true, "line drawn on " + page.Name);
            }
            catch (Exception e) { Reply(id, op, false, "draw failed: " + e.Message); }
        }

        private void DrawPolyline(string id, string op, Dictionary<string, string> req)
        {
            string path, pageName, payload; req.TryGetValue("project", out path); req.TryGetValue("page", out pageName); req.TryGetValue("payload", out payload);
            var d = JsonB64.DecodeObject(payload);
            if (d == null) { Reply(id, op, false, "missing or invalid payload"); return; }
            List<object> raw = null;
            if (d.ContainsKey("points")) raw = d["points"] as List<object>;
            if (raw == null && d.ContainsKey("points"))
            {
                var al = d["points"] as System.Collections.ArrayList;
                if (al != null) { raw = new List<object>(); foreach (object o in al) raw.Add(o); }
            }
            if (raw == null || raw.Count < 2) { Reply(id, op, false, "need >=2 points"); return; }
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
            Page page = FindPage(prj, pageName);
            if (page == null) { Reply(id, op, false, "page not found: " + pageName); return; }
            try
            {
                var poly = new PolyLine();
                poly.Create(page);
                for (int i = 0; i < raw.Count; i++)
                {
                    PointD? ptn = PointOf(raw[i]);
                    if (ptn.HasValue)
                    {
                        PointD pt = ptn.Value;
                        poly.SetPointAt(i, ref pt);
                    }
                }
                bool closed = false;
                if (d.ContainsKey("closed")) closed = d["closed"] is bool && (bool)d["closed"];
                poly.Closed = closed;
                ApplyPen(poly, d);
                Reply(id, op, true, "polyline drawn on " + page.Name + " (" + raw.Count + " points)");
            }
            catch (Exception e) { Reply(id, op, false, "draw failed: " + e.Message); }
        }

        // =====================================================================
        // Data edits & exports
        // =====================================================================

        private void RenameTag(string id, string op, Dictionary<string, string> req)
        {
            string path, oldTag, newTag; req.TryGetValue("project", out path); req.TryGetValue("old", out oldTag); req.TryGetValue("new", out newTag);
            if (string.IsNullOrEmpty(oldTag) || string.IsNullOrEmpty(newTag))
            { Reply(id, op, false, "need old and new tag"); return; }
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
            int renamed = 0;
            foreach (Function fn in AllFunctions(prj))
            {
                string cur = DeviceTagOf(fn);
                if (!string.IsNullOrEmpty(cur) && cur.Equals(oldTag, StringComparison.OrdinalIgnoreCase))
                {
                    try { SetDeviceTag(fn, newTag); renamed++; }
                    catch { }
                }
            }
            Reply(id, op, renamed > 0, "renamed " + renamed + " function(s) to " + newTag);
        }

        private void RemoveDevice(string id, string op, Dictionary<string, string> req)
        {
            string path, tag; req.TryGetValue("project", out path); req.TryGetValue("tag", out tag);
            if (string.IsNullOrEmpty(tag)) { Reply(id, op, false, "missing tag"); return; }
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, path);
            if (prj == null) { Reply(id, op, false, "project not found: " + path); return; }
            int removed = 0;
            var errs = new List<string>();
            foreach (Function fn in AllFunctions(prj))
            {
                string cur = DeviceTagOf(fn);
                if (!string.IsNullOrEmpty(cur) && cur.EndsWith(tag, StringComparison.OrdinalIgnoreCase))
                {
                    try { fn.Remove(); removed++; }
                    catch (Exception e) { errs.Add("[" + e.GetType().Name + "] " + e.Message); }
                }
            }
            string detail = "removed " + removed + " function(s) with tag " + tag;
            if (errs.Count > 0) detail += " | errors: " + string.Join("; ", errs);
            Reply(id, op, removed > 0, detail);
        }

        private Page ResolvePage(string path, string pageName, out Project prj, out string err)
        {
            err = null; prj = null;
            var pm = new ProjectManager();
            prj = ResolveProject(pm, path);
            if (prj == null) { err = "project not found: " + path; return null; }
            Page page = FindPage(prj, pageName);
            if (page == null) { err = "page not found: " + pageName; return null; }
            return page;
        }

        private void ListSymbols(string id, string op, Dictionary<string, string> req)
        {
            string path, pageName;
            req.TryGetValue("project", out path); req.TryGetValue("page", out pageName);
            Project prj; string err;
            Page page = ResolvePage(path, pageName, out prj, out err);
            if (page == null) { Reply(id, op, false, err); return; }
            var syms = new List<Dictionary<string, object>>();
            foreach (Placement p in page.AllPlacements)
            {
                if (p is SymbolReference)
                {
                    try
                    {
                        PointD loc = p.Location;
                        syms.Add(new Dictionary<string, object>
                        {
                            { "type", p.GetType().Name },
                            { "x", loc.X },
                            { "y", loc.Y },
                        });
                    }
                    catch { }
                }
            }
            ReplyWithData(id, op, true, "symbol references: " + syms.Count,
                new Dictionary<string, object> { { "symbols", syms } });
        }

        private void ListTexts(string id, string op, Dictionary<string, string> req)
        {
            string path, pageName;
            req.TryGetValue("project", out path); req.TryGetValue("page", out pageName);
            Project prj; string err;
            Page page = ResolvePage(path, pageName, out prj, out err);
            if (page == null) { Reply(id, op, false, err); return; }
            var texts = new List<Dictionary<string, object>>();
            foreach (Placement p in page.AllPlacements)
            {
                if (p is Eplan.EplApi.DataModel.Graphics.Text)
                {
                    var txt = (Eplan.EplApi.DataModel.Graphics.Text)p;
                    try
                    {
                        PointD loc = p.Location;
                        string content = "";
                        string raw = txt.Contents.InternalString;
                        if (raw != null)
                        {
                            int at = raw.IndexOf('@');
                            int semi = raw.IndexOf(';');
                            if (at >= 0 && semi > at) content = raw.Substring(at + 1, semi - at - 1);
                            else content = raw;
                        }
                        texts.Add(new Dictionary<string, object>
                        {
                            { "x", loc.X },
                            { "y", loc.Y },
                            { "text", content },
                        });
                    }
                    catch { }
                }
            }
            ReplyWithData(id, op, true, "texts: " + texts.Count,
                new Dictionary<string, object> { { "texts", texts } });
        }

        private void PlaceText(string id, string op, Dictionary<string, string> req)
        {
            string path, pageName, content;
            req.TryGetValue("project", out path); req.TryGetValue("page", out pageName); req.TryGetValue("content", out content);
            double x = 0, y = 0;
            try { x = double.Parse(Get(req, "x", "0")); y = double.Parse(Get(req, "y", "0")); } catch { }
            Project prj; string err;
            Page page = ResolvePage(path, pageName, out prj, out err);
            if (page == null) { Reply(id, op, false, err); return; }
            try
            {
                var txt = new Eplan.EplApi.DataModel.Graphics.Text();
                txt.Create(page, content, 2.5);
                txt.Location = new PointD(x, y);
                Reply(id, op, true, "text placed: '" + content + "' at " + x + "," + y);
            }
            catch (Exception e) { Reply(id, op, false, "place_text failed: " + e.GetType().Name + ": " + e.Message); }
        }

        private void SetTexts(string id, string op, Dictionary<string, string> req)
        {
            string path, pageName, from, to;
            req.TryGetValue("project", out path); req.TryGetValue("page", out pageName);
            req.TryGetValue("from", out from); req.TryGetValue("to", out to);
            double xmin = double.MinValue, xmax = double.MaxValue, ymin = double.MinValue, ymax = double.MaxValue;
            try
            {
                xmin = double.Parse(Get(req, "xmin", "-1e18")); xmax = double.Parse(Get(req, "xmax", "1e18"));
                ymin = double.Parse(Get(req, "ymin", "-1e18")); ymax = double.Parse(Get(req, "ymax", "1e18"));
            }
            catch { }
            Project prj; string err;
            Page page = ResolvePage(path, pageName, out prj, out err);
            if (page == null) { Reply(id, op, false, err); return; }
            int changed = 0;
            foreach (Placement p in page.AllPlacements)
            {
                if (p is Eplan.EplApi.DataModel.Graphics.Text)
                {
                    var txt = (Eplan.EplApi.DataModel.Graphics.Text)p;
                    try
                    {
                        PointD loc = txt.Location;
                        if (loc.X < xmin || loc.X > xmax || loc.Y < ymin || loc.Y > ymax) continue;
                        // MultiLangString.ToString() renders "lang@value;..."
                        if (txt.Contents.ToString().Contains("@" + from + ";"))
                        {
                            var mls = new MultiLangString();
                            mls.SetAsString(to);
                            txt.Contents = mls;
                            changed++;
                        }
                    }
                    catch { }
                }
            }
            Reply(id, op, true, "changed " + changed + " text(s) '" + from + "'->'" + to + "'");
        }

        private void RemoveSymbols(string id, string op, Dictionary<string, string> req)
        {
            string path, pageName;
            req.TryGetValue("project", out path); req.TryGetValue("page", out pageName);
            double xmin = double.MinValue, xmax = double.MaxValue, ymin = double.MinValue, ymax = double.MaxValue;
            try
            {
                xmin = double.Parse(Get(req, "xmin", "-1e18")); xmax = double.Parse(Get(req, "xmax", "1e18"));
                ymin = double.Parse(Get(req, "ymin", "-1e18")); ymax = double.Parse(Get(req, "ymax", "1e18"));
            }
            catch { }
            Project prj; string err;
            Page page = ResolvePage(path, pageName, out prj, out err);
            if (page == null) { Reply(id, op, false, err); return; }
            int removed = 0;
            var errors = new List<string>();
            var toRemove = new List<SymbolReference>();
            foreach (Placement p in page.AllPlacements)
            {
                if (p is SymbolReference)
                {
                    var sr = (SymbolReference)p;
                    try
                    {
                        PointD loc = sr.Location;
                        if (loc.X >= xmin && loc.X <= xmax && loc.Y >= ymin && loc.Y <= ymax)
                            toRemove.Add(sr);
                    }
                    catch { }
                }
            }
            foreach (SymbolReference sr in toRemove)
            {
                try { sr.Remove(); removed++; }
                catch (Exception e) { errors.Add("remove fail: " + e.GetType().Name + ": " + e.Message); }
            }
            if (removed == 0)
            {
                foreach (Placement p in page.AllPlacements)
                {
                    if (p is Eplan.EplApi.DataModel.Graphics.Text) continue;
                    try
                    {
                        PointD loc = p.Location;
                        errors.Add("DEBUG '" + p.GetType().FullName + "' loc=(" +
                                   loc.X.ToString("0.0") + "," + loc.Y.ToString("0.0") + ")");
                    }
                    catch (Exception e) { errors.Add("DEBUG '" + p.GetType().Name + "' fail: " + e.Message); }
                }
            }
            Reply(id, op, true, "removed " + removed + " symbol reference(s) in area" +
                  (errors.Count > 0 ? " | " + string.Join("; ", errors) : ""));
        }

        private void ExecCmd(string id, string op, Dictionary<string, string> req)
        {
            string cmd; req.TryGetValue("command", out cmd);
            if (string.IsNullOrEmpty(cmd)) { Reply(id, op, false, "missing command"); return; }
            try
            {
                var cli = new CommandLineInterpreter();
                bool ok = cli.Execute(cmd);
                Reply(id, op, ok, "command '" + cmd + "' returned " + ok);
            }
            catch (Exception e) { Reply(id, op, false, "command failed: " + e.GetType().Name + ": " + e.Message); }
        }

        private void ExportPdf(string id, string op, Dictionary<string, string> req)
        {
            string outPath, scheme, pagesStr, project; req.TryGetValue("out", out outPath); req.TryGetValue("scheme", out scheme);
            req.TryGetValue("pages", out pagesStr); req.TryGetValue("project", out project);
            if (string.IsNullOrEmpty(outPath)) { Reply(id, op, false, "missing out path"); return; }
            var pm = new ProjectManager();
            Project prj = ResolveProject(pm, project);
            if (prj == null) { Reply(id, op, false, "project not found: " + project); return; }
            try
            {
                var ex = new Export();
                if (!string.IsNullOrEmpty(pagesStr))
                {
                    var pages = new ArrayList();
                    foreach (string n in pagesStr.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        Page p = FindPage(prj, n);
                        if (p != null) pages.Add(p);
                    }
                    ex.PdfPages(pages, string.IsNullOrEmpty(scheme) ? "" : scheme, outPath,
                                Export.DegreeOfColor.BlackAndWhite, false, "", false);
                    Reply(id, op, true, "PDF exported (" + pages.Count + " pages): " + outPath);
                }
                else
                {
                    ex.PdfProject(prj, outPath, string.IsNullOrEmpty(scheme) ? "" : scheme,
                                  Export.DegreeOfColor.BlackAndWhite, false, "", false);
                    Reply(id, op, true, "PDF exported: " + outPath);
                }
            }
            catch (Exception e) { Reply(id, op, false, "PDF export failed: " + e.Message); }
        }

        private void ExportPartsList(string id, string op, Dictionary<string, string> req)
        {
            string outPath, project, format; req.TryGetValue("out", out outPath); req.TryGetValue("project", out project); req.TryGetValue("fmt", out format);
            if (string.IsNullOrEmpty(outPath)) { Reply(id, op, false, "missing out path"); return; }
            if (string.IsNullOrEmpty(format)) format = "csv";
            try
            {
                var cli = new CommandLineInterpreter();
                string cmd = "partslist /TYPE:EXPORT /PROJECTNAME:" + Quote(project) +
                             " /EXPORTFILE:" + Quote(outPath) + " /FORMAT:" + Quote(format);
                bool ok = cli.Execute(cmd);
                Reply(id, op, ok, ok ? "parts list exported: " + outPath : "partslist action failed");
            }
            catch (Exception e) { Reply(id, op, false, "export failed: " + e.Message); }
        }

        private void ExportDeviceList(string id, string op, Dictionary<string, string> req)
        {
            string outPath, project, format; req.TryGetValue("out", out outPath); req.TryGetValue("project", out project); req.TryGetValue("fmt", out format);
            if (string.IsNullOrEmpty(outPath)) { Reply(id, op, false, "missing out path"); return; }
            if (string.IsNullOrEmpty(format)) format = "csv";
            try
            {
                var cli = new CommandLineInterpreter();
                string cmd = "devicelist /TYPE:EXPORT /PROJECTNAME:" + Quote(project) +
                             " /EXPORTFILE:" + Quote(outPath) + " /FORMAT:" + Quote(format);
                bool ok = cli.Execute(cmd);
                Reply(id, op, ok, ok ? "device list exported: " + outPath : "devicelist action failed");
            }
            catch (Exception e) { Reply(id, op, false, "export failed: " + e.Message); }
        }

        private void RunActions(string id, string op, Dictionary<string, string> req)
        {
            string actions; req.TryGetValue("actions", out actions);
            if (string.IsNullOrEmpty(actions)) { Reply(id, op, false, "missing actions"); return; }
            try
            {
                var cli = new CommandLineInterpreter();
                bool ok = cli.Execute(actions);
                Reply(id, op, ok, ok ? "actions executed: " + actions : "action failed: " + actions);
            }
            catch (Exception e) { Reply(id, op, false, "action failed: " + e.Message); }
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private sealed class Result
        {
            public bool ok; public string detail;
            public static Result Ok(string d) { return new Result { ok = true, detail = d }; }
            public static Result Fail(string d) { return new Result { ok = false, detail = d }; }
        }

        private Project ResolveProject(ProjectManager pm, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                try { return pm.CurrentProject; } catch { return null; }
            }
            try { return pm.GetProject(path); } catch { }
            try { return pm.OpenProject(path); } catch { }
            // also accept a project name (e.g. "Demo")
            foreach (Project p in pm.OpenProjects)
            {
                if (p.ProjectName.Equals(path, StringComparison.OrdinalIgnoreCase)) return p;
            }
            return null;
        }

        private Page FindPage(Project prj, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (Page p in prj.Pages)
            {
                if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    p.IdentifyingName.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }

        private IEnumerable<Function> AllFunctions(Project prj)
        {
            foreach (Page p in prj.Pages)
                foreach (Function f in p.Functions)
                    yield return f;
        }

        private string DeviceTagOf(Function fn)
        {
            try
            {
                string n = fn.Name;
                if (!string.IsNullOrEmpty(n)) return n;
            }
            catch { }
            try
            {
                PropertyValue v = fn.Properties[PFunction.FUNC_DEVICETAG_FULLNAME];
                return v != null ? v.ToString() : "";
            }
            catch { return ""; }
            return "";
        }

        private void SetDeviceTag(Function fn, string tag)
        {
            Exception last = null;
            foreach (var value in new[] { tag, tag.TrimStart('-') })
            {
                try { fn.Name = value; return; }
                catch (Exception e) { last = e; }
            }
            throw last ?? new Exception("cannot set device tag: " + tag);
        }

        private Function FindFunction(Project prj, string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            foreach (Function fn in AllFunctions(prj))
            {
                string cur = DeviceTagOf(fn);
                if (!string.IsNullOrEmpty(cur) &&
                    (cur.Equals(tag, StringComparison.OrdinalIgnoreCase) ||
                     cur.EndsWith(tag, StringComparison.OrdinalIgnoreCase)))
                    return fn;
            }
            return null;
        }

        private void SplitTagPin(string spec, out string tag, out int idx)
        {
            tag = spec; idx = -1;
            int c = spec.IndexOf(':');
            if (c > 0)
            {
                tag = spec.Substring(0, c).Trim();
                int i;
                if (int.TryParse(spec.Substring(c + 1).Trim(), out i)) idx = i;
            }
        }

        private Pin FindPin(Function fn, int index)
        {
            foreach (Pin p in fn.Pins) if (p.Index == index) return p;
            return null;
        }

        private Pin FirstPin(Function fn)
        {
            if (fn.Pins == null || fn.Pins.Length == 0) return null;
            return fn.Pins[0];
        }

        private SymbolVariant FindSymbolVariant(Project prj, string symbolName, int variant)
        {
            foreach (SymbolLibrary lib in prj.SymbolLibraries)
            {
                Symbol sym = null;
                try { sym = lib[symbolName]; }
                catch { }
                if (sym == null) continue;
                var sv = new SymbolVariant();
                sv.Initialize(sym, variant);
                return sv;
            }
            return null;
        }

        private string ListSymbolLibraries(Project prj)
        {
            var parts = new List<string>();
            foreach (SymbolLibrary lib in prj.SymbolLibraries)
            {
                parts.Add("[" + lib.Name + " x" + lib.Symbols.Length + "]");
                int n = 0;
                int cap = lib.Name.StartsWith("SPECIAL") ? 100000 :
                          (lib.Name.StartsWith("GB_symbol") ? 100000 : 30);
                foreach (Symbol s in lib.Symbols)
                {
                    if (s.Name != null && n < cap)
                    {
                        if (lib.Name.StartsWith("SPECIAL") && s.Name.IndexOf("CO", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        if (lib.Name.StartsWith("GB_symbol") &&
                            !(s.Name.StartsWith("S") || s.Name.StartsWith("K") ||
                              s.Name.StartsWith("SW") || s.Name.StartsWith("SB") ||
                              s.Name.StartsWith("BT")))
                            continue;
                        parts.Add(s.Name); n++;
                    }
                }
            }
            return string.Join(", ", parts);
        }

        private FunctionDefinition FindFunctionDefinition(Project prj, string identifier)
        {
            var lib = new FunctionDefinitionLibrary(prj);
            FunctionDefinition fallback = null;
            foreach (FunctionDefinition fd in lib)
            {
                if (fd.Identifier != null && fd.Identifier.Equals(identifier, StringComparison.OrdinalIgnoreCase))
                {
                    if (HasBaseSymbol(fd)) return fd;
                    if (fallback == null) fallback = fd;
                }
            }
            foreach (FunctionDefinition fd in lib)
            {
                if (fd.Identifier != null && fd.Identifier.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (HasBaseSymbol(fd)) return fd;
                    if (fallback == null) fallback = fd;
                }
            }
            return fallback;
        }

        private bool HasBaseSymbol(FunctionDefinition fd)
        {
            try { return fd.BaseSymbol != null; }
            catch { return false; }
        }

        private string FdBaseSymbolInfo(Project prj, string identifier)
        {
            var lib = new FunctionDefinitionLibrary(prj);
            var parts = new List<string>();
            foreach (FunctionDefinition fd in lib)
            {
                if (fd.Identifier != null && fd.Identifier.Equals(identifier, StringComparison.OrdinalIgnoreCase))
                {
                    string bs;
                    try
                    {
                        SymbolVariant s = fd.BaseSymbol;
                        bs = (s != null && s.SymbolName != null) ? s.SymbolName : "<null>";
                    }
                    catch (Exception e) { bs = "err:" + e.Message; }
                    parts.Add(bs);
                }
            }
            return identifier + " x" + parts.Count + ": " + string.Join(", ", parts);
        }

        private string RelayCandidates(Project prj)
        {
            var lib = new FunctionDefinitionLibrary(prj);
            var hits = new List<string>();
            foreach (FunctionDefinition fd in lib)
            {
                if (fd.Identifier == null) continue;
                string id = fd.Identifier.ToUpperInvariant();
                if (id.IndexOf("RELAY") >= 0 || id.IndexOf("COIL") >= 0 ||
                    id.IndexOf("CONTACT") >= 0 || id.IndexOf("CONTACTOR") >= 0)
                {
                    if (hits.Count < 25) hits.Add(fd.Identifier);
                }
            }
            hits.Sort();
            string list = string.Join(", ", hits);
            if (hits.Count == 0)
            {
                var all = new List<string>();
                foreach (FunctionDefinition fd in lib)
                    if (fd.Identifier != null && all.Count < 30) all.Add(fd.Identifier);
                all.Sort();
                list = "(none relay-like) all: " + string.Join(", ", all);
            }
            return list;
        }

        // Places a device on a page.
        private Function PlaceBySymbol(Project prj, Page page, SymbolVariant sv, PointD pos1, PointD pos2)
        {
            try
            {
                Function fn = new Function();
                fn.Create(page, sv, pos1, pos2);
                return fn;
            }
            catch
            {
                return null;
            }
        }

        private void ParsePageName(string name, PagePropertyList props)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return;
            string rest = name;
            int i;
            if (rest.StartsWith("="))
            {
                i = rest.IndexOfAny(new[] { '+', '/' });
                if (i > 1)
                {
                    props[PPage.DESIGNATION_PLANT] = rest.Substring(1, i - 1);
                    rest = rest.Substring(i);
                }
            }
            if (rest.StartsWith("+"))
            {
                i = rest.IndexOf('/');
                if (i > 1)
                {
                    props[PPage.DESIGNATION_LOCATION] = rest.Substring(1, i - 1);
                    rest = rest.Substring(i);
                }
            }
            if (rest.StartsWith("/") && rest.Length > 1)
                props[PPage.PAGE_NUMBER] = rest.Substring(1);
        }

        private DocumentTypeManager.DocumentType PageTypeOf(string pageType)
        {
            if (string.IsNullOrEmpty(pageType)) return DocumentTypeManager.DocumentType.Circuit;
            try
            {
                return (DocumentTypeManager.DocumentType)Enum.Parse(
                    typeof(DocumentTypeManager.DocumentType), pageType, true);
            }
            catch { return DocumentTypeManager.DocumentType.Circuit; }
        }

        private void ApplyPen(object obj, Dictionary<string, object> d)
        {
            try
            {
                Type t = obj.GetType();
                object pen = t.GetProperty("Pen").GetValue(obj, null);
                if (pen == null) return;
                Type pt = pen.GetType();
                if (d.ContainsKey("colorId")) pt.GetProperty("ColorId").SetValue(pen, (short)Num(d, "colorId", 0), null);
                if (d.ContainsKey("styleId")) pt.GetProperty("StyleId").SetValue(pen, (short)Num(d, "styleId", 0), null);
                if (d.ContainsKey("width")) pt.GetProperty("Width").SetValue(pen, Num(d, "width", 1), null);
                t.GetProperty("Pen").SetValue(obj, pen, null);
            }
            catch { }
        }

        private double[] PointsOf(Dictionary<string, object> d, string key)
        {
            if (!d.ContainsKey(key)) return null;
            object v = d[key];
            if (v is List<object>)
            {
                var arr = (List<object>)v;
                if (arr.Count >= 2)
                    return new[] { ConvDouble(arr[0]), ConvDouble(arr[1]) };
            }
            if (v is System.Collections.ArrayList)
            {
                var arr = (System.Collections.ArrayList)v;
                if (arr.Count >= 2)
                    return new[] { ConvDouble(arr[0]), ConvDouble(arr[1]) };
            }
            return null;
        }

        private PointD? PointOf(object v)
        {
            List<object> list = v as List<object>;
            System.Collections.ArrayList al = v as System.Collections.ArrayList;
            if (list == null && al == null) return null;
            if (al != null && al.Count >= 2)
                return new PointD(ConvDouble(al[0]), ConvDouble(al[1]));
            if (list != null && list.Count >= 2)
                return new PointD(ConvDouble(list[0]), ConvDouble(list[1]));
            return null;
        }

        private static string Get(Dictionary<string, string> d, string k, string def)
        {
            string v; return d.TryGetValue(k, out v) ? v : def;
        }

        private static string Str(Dictionary<string, object> d, string k)
        {
            object v;
            return (d.TryGetValue(k, out v) && v != null) ? v.ToString() : "";
        }

        private static double Num(Dictionary<string, object> d, string k, double def)
        {
            object v;
            if (d.TryGetValue(k, out v) && v != null)
            {
                try { return Convert.ToDouble(v); } catch { }
            }
            return def;
        }

        private static double ConvDouble(object v)
        {
            try { return Convert.ToDouble(v); } catch { return 0; }
        }

        private static int Count(IEnumerable<Dictionary<string, object>> items)
        {
            int n = 0;
            foreach (var _ in items) n++;
            return n;
        }

        private static string Quote(string s)
        {
            if (string.IsNullOrEmpty(s)) return "\"\"";
            return "\"" + s.Replace("\"", "'") + "\"";
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }
    }

    // -------------------------------------------------------------------------
    // base64(JSON) helper: stays inside the flat key=value message contract.
    // -------------------------------------------------------------------------
    internal static class JsonB64
    {
        private static readonly JavaScriptSerializer _js = new JavaScriptSerializer();

        public static string Encode(object o)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(_js.Serialize(o)));
        }

        public static Dictionary<string, object> DecodeObject(string b64)
        {
            try
            {
                if (string.IsNullOrEmpty(b64)) return null;
                var o = _js.DeserializeObject(DecodeStr(b64)) as Dictionary<string, object>;
                return o;
            }
            catch { return null; }
        }

        public static List<Dictionary<string, object>> DecodeArray(string b64)
        {
            try
            {
                if (string.IsNullOrEmpty(b64)) return null;
                var arr = _js.DeserializeObject(DecodeStr(b64)) as ArrayList;
                if (arr == null) return null;
                var list = new List<Dictionary<string, object>>();
                foreach (object o in arr)
                {
                    var d = o as Dictionary<string, object>;
                    if (d != null) list.Add(d);
                }
                return list;
            }
            catch { return null; }
        }

        private static string DecodeStr(string b64)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
        }
    }

    internal sealed class Transport
    {
        public const int PeriodMs = 500;

        private readonly string _root;
        private readonly string _inbox;
        private readonly string _outbox;
        private readonly string _signal;

        public Transport()
        {
            _root   = Environment.GetEnvironmentVariable("EPLAN_MCP_BASE_DIR");
            if (string.IsNullOrEmpty(_root)) _root = @"C:\eplan_mcp";
            _inbox  = Path.Combine(_root, "command.txt");
            _outbox = Path.Combine(_root, "result.txt");
            _signal = Path.Combine(_root, "trigger.flag");
        }

        public bool TryTakeRequest()
        {
            if (!File.Exists(_signal)) return false;
            File.Delete(_signal);
            return File.Exists(_inbox);
        }

        public Dictionary<string, string> ReadRequest()
        {
            if (!File.Exists(_inbox)) return null;
            var d = ParseFields(File.ReadAllLines(_inbox));
            try { File.Delete(_inbox); } catch { }
            return d;
        }

        public void WriteReply(Dictionary<string, string> fields)
        {
            var sb = new StringBuilder();
            foreach (var kv in fields) sb.AppendLine(kv.Key + "=" + kv.Value);
            File.WriteAllText(_outbox, sb.ToString());
        }

        private static Dictionary<string, string> ParseFields(IEnumerable<string> raws)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in raws)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int p = line.IndexOf('=');
                if (p < 0) continue;
                d[line.Substring(0, p).Trim()] = line.Substring(p + 1).Trim();
            }
            return d;
        }
    }
}
