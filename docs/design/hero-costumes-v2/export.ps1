$ErrorActionPreference = 'Stop'
# Preserve the generated artwork/alpha; export at the supplied NoS template anchors.
. "$PSScriptRoot/inspect.ps1" | Out-Null
function Get-CosmicPath([string]$Value) {
    [long]$taskHash = 0
    foreach ($taskCharacter in $Value.ToCharArray()) { $taskHash = ($taskHash * 467 + [int]$taskCharacter) % 531206959292021 }
    $taskAlphabet = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'
    $taskResult = ''
    while ($taskHash -gt 0) {
        $taskResult += $taskAlphabet[[int]($taskHash % 62)]
        $taskHash = [long][Math]::Floor($taskHash / 62)
    }
    return $taskResult
}
$taskAuthor = 'TanukiBCL'
$taskAuthorPath = Get-CosmicPath $taskAuthor
$taskEntries = @()
$taskRecords = @()
foreach ($taskHero in @('SuperHero','EvilHero')) {
    $taskPrefix = if ($taskHero -eq 'SuperHero') { 'super' } else { 'evil' }
    $taskRelative = 'hats/' + $taskAuthorPath + '/' + (Get-CosmicPath $taskHero)
    $taskDirectory = Join-Path "$PSScriptRoot/MoreCosmic" $taskRelative
    New-Item -ItemType Directory -Force -Path $taskDirectory | Out-Null
    $taskEntry = [ordered]@{ Name=$taskHero; Author=$taskAuthor; Package='TanukiHeroes'; IsHidden=$true; Adaptive=$false; Bounce=$false; HideHands=$true; FPS=1 }
    foreach ($taskPose in @(@('main','Main'),@('climb','Climb'),@('climb-down','ClimbDown'))) {
        $taskSource = Join-Path "$PSScriptRoot/source" ($taskPrefix+'-'+$taskPose[0]+'.png')
        $taskTemplate = Join-Path "$PSScriptRoot/reference" ($taskPose[0]+'.png')
        $taskFrom = [HeroPixelInspector]::Anchors($taskSource)
        $taskTo = [HeroPixelInspector]::Anchors($taskTemplate)
        # ClimbUp's lower foot alternates sides. Align the helmet/torso, not that foot.
        if ($taskPose[1] -eq 'Climb') {
            $taskTo = @(300,375,90,301,158.5)
            $taskFrom = if ($taskHero -eq 'SuperHero') { @(1122,1402,453,1101,628) } else { @(1122,1402,436,1102,619) }
        }
        $taskScale = ($taskTo[3]-$taskTo[2]) / ($taskFrom[3]-$taskFrom[2])
        $taskX = $taskTo[4] - $taskFrom[4] * $taskScale
        $taskY = $taskTo[2] - $taskFrom[2] * $taskScale
        $taskBitmap = [Drawing.Bitmap]::new(300,375,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $taskImage = [Drawing.Bitmap]::new($taskSource)
        $taskGraphics = [Drawing.Graphics]::FromImage($taskBitmap)
        $taskFilename = $taskPose[1]+'.png'
        try {
            $taskGraphics.Clear([Drawing.Color]::Transparent)
            $taskGraphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $taskGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $taskGraphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $taskRect = [Drawing.RectangleF]::new([single]$taskX,[single]$taskY,[single]($taskImage.Width*$taskScale),[single]($taskImage.Height*$taskScale))
            $taskGraphics.DrawImage($taskImage,$taskRect,[Drawing.RectangleF]::new(0,0,$taskImage.Width,$taskImage.Height),[Drawing.GraphicsUnit]::Pixel)
            $taskBitmap.Save((Join-Path $taskDirectory $taskFilename),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $taskGraphics.Dispose(); $taskImage.Dispose(); $taskBitmap.Dispose() }
        # ZIP/local loaders resolve Address relative to hats/, unlike remote caches.
        $taskEntry[$taskPose[1]] = @{ Address=($taskRelative.Substring(5)+'/'+$taskFilename) }
        $taskRecords += [ordered]@{hero=$taskHero;pose=$taskPose[1];file=($taskRelative+'/'+$taskFilename);source=($taskPrefix+'-'+$taskPose[0]+'.png');scale=$taskScale;x=$taskX;y=$taskY;templateAnchors=$taskTo}
    }
    $taskEntries += $taskEntry
}
$taskJson = [ordered]@{BundleName='TanukiHeroCostumesV2'; Hats=$taskEntries; Visors=@(); Nameplates=@(); Stamps=@(); Packages=@()}
[IO.File]::WriteAllText("$PSScriptRoot/MoreCosmic/Contents.json",($taskJson | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText("$PSScriptRoot/alignment.json",($taskRecords | ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
Write-Output 'Exported 6 transparent 300x375 textures and UTF-8 MoreCosmic/Contents.json.'
if (Test-Path "$PSScriptRoot/source/super-walk.png") { & "$PSScriptRoot/export-walk.ps1" }
