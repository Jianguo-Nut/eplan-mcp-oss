# Installation

This guide walks through setting up `eplan-mcp`: installing the Python
dependencies, configuring the working directory, building and registering the
reference add-in, and registering the server with your MCP client.

## Prerequisites

- Windows with EPLAN P8 installed.
- Python 3.11 or newer, available on your `PATH`.
- An MCP-capable client (for example, Claude Code).
- The EPLAN P8 API assemblies that ship with your EPLAN installation, plus the
  .NET Framework, for building the add-in.

## 1. Install Python dependencies

From the package root, install the MCP runtime the server uses:

```powershell
pip install "mcp[cli]"
```

Installing into a dedicated virtual environment is recommended.

## 2. Configure the working directory

The server, bridge, and add-in exchange messages through files in a shared
working directory. Copy the template and adjust it for your machine:

```powershell
Copy-Item src/config.template.json config.json
```

Open `config.json` and set:

- the **working directory** the bridge and add-in will share, and
- the path to your **EPLAN executable**.

You can also supply these through environment variables instead of the config
file. Make sure the working directory exists and is writable.

## 3. Build and register the add-in

The companion add-in runs inside EPLAN. A reference implementation is provided
under `addin/`.

1. Compile it to a class library, referencing the EPLAN P8 API assemblies from
   your EPLAN installation. You can use `addin/build.ps1` for this:

   ```powershell
   .\addin\build.ps1 -EplanApiDir "C:\Program Files\EPLAN\Platform\<version>\bin"
   ```

   EPLAN Platform 2.9.x ships the managed API assemblies under physical file
   names with a trailing `u` (`Eplan.EplApi.AFu.dll`, `Baseu.dll`,
   `DataModelu.dll`, `HEServicesu.dll`); `build.ps1` references those files
   already. The namespaces inside keep the classic `Eplan.EplApi.*` form.

2. Copy `addin\build\ReferenceAddIn.dll` to the EPLAN add-in location (for
   example `C:\Program Files\EPLAN\Platform\<version>\Bin\Addins\`, creating
   the folder if needed) so EPLAN loads it at startup.

The exact API assembly versions depend on your EPLAN platform release. See
[`../addin/README.md`](../addin/README.md) for protocol and
build details. After rebuilding the add-in, restart EPLAN so the new DLL is
loaded.

## 4. Restart EPLAN and verify the add-in

Restart EPLAN so the add-in loads. With the add-in loaded, the server's status
tool can confirm the add-in is responding. You can check this from your MCP
client once the server is registered (next step), or by running the bridge's
status check directly.

## 5. Register the server with your MCP client

Register `eplan-mcp` as an MCP server so your client can call its tools. Point
your client at your Python interpreter and `src/mcp_server.py` from this
package, following your client's documentation for adding a stdio MCP server.

For example, with Claude Code:

```powershell
claude mcp add eplan-mcp -- python path\to\src\mcp_server.py
```

Adjust the interpreter and script path to match your environment. Other clients
accept an equivalent stdio server entry in their configuration.

## 6. Confirm it works

From your MCP client, ask it to check EPLAN's status, then to list the pages of
a project you have open. If the status check succeeds and pages come back, the
installation is complete.

For the available tools and example workflows, see [`USAGE.md`](USAGE.md).

## Uninstall

To remove the server registration, delete the `eplan-mcp` server entry from
your MCP client's configuration. With Claude Code:

```powershell
claude mcp remove eplan-mcp
```

Then unregister the add-in from EPLAN if you no longer need it.

## Troubleshooting

- **The status check reports the add-in is not responding.** Confirm EPLAN is
  running with the add-in loaded, and that the working directory in
  `config.json` matches the paths configured in the add-in.
- **The client does not list the tools.** Re-check the server registration with
  your MCP client and that your Python interpreter path is correct.
