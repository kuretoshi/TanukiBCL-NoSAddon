param([Parameter(Mandatory = $true)][string]$GameDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskGame = (Resolve-Path -LiteralPath $GameDirectory).Path
$taskExe = Join-Path $taskGame 'Among Us.exe'
if (Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq $taskExe }) {
    throw 'Close NoS before replacing its addon ZIP.'
}
$taskZipName = 'TanukiBCL-NoSAddon.zip'
$taskZip = Join-Path $taskRoot ('dist/' + $taskZipName)
if (!(Test-Path -LiteralPath $taskZip)) { throw 'Run package.ps1 first.' }
$taskAddons = Join-Path $taskGame 'Addons'
$taskBackup = Join-Path $taskRoot ('dist/installed-backups/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskBackup -Force | Out-Null
foreach ($taskOld in Get-ChildItem -LiteralPath $taskAddons -File | Where-Object { $_.Name -eq 'TanukiBCL-NoSAddon.zip' -or $_.Name -like 'TanukiBCL-Roles-*.zip' -or $_.Name -like 'TanukiBCL-NoSAddon-*.zip' }) {
    Move-Item -LiteralPath $taskOld.FullName -Destination (Join-Path $taskBackup $taskOld.Name)
}
Copy-Item -LiteralPath $taskZip -Destination (Join-Path $taskAddons $taskZipName)
if ((Get-FileHash -LiteralPath $taskZip).Hash -ne (Get-FileHash -LiteralPath (Join-Path $taskAddons $taskZipName)).Hash) {
    throw 'Installed ZIP hash does not match.'
}
Write-Output ('Installed ' + $taskZipName)
