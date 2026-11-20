"""Validate the shipped sprites and lay out a preview; creates no new artwork."""
from pathlib import Path
import json
import hashlib
import subprocess
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
DESIGN = ROOT / 'docs/design/bit-character'
COSMIC = ROOT / 'addon/MoreCosmic'
STAGES = [2, 4, 8, 16, 32]
PARTS = ['Main', 'Move', 'Climb', 'ClimbDown', 'EnterVent']


def font(size, bold=False):
    return ImageFont.truetype('C:/Windows/Fonts/' + ('meiryob.ttc' if bold else 'meiryo.ttc'), size)


def tint(image):
    # Same adaptive RGB-mask convention as BCL's NoS avatar renderer.
    result = image.copy()
    body, visor = (198, 17, 17), (154, 202, 213)
    pixels = []
    for r, g, b, a in image.getdata():
        if a == 0 or max(r, g, b) == min(r, g, b):
            pixels.append((r, g, b, a))
            continue
        rgb = tuple(round((body[c] * .6 * b / 255 * (1-r/255) + body[c] * r/255) * (1-g/255) + visor[c] * g/255) for c in range(3))
        pixels.append(rgb + (a,))
    result.putdata(pixels)
    return result


def load_assets():
    contents = json.loads((COSMIC / 'Contents.json').read_text(encoding='utf-8-sig'))
    previous = json.loads(subprocess.check_output(['git', '-c', 'safe.directory=' + ROOT.as_posix(), '-C', str(ROOT), 'show', 'HEAD:addon/MoreCosmic/Contents.json'], text=True))
    # Hero motion now has its own exporter; retain its identity/idle costume
    # while allowing intentional animation updates independently of bit art.
    for hero in contents['Hats'][:2]:
        before = next(h for h in previous['Hats'] if h['Name'] == hero['Name'])
        for key in ['Name', 'Author', 'Package', 'IsHidden', 'Adaptive', 'Bounce', 'HideHands', 'Main']:
            assert hero[key] == before[key], 'hero identity and idle art preserved'
    assert 'BitCharacter64' not in {h['Name'] for h in contents['Hats']}, '64bit removed from playable costumes'
    hats = {hat['Name']: hat for hat in contents['Hats']}
    images = {}
    for stage in STAGES:
        hat = hats['BitCharacter' + str(stage)]
        assert hat['Adaptive'] == (stage != 2) and not hat['Bounce'] and hat['HideHands']
        assert hat['Move']['Length'] == 8 and hat['Move']['AnimSync'] == 1
        assert hat['Climb']['Length'] == 2 and hat['Climb']['AnimSync'] == 1
        assert hat['ClimbDown']['Address'] != hat['Climb']['Address'], 'descent must have its own overhead-hand sprite'
        assert hat['ClimbDown']['Length'] == 1
        for part in PARTS:
            image = Image.open(COSMIC / 'hats' / hat[part]['Address']).convert('RGBA')
            count = 8 if part == 'Move' else 2 if part == 'Climb' else 1
            assert image.size == (300*count, 375)
            alpha = image.getchannel('A')
            if stage != 64:
                assert set(alpha.getdata()) <= {0, 255}, 'point-filtered binary transparency'
            else:
                assert any(0 < a < 255 for a in alpha.getdata()), '64bit keeps smooth edge alpha'
            frames = [image.crop((i*300, 0, (i+1)*300, 375)) for i in range(count)]
            for frame in frames:
                box = frame.getbbox()
                assert box and box[0] > 0 and box[1] > 0 and box[2] < 300 and box[3] < 375, 'no clipped sprite'
            if part == 'Move':
                assert len({hashlib.sha256(f.tobytes()).digest() for f in frames}) == 8, 'eight distinct walk poses'
                core_bounds = [f.getchannel('A').point(lambda a: 255 if a >= 128 else 0).getbbox() for f in frames]
                assert {box[1] for box in core_bounds} == {115}, 'stationary helmet origin (ignores smooth edge coverage)'
            if part == 'Climb':
                assert frames[0].tobytes() != frames[1].tobytes(), 'alternating hand/leg poses'
            images[(stage, part)] = image if stage == 2 else tint(image)
    print('PASS sprite assets: five stages, no 64bit costume, preserved heroes, eight walk source poses, two-frame ascent, dedicated descent, adaptive flags and no clipping')
    return images


def draw_preview(images):
    board = Image.new('RGB', (1325, 1595), '#111b28')
    draw = ImageDraw.Draw(board)
    draw.text((30, 22), 'bitキャラクター：ゲーム用差分', font=font(34, True), fill='#f0f8ff')
    draw.text((32, 76), '素材プレビュー（ゲーム画面ではありません）／色追従は赤で仮表示', font=font(19), fill='#b9c8da')
    labels = ['通常', '歩行\n8コマ', '上り\nコマ1', '上り\nコマ2', '下り', 'ベント']
    display_parts = ['Main', 'Move', 'Climb', 'ClimbPose2', 'ClimbDown', 'EnterVent']
    for col, stage in enumerate(STAGES):
        draw.text((160+col*205, 124), str(stage)+'bit', font=font(25, True), fill='#75e5ef', anchor='mm')
        for row, part in enumerate(display_parts):
            sprite = images[(stage, 'Climb' if part == 'ClimbPose2' else part)]
            if part == 'Move':
                sprite = sprite.crop((0, 0, 300, 375))
            if part in ('Climb', 'ClimbPose2'):
                offset = 300 if part == 'ClimbPose2' else 0
                sprite = sprite.crop((offset, 0, offset+300, 375))
            sampling = Image.Resampling.LANCZOS if stage == 64 else Image.Resampling.NEAREST
            tile = sprite.crop((30, 75, 275, 345)).resize((181, 200), sampling)
            x, y = 70+col*205, 155+row*225
            draw.rounded_rectangle((x-5, y, x+190, y+214), radius=12, fill='#223044')
            board.paste(tile, (x+2, y+1), tile)
            if col == 0:
                draw.text((31, y+101), labels[row], font=font(17), fill='#dce9f5', anchor='mm', direction=None)
    draw.text((32, 1525), '2→4→8→16→32bit→本人の元の衣装・通常アニメーションに戻ります。', font=font(23, True), fill='#f0f8ff')
    board.save(DESIGN / 'animations-preview.png', optimize=True)
    frames = []
    for index in range(8):
        frame = Image.new('RGB', (1315, 310), '#111b28')
        draw = ImageDraw.Draw(frame)
        draw.text((25, 12), '歩行差分プレビュー（8コマ・ループ）', font=font(25, True), fill='#f0f8ff')
        for col, stage in enumerate(STAGES):
            sampling = Image.Resampling.LANCZOS if stage == 64 else Image.Resampling.NEAREST
            sprite = images[(stage, 'Move')].crop((index*300+30, 75, index*300+275, 345)).resize((181, 200), sampling)
            frame.paste(sprite, (col*215+25, 65), sprite)
            draw.text((col*215+115, 282), str(stage)+'bit', font=font(22, True), fill='#75e5ef', anchor='mm')
        frames.append(frame)
    frames[0].save(DESIGN / 'walk-preview.gif', save_all=True, append_images=frames[1:], duration=100, loop=0, disposal=2)


def draw_stage_table(images):
    # The final column is a real NoS body template, not a generated 64bit pose.
    original = tint(Image.open(ROOT / 'docs/design/hero-costumes-v2/reference/main.png').convert('RGBA'))
    board = Image.new('RGB', (1325, 685), '#111b28')
    draw = ImageDraw.Draw(board)
    draw.text((30, 20), 'bitキャラクター：2〜32bit＋元の姿', font=font(34, True), fill='#f0f8ff')
    draw.text((32, 77), '2→4→8→16→32bit→元の姿 ／ 制作素材の比較表（ゲーム画面ではありません）', font=font(20), fill='#b9c8da')
    looks = ['白黒・粗いドット', '大きなドット', 'レトロなドット', '細かなドット', 'さらに精細', '本人の元の衣装']
    voices = ['最も粗い', '粗め', 'レトロ', 'やや鮮明', '鮮明', '通常・加工なし']
    for col in range(len(STAGES)+1):
        x, center = 70+col*205, 160+col*205
        final = col == len(STAGES)
        title = '元の姿' if final else str(STAGES[col])+'bit'
        draw.text((center, 132), title, font=font(25, True), fill='#a3efc9' if final else '#75e5ef', anchor='mm')
        draw.rounded_rectangle((x-5, 158, x+190, 551), radius=12, fill='#233d3f' if final else '#223044')
        sprite = original if final else images[(STAGES[col], 'Main')]
        smooth = final or STAGES[col] == 64
        tile = sprite.crop((30, 75, 275, 345)).resize((181, 200), Image.Resampling.LANCZOS if smooth else Image.Resampling.NEAREST)
        board.paste(tile, (x+2, 169), tile)
        draw.text((center, 409), looks[col], font=font(18, True), fill='#f0f8ff', anchor='mm')
        draw.text((center, 465), voices[col], font=font(20), fill='#d8e7f1', anchor='mm')
        draw.text((center, 521), 'その姿を維持' if final else '5〜180秒', font=font(19), fill='#d8e7f1', anchor='mm')
    for y in [382, 438, 494]:
        draw.line((65, y, 1285, y), fill='#456074', width=1)
    for label, y in [('見た目', 409), ('声', 465), ('時間', 521)]:
        draw.text((31, y), label, font=font(17), fill='#dce9f5', anchor='mm')
    draw.text((30, 585), '※「元の姿」の画像は無装飾の例です。実際は本人の色・帽子・服・バイザーに戻ります。', font=font(21), fill='#d8e7f1')
    draw.text((30, 627), '会議中は時間停止・声は通常。各bit段階の継続時間は個別設定でき、初期値は各30秒です。', font=font(21), fill='#b9c8da')
    board.save(DESIGN / 'stages-comparison.png', optimize=True)


def draw_ladder(draw, center, offset):
    draw.rounded_rectangle((center-96, 100, center+96, 334), radius=12, fill='#223044')
    draw.line((center-53, 104, center-53, 330), fill='#75869b', width=9)
    draw.line((center+53, 104, center+53, 330), fill='#75869b', width=9)
    for rung in range(-1, 8):
        y = 103 + rung*38 + offset
        if 103 <= y <= 330:
            draw.line((center-53, y, center+53, y), fill='#8b9db1', width=7)


def draw_climb_preview(images, descending=False):
    # Up cycles two poses; down uses the separate overhead-hand NoS pose.
    frames = []
    for index in range(24):
        frame = Image.new('RGB', (1315, 390), '#111b28')
        draw = ImageDraw.Draw(frame)
        title = 'はしご下り：両手を上げる専用ポーズ' if descending else 'はしご上り：2コマ交互アニメーション'
        draw.text((25, 12), title, font=font(28, True), fill='#f0f8ff')
        draw.text((25, 58), 'ゲーム用素材の移動プレビュー（ゲーム画面ではありません）', font=font(20), fill='#b9c8da')
        for col, stage in enumerate(STAGES):
            center = col*215+115
            direction = -1 if descending else 1
            draw_ladder(draw, center, direction*round((index % 6)*38/6))
            offset = 0 if descending else (index // 6 % 2)*300
            part = 'ClimbDown' if descending else 'Climb'
            sampling = Image.Resampling.LANCZOS if stage == 64 else Image.Resampling.NEAREST
            sprite = images[(stage, part)].crop((offset+30, 50, offset+275, 355)).resize((181, 225), sampling)
            frame.paste(sprite, (center-90, 112), sprite)
            draw.text((center, 359), str(stage)+'bit', font=font(24, True), fill='#75e5ef', anchor='mm')
        frames.append(frame)
    filename = 'climb-down-preview.gif' if descending else 'climb-up-preview.gif'
    frames[0].save(DESIGN / filename, save_all=True, append_images=frames[1:], duration=70, loop=0, disposal=2)


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--climb-only', action='store_true')
    parser.add_argument('--table-only', action='store_true')
    args = parser.parse_args()
    assets = load_assets()
    if args.table_only:
        draw_stage_table(assets)
    else:
        if not args.climb_only:
            draw_preview(assets)
            draw_stage_table(assets)
        draw_climb_preview(assets)
        draw_climb_preview(assets, descending=True)
