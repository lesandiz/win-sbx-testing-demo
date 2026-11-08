<#
.SYNOPSIS
  Publish RomanCalc self-contained and run its UI test in a fresh, offline Windows Sandbox via the sbx CLI.

.EXAMPLE
  .\sandbox\Run-SandboxTest.ps1            # publish + test
  .\sandbox\Run-SandboxTest.ps1 -NoPublish # reuse artifacts\publish
  .\sandbox\Run-SandboxTest.ps1 -Keep      # leave the sandbox running to poke at it (sbx ui tree, sbx shot, sbx stop)
#>
[CmdletBinding()]
param(
    [switch]$NoPublish,
    [switch]$Keep,
    [switch]$Record,
    [string]$Sbx = (Join-Path $env:LOCALAPPDATA 'sandbox-mcp\releases\v1.2.0\sbx.cmd')
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root 'artifacts\publish'
$exe = Join-Path $publish 'RomanCalc.App.exe'

if (-not (Test-Path $Sbx)) { throw "sbx not found at $Sbx" }

# Keep sbx state/run folders out of %LOCALAPPDATA% unless already configured. Needed when this runs from a
# packaged (MSIX) host such as the Claude desktop app, whose AppData writes are redirected and invisible to wsb.exe.
if (-not $env:SANDBOX_MCP_HOME) { $env:SANDBOX_MCP_HOME = Join-Path $env:USERPROFILE 'sandbox-mcp' }

if (-not $NoPublish) {
    dotnet publish (Join-Path $root 'src\RomanCalc.App') -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none `
        -o $publish -nologo -v q
    if ($LASTEXITCODE) { throw 'dotnet publish failed' }
}
if (-not (Test-Path $exe)) { throw "Missing $exe; run without -NoPublish." }

$sbxArgs = @('run', $exe, '--network', 'off', '--wait', '5', '--test', (Join-Path $PSScriptRoot 'ui-test.ps1'))
if ($Keep) { $sbxArgs += '--keep' }
if ($Record) { $sbxArgs += '--record' }

& $Sbx @sbxArgs
$code = $LASTEXITCODE
Write-Host ("`nsbx exit code: {0} ({1})" -f $code, $(if ($code -eq 0) { 'PASS' } else { 'FAIL' }))
exit $code
