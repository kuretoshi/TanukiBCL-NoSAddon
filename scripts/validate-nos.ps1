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
Push-Location -LiteralPath $gameRoot
try {
    & $compiler --module-name Script.TanukiBclRoles --source-dir (Join-Path $taskRoot 'addon/Scripts') --output $output --config $references
    if ($LASTEXITCODE -ne 0) { throw "NoS addon compilation failed; see $output.log" }
    Get-Content -LiteralPath ($output + '.log') -Encoding UTF8
} finally {
    Pop-Location
}
