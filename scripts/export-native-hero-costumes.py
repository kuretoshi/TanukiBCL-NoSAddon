"""Lay out generated hero artwork using the supplied native crewmate anchors.

Artwork is produced by built-in image_gen. This exporter only splits its atlas,
scales whole poses uniformly and places them in the game's 300x375 cells.
"""
from pathlib import Path
import hashlib
import io
import json
import statistics
import subprocess
from PIL import Image, ImageDraw, ImageFont
from zipline_layout import components, seated_frames

ROOT = Path(__file__).resolve().parent.parent
DESIGN = ROOT / 'docs/design/hero-costumes-native'
REFERENCE = ROOT / 'docs/design/hero-costumes-v2/reference'
BOB = [0, -2, -1, 1, 4, 3, 0, -2, -1, 1, 4, 3]
HEROES = [('SuperHero', 'super', 'HOb85tfub'), ('EvilHero', 'evil', '0Wg0o9Ic')]


def bounds(image):
    b = image.getchannel('A').point(lambda a: 255 if a >= 128 else 0).getbbox()
    assert b, 'Empty sprite'
    return b


def body(image):
    return components(image)[0]['bounds']


def head(image):
    b = body(image)
    top = image.crop((0, b[1], image.width, b[1] + round((b[3]-b[1])*.25)))
    h = bounds(top)
    return (h[0]+h[2])/2, b[1]


def feet(image):
    b = body(image)
    low = image.crop((0, b[3]-12, image.width, b[3]))
    f = bounds(low)
    return (f[0]+f[2])/2, b[3]


def place(source, scale, source_anchor, target_anchor):
    tile = source.resize((round(source.width*scale), round(source.height*scale)), Image.Resampling.LANCZOS)
    output = Image.new('RGBA', (300, 375))
    offset = tuple(round(t-s*scale) for s,t in zip(source_anchor, target_anchor))
    output.alpha_composite(tile, offset)
    b = output.getchannel('A').point(lambda a: 255 if a >= 4 else 0).getbbox()
    assert b and 0 < b[0] < b[2] < 300 and 0 < b[1] < b[3] < 375, 'Clipped sprite'
    return output


def strip(frames, path):
    output = Image.new('RGBA', (len(frames)*300, 375))
    for i, frame in enumerate(frames):
        output.alpha_composite(frame, (i*300, 0))
    output.save(path)


def main():
    native = Image.open(REFERENCE/'main.png').convert('RGBA')
    native_bounds = body(native)
    native_height = native_bounds[3]-native_bounds[1]
    native_feet = feet(native)
    definition_path = ROOT/'addon/MoreCosmic/Contents.json'
    definition = json.loads(definition_path.read_text(encoding='utf-8-sig'))
    records, previews = [], []
    for name, prefix, directory in HEROES:
        source_path = DESIGN/(prefix+'-atlas.png')
        sheet = Image.open(source_path).convert('RGBA')
        assert sheet.width % 4 == 0 and sheet.height % 6 == 0
        w,h = sheet.width//4, sheet.height//6
        cells = [sheet.crop((i%4*w, i//4*h, (i%4+1)*w, (i//4+1)*h)) for i in range(24)]
        assert all(c.getchannel('A').getextrema()[1] <= 1 for c in cells[21:]), 'Unused cells contain artwork'
        folder = ROOT/'addon/MoreCosmic/hats/tWg4uMeMb'/directory
        old = Image.open(io.BytesIO(subprocess.check_output(
            ['git', '-c', 'safe.directory='+str(ROOT).replace('\\','/'), '-C', str(ROOT),
             'show', '7eae16f9ffae9f3f90e6e70ec04c83defd4bac38:addon/MoreCosmic/hats/tWg4uMeMb/'+directory+'/Main.png']))).convert('RGBA')
        source_body = body(cells[0])
        scale = native_height/(source_body[3]-source_body[1])
        idle = place(cells[0], scale, feet(cells[0]), native_feet)
        assert abs((body(idle)[3]-body(idle)[1])-native_height) <= 2
        target_head = head(idle)
        walk = [place(cells[i+1], scale, head(cells[i+1]),
                      (target_head[0], target_head[1]+BOB[i])) for i in range(12)]
        # The user-supplied ladder guides have a different back-view body
        # extent. Normalize that pose group, never individual walk frames.
        up_height = body(Image.open(REFERENCE/'climb.png').convert('RGBA'))
        up_height = up_height[3]-up_height[1]
        up_scale = up_height/statistics.median(body(cells[i])[3]-body(cells[i])[1] for i in [13,14])
        climb = [place(cells[i], up_scale, head(cells[i]), (158.5,91)) for i in [13,14]]
        # In the descent guide the raised hands overlap the body component;
        # its actual helmet/feet extent is y=126..338, not the hand bounds.
        down_body = body(cells[15])
        down = place(cells[15], 212/(down_body[3]-down_body[1]), head(cells[15]), (158.5,126))
        crouch = place(cells[16], scale, head(cells[16]), (target_head[0],168))
        zip_source = Image.new('RGBA',(w*4,h))
        for i in range(4):
            zip_source.alpha_composite(cells[17+i],(i*w,0))
        zip_frames, zip_layout = seated_frames(zip_source)
        idle.save(folder/'Main.png')
        strip(walk,folder/'Move.png')
        strip(climb,folder/'Climb.png')
        down.save(folder/'ClimbDown.png')
        crouch.save(folder/'EnterVent.png')
        strip(zip_frames,ROOT/'addon/Resources/Heroes'/(prefix.title()+'Zipline.png'))
        hat = next(h for h in definition['Hats'] if h['Name'] == name)
        base = hat['Main']['Address'].rsplit('/',1)[0]
        for part,count in [('Main',1),('Move',12),('Climb',2),('ClimbDown',1),('EnterVent',1)]:
            hat[part] = {'Address':base+'/'+part+'.png'}
            if count > 1:
                hat[part].update(Length=count,AnimSync=1)
        hat['ExitVent'] = dict(hat['EnterVent'])
        hat['Exile'] = dict(hat['Main'])
        assert len({hashlib.sha256(c.tobytes()).digest() for c in walk}) == 12
        records.append({'hero':name,'source':source_path.relative_to(ROOT).as_posix(),
                        'nativeBodyBounds':native_bounds,'idleBounds':body(idle),
                        'nativeFeet':native_feet,'sourceBodyBounds':source_body,'uniformScale':scale,
                        'walkFrames':12,'walkBobPixels':BOB,'ladderUpScale':up_scale,
                        'zipline':zip_layout})
        previews.append((name,old,idle,walk,climb,down,crouch,zip_frames))
    definition_path.write_text(json.dumps(definition,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (DESIGN/'alignment.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    font = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc',17)
    # Reference display only: use the existing adaptive-color preview helper.
    import importlib.util
    spec = importlib.util.spec_from_file_location('bit_preview',ROOT/'scripts/preview-bit-character.py')
    preview = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(preview)
    normal = preview.tint(native)
    board = Image.new('RGB',(1200,850),'#1d2734')
    draw = ImageDraw.Draw(board)
    for row,data in enumerate(previews):
        for col,(label,art) in enumerate([('通常クルー / 基準',normal),('変更前',data[1]),('新デザイン',data[2]),('はしご上り',data[4][0])]):
            draw.text((col*300+14,row*425+12),data[0]+' / '+label,font=font,fill='white')
            draw.line((col*300+12,row*425+348,col*300+288,row*425+348),fill='#62758b')
            board.paste(art,(col*300,row*425+40),art)
    board.save(DESIGN/'comparison.png')
    for label,index,count in [('walk',3,12),('climb-up',4,2),('zipline',7,4)]:
        frames=[]
        for i in range(count):
            canvas=Image.new('RGB',(900,430),'#1d2734')
            d=ImageDraw.Draw(canvas)
            for col,(name,art) in enumerate([('通常クルー / 寸法基準',normal)]+[(p[0],p[index][i]) for p in previews]):
                d.text((col*300+14,10),name,font=font,fill='white')
                canvas.paste(art,(col*300,40),art)
            frames.append(canvas)
        frames[0].save(DESIGN/(label+'.gif'),save_all=True,append_images=frames[1:],
                       duration=85 if label=='walk' else 270 if label=='climb-up' else 140,
                       loop=0,disposal=2)
    print('PASS generated native-size hero atlas export: idle body height/feet, 12 walk frames, ladder up/down, vent, seated zipline; alpha and no clipping')


if __name__=='__main__':
    main()
