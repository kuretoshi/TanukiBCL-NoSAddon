$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$addonPath = Join-Path $taskRoot 'addon'
$outputDir = Join-Path $taskRoot 'dist'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$outputPath = Join-Path $outputDir 'TanukiBCL-NoSAddon.zip'
$taskStream = [System.IO.File]::Open($outputPath, [System.IO.FileMode]::Create)
$taskArchive = [System.IO.Compression.ZipArchive]::new($taskStream, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($taskFile in Get-ChildItem -LiteralPath $addonPath -Recurse -File -Force) {
        # NoS searches for "Scripts/" literally; Windows Compress-Archive writes backslashes.
        $entryName = $taskFile.FullName.Substring($addonPath.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($taskArchive, $taskFile.FullName, $entryName) | Out-Null
    }
} finally {
    $taskArchive.Dispose()
    $taskStream.Dispose()
}
Write-Output $outputPath
