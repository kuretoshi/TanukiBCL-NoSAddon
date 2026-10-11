from PIL import Image, ImageDraw
import re, math, os
from pathlib import Path
root = Path(__file__).resolve().parent.parent
src = (root / 'addon/Scripts/MegaphoneVisual.cs').read_text(encoding='utf-8-sig')
plates=[]
for c,p in re.findall(r'new\(new Color\(([^)]+)\), ([^\n]+)\),',src):
    nums=lambda s: [float(v.strip().replace('f','')) for v in s.split(',')]
    color=tuple(round(v*255) for v in nums(c))
    points=nums(p)
    plates.append((color,list(zip(points[::2],points[1::2]))))
assert len(plates)==15
im=Image.new('RGB',(1200,720),'#101827'); d=ImageDraw.Draw(im)
for x in range(0,1200,40): d.line((x,0,x,720),fill='#172132')
for y in range(0,720,40): d.line((0,y,1200,y),fill='#172132')
fontpath=Path(os.environ['WINDIR'])/'Fonts/segoeuib.ttf'
from PIL import ImageFont
font=ImageFont.truetype(str(fontpath),25); small=ImageFont.truetype(str(fontpath),17)
d.text((45,24),'MEGAPHONE BAZOOKA  /  0.5.1',font=font,fill='#e8f6ff')
for row,(y,energy,firing) in enumerate([(225,.65,False),(530,1,True)]):
    ox=145; scale=270; muzzle=ox+.75*scale
    d.text((45,y-115),'CHARGING' if not firing else 'FIRING',font=small,fill='#56ddff')
    d.text((820,y+105),'PULSING CORE / ENERGY CELLS' if not firing else 'BLUE AURA / WHITE-HOT CORE',font=small,fill='#8295b3')
    if firing:
        d.rectangle((muzzle,y-72,1199,y+72),fill='#1b315f')
        d.rectangle((muzzle,y-52,1199,y+52),fill='#168daa')
        d.rectangle((muzzle,y-22,1199,y+22),fill='#e4ffff')
        for side in [-1,1]:
            pts=[(muzzle+i*25,y+side*144*(.37+.06*math.sin(i*1.8))) for i in range(34)]
            d.line(pts,fill='#8aecff',width=3)
    else:
        d.line((muzzle,y,1110,y),fill='#328396',width=2)
        d.line((muzzle,y-2,1040,y-2),fill='#204751',width=1)
    for color,poly in plates:
        d.polygon([(ox+x*scale,y-py*scale) for x,py in poly],fill=color)
    for i in range(6):
        px=ox+(.20+i*.048)*scale
        d.line((px,y-.055*scale,px,y+.055*scale),fill='#6bffff' if energy*6>=i+1 else '#123346',width=8)
    r=85 if not firing else 70
    d.ellipse((muzzle-r*.16,y-r,muzzle+r*.16,y+r),outline='#91ffff',width=5)
    r=128 if not firing else 140
    d.ellipse((muzzle-r*.20,y-r,muzzle+r*.20,y+r),outline='#286ab1',width=3)
    if firing:
        # Same skewed ellipse geometry as FlowArc. Beam-axis spacing is compressed
        # here to show several rings in a compact illustration, not a game screenshot.
        for k in range(5):
            center=muzzle+115+k*157
            opacity=(1-(.16+k*.22)/1.98)**2
            color=tuple(int(v*opacity+b*(1-opacity)) for v,b in zip((140,255,255),(22,60,80)))
            radius=88
            points=[]
            for i in range(33):
                theta=i/32*math.pi*2
                points.append((center+radius*(math.cos(theta)*.30+math.sin(theta)*.12),y-math.sin(theta)*radius))
            d.line(points,fill=color,width=4)
(root/'dist').mkdir(exist_ok=True)
im.save(str(root/'dist/Megaphone-0.5.1-preview.png'))
print(root/'dist/Megaphone-0.5.1-preview.png')
