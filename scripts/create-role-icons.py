from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
root=Path(__file__).resolve().parent.parent
dark='#15202d'; white='#f4ffff'; tan='#c4a470'; mint='#64dcb4'
# Vector primitives in a 128-square canvas. The same primitives generate Unity art.
def ellipse(box,color): return ('ellipse',box,color)
def poly(points,color): return ('poly',points,color)
def line(points,color,width): return ('line',points,color,width)
def crew(color):
    return [ellipse((29,41,91,111),dark),ellipse((34,46,86,105),color),
      ellipse((38,82,58,118),dark),ellipse((43,84,54,113),color),
      ellipse((65,82,85,118),dark),ellipse((69,84,81,113),color),
      ellipse((21,60,40,98),dark),ellipse((25,65,36,94),color),
      ellipse((41,53,98,83),dark),ellipse((46,57,93,78),'#b4e9f3'),
      ellipse((50,58,77,66),white)]
bibiri=crew(tan)+[
 line([(48,74),(54,69),(60,74)],dark,3),line([(72,74),(78,69),(84,74)],dark,3),
 poly([(96,37),(102,48),(102,54),(98,57),(94,54),(94,48)],'#70d9ff'),
 line([(18,46),(12,54),(18,61),(12,70)],tan,4),
 line([(108,65),(114,73),(108,82),(114,91)],tan,4),
 line([(42,30),(38,19)],tan,4),line([(60,28),(61,14)],tan,4),line([(78,30),(84,20)],tan,4)]
talk=crew(mint)+[
 ellipse((60,5,119,47),dark),poly([(75,38),(70,55),(94,40)],dark),
 ellipse((65,10,114,42),white),poly([(79,36),(78,45),(90,36)],white),
 ellipse((74,23,79,28),mint),ellipse((86,23,91,28),mint),ellipse((98,23,103,28),mint),
 line([(102,66),(109,72),(102,79)],mint,4),line([(114,59),(122,72),(114,86)],mint,4)]
sets=[bibiri,talk]
def render(shapes):
    im=Image.new('RGBA',(512,512)); d=ImageDraw.Draw(im)
    for s in shapes:
        coords=s[1]
        if s[0]=='ellipse': d.ellipse(tuple(v*4 for v in coords),fill=s[2])
        else:
            p=[(x*4,y*4) for x,y in coords]
            if s[0]=='poly': d.polygon(p,fill=s[2])
            else: d.line(p,fill=s[2],width=s[3]*4,joint='curve')
    return im.resize((256,256),Image.LANCZOS)
out=root/'dist'; out.mkdir(exist_ok=True)
for name,shapes in zip(['bibiri','talkative'],sets): render(shapes).save(out/(name+'-icon.png'))
preview=Image.new('RGB',(720,250),'#10151c')
d=ImageDraw.Draw(preview)
fontpath=Path('C:/Windows/Fonts/meiryob.ttc')
font=ImageFont.truetype(str(fontpath),23)
for index,(label,color,shapes) in enumerate(zip(['ビビリ','おしゃべり'],[tan,mint],sets)):
    x=18+index*354
    d.rounded_rectangle((x,20,x+335,110),18,outline=white,width=3)
    preview.paste(render(shapes).resize((86,86),Image.LANCZOS),(x+6,23),render(shapes).resize((86,86),Image.LANCZOS))
    d.text((x+115,45),label,font=font,fill=color)
    preview.paste(render(shapes).resize((115,115),Image.LANCZOS),(x+110,125),render(shapes).resize((115,115),Image.LANCZOS))
preview.save(out/'voice-role-icons-preview.png')
def arr(shape):
    coords=shape[1] if shape[0]=='ellipse' else [v for pair in shape[1] for v in pair]
    color=shape[2].lstrip('#'); rgb=[int(color[i:i+2],16) for i in (0,2,4)]
    return 'new Shape(%d, new Color32(%s,255), %sf, new float[] {%s})'%({'ellipse':0,'poly':1,'line':2}[shape[0]],','.join(map(str,rgb)),shape[3] if shape[0]=='line' else 0,','.join(str(v)+'f' for v in coords))
src='''using UnityEngine;
using Nebula.Utilities;
using Virial.Media;
namespace TanukiBCL.Roles;
public static class VoiceRoleIcons
{
    public static readonly Image Bibiri = new WrapSpriteLoader(() => Create(0));
    public static readonly Image Talkative = new WrapSpriteLoader(() => Create(1));
    public static Sprite TalkativeSprite => Create(1);
    public static Sprite BibiriSprite => Create(0);
    private static readonly Sprite?[] cache = new Sprite?[2];
    private sealed class Shape
    {
        public readonly int Kind; public readonly Color32 Color; public readonly float Width; public readonly float[] P;
        public Shape(int kind, Color32 color, float width, float[] p) { Kind=kind; Color=color; Width=width; P=p; }
        public bool Contains(float x, float y)
        {
            if (Kind == 0) { float rx=(P[2]-P[0])/2, ry=(P[3]-P[1])/2; x=(x-(P[0]+P[2])/2)/rx; y=(y-(P[1]+P[3])/2)/ry; return x*x+y*y<=1; }
            if (Kind == 2) {
                for(int i=2;i<P.Length;i+=2) { float ax=P[i-2],ay=P[i-1],dx=P[i]-ax,dy=P[i+1]-ay; float t=Mathf.Clamp01(((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy)); float ex=x-ax-t*dx,ey=y-ay-t*dy; if(ex*ex+ey*ey<=Width*Width/4)return true; }
                return false;
            }
            bool hit=false;
            for(int i=0,j=P.Length-2;i<P.Length;j=i,i+=2)
                if((P[i+1]>y)!=(P[j+1]>y) && x<(P[j]-P[i])*(y-P[i+1])/(P[j+1]-P[i+1])+P[i])hit=!hit;
            return hit;
        }
    }
    private static readonly Shape[][] art = new Shape[][] {
'''+',\n'.join('new Shape[] {\n'+',\n'.join(arr(s) for s in shapes)+'\n}' for shapes in sets)+'''
    };
    private static Sprite Create(int kind)
    {
        if(cache[kind]!=null)return cache[kind]!;
        const int size=256;
        var pixels=new Color[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++) {
            Color sum=Color.clear;
            for(int sy=0;sy<2;sy++)for(int sx=0;sx<2;sx++) {
                Color c=Color.clear;
                foreach(var shape in art[kind])if(shape.Contains((x+(sx+.5f)/2)/2,128-(y+(sy+.5f)/2)/2))c=shape.Color;
                sum+=c*.25f;
            }
            // Straight alpha avoids dark edges on the white UI frame.
            if(sum.a>0){sum.r/=sum.a;sum.g/=sum.a;sum.b/=sum.a;}
            pixels[y*size+x]=sum;
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
        texture.filterMode=FilterMode.Bilinear; texture.wrapMode=TextureWrapMode.Clamp;
        texture.SetPixels(pixels);texture.Apply();
        cache[kind]=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),175f);
        return cache[kind]!;
    }
}
'''
(root/'addon/Scripts/VoiceRoleIcons.cs').write_text(src,encoding='utf-8')
