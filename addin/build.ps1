<#
.SYNOPSIS
    Builds the reference EPLAN add-in (ReferenceAddIn.cs) into a .NET class
    library DLL.

.DESCRIPTION
    The add-in references the EPLAN P8 API assemblies (Eplan.EplApi.*) that
    ship with your EPLAN installation. This script locates those assemblies,
    invokes the .NET Framework C# compiler (csc.exe), and emits a DLL you can
    register as an EPLAN add-in.

    Nothing here is specific to a particular EPLAN release or machine layout:
    point -EplanApiDir at the folder that contains your Eplan.EplApi.*.dll
    files (or let the script try a few common locations) and run it.

.PARAMETER EplanApiDir
    Directory containing the Eplan.EplApi.*.dll API assemblies. If omitted,
    the script searches a few common EPLAN install locations.

.PARAMETER OutputDir
    Directory to write the compiled DLL into. Defaults to .\build next to
    this script.

.PARAMETER AssemblyName
    Output DLL file name (without extension). Defaults to ReferenceAddIn.

.EXAMPLE
    .\build.ps1 -EplanApiDir "C:\Program Files\EPLAN\Platform\<version>\bin"
#>

[CmdletBinding()]
param(
    [string]$EplanApiDir = "",
    [string]$OutputDir   = "",
    [string]$AssemblyName = "ReferenceAddIn"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceFile = Join-Path $scriptDir "ReferenceAddIn.cs"

if (-not (Test-Path $sourceFile)) {
    throw "Source file not found: $sourceFile"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $scriptDir "build"
}
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

# --- Locate the EPLAN API assemblies ------------------------------------------
function Find-EplanApiDir {
    param([string]$hint)

    if (-not [string]::IsNullOrWhiteSpace($hint) -and (Test-Path $hint)) {
        if (Get-ChildItem -Path $hint -Filter "Eplan.EplApi.*.dll" -ErrorAction SilentlyContinue) {
            return (Resolve-Path $hint).Path
        }
    }

    # Search a few common install roots for a folder holding the API DLLs.
    $roots = @(
        "C:\Program Files\EPLAN",
        "C:\Program Files (x86)\EPLAN"
    )
    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }
        $hit = Get-ChildItem -Path $root -Recurse -Filter "Eplan.EplApi.AFu.dll" -ErrorAction SilentlyContinue |
               Select-Object -First 1
        if ($hit) { return $hit.Directory.FullName }
    }
    return $null
}

$apiDir = Find-EplanApiDir -hint $EplanApiDir
if (-not $apiDir) {
    throw @"
Could not locate the EPLAN API assemblies (Eplan.EplApi.*.dll).
Pass the folder explicitly, e.g.:
    .\build.ps1 -EplanApiDir "C:\Program Files\EPLAN\Platform\<version>\bin"
"@
}
Write-Host "EPLAN API directory: $apiDir"

# Physical file names of the managed API assemblies as shipped by EPLAN
# Platform 2.9.x (assembly names inside still use the classic Eplan.EplApi.*
# names; only the file names carry the trailing 'u').
$apiRefs = @(
    "Eplan.EplApi.AFu.dll",            # Eplan.EplApi.ApplicationFramework
    "Eplan.EplApi.Baseu.dll",          # Eplan.EplApi.Base
    "Eplan.EplApi.DataModelu.dll",     # Eplan.EplApi.DataModel
    "Eplan.EplApi.HEServicesu.dll"     # Eplan.EplApi.HEServices
)

$references = @("System.dll", "System.Web.Extensions.dll")
foreach ($r in $apiRefs) {
    $full = Join-Path $apiDir $r
    if (Test-Path $full) {
        $references += $full
    } else {
        Write-Warning "API assembly not found (continuing): $r"
    }
}

# --- Locate the C# compiler ---------------------------------------------------
$csc = Join-Path ([System.Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) "csc.exe"
if (-not (Test-Path $csc)) {
    $csc = Get-ChildItem -Path "C:\Windows\Microsoft.NET\Framework64" -Recurse -Filter "csc.exe" -ErrorAction SilentlyContinue |
           Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $csc -or -not (Test-Path $csc)) {
    throw "Could not find the C# compiler (csc.exe). Install the .NET Framework SDK."
}
Write-Host "Compiler: $csc"

# --- Compile ------------------------------------------------------------------
$outDll = Join-Path $OutputDir "$AssemblyName.dll"
$refArgs = $references | ForEach-Object { "/reference:`"$_`"" }

$cscArgs = @(
    "/target:library",
    "/nologo",
    "/optimize+",
    "/out:`"$outDll`""
) + $refArgs + @("`"$sourceFile`"")

Write-Host "Building $outDll ..."
& $csc $cscArgs
if ($LASTEXITCODE -ne 0) {
    throw "Build failed (csc exit code $LASTEXITCODE)."
}

Write-Host ""
Write-Host "Build succeeded: $outDll"
Write-Host "Register this DLL as an EPLAN add-in to load it."
