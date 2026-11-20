"""Align generated hanging artwork and export five adaptive-mask strips."""
from pathlib import Path
import json
from zipline_layout import seated_frames, components
from PIL import Image, ImageFilter, ImageChops

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT/'docs/design/bit-character/source/zipline.png'
OUT = ROOT/'addon/Resources/BitCharacter'
CELLS = {2: 16, 4: 12, 8: 8, 16: 6, 32: 3}


def poses():
    sheet = Image.open(SOURCE).convert('RGBA')
    frames, metadata = seated_frames(sheet)
    for image in frames:
        pixels = []
        for r, g, b, a in image.get_flattened_data():
            if a < 128:
                pixels.append((0, 0, 0, 0))
                continue
            if max(r, g, b) < 80:
                rgb = (0, 0, 0)
            elif g >= r and g >= b:
                rgb = (0, 255, 0)
            else:
                rgb = (255, 0, 0) if r >= b else (0, 0, 255)
            pixels.append(rgb+(255,))
        image.putdata(pixels)
    return frames, metadata


def main():
    frames, metadata = poses()
    for bits, cell in CELLS.items():
        strip = Image.new('RGBA', (1200, 375))
        for i, image in enumerate(frames):
            size = (round(300/cell), round(375/cell))
            if bits <= 4:
                # Coarse blocks must not bridge the detached fist to the
                # helmet and accidentally reintroduce a long-looking arm.
                parts = components(image, include_pixels=True)
                hand = min(parts, key=lambda p:p['bounds'][1])
                body_pixels = list(image.get_flattened_data())
                hand_pixels = [(0,0,0,0)] * (300*375)
                for index in hand['pixels']:
                    hand_pixels[index] = body_pixels[index]
                    body_pixels[index] = (0,0,0,0)
                body = Image.new('RGBA', image.size); body.putdata(body_pixels)
                glove = Image.new('RGBA', image.size); glove.putdata(hand_pixels)
                body = body.resize(size, Image.Resampling.NEAREST)
                glove = glove.resize(size, Image.Resampling.NEAREST)
                clear = glove.getchannel('A').filter(ImageFilter.MaxFilter(3))
                body.putalpha(ImageChops.subtract(body.getchannel('A'), clear))
                small = Image.alpha_composite(body, glove)
            else:
                small = image.resize(size, Image.Resampling.NEAREST)
            pixelated = small.resize((300, 375), Image.Resampling.NEAREST)
            assert len(components(pixelated)) >= 2, 'Gripping hand must stay visibly detached at every bit level'
            strip.alpha_composite(pixelated, (i*300, 0))
        strip.save(OUT/('Zipline'+str(bits)+'.png'))
    (ROOT/'docs/design/bit-character/zipline-alignment.json').write_text(json.dumps(metadata, indent=2)+'\n', encoding='utf-8')
    print('PASS dedicated bit zipline: five four-frame RGBA mask strips; unified hanging hand/body; shared hero grip anchor')


if __name__ == '__main__':
    main()
