from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageFont
import re, math, os

root = Path(__file__).resolve().parent.parent
src = (root / 'addon/Scripts/MegaphoneVisual.cs').read_text(encoding='utf-8-sig')
plates = []
for color, points in re.findall(r'new\(new Color\(([^)]+)\), ([^\n]+)\),', src):
    nums = lambda s: [float(v.strip().replace('f', '')) for v in s.split(',')]
    p = nums(points)
    plates.append((nums(color), list(zip(p[::2], p[1::2]))))

def inside(p, polygon):
    hit = False
    for i in range(len(polygon)):
        a, b = polygon[i], polygon[i-1]
        if (a[1] > p[1]) != (b[1] > p[1]) and p[0] < (b[0]-a[0])*(p[1]-a[1])/(b[1]-a[1])+a[0]:
            hit = not hit
    return hit

def make_icon(portable):
    icon = Image.new('RGBA', (256, 256))
    pixels = icon.load()
    for y in range(256):
        for x in range(256):
            px, py = (x+.5)/256*1.5-.3, (y+.5)/256*1.5-.75
            ox, oy = (px-.4)/.86, (py+.1)/.86
            a = math.radians(-12)
            px, py = ox*math.cos(a)-oy*math.sin(a)+.4, ox*math.sin(a)+oy*math.cos(a)-.1
            color = None
            for c, polygon in plates:
                if inside((px, py), polygon): color = c
            if abs(px+.21) < .013 and abs(py) < .12: color = [.73, .82, .9]
            if -.105 < px < .035 and -.54 < py < -.28 and (-py) % .08 < .017: color = [.07, .1, .15]
            if .27 < px < .49 and abs(py) < .11 and px % .07 < .026: color = [.04, .1, .17]
            if portable:
                if color and color[0] > .9 and .4 < color[1] < .7: color = [.24, .92, .64]
                if .24 < px < .49 and abs(py) < .07:
                    color = [.4, 1, .72] if (px-.24) % .085 < .057 else [.04, .12, .14]
            if px > .85:
                r = math.hypot(px-.72, py)
                if abs(r-.25) < .014 or abs(r-.4) < .014: color = [.2, .92, 1]
            if color: pixels[x, 255-y] = tuple(round(c*255) for c in color) + (255,)
    mask = icon.getchannel('A')
    outlined = Image.new('RGBA', icon.size)
    for radius, color in [(11, '#05080d'), (8, '#f0faff'), (3, '#05080d')]:
        layer = Image.new('RGBA', icon.size, color)
        layer.putalpha(mask.filter(ImageFilter.MaxFilter(radius*2+1)))
        outlined.alpha_composite(layer)
    outlined.alpha_composite(icon)
    return outlined

laser, portable = make_icon(False), make_icon(True)
portable.save(root / 'dist/portable-megaphone-button.png')
preview = Image.new('RGB', (800, 340), '#202631')
draw = ImageDraw.Draw(preview)
font = ImageFont.truetype(str(Path(os.environ['WINDIR']) / 'Fonts/meiryo.ttc'), 22)
small = ImageFont.truetype(str(Path(os.environ['WINDIR']) / 'Fonts/meiryo.ttc'), 16)
for x, icon, title, color in [(20, laser, 'メガホンレーザー', '#ff4545'), (295, portable, 'メガホン', '#64dcb4')]:
    preview.paste(icon, (x, 22), icon)
    draw.text((x+25, 280), title, font=font, fill=color)
preview.paste(portable.resize((96, 96), Image.Resampling.LANCZOS), (640, 104), portable.resize((96, 96), Image.Resampling.LANCZOS))
draw.rounded_rectangle((618, 185, 758, 220), 6, fill='#14202c', outline='#64dcb4', width=2)
draw.text((637, 190), '電池 100%', font=small, fill='#a3ffd4')
draw.text((641, 240), '専用ボタン', font=small, fill='#ddefff')
preview.save(root / 'dist/portable-megaphone-preview.png')
print(root / 'dist/portable-megaphone-preview.png')
