"""Preview the dedicated bit zipline assets, not separate idle parts."""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT/'docs/design/bit-character/zipline-preview.png'
STAGES = [2, 4, 8, 16, 32]


def tint(image, bits):
    body, back, visor = ((170,)*3, (85,)*3, (255,)*3) if bits == 2 else ((198,17,17), (119,10,10), (154,202,213))
    out = image.copy()
    pixels = []
    for r, g, b, a in image.get_flattened_data():
        rgb = visor if g == 255 else body if r == 255 else back if b == 255 else (0,0,0)
        pixels.append(rgb+(a,))
    out.putdata(pixels)
    return out


def main():
    font = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc', 21)
    small = ImageFont.truetype('C:/Windows/Fonts/meiryo.ttc', 16)
    strips = {bits: tint(Image.open(ROOT/'addon/Resources/BitCharacter'/('Zipline'+str(bits)+'.png')).convert('RGBA'), bits) for bits in STAGES}
    frames = []
    for index in range(4):
        board = Image.new('RGBA', (1250, 790), '#172531')
        d = ImageDraw.Draw(board)
        d.text((20, 12), 'bitキャラクター：ファングル風の座ったジップライン姿勢', fill='white', font=font)
        d.text((20, 48), '両足を前に曲げる・体を後ろへ傾ける・腕を描かず手を離す／実素材プレビュー（ゲーム画面ではありません）', fill='#b6c8cf', font=small)
        for row, flip in enumerate([False, True]):
            for col, bits in enumerate(STAGES):
                x, y = col*250, 90+row*345
                d.text((x+125,y), str(bits)+'bit / '+('左向き' if flip else '右向き'), anchor='mt', fill='#80ecee', font=font)
                image = strips[bits].crop((index*300, 0, (index+1)*300, 375))
                if flip:
                    image = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                image = image.resize((240, 300), Image.Resampling.NEAREST)
                board.alpha_composite(image, (x+5,y+32))
        frames.append(board.convert('RGB'))
    frames[0].save(OUT)
    frames[0].save(OUT.with_suffix('.gif'), save_all=True, append_images=frames[1:], duration=140, loop=0, disposal=2)
    OUT.with_suffix('.json').write_text(json.dumps({'previewOnly': True, 'gameCapture': False,
        'source': 'addon/Resources/BitCharacter/Zipline{bits}.png', 'stages': STAGES, 'frames': 4,
        'pose': 'seated with both feet forward, leaning back, detached gripping hand, no long arm', 'gripPixels': [220,55],
        'directions': ['right','left'], 'selectedColorExample': 'red'}, indent=2)+'\n', encoding='utf-8')
    print(OUT)


if __name__ == '__main__':
    main()
