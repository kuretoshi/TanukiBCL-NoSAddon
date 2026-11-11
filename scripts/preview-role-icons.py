from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import hashlib

ROOT = Path(__file__).resolve().parent.parent
RESOURCES = ROOT / 'addon/Resources/RoleIcons'
DESIGN = ROOT / 'docs/design'
ROLES = [
    ('SuperHero', 'スーパーヒーロー', '光・正義', 3, 'クルーメイト'),
    ('EvilHero', 'イーヴィルヒーロー', '闇・悪', 4, 'インポスター'),
    ('Bibiri', 'ビビリ', '震え・汗', 10, 'モディファイア'),
    ('Talkative', 'おしゃべり', '会話・吹き出し', 11, 'モディファイア'),
    ('LimitSpeech', 'リミットスピーチ', '残り発話時間・砂時計', 12, 'モディファイア'),
    ('NGWord', 'NGワード', '禁止の言葉', 13, 'モディファイア'),
    ('Interviewer', 'インタビュアー', '取材・マイク', 2, 'クルーメイト・マッドメイト'),
    ('VoiceJamminger', 'ボイスジャミンガー', '妨害電波・混線', 5, 'インポスター'),
    ('VoiceStone', 'ボイスストーン', '言葉を石にする', 9, 'マッドメイト'),
    ('DyingVoiceMessage', 'ダイイングボイスメッセージ', '遺した声・テープレコーダー', 14, 'モディファイア'),
    ('MegaphoneLaser', 'メガホンレーザー', 'チャージ・レーザー砲', 0, 'インポスター'),
    ('PortableMegaphone', 'メガホン', '拡声・充電バッテリー', 1, 'クルーメイト・マッドメイト'),
    ('WallEar', '壁に耳あり', '耳・壁越しの声', 6, 'クルーメイト・マッドメイト'),
    ('ParrotTrainer', 'オウムトレーナー', '可愛いオウム・声まね', 7, 'クルーメイト・マッドメイト'),
]


def font(size, bold=False):
    return ImageFont.truetype('C:/Windows/Fonts/' + ('meiryob.ttc' if bold else 'meiryo.ttc'), size)


def draw_board(roles, icons, filename, title):
    height = 175 + ((len(roles) + 1) // 2) * 495
    preview = Image.new('RGB', (1100, height), '#10171e')
    draw = ImageDraw.Draw(preview)
    draw.text((40, 25), title, font=font(40, True), fill='#f5f9ff')
    draw.text((42, 87), f'全{len(roles)}種・共用なし ／ 透過PNG・各512×512px', font=font(22), fill='#adbfcd')
    for index, (name, label, motif, _, _) in enumerate(roles):
        x, y = 28 + index % 2 * 544, 145 + index // 2 * 495
        draw.rounded_rectangle((x, y, x + 516, y + 471), radius=22, fill='#1c2833', outline='#344654', width=2)
        icon = icons[name].resize((330, 330), Image.Resampling.LANCZOS)
        preview.paste(icon, (x + 93, y + 12), icon)
        draw.text((x + 258, y + 348), label, font=font(27 if len(label) > 11 else 31, True), fill='#f5f9ff', anchor='mt')
        draw.text((x + 258, y + 397), motif, font=font(20), fill='#a8c9d3', anchor='mt')
        tiny = icons[name].resize((48, 48), Image.Resampling.LANCZOS)
        for tx, background in [(x + 368, '#10171e'), (x + 430, '#f0f4f8')]:
            draw.rounded_rectangle((tx, y + 411, tx + 54, y + 465), radius=8, fill=background)
            preview.paste(tiny, (tx + 3, y + 414), tiny)
    draw.text((40, height - 30), '小さい2枚は48px表示の確認用です。', font=font(19), fill='#a5b8c8')
    preview.save(DESIGN / filename, optimize=True)


def update_catalog(icons):
    path = DESIGN / 'role-icons-current.png'
    if not path.exists():
        return
    catalog = Image.open(path).convert('RGB')
    draw = ImageDraw.Draw(catalog)
    for name, label, _, index, category in ROLES:
        x, y = 36 + index % 3 * 438, 156 + index // 3 * 346
        draw.rounded_rectangle((x, y, x + 402, y + 326), radius=22, fill='#1c2833', outline='#344654', width=2)
        color = '#ff858b' if category == 'インポスター' else '#e0b4ff' if category == 'モディファイア' else '#ffcf80' if category == 'マッドメイト' else '#92e8e1'
        draw.text((x + 201, y + 15), category, font=font(19), fill=color, anchor='mt')
        icon = icons[name].resize((208, 208), Image.Resampling.LANCZOS)
        catalog.paste(icon, (x + 97, y + 49), icon)
        if name == 'DyingVoiceMessage':
            for line, at in [('ダイイングボイス', 255), ('メッセージ', 285)]:
                draw.text((x + 201, y + at), line, font=font(24, True), fill='#f6fbff', anchor='mt')
        else:
            draw.text((x + 201, y + 266), label, font=font(25, True), fill='#f6fbff', anchor='mt')
    catalog.save(path, optimize=True)


def main():
    icons = {}
    fingerprints = set()
    for name, *_ in ROLES:
        path = RESOURCES / (name + '.png')
        icon = Image.open(path).convert('RGBA')
        assert icon.size == (512, 512), path
        assert icon.getchannel('A').getextrema() == (0, 255), path
        fingerprint = hashlib.sha256(path.read_bytes()).hexdigest()
        assert fingerprint not in fingerprints, 'Duplicate role artwork'
        fingerprints.add(fingerprint)
        icons[name] = icon
    DESIGN.mkdir(parents=True, exist_ok=True)
    draw_board(ROLES, icons, 'role-icons-redrawn.png', 'タヌキ丼  新しい役職アイコン')
    draw_board(ROLES[6:10], icons, 'role-icons-additional.png', 'タヌキ丼  追加4種の役職アイコン')
    draw_board(ROLES[10:], icons, 'role-icons-megaphone-ear-parrot.png', 'タヌキ丼  メガホン・耳・オウム')
    update_catalog(icons)
    print(f'PASS: {len(ROLES)} distinct transparent assets; role previews refreshed')


if __name__ == '__main__':
    main()
