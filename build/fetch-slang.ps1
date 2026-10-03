<#
.SYNOPSIS
    Downloads the Slang compiler this checkout builds its shaders with, into build/tools/slang.

.DESCRIPTION
    The PowerShell twin of fetch-slang.sh. Every shader the engine draws with is Slang, compiled
    to SPIR-V by slangc when it is loaded, and the engine looks for it at build/tools/slang/bin,
    walking up from the running program and from the working directory. ENGINE_SLANGC still wins
    when it is set. A shipped game reads its .slang-cache instead and needs no compiler.

.PARAMETER Force
    Download it again over what is there.
#>
[CmdletBinding()]
param([switch] $Force)

$ErrorActionPreference = 'Stop'

$Version = '2026.18.2'
$ToolsDir = Join-Path $PSScriptRoot 'tools'
$SlangDir = Join-Path $ToolsDir 'slang'
$Stamp = Join-Path $SlangDir '.version'

if (-not $Force -and (Test-Path $Stamp) -and ((Get-Content $Stamp) -eq $Version)) {
    Write-Host "==> slangc $Version is already at $SlangDir"
    return
}

$arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'aarch64' } else { 'x86_64' }

if ($IsLinux) {
    $archive = "slang-$Version-linux-$arch-glibc-2.28.tar.gz"
} elseif ($IsMacOS) {
    $archive = "slang-$Version-macos-$arch.tar.gz"
} else {
    $archive = "slang-$Version-windows-$arch.zip"
}

$url = "https://github.com/shader-slang/slang/releases/download/v$Version/$archive"
$download = Join-Path $ToolsDir $archive

New-Item -ItemType Directory -Force -Path $ToolsDir | Out-Null
Write-Host "==> downloading $url"
Invoke-WebRequest -Uri $url -OutFile $download

if (Test-Path $SlangDir) { Remove-Item -Recurse -Force $SlangDir }
New-Item -ItemType Directory -Force -Path $SlangDir | Out-Null

if ($archive.EndsWith('.zip')) {
    Expand-Archive -Path $download -DestinationPath $SlangDir
} else {
    tar -xzf $download -C $SlangDir
}

Remove-Item -Force $download
Set-Content -Path $Stamp -Value $Version

Write-Host "==> slangc $Version is at $(Join-Path $SlangDir 'bin')"
