using UnityEngine;
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
new Shape[] {
new Shape(0, new Color32(21,32,45,255), 0f, new float[] {29f,41f,91f,111f}),
new Shape(0, new Color32(196,164,112,255), 0f, new float[] {34f,46f,86f,105f}),
new Shape(0, new Color32(21,32,45,255), 0f, new float[] {38f,82f,58f,118f}),
new Shape(0, new Color32(196,164,112,255), 0f, new float[] {43f,84f,54f,113f}),
new Shape(0, new Color32(21,32,45,255), 0f, new float[] {65f,82f,85f,118f}),
new Shape(0, new Color32(196,164,112,255), 0f, new float[] {69f,84f,81f,113f}),
new Shape(0, new Color32(21,32,45,255), 0f, new float[] {21f,60f,40f,98f}),
new Shape(0, new Color32(196,164,112,255), 0f, new float[] {25f,65f,36f,94f}),
new Shape(0, new Color32(21,32,45,255), 0f, new float[] {41f,53f,98f,83f}),
new Shape(0, new Color32(180,233,243,255), 0f, new float[] {46f,57f,93f,78f}),
new Shape(0, new Color32(244,255,255,255), 0f, new float[] {50f,58f,77f,66f}),
new Shape(2, new Color32(21,32,45,255), 3f, new float[] {48f,74f,54f,69f,60f,74f}),
new Shape(2, new Color32(21,32,45,255), 3f, new float[] {72f,74f,78f,69f,84f,74f}),
new Shape(1, new Color32(112,217,255,255), 0f, new float[] {96f,37f,102f,48f,102f,54f,98f,57f,94f,54f,94f,48f}),
new Shape(2, new Color32(196,164,112,255), 4f, new float[] {18f,46f,12f,54f,18f,61f,12f,70f}),
new Shape(2, new Color32(196,164,112,255), 4f, new float[] {108f,65f,114f,73f,108f,82f,114f,91f}),
new Shape(2, new Color32(196,164,112,255), 4f, new float[] {42f,30f,38f,19f}),
new Shape(2, new Color32(196,164,112,255), 4f, new float[] {60f,28f,61f,14f}),
new Shape(2, new Color32(196,164,112,255), 4f, new float[] {78f,30f,84f,20f})
},
new Shape[] {
new Shape(0, new Color32(21,32,45,255), 0f, new float[] {29f,41f,91f,111f}),
new Shape(0, new Color32(100,220,180,255), 0f, new float[] {34f,46f,86f,105f}),
new Shape(0, new Color32(21,32,45,255), 0f, new float[] {38f,82f,58f,118f}),
new Shape(0, new Color32(100,220,180,255), 0f, new float[] {43f,84f,54f,113f}),
new Shape(0, new Color32(21,32,45,255), 0f, new float[] {65f,82f,85f,118f}),
new Shape(0, new Color32(100,220,180,255), 0f, new float[] {69f,84f,81f,113f}),
new Shape(0, new Color32(21,32,45,255), 0f, new float[] {21f,60f,40f,98f}),
new Shape(0, new Color32(100,220,180,255), 0f, new float[] {25f,65f,36f,94f}),
new Shape(0, new Color32(21,32,45,255), 0f, new float[] {41f,53f,98f,83f}),
new Shape(0, new Color32(180,233,243,255), 0f, new float[] {46f,57f,93f,78f}),
new Shape(0, new Color32(244,255,255,255), 0f, new float[] {50f,58f,77f,66f}),
new Shape(0, new Color32(21,32,45,255), 0f, new float[] {60f,5f,119f,47f}),
new Shape(1, new Color32(21,32,45,255), 0f, new float[] {75f,38f,70f,55f,94f,40f}),
new Shape(0, new Color32(244,255,255,255), 0f, new float[] {65f,10f,114f,42f}),
new Shape(1, new Color32(244,255,255,255), 0f, new float[] {79f,36f,78f,45f,90f,36f}),
new Shape(0, new Color32(100,220,180,255), 0f, new float[] {74f,23f,79f,28f}),
new Shape(0, new Color32(100,220,180,255), 0f, new float[] {86f,23f,91f,28f}),
new Shape(0, new Color32(100,220,180,255), 0f, new float[] {98f,23f,103f,28f}),
new Shape(2, new Color32(100,220,180,255), 4f, new float[] {102f,66f,109f,72f,102f,79f}),
new Shape(2, new Color32(100,220,180,255), 4f, new float[] {114f,59f,122f,72f,114f,86f})
}
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
