# -*- coding: utf-8 -*-
"""Inspect the installed game's real Walk clip alongside shipped bit poses.

Requires Python 3.12, UnityPy and Pillow. No game files are modified or copied.
Only the comparison board, GIF and compact metadata are saved.
"""
from pathlib import Path
import argparse
import importlib.util
import json
import UnityPy
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'docs/design/bit-character'
spec = importlib.util.spec_from_file_location('preview', ROOT / 'scripts/preview-bit-character.py')
preview = importlib.util.module_from_spec(spec)
spec.loader.exec_module(preview)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--game-dir', default='C:/Program Files (x86)/Steam/steamapps/common/Among Us NoS')
    args = parser.parse_args()
    env = UnityPy.load(str(Path(args.game_dir) / 'Among Us_Data/sharedassets2.assets'))
    objects = {o.path_id: o for o in env.objects}
    clip = next(o.read_typetree() for o in env.objects if o.type.name == 'AnimationClip' and o.peek_name() == 'Walk')
    frames, metadata = [], []
    for ref in clip['m_ClipBindingConstant']['pptrCurveMapping']:
        data = objects[ref['m_PathID']].read()
        frames.append((preview.tint(data.image.convert('RGBA')), data.m_Pivot))
        metadata.append({'name': data.m_Name, 'size': list(data.image.size),
                         'pivot': [data.m_Pivot.x, data.m_Pivot.y], 'ppu': data.m_PixelsToUnits})
    contents = json.loads((ROOT / 'addon/MoreCosmic/Contents.json').read_text(encoding='utf-8-sig'))
    stages = [16, 32]
    assets = {}
    for bits in stages:
        hat = next(h for h in contents['Hats'] if h['Name'] == 'BitCharacter' + str(bits))
        assets[bits] = preview.tint(Image.open(ROOT / 'addon/MoreCosmic/hats' / hat['Move']['Address']).convert('RGBA'))
    font = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc', 18)
    labels = ['通常クルー（実際のWalk）', '16bit（旧8コマ素材）', '32bit（旧8コマ素材）']
    board = Image.new('RGB', (1700, 780), '#17212d')
    draw = ImageDraw.Draw(board)
    draw.text((20, 10), '変更前の比較：通常12コマと旧bit8コマ／各行1周期・ゲーム画面ではありません', fill='white', font=font)
    for row in range(len(labels)):
        y = 45 + row * 240
        draw.text((20, y), labels[row], fill='#8ae7f1', font=font)
        count = len(frames) if row == 0 else 8
        for i in range(count):
            x = 25 + i * 138
            draw.line((x, y + 213, x + 128, y + 213), fill='#536170')
            if row == 0:
                art, pivot = frames[i]
                # Preserve the actual pivot so native torso bob is visible.
                scale = .68
                tile = art.resize((round(art.width * scale), round(art.height * scale)), Image.Resampling.NEAREST)
                left = x + 64 - round(art.width * pivot.x * scale)
                top = y + 145 - round(art.height * (1-pivot.y) * scale)
            else:
                art = assets[stages[row-1]].crop((i*300, 0, (i+1)*300, 375))
                tile = art.resize((210, 263), Image.Resampling.NEAREST if row < 3 else Image.Resampling.LANCZOS)
                left, top = x + 64 - 111, y + 0
            board.paste(tile, (left, top), tile)
            draw.text((x + 60, y + 215), str(i + 1), fill='white', font=font, anchor='mt')
    board.save(OUT / 'walk-native-comparison.png')
    # Equal-duration preview. Native 12 and bit 8 poses are sampled over 24 ticks.
    loop = []
    for tick in range(24):
        canvas = Image.new('RGB', (690, 320), '#17212d')
        d = ImageDraw.Draw(canvas)
        for row in range(len(labels)):
            x = row * 230
            d.text((x + 8, 12), labels[row], fill='white', font=font)
            d.line((x+10, 260, x+220, 260), fill='#536170')
            if row == 0:
                art, pivot = frames[tick * len(frames)//24]
                tile = art.resize((round(art.width*.9), round(art.height*.9)), Image.Resampling.NEAREST)
                left, top = x + 115 - round(art.width*pivot.x*.9), 170 - round(art.height*(1-pivot.y)*.9)
            else:
                i = tick//3
                tile = assets[stages[row-1]].crop((i*300,0,(i+1)*300,375))
                left, top = x-44, -44
            canvas.paste(tile, (left, top), tile)
        loop.append(canvas)
    loop[0].save(OUT / 'walk-native-comparison.gif', save_all=True, append_images=loop[1:], duration=40, loop=0, disposal=2)
    report = {'nativeClip': clip['m_Name'], 'nativeFrames': metadata, 'nativeSampleRate': clip['m_SampleRate'],
              'nativeDuration': clip['m_MuscleClip']['m_StopTime'], 'bitFrames': 8,
              'note': 'BEFORE FIX: old eight-frame bit artwork, not the new native-body rendering. Equal-duration GIF is slowed for inspection.'}
    (OUT / 'walk-native-comparison.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('Native Walk:', len(frames), 'frames,', report['nativeDuration'], 'seconds; bit poses: 8 frames per cycle.')
    print(OUT / 'walk-native-comparison.png')


if __name__ == '__main__':
    main()
