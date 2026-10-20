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
$taskOldZips = @(Get-ChildItem -LiteralPath $taskAddons -File | Where-Object {
    $_.Name -match '^(?:\[M\d+\])?TanukiBCL-(?:NoSAddon(?:-.*)?|Roles-.*)\.zip$'
})
# Keep the subscription marker so NoS does not download a second copy at startup.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskMarketBytes = $null
$taskInstalledName = $taskZipName
foreach ($taskOld in $taskOldZips) {
    $taskArchive = [System.IO.Compression.ZipFile]::OpenRead($taskOld.FullName)
    try {
        $taskEntry = $taskArchive.GetEntry('.marketplace')
        if ($null -eq $taskEntry) { continue }
        $taskEntryStream = $taskEntry.Open()
        $taskBuffer = New-Object System.IO.MemoryStream
        try {
            $taskEntryStream.CopyTo($taskBuffer)
            $taskMarketBytes = $taskBuffer.ToArray()
            $taskInstalledName = $taskOld.Name
        } finally { $taskEntryStream.Dispose(); $taskBuffer.Dispose() }
        break
    } finally { $taskArchive.Dispose() }
}
foreach ($taskOld in $taskOldZips) {
    Move-Item -LiteralPath $taskOld.FullName -Destination (Join-Path $taskBackup $taskOld.Name)
}
$taskInstalledPath = Join-Path $taskAddons $taskInstalledName
Copy-Item -LiteralPath $taskZip -Destination $taskInstalledPath
if ((Get-FileHash -LiteralPath $taskZip).Hash -ne (Get-FileHash -LiteralPath $taskInstalledPath).Hash) {
    throw 'Installed ZIP hash does not match.'
}
if ($null -ne $taskMarketBytes) {
    # NoS also uses this hash as its compiled-script cache key.
    $taskMd5 = [System.Security.Cryptography.MD5]::Create()
    $taskZipStream = [System.IO.File]::OpenRead($taskZip)
    try { $taskHashString = [BitConverter]::ToString($taskMd5.ComputeHash($taskZipStream)) }
    finally { $taskZipStream.Dispose(); $taskMd5.Dispose() }
    $taskScriptHash = 0L
    foreach ($taskCharacter in $taskHashString.ToCharArray()) {
        $taskScriptHash = ($taskScriptHash * 467L + [int]$taskCharacter) % 2147283659L
    }
    $taskMarketMeta = [System.Text.Encoding]::UTF8.GetString($taskMarketBytes) | ConvertFrom-Json
    $taskMarketMeta.Hash = [int]$taskScriptHash
    $taskMarketBytes = [System.Text.Encoding]::UTF8.GetBytes(($taskMarketMeta | ConvertTo-Json -Compress))
    $taskArchive = [System.IO.Compression.ZipFile]::Open($taskInstalledPath, [System.IO.Compression.ZipArchiveMode]::Update)
    try {
        $taskExistingEntry = $taskArchive.GetEntry('.marketplace')
        if ($null -ne $taskExistingEntry) { $taskExistingEntry.Delete() }
        $taskEntryStream = $taskArchive.CreateEntry('.marketplace').Open()
        try { $taskEntryStream.Write($taskMarketBytes, 0, $taskMarketBytes.Length) }
        finally { $taskEntryStream.Dispose() }
    } finally { $taskArchive.Dispose() }
}
Write-Output ('Installed ' + $taskInstalledName)
