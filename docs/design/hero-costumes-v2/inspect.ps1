param([string]$Directory = "$PSScriptRoot/source")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
if (-not ('HeroPixelInspector' -as [type])) {
$taskReferences = @('System.Drawing.Common.dll','System.Drawing.Primitives.dll','System.Private.Windows.GdiPlus.dll','System.Private.Windows.Core.dll','System.Collections.dll','System.Runtime.dll','System.Private.CoreLib.dll') | ForEach-Object { Join-Path $PSHOME $_ }
Add-Type -ReferencedAssemblies $taskReferences -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class HeroPixelInspector {
  public static double[] Anchors(string path) {
    using var image = new Bitmap(path);
    int foot=-1;
    for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++)
      if(image.GetPixel(x,y).A>=128) foot=y;
    if(foot<0) throw new Exception("Empty texture: "+path);
    int left=image.Width,right=-1;
    for(int y=Math.Max(0,foot-image.Height/25);y<=foot;y++) for(int x=0;x<image.Width;x++) {
      if(image.GetPixel(x,y).A<128) continue;
      left=Math.Min(left,x);right=Math.Max(right,x);
    }
    double centre=(left+right+1)*.5;
    int head=foot;
    for(int y=0;y<=foot;y++) {
      int count=0;
      for(int x=(int)centre-2;x<=(int)centre+2;x++) if(image.GetPixel(x,y).A>=128) count++;
      if(count>=3) {head=y;break;}
    }
    return new double[]{image.Width,image.Height,head,foot+1,centre};
  }
  public static string Inspect(string path) {
    using var image = new Bitmap(path);
    var data = image.LockBits(new Rectangle(0,0,image.Width,image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    var bytes = new byte[data.Stride * image.Height];
    Marshal.Copy(data.Scan0, bytes, 0, bytes.Length); image.UnlockBits(data);
    int w=image.Width, h=image.Height;
    var seen=new bool[w*h]; var queue=new int[w*h]; string parts="";
    for(int p=0;p<w*h;p++) {
      if(seen[p] || bytes[(p/w)*data.Stride+(p%w)*4+3]<64) continue;
      int head=0, tail=0, left=w, right=0, top=h, bottom=0;
      queue[tail++]=p; seen[p]=true;
      while(head<tail) {
        int n=queue[head++], x=n%w, y=n/w;
        left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
        foreach(int next in new[]{n-1,n+1,n-w,n+w}) {
          if(next<0 || next>=w*h || seen[next] || Math.Abs(next%w-x)+Math.Abs(next/w-y)!=1) continue;
          if(bytes[(next/w)*data.Stride+(next%w)*4+3]<64) continue;
          seen[next]=true; queue[tail++]=next;
        }
      }
      if(tail>100) parts += tail+":"+new Rectangle(left,top,right-left+1,bottom-top+1)+"; ";
    }
    return System.IO.Path.GetFileName(path)+" "+w+"x"+h+" "+parts;
  }
}
'@
}
Get-ChildItem -LiteralPath $Directory -Filter '*.png' | ForEach-Object {
    [HeroPixelInspector]::Inspect($_.FullName)
    '  anchors width,height,head,foot,feetX: ' + ([HeroPixelInspector]::Anchors($_.FullName) -join ',')
}
