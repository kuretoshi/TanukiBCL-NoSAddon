"""Export generated hero poses without warping the body or cape.

Python 3.12 + Pillow. Only selected source sheets and final game assets are
kept; the old design/export folder is not duplicated. This is a layout and
alignment step, not an artwork generator.
"""
from pathlib import Path
import hashlib
import json
import statistics
from PIL import Image, ImageDraw, ImageFont
from zipline_layout import seated_frames

ROOT = Path(__file__).resolve().parent.parent
DESIGN = ROOT / 'docs/design/hero-costumes-v2'
ASSETS = ROOT / 'addon/MoreCosmic/hats'
HEROES = [('SuperHero', 'super', 'HOb85tfub'), ('EvilHero', 'evil', '0Wg0o9Ic')]
# Small torso motion is baked once. The Hat synchronizer must not add another
# full-body movement on top. Both heroes use the same 12-frame contact rhythm.
BOB = [0, -2, -1, 1, 4, 3, 0, -2, -1, 1, 4, 3]


def bounds(image):
    result = image.getchannel('A').point(lambda a: 255 if a >= 128 else 0).getbbox()
    assert result is not None, 'Empty pose'
    return result


def head(image):
    b = bounds(image)
    # The cape starts below the helmet, so it cannot shift the head anchor.
    top = image.crop((0, b[1], image.width, b[1] + round((b[3]-b[1])*.30)))
    h = bounds(top)
    return (h[0]+h[2])/2, b[1]


def place(source, scale, x, y, target_x, target_y):
    out = Image.new('RGBA', (300, 375))
    tile = source.resize((round(source.width*scale), round(source.height*scale)), Image.Resampling.LANCZOS)
    out.alpha_composite(tile, (round(target_x-x*scale), round(target_y-y*scale)))
    b = bounds(out)
    assert b[0] > 0 and b[1] > 0 and b[2] < 300 and b[3] < 375, 'Clipped pose'
    # Generated sheets may contain alpha=1 noise far outside the drawing.
    # Preserve their alpha, but exclude that invisible noise from the bounds
    # check rather than mistaking it for a clipped edge of the character.
    alpha_bounds = out.getchannel('A').point(lambda a: a if a >= 4 else 0).getbbox()
    assert alpha_bounds[0] > 0 and alpha_bounds[1] > 0 and alpha_bounds[2] < 300 and alpha_bounds[3] < 375, 'Clipped antialiasing'
    return out


def strip(frames, output):
    out = Image.new('RGBA', (300*len(frames), 375))
    for i, frame in enumerate(frames):
        out.alpha_composite(frame, (i*300, 0))
    out.save(output)


def main():
    if (ROOT/'docs/design/hero-costumes-native/super-atlas.png').exists():
        import runpy
        runpy.run_path(str(ROOT/'scripts/export-native-hero-costumes.py'), run_name='__main__')
        return
    definition_path = ROOT / 'addon/MoreCosmic/Contents.json'
    definition = json.loads(definition_path.read_text(encoding='utf-8-sig'))
    metadata, previews = [], []
    for name, prefix, directory in HEROES:
        source_path = DESIGN / 'source' / (prefix+'-motion.png')
        sheet = Image.open(source_path).convert('RGBA')
        assert sheet.getpixel((0, 0))[3] == 0, 'Source sheet must be transparent'
        w, h = sheet.width/4, sheet.height/4
        cells = [sheet.crop(tuple(round(v) for v in (i%4*w, i//4*h, (i%4+1)*w, (i//4+1)*h))) for i in range(16)]
        folder = ASSETS / 'tWg4uMeMb' / directory
        idle = Image.open(folder/'Main.png').convert('RGBA')
        idle_b = bounds(idle)
        scale = (idle_b[3]-idle_b[1])/statistics.median(bounds(c)[3]-bounds(c)[1] for c in cells[:12])
        target_x, target_y = head(idle)
        walk = [place(cell, scale, *head(cell), target_x, target_y+BOB[i]) for i, cell in enumerate(cells[:12])]
        # One common scale for the two ascent poses. Align the back of the
        # helmet, not the raised glove or the alternating lowest boot.
        ladder_x = w * (.57 if prefix == 'super' else .53)
        ladder_y = h * .15
        climb = [place(cells[i], scale, ladder_x, ladder_y, 158.5, 90) for i in [12, 13]]
        down = place(cells[14], scale, ladder_x, ladder_y, 158.5, 126)
        crouch = place(cells[15], scale, *head(cells[15]), target_x, 168)
        strip(walk, folder/'Move.png')
        strip(climb, folder/'Climb.png')
        down.save(folder/'ClimbDown.png')
        crouch.save(folder/'EnterVent.png')
        hat = next(hat for hat in definition['Hats'] if hat['Name'] == name)
        base = hat['Main']['Address'].rsplit('/', 1)[0]
        for part, count in [('Move', 12), ('Climb', 2), ('ClimbDown', 1), ('EnterVent', 1)]:
            hat[part] = {'Address': base+'/'+part+'.png', 'Length': count}
            if count > 1:
                hat[part]['AnimSync'] = 1
        hat['ExitVent'] = dict(hat['EnterVent'])
        hat['Exile'] = dict(hat['Main'])
        assert len({hashlib.sha256(c.tobytes()).digest() for c in walk}) == 12
        # The cape itself changes across the cycle, independently of the
        # boots. This check covers the trailing cape area, not body position.
        capes = [c.crop((20, 185, 105, 270)) for c in walk]
        assert len({hashlib.sha256(c.tobytes()).digest() for c in capes}) >= 8
        metadata.append({'hero': name, 'source': source_path.relative_to(ROOT).as_posix(), 'scale': scale,
                         'walkFrames': 12, 'ascentFrames': 2, 'descentFrames': 1,
                         'walkBobPixels': BOB, 'capeFramesDistinct': len({c.tobytes() for c in capes})})
        zip_source = Image.open(DESIGN/'source'/(prefix+'-zipline.png')).convert('RGBA')
        assert zip_source.getpixel((0, 0))[3] == 0, 'Zipline source must be transparent'
        zip_frames, zip_layout = seated_frames(zip_source)
        strip(zip_frames, ROOT/'addon/Resources/Heroes'/(prefix.title()+'Zipline.png'))
        metadata[-1]['ziplineFrames'] = 4
        metadata[-1]['ziplineGripPixels'] = zip_layout['gripPixels']
        metadata[-1]['ziplineScale'] = zip_layout['scale']
        metadata[-1]['ziplineLayout'] = zip_layout
        previews.append((name, idle, walk, climb, down, crouch, zip_frames))
    definition_path.write_text(json.dumps(definition, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    (DESIGN/'motion-alignment.json').write_text(json.dumps(metadata, indent=2)+'\n', encoding='utf-8')
    font = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc', 18)
    for kind, index, length in [('walk', 2, 12), ('climb-up', 3, 2), ('zipline', 6, 4)]:
        frames = []
        for i in range(length):
            canvas = Image.new('RGB', (640, 440), '#18232f')
            draw = ImageDraw.Draw(canvas)
            draw.text((20, 12), 'Costume preview / '+kind+' / '+str(i+1), font=font, fill='white')
            for j, data in enumerate(previews):
                art = data[index][i]
                draw.text((j*320+20, 48), data[0], font=font, fill='#85ebef')
                canvas.paste(art, (j*320+10, 64), art)
            frames.append(canvas)
        frames[0].save(DESIGN/(kind+'-preview.gif'), save_all=True, append_images=frames[1:],
                       duration=85 if kind == 'walk' else 140 if kind == 'zipline' else 270, loop=0, disposal=2)
    board = Image.new('RGB', (1200, 840), '#18232f')
    draw = ImageDraw.Draw(board)
    for row, data in enumerate(previews):
        for col, (label, art) in enumerate([('Idle', data[1]), ('Walk', data[2][0]), ('Ascent', data[3][0]), ('Descent', data[4])]):
            draw.text((col*300+12, row*420+12), data[0]+' / '+label, font=font, fill='white')
            board.paste(art, (col*300, row*420+35), art)
    board.save(DESIGN/'motion-preview.png')
    print('PASS both hero assets: 12 walk poses, fluttering capes, alternating ladder ascent, dedicated descent/vent, four zipline poses; true alpha and no clipping')


if __name__ == '__main__':
    main()
