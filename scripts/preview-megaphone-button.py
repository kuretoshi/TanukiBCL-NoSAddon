from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter,ImageFont
import re,math,os
root=Path(__file__).resolve().parent.parent
src=(root/'addon/Scripts/MegaphoneVisual.cs').read_text(encoding='utf-8-sig')
raw=Image.new('RGBA',(256,256)); d=ImageDraw.Draw(raw)
xy=lambda x,y:((x+.3)/1.5*256,(.75-y)/1.5*256)
for c,p in re.findall(r'new\(new Color\(([^)]+)\), ([^\n]+)\),',src):
    nums=lambda s:[float(v.strip().replace('f','')) for v in s.split(',')]
    color=tuple(round(v*255) for v in nums(c))+(255,)
    points=nums(p)
    d.polygon([xy(x,y) for x,y in zip(points[::2],points[1::2])],fill=color)
for i in range(4):
    x=.28+i*.055
    d.line((xy(x,-.10),xy(x,.10)),fill='#091c2d',width=5)
for y in [-.30,-.38,-.46,-.54]:
    d.line((xy(-.10,y),xy(.028,y)),fill='#121a26',width=3)
for r in [.25,.40]:
    d.arc((xy(.72-r,r),xy(.72+r,-r)),start=-65,end=65,fill='#33ebff',width=5)
raw=raw.rotate(12,resample=Image.BICUBIC,center=xy(.4,-.1))
cx,cy=xy(.4,-.1)
raw=raw.transform((256,256),Image.AFFINE,(1/.86,0,cx-cx/.86,0,1/.86,cy-cy/.86),resample=Image.BICUBIC)
mask=raw.getchannel('A')
icon=Image.new('RGBA',(256,256))
for radius,color in [(11,(5,8,13,242)),(8,(240,250,255,255)),(3,(5,8,13,242))]:
    layer=Image.new('RGBA',(256,256),color); layer.putalpha(mask.filter(ImageFilter.MaxFilter(radius*2+1))); icon.alpha_composite(layer)
icon.alpha_composite(raw)
icon.save(str(root/'dist/Megaphone-button-0.5.2.png'))
preview=Image.new('RGB',(700,320),'#202631')
for x in range(0,700,32):ImageDraw.Draw(preview).line((x,0,x,320),fill='#282f3a')
preview.paste(icon,(55,28),icon)
small=icon.resize((96,96),Image.LANCZOS); preview.paste(small,(446,90),small)
font=ImageFont.truetype(str(Path(os.environ['WINDIR'])/'Fonts/segoeuib.ttf'),18)
ImageDraw.Draw(preview).text((430,214),'HUD SIZE',font=font,fill='#ddefff')
preview.save(str(root/'dist/Megaphone-button-0.5.2-preview.png'))
print(root/'dist/Megaphone-button-0.5.2-preview.png')
