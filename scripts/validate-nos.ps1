param(
    [Parameter(Mandatory = $true)][string]$GameDirectory
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$gameRoot = (Resolve-Path -LiteralPath $GameDirectory).Path
$compiler = Join-Path $gameRoot 'BepInEx/tools/AddonScriptCompiler.exe'
$references = Join-Path $gameRoot 'Cache/Dll/references.txt'
if (!(Test-Path -LiteralPath $compiler) -or !(Test-Path -LiteralPath $references)) {
    throw 'Launch NoS once to create the addon compiler and reference list.'
}
New-Item -ItemType Directory -Force -Path (Join-Path $taskRoot 'dist') | Out-Null
$output = Join-Path $taskRoot 'dist/NoSCompilerValidation.dll'
$taskCompilerArgs = @('--module-name', 'Script.TanukiBclRoles', '--source-dir', (Join-Path $taskRoot 'addon/Scripts'), '--output', $output, '--config', $references)
$taskBehaviour = Get-Content -LiteralPath (Join-Path $taskRoot 'addon/Scripts/.behaviour') -Raw | ConvertFrom-Json
if ($taskBehaviour.UseHiddenMembers) { $taskCompilerArgs += '--use-hidden-members' }
Push-Location -LiteralPath $gameRoot
try {
    & $compiler @taskCompilerArgs
    if ($LASTEXITCODE -ne 0) { throw "NoS addon compilation failed; see $output.log" }
    Get-Content -LiteralPath ($output + '.log') -Encoding UTF8
} finally {
    Pop-Location
}
