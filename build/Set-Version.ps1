<#
.SYNOPSIS
    Stamps a version into every file that carries one by hand.

.DESCRIPTION
    The two NuGet projects take their version from Directory.Build.props, so they are not touched
    here - a pack is given "-p:Version" instead. Everything else lives in a format MSBuild cannot
    reach into: the VSIX manifest, the VS Code manifest and the Rider plugin properties. This
    script is what the release workflow runs before it builds any of them.

    The files are edited in place and are not meant to be committed: only the series in
    Directory.Build.props is tracked, and the patch belongs to the release that produced it.

.PARAMETER Version
    The three-part version to write, for example 5.0.7.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')

# Read and write as UTF-8 without a BOM throughout, so that neither the umlauts nor anything else
# non-ASCII in these files is mangled by a round trip.
function Edit-File {
    param(
        [string] $Path,
        [string] $Pattern,
        [string] $Replacement
    )

    $full = Join-Path $root $Path

    if (-not (Test-Path $full)) {
        throw "$Path does not exist."
    }

    $text = [System.IO.File]::ReadAllText($full)

    if ($text -notmatch $Pattern) {
        throw "No version to replace was found in $Path - the pattern '$Pattern' did not match."
    }

    $updated = [regex]::Replace($text, $Pattern, $Replacement)
    [System.IO.File]::WriteAllText($full, $updated, (New-Object System.Text.UTF8Encoding $false))

    Write-Host "  $Path"
}

Write-Host "Stamping version $Version into:" -ForegroundColor Cyan

# The VSIX manifest version is the one the Visual Studio Marketplace shows and orders releases by.
Edit-File `
    -Path 'editors/vs/Lens.VisualStudio/source.extension.vsixmanifest' `
    -Pattern '(<Identity\b[^>]*?\bVersion=")[^"]*(")' `
    -Replacement "`${1}$Version`${2}"

# The VSIX assembly, kept in step with the manifest so a loaded extension reports the same version.
Edit-File `
    -Path 'editors/vs/Lens.VisualStudio/Lens.VisualStudio.csproj' `
    -Pattern '(<Version>)[^<]*(</Version>)' `
    -Replacement "`${1}$Version`${2}"

# npm, and therefore vsce, take the version from here.
Edit-File `
    -Path 'editors/vscode/package.json' `
    -Pattern '("version"\s*:\s*")[^"]*(")' `
    -Replacement "`${1}$Version`${2}"

# The Rider plugin is built locally rather than by the workflow, but its version is stamped from
# the same place so that a release built by hand carries the same number as the rest.
Edit-File `
    -Path 'editors/rider/gradle.properties' `
    -Pattern '(?m)^(pluginVersion=).*$' `
    -Replacement "`${1}$Version"
