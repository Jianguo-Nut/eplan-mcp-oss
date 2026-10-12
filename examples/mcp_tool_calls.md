# Running the example through an MCP client

`quickstart.py` talks to the add-in over the transport directly. If you
have wired the MCP server into an MCP client (for example Claude Code), you can
run the same workflow as a sequence of tool calls instead. The tools below are
part of the public framework surface.

## The workflow

| Step | Tool | Arguments | Purpose |
|------|------|-----------|---------|
| 1 | `eplan_status` | — | Check that EPLAN is running and the add-in answers. |
| 2 | `eplan_ensure_ready` | — | Start EPLAN if needed and wait until it is ready. |
| 3 | `eplan_open_project` | `path` | Open the sample project. |
| 4 | `eplan_list_pages` | `path` | List the page names of the project. |
| 5 | `eplan_export_pdf` | `path`, `out` | Export the project to PDF. |
| 6 | `eplan_close_project` | `path` | Close the project. |

## Example prompt

A natural-language request to an MCP client that exposes these tools:

> Open the project at `C:\projects\sample_project.elk`, list its pages, then
> export it to `C:\out\sample_project.pdf`.

The client selects `eplan_open_project`, `eplan_list_pages`, and
`eplan_export_pdf` in turn and reports the result of each step.

## Example arguments

```json
{
  "eplan_open_project":  { "path": "C:\\projects\\sample_project.elk" },
  "eplan_list_pages":    { "path": "C:\\projects\\sample_project.elk" },
  "eplan_export_pdf":    { "path": "C:\\projects\\sample_project.elk",
                           "out":  "C:\\out\\sample_project.pdf" },
  "eplan_close_project": { "path": "C:\\projects\\sample_project.elk" }
}
```
