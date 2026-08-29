<#
.SYNOPSIS
    Builds the LENS extension for Visual Studio.

.DESCRIPTION
    Publishes the language server into Lens.VisualStudio\server and then builds the VSIX around it.
    The two steps are separate because the VSIX is a .NET Framework project built by MSBuild while
    the server is a net10.0 project built by the dotnet CLI - neither build system can drive the
    other, so a script has to run both in order.

.PARAMETER Configuration
    Release by default; Debug produces a VSIX that can be debugged in an experimental instance.

.PARAMETER SkipServer
    Leaves whatever is already in server\ alone. Useful because a running Visual Studio holds the
    server files open, which makes the publish fail with a locked file.
#>
[CmdletBinding()]
param(
    [string] $Configuration = 'Release',
    [switch] $SkipServer
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$repository = Resolve-Path (Join-Path $root '..\..')
$project = Join-Path $root 'Lens.VisualStudio\Lens.VisualStudio.csproj'
$serverOutput = Join-Path $root 'Lens.VisualStudio\server'

if (-not $SkipServer) {
    Write-Host "Publishing the language server into $serverOutput" -ForegroundColor Cyan

    & dotnet publish (Join-Path $repository 'Lens.LanguageServer\Lens.LanguageServer.csproj') `
        -c Release `
        -o $serverOutput

    if ($LASTEXITCODE -ne 0) {
        throw 'Publishing the language server failed. Close any Visual Studio instance running the extension - it holds the server files open - or pass -SkipServer.'
    }
}

# MSBuild rather than "dotnet build": the VSIX targets are .NET Framework tasks that the dotnet CLI
# cannot load
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -prerelease -products * `
    -requires Microsoft.Component.MSBuild `
    -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1

if (-not $msbuild) {
    throw 'MSBuild was not found. Install Visual Studio 2022 or newer.'
}

Write-Host "Building the VSIX with $msbuild" -ForegroundColor Cyan

& $msbuild $project -t:Rebuild -restore -p:Configuration=$Configuration -v:minimal -nologo

if ($LASTEXITCODE -ne 0) {
    throw 'Building the VSIX failed.'
}

$vsix = Join-Path $root "Lens.VisualStudio\bin\$Configuration\net472\Lens.VisualStudio.vsix"

Write-Host ''
Write-Host "Built $vsix" -ForegroundColor Green
