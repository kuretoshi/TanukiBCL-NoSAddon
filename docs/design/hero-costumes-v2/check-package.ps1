param([Parameter(Mandatory=$true)][string]$ZipPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing
$taskArchive=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ZipPath).Path)
try {
    $taskEntry=$taskArchive.GetEntry('MoreCosmic/Contents.json')
    if(!$taskEntry){throw 'MoreCosmic/Contents.json is missing.'}
    $taskReader=[IO.StreamReader]::new($taskEntry.Open())
    try {$taskDefinition=$taskReader.ReadToEnd() | ConvertFrom-Json} finally {$taskReader.Dispose()}
    foreach($taskHat in $taskDefinition.Hats) {
        foreach($taskPose in @('Main','Climb','ClimbDown','Move','EnterVent')) {
            if(!$taskHat.$taskPose){continue}
            $taskPath='MoreCosmic/hats/'+$taskHat.$taskPose.Address
            $taskImageEntry=$taskArchive.GetEntry($taskPath)
            if(!$taskImageEntry){throw ('Missing image at NoS ZIP-loader path: '+$taskPath)}
            $taskStream=$taskImageEntry.Open()
            $taskImage=[Drawing.Bitmap]::new($taskStream)
            try {
                $taskFrames=if($taskHat.$taskPose.Length){[int]$taskHat.$taskPose.Length}else{1}
                if($taskImage.Width -ne (300*$taskFrames) -or $taskImage.Height -ne 375 -or $taskImage.GetPixel(0,0).A -ne 0){throw ('Invalid dimensions/transparency: '+$taskPath)}
                if($taskFrames -gt 1 -and $taskHat.$taskPose.AnimSync -ne 1){throw 'Animated poses must sync to the native animation cycle.'}
                if($taskHat.Name -in @('SuperHero','EvilHero')) {
                    $taskExpected=if($taskPose -eq 'Move'){12}elseif($taskPose -eq 'Climb'){2}else{1}
                    if($taskFrames -ne $taskExpected){throw ('Unexpected hero frame count: '+$taskPose)}
                }
            } finally {$taskImage.Dispose();$taskStream.Dispose()}
            Write-Output ('PASS '+$taskHat.Name+' '+$taskPose+' native ZIP path / '+$taskFrames+' x 300x375 / alpha')
        }
    }
} finally {$taskArchive.Dispose()}
