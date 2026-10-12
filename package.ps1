<#
.SYNOPSIS
    Build a clean release archive of eplan-mcp.

.DESCRIPTION
    The release archive is produced exclusively with `git archive`, which
    serializes only the files committed to the repository. It therefore
    respects both .gitignore (which fails closed by whitelisting publishable
    paths) and the `export-ignore` rules in .gitattributes.

    This matters for hygiene: a developer's working tree often contains local,
    untracked build artifacts -- compiled caches, virtual environments, local
    config -- that are deliberately kept out of version control. Zipping the
    working directory by hand would sweep those artifacts into the release.
    `git archive` cannot, because it reads from the commit, not the working
    tree, so anything untracked or ignored is structurally excluded.

    Always package with this script (or `git archive` directly). Never hand-zip
    the working folder.

.PARAMETER Ref
    The git ref (tag, branch, or commit) to archive. Defaults to HEAD.

.PARAMETER OutDir
    Directory to write the archive into. Defaults to the system temp folder so
    the artifact never lands inside the working tree.

.EXAMPLE
    ./package.ps1
    Package the current HEAD into a .zip in the temp folder.

.EXAMPLE
    ./package.ps1 -Ref v1.0.0
    Package a specific tagged release.
#>
[CmdletBinding()]
param(
    [string]$Ref = "HEAD",
    [string]$OutDir = $env:TEMP
)

$ErrorActionPreference = "Stop"

# Resolve the repository root from this script's location so the script works
# regardless of the caller's current directory.
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $repoRoot
try {
    # Confirm we are inside a git work tree; the clean-archive guarantee only
    # holds when packaging from committed history.
    git rev-parse --is-inside-work-tree > $null
    if ($LASTEXITCODE -ne 0) {
        throw "Not a git repository. Release archives must be built from committed history, not a loose folder."
    }

    # Warn if the tree is dirty. Uncommitted edits will NOT be in the archive
    # (that is the point), but the operator should know the archive reflects the
    # committed ref rather than what they see on disk.
    $dirty = git status --porcelain
    if ($dirty) {
        Write-Warning "Working tree has uncommitted changes; they will NOT be included. Archiving committed ref '$Ref'."
    }

    # --- Sweep compiled caches out of the working tree (defense in depth) -----
    # `git archive` already excludes these via .gitignore/.gitattributes, so the
    # produced archive is clean regardless. We still remove them from disk so that
    # a stray cache can never be picked up by an out-of-band hand-zip, and so the
    # tree carries no compiled artifacts at all. Compiled caches embed the absolute
    # source path of the machine that built them, which is not something a release
    # should ever expose.
    $cachePaths = @(Get-ChildItem -LiteralPath $repoRoot -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object { ($_.PSIsContainer -and $_.Name -eq '__pycache__') -or ($_.Extension -in '.pyc', '.pyo') })
    foreach ($c in $cachePaths) {
        if (Test-Path -LiteralPath $c.FullName) {
            Remove-Item -LiteralPath $c.FullName -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    # Hard gate: refuse to package if any compiled cache survives on disk.
    $leftover = @(Get-ChildItem -LiteralPath $repoRoot -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object { ($_.PSIsContainer -and $_.Name -eq '__pycache__') -or ($_.Extension -in '.pyc', '.pyo') })
    if ($leftover.Count -gt 0) {
        $names = ($leftover | ForEach-Object { $_.FullName }) -join "`n  "
        throw "Compiled cache present after cleanup; aborting to avoid leaking build-machine paths:`n  $names"
    }

    $name    = (Get-Content (Join-Path $repoRoot "VERSION") -ErrorAction SilentlyContinue | Select-Object -First 1)
    if (-not $name) { $name = "snapshot" }
    $stamp   = Get-Date -Format "yyyyMMdd-HHmmss"
    $outFile = Join-Path $OutDir ("eplan-mcp-{0}-{1}.zip" -f $name, $stamp)

    Write-Host "Packaging ref '$Ref' -> $outFile"

    # The whole point: git archive serializes the committed tree only, honoring
    # .gitignore and .gitattributes export-ignore. Untracked caches/artifacts in
    # the working folder cannot leak into the output.
    git archive --format=zip --prefix="eplan-mcp/" --output="$outFile" $Ref
    if ($LASTEXITCODE -ne 0) {
        throw "git archive failed for ref '$Ref'."
    }

    Write-Host "Done. Clean archive written to:" -ForegroundColor Green
    Write-Host "  $outFile"
}
finally {
    Pop-Location
}
