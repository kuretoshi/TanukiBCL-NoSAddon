$ErrorActionPreference='Stop'
. "$PSScriptRoot/inspect.ps1" | Out-Null
if (-not ('HeroWalkExporter' -as [type])) {
$taskReferences = @('System.Drawing.Common.dll','System.Drawing.Primitives.dll','System.Private.Windows.GdiPlus.dll','System.Private.Windows.Core.dll','System.Collections.dll','System.Runtime.dll','System.Private.CoreLib.dll') | ForEach-Object { Join-Path $PSHOME $_ }
Add-Type -ReferencedAssemblies $taskReferences -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class HeroWalkExporter {
    static Rectangle Bounds(Bitmap image, Rectangle cell) {
        int left=cell.Right, top=cell.Bottom, right=cell.Left, bottom=cell.Top;
        for(int y=cell.Top;y<cell.Bottom;y++) for(int x=cell.Left;x<cell.Right;x++) {
            if(image.GetPixel(x,y).A<128) continue;
            left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
        }
        if(top>=bottom) throw new Exception("Empty walk frame");
        return Rectangle.FromLTRB(left,top,right+1,bottom+1);
    }
    static double HeadX(Bitmap image, Rectangle bounds) {
        var head=Bounds(image,new Rectangle(bounds.X,bounds.Y,bounds.Width,(int)(bounds.Height*.4)));
        return (head.Left+head.Right)*.5;
    }
    public static void Export(string input,string reference,string output) {
        using var source=new Bitmap(input); using var main=new Bitmap(reference);
        int width=source.Width/4,height=source.Height/2;
        var boxes=new Rectangle[8]; var heights=new int[8];
        for(int i=0;i<8;i++) {boxes[i]=Bounds(source,new Rectangle(i%4*width,i/4*height,width,height)); heights[i]=boxes[i].Height;}
        Array.Sort(heights);
        var target=Bounds(main,new Rectangle(0,0,main.Width,main.Height));
        double scale=target.Height/((heights[3]+heights[4])*.5);
        using var strip=new Bitmap(2400,375,PixelFormat.Format32bppArgb);
        using var graphics=Graphics.FromImage(strip);
        graphics.Clear(Color.Transparent); graphics.CompositingMode=CompositingMode.SourceCopy;
        graphics.InterpolationMode=InterpolationMode.HighQualityBicubic; graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
        for(int i=0;i<8;i++) {
            var cell=new Rectangle(i%4*width,i/4*height,width,height);
            double x=i*300+HeadX(main,target)-(HeadX(source,boxes[i])-cell.X)*scale;
            double y=target.Top-(boxes[i].Top-cell.Y)*scale;
            graphics.SetClip(new Rectangle(i*300,0,300,375));
            graphics.DrawImage(source,new RectangleF((float)x,(float)y,(float)(width*scale),(float)(height*scale)),cell,GraphicsUnit.Pixel);
            graphics.ResetClip();
        }
        strip.Save(output,ImageFormat.Png);
    }
}
'@
}
foreach($taskHero in @(@('super','HOb85tfub'),@('evil','0Wg0o9Ic'))) {
    $taskFolder="$PSScriptRoot/MoreCosmic/hats/tWg4uMeMb/"+$taskHero[1]
    [HeroWalkExporter]::Export("$PSScriptRoot/source/$($taskHero[0])-walk.png","$taskFolder/Main.png","$taskFolder/Move.png")
}
$taskContents=Get-Content "$PSScriptRoot/MoreCosmic/Contents.json" -Raw | ConvertFrom-Json
foreach($taskHat in $taskContents.Hats) {
    $taskMove=[ordered]@{Address=($taskHat.Main.Address -replace 'Main.png$','Move.png');Length=8;AnimSync=1}
    $taskHat | Add-Member -NotePropertyName Move -NotePropertyValue $taskMove -Force
}
[IO.File]::WriteAllText("$PSScriptRoot/MoreCosmic/Contents.json",($taskContents|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
Write-Output 'Exported two 8-frame walk strips; fixed helmet anchors and common scale avoid whole-body bouncing.'
