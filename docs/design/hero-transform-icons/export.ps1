$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$taskAssets = Join-Path $taskRoot 'addon/Resources/Heroes'
foreach ($taskKind in @('Super', 'Evil')) {
    $taskSource = [Drawing.Bitmap]::new((Join-Path $PSScriptRoot ('source/' + $taskKind + 'Transform.png')))
    $taskTarget = [Drawing.Bitmap]::new(256, 256, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $taskGraphics = [Drawing.Graphics]::FromImage($taskTarget)
    try {
        $taskGraphics.Clear([Drawing.Color]::Transparent)
        $taskGraphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
        $taskGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $taskScale = 224.0 / [Math]::Max($taskSource.Width, $taskSource.Height)
        $taskWidth = [int][Math]::Round($taskSource.Width * $taskScale)
        $taskHeight = [int][Math]::Round($taskSource.Height * $taskScale)
        $taskRect = [Drawing.Rectangle]::new([int]((256-$taskWidth)/2), [int]((256-$taskHeight)/2), $taskWidth, $taskHeight)
        $taskGraphics.DrawImage($taskSource, $taskRect)
        $taskTarget.Save((Join-Path $taskAssets ($taskKind + 'Transform.png')), [Drawing.Imaging.ImageFormat]::Png)
    } finally { $taskGraphics.Dispose(); $taskTarget.Dispose(); $taskSource.Dispose() }
}
Write-Output 'Exported 2 transparent 256x256 transformation icons with 16px margins.'
