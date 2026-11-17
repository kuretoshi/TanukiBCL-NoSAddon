param([string]$OriginalImages = 'C:/Users/tamo-/.codex/generated_images/01a120bc-0d8f-71e3-80a6-999592850f7d')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskDesign = Join-Path $taskRoot 'docs/design/bit-character'
$taskSource = Join-Path $taskDesign 'source'
New-Item -ItemType Directory -Force -Path $taskSource | Out-Null
$taskImages = [ordered]@{
    '2'='exec-7ecae5cb-88b9-4726-a65f-b302a7bc621e.png'
    '4'='exec-8738aef2-d263-4f49-a517-6b0e8565b76b.png'
    '8'='exec-222500ef-06a2-45ad-873c-a2637dce5c17.png'
    '16'='exec-36223556-2bd4-4d8e-8cdd-ed6c9408258c.png'
    '32'='exec-2eb39c20-59a8-491b-8227-c8e5a458df25.png'
    '64'='exec-59e92a83-d9dc-427b-9b4e-4ba529a71fbe.png'
}
foreach ($taskStage in $taskImages.Keys) {
    $taskTarget = Join-Path $taskSource "$($taskStage)bit.png"
    if (!(Test-Path -LiteralPath $taskTarget)) { Copy-Item -LiteralPath (Join-Path $OriginalImages $taskImages[$taskStage]) -Destination $taskTarget }
}
Add-Type -AssemblyName System.Drawing
if (-not ('BitSpriteExporter' -as [type])) {
$taskReferences = @('System.Drawing.Common.dll','System.Drawing.Primitives.dll','System.Private.Windows.GdiPlus.dll','System.Private.Windows.Core.dll','System.Collections.dll','System.Runtime.dll','System.Private.CoreLib.dll') | ForEach-Object { Join-Path $PSHOME $_ }
Add-Type -ReferencedAssemblies $taskReferences -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class BitSpriteExporter {
    static Rectangle Bounds(Bitmap b, Rectangle c) {
        int l=c.Right,t=c.Bottom,r=c.Left,d=c.Top;
        for(int y=c.Top;y<c.Bottom;y++) for(int x=c.Left;x<c.Right;x++) {
            if(b.GetPixel(x,y).A<128) continue;
            l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);d=Math.Max(d,y);
        }
        if(r<=l || d<=t) throw new Exception("Empty sprite cell");
        return Rectangle.FromLTRB(l,t,r+1,d+1);
    }
    static Rectangle Head(Bitmap b, Rectangle c) => Bounds(b,new Rectangle(c.X,c.Y,c.Width,Math.Max(1,(int)(c.Height*.35))));
    static (double center,double width) TorsoRow(Bitmap b,Rectangle bounds,int y) {
        int start=-1,left=0,width=0;
        for(int x=bounds.Left;x<=bounds.Right;x++) {
            var pixel=x<bounds.Right ? b.GetPixel(x,y) : Color.Transparent;
            // The inner backpack panel stays centered when a hand touches
            // the body's outline. Exclude outlines from the anchor scan.
            bool solid=pixel.A>=128 && Math.Max(pixel.R,Math.Max(pixel.G,pixel.B))>48;
            if(solid) { if(start<0) start=x;continue; }
            if(start>=0 && x-start>width) { left=start;width=x-start; }
            start=-1;
        }
        if(width==0) throw new Exception("Missing ladder torso");
        return (left+width*.5,width);
    }
    static (double center,double width,double top) ClimbAnchor(Bitmap b,Rectangle bounds) {
        int start=bounds.Top+(int)(bounds.Height*.4),end=bounds.Top+(int)(bounds.Height*.6);
        var centers=new double[end-start];var widths=new double[end-start];
        for(int y=start;y<end;y++) {
            var row=TorsoRow(b,bounds,y);centers[y-start]=row.center;widths[y-start]=row.width;
        }
        Array.Sort(centers);Array.Sort(widths);double center=centers[centers.Length/2];
        int top=bounds.Top,x=Math.Clamp((int)Math.Round(center),bounds.Left,bounds.Right-1);
        for(int y=bounds.Top;y<bounds.Bottom;y++) {
            if(b.GetPixel(x,y).A<128) continue;
            top=y;break;
        }
        return (center,widths[widths.Length/2],top);
    }
    static Color Mask(Color c,int bits) {
        if(c.A<(bits==64 ? 8 : 128)) return Color.Transparent;
        int alpha=bits==64 ? c.A : 255;
        if(bits==2) {
            int g=(int)Math.Round((c.R+c.G+c.B)/3d/85)*85;
            return Color.FromArgb(255,g,g,g);
        }
        if(Math.Max(c.R,Math.Max(c.G,c.B))<48) return Color.FromArgb(alpha,0,0,25);
        if(c.R>210 && c.G>200 && c.B>200) return Color.FromArgb(alpha,255,255,255);
        if(c.R>c.G*1.35 && c.R>c.B*1.15) {
            if(bits==4) return Color.FromArgb(255,255,0,0);
            double body=Math.Clamp((c.R-85)/170d,0,1);
            if(bits!=64) {
                int tones=bits==8 ? 2 : bits==16 ? 4 : 8;
                body=Math.Round(body*(tones-1))/(tones-1);
            }
            return Color.FromArgb(alpha,(int)Math.Round(body*255),0,(int)Math.Round((1-body)*255));
        }
        int visor=Math.Clamp((int)Math.Max(c.G,c.B),40,255);
        if(bits==4) visor=255;
        else if(bits!=64) {int tones=bits==8 ? 2 : bits==16 ? 4 : 8;visor=80+(int)Math.Round(Math.Round(Math.Max(0,visor-80)/175d*(tones-1))/(tones-1)*175);}
        return Color.FromArgb(alpha,0,visor,0);
    }
    static Rectangle GridCell(Bitmap source,int index,int rows) {
        int col=index%4,row=index/4;
        int left=col*source.Width/4,right=(col+1)*source.Width/4;
        int top=row*source.Height/rows,bottom=(row+1)*source.Height/rows;
        return new Rectangle(left,top,right-left,bottom-top);
    }
    static Bitmap Frame(Bitmap source,int index,int bits,bool mask) {
        int rows=bits==64 ? 4 : 3;
        var cell=GridCell(source,index,rows);
        var bounds=Bounds(source,cell); var head=Head(source,bounds);
        int height=index==9 || index==10 ? 211 : index==11 ? 140 : 190;
        double scale=height/(double)bounds.Height;
        // Fix all walking helmet widths and top positions; leg extent must not change body scale.
        if(index<8) {
            var idle=Bounds(source,GridCell(source,8,rows));var idleHead=Head(source,idle);
            scale=(190d/idle.Height)*idleHead.Width/head.Width;
        }
        double headX=(head.Left+head.Right)*.5;
        double top=bounds.Top;
        if(index==9 || index==10) {
            // Hands alternate outside the torso. Anchor the body itself so
            // changing hands cannot pull the entire character sideways.
            var anchor=ClimbAnchor(source,bounds);
            var referenceBounds=Bounds(source,GridCell(source,9,rows));
            var reference=ClimbAnchor(source,referenceBounds);
            scale=211d/referenceBounds.Height*reference.width/anchor.width;
            headX=anchor.center;top=anchor.top;
        }
        double x=153-headX*scale;
        double y=(index==9 || index==10 ? 90 : index==11 ? 160 : 115)-top*scale;
        return Render(source,cell,scale,x,y,bits,mask);
    }
    static Bitmap Descent(Bitmap source,int index,int bits) {
        int left=index*source.Width/5,right=(index+1)*source.Width/5;
        var cell=new Rectangle(left,0,right-left,source.Height);
        return DescentCell(source,cell,bits);
    }
    static Bitmap DescentCell(Bitmap source,Rectangle cell,int bits) {
        var bounds=Bounds(source,cell);var anchor=ClimbAnchor(source,bounds);
        // NoS's descent template hangs with both hands overhead. Keep its
        // lower helmet/feet placement rather than reusing an ascent frame.
        double scale=195d/(bounds.Bottom-anchor.top);
        return Render(source,cell,scale,153-anchor.center*scale,140-anchor.top*scale,bits,true);
    }
    static Bitmap Render(Bitmap source,Rectangle cell,double scale,double x,double y,int bits,bool mask) {
        var result=new Bitmap(300,375,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(result)) {
            g.Clear(Color.Transparent);g.CompositingMode=CompositingMode.SourceCopy;
            g.InterpolationMode=bits==64 ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
            g.DrawImage(source,new RectangleF((float)(x+cell.X*scale),(float)(y+cell.Y*scale),(float)(cell.Width*scale),(float)(cell.Height*scale)),cell,GraphicsUnit.Pixel);
        }
        for(int py=0;py<375;py++) for(int px=0;px<300;px++) {
            var c=result.GetPixel(px,py);
            result.SetPixel(px,py,mask ? Mask(c,bits) : bits==64 ? c : c.A<128 ? Color.Transparent : Color.FromArgb(255,c.R,c.G,c.B));
        }
        return result;
    }
    public static void Export(string input,string descentInput,int descentIndex,string folder,int bits,string preview) {
        using var source=new Bitmap(input);
        int rows=bits==64 ? 4 : 3;
        if(source.Width<4 || source.Height<rows) throw new Exception("Invalid sprite atlas grid");
        using var descentSource=new Bitmap(descentInput);
        if(descentSource.Width<5) throw new Exception("Descent sheet must contain five columns");
        using var walk=new Bitmap(2400,375,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(walk)) for(int i=0;i<8;i++) {
            using var f=Frame(source,i,bits,true);g.DrawImageUnscaled(f,i*300,0);
        }
        walk.Save(System.IO.Path.Combine(folder,"Move.png"),ImageFormat.Png);
        using var climb=new Bitmap(600,375,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(climb)) for(int i=0;i<2;i++) {
            using var f=Frame(source,i+9,bits,true);g.DrawImageUnscaled(f,i*300,0);
        }
        climb.Save(System.IO.Path.Combine(folder,"Climb.png"),ImageFormat.Png);
        using var descent=bits==64 ? DescentCell(source,GridCell(source,12,4),bits) : Descent(descentSource,descentIndex,bits);
        descent.Save(System.IO.Path.Combine(folder,"ClimbDown.png"),ImageFormat.Png);
        using var idle=Frame(source,8,bits,true);idle.Save(System.IO.Path.Combine(folder,"Main.png"),ImageFormat.Png);
        using var vent=Frame(source,11,bits,true);vent.Save(System.IO.Path.Combine(folder,"EnterVent.png"),ImageFormat.Png);
        if(!string.IsNullOrEmpty(preview)) {
            using var p=Frame(source,8,bits,false);p.Save(preview,ImageFormat.Png);
        }
    }
}
'@
}
function Get-CosmicPath([string]$Value) {
    [long]$taskHash = 0
    foreach ($taskCharacter in $Value.ToCharArray()) { $taskHash = ($taskHash * 467 + [int]$taskCharacter) % 531206959292021 }
    $taskAlphabet = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'; $taskResult = ''
    while ($taskHash -gt 0) { $taskResult += $taskAlphabet[[int]($taskHash % 62)]; $taskHash = [long][Math]::Floor($taskHash / 62) }
    return $taskResult
}
$taskContentsPath = Join-Path $taskRoot 'addon/MoreCosmic/Contents.json'
$taskContents = Get-Content -LiteralPath $taskContentsPath -Raw | ConvertFrom-Json
$taskHats = @($taskContents.Hats | Where-Object { $_.Name -notlike 'BitCharacter*' })
$taskIconFolder=Join-Path $taskRoot 'addon/Resources/BitCharacter'
New-Item -ItemType Directory -Force -Path $taskIconFolder | Out-Null
$taskDescentSource=Join-Path $taskSource 'climb-down-stages.png'
$taskStageIndex=0
foreach ($taskStage in $taskImages.Keys) {
    $taskName = 'BitCharacter' + $taskStage
    $taskRelative = (Get-CosmicPath 'TanukiBCL') + '/' + (Get-CosmicPath $taskName)
    $taskFolder = Join-Path $taskRoot ('addon/MoreCosmic/hats/' + $taskRelative)
    New-Item -ItemType Directory -Path $taskFolder -Force | Out-Null
    $taskPreview = if ($taskStage -eq '8') { Join-Path $taskIconFolder 'Icon.png' } else { $null }
    [BitSpriteExporter]::Export((Join-Path $taskSource "$($taskStage)bit.png"),$taskDescentSource,$taskStageIndex,$taskFolder,[int]$taskStage,$taskPreview)
    $taskStageIndex++
    $taskHats += [ordered]@{ Name=$taskName;Author='TanukiBCL';Package='TanukiBits';IsHidden=$true;Adaptive=($taskStage -ne '2');Bounce=$false;HideHands=$true;FPS=1
        Main=@{Address="$taskRelative/Main.png"};Move=@{Address="$taskRelative/Move.png";Length=8;AnimSync=1}
        Climb=@{Address="$taskRelative/Climb.png";Length=2;AnimSync=1};ClimbDown=@{Address="$taskRelative/ClimbDown.png";Length=1}
        EnterVent=@{Address="$taskRelative/EnterVent.png"};ExitVent=@{Address="$taskRelative/EnterVent.png"};Exile=@{Address="$taskRelative/Main.png"} }
}
$taskContents.Hats=$taskHats
[IO.File]::WriteAllText($taskContentsPath,($taskContents | ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
Write-Output 'Exported six stages: 300x375 idle/vent/dedicated descent, 600x375 two-frame ascent, 2400x375 eight-frame walks; smooth alpha/color gradients for 64bit.'
