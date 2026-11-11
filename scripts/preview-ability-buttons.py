from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import textwrap

ROOT = Path(__file__).resolve().parent.parent
RES = ROOT / 'addon/Resources'
NOS = Path('D:/VSCode/Nebula-Public-Upstream/NebulaPluginNova/Resources/Buttons')
OUT = ROOT / 'docs/design/ability-buttons-current.png'
BUTTONS = [
    ('メガホンレーザー', '通常時', 'メガホン', RES/'RoleIcons/MegaphoneLaser.png', '蓄積中 → 発射中'),
    ('メガホン', '通常時', 'メガホン', RES/'RoleIcons/PortableMegaphone.png', '使用中／電池切れ／通信妨害中'),
    ('インタビュアー', '通常時', '録音', RES/'RoleIcons/Interviewer.png', '録音中／通信妨害中'),
    ('インタビュアー', '会議・左上', '録音を再生', RES/'RoleIcons/Interviewer.png', '再生中／録音なし／再生回数なし'),
    ('ボイスジャミンガー', '通常時・対象を選択', '声を妨害', RES/'RoleIcons/VoiceJamminger.png', '次の会議の対象に妨害を仕込む'),
    ('ボイスジャミンガー', '会議・左上', '全体ピッチ変更', RES/'RoleIcons/VoiceJamminger.png', 'ピッチを再変更／残り回数なし'),
    ('スーパーヒーロー', '通常時', '変身', RES/'Heroes/SuperTransform.png', '「設定ワード」と発声／変身中／音声認識準備中'),
    ('スーパーヒーロー', '変身中', '正義執行', NOS/'VanillaKillButton.png', 'NoS標準キルアイコン'),
    ('イーヴィルヒーロー', '通常時', '変身', RES/'Heroes/EvilTransform.png', '「設定ワード」と発声／変身中／音声認識準備中'),
    ('イーヴィルヒーロー', '変身中／設定で変身前も', 'キル', NOS/'VanillaKillButton.png', 'NoS標準キルアイコン'),
    ('壁に耳あり', '通常時', '耳を設置', RES/'RoleIcons/WallEar.png', '設置可能な残り数を表示'),
    ('オウムトレーナー', '通常時', 'オウムを設置', RES/'RoleIcons/ParrotTrainer.png', '設置可能な残り数を表示'),
    ('ボイスストーン', '通常時', '設置', RES/'RoleIcons/VoiceStone.png', '単語待ち'),
    ('クローン', '通常時', '録音', RES/'RoleIcons/DyingVoiceMessage.png', '録音中（10秒）'),
    ('クローン', '録音完了後', '召喚', NOS/'DoppelgangerSwapButton.png', '同時に1体／消滅後クールダウン'),
]


def font(size, bold=False):
    return ImageFont.truetype('C:/Windows/Fonts/' + ('meiryob.ttc' if bold else 'meiryo.ttc'), size)


def main():
    board = Image.new('RGB', (1350, 2200), '#10171e')
    draw = ImageDraw.Draw(board)
    draw.text((45, 26), 'タヌキ丼  能力発動ボタン一覧', font=font(41, True), fill='#f5f9ff')
    draw.text((48, 90), 'アドオンで追加する15枠（会議用を含む）｜ 現行ソースで参照している画像・表示名', font=font(21), fill='#a9bfce')
    for i, (role, scene, label, path, states) in enumerate(BUTTONS):
        assert path.is_file(), path
        x, y = 36 + i % 3 * 438, 152 + i // 3 * 360
        draw.rounded_rectangle((x, y, x + 402, y + 340), radius=22, fill='#1c2833', outline='#344654', width=2)
        draw.text((x + 15, y + 12), f'{i+1:02d}', font=font(16), fill='#6d879b')
        draw.text((x + 201, y + 15), role, font=font(22, True), fill='#f5f9ff', anchor='mt')
        draw.text((x + 201, y + 53), scene, font=font(17), fill='#89dcd9', anchor='mt')
        icon = Image.open(path).convert('RGBA')
        icon = icon.crop(icon.getbbox())
        scale = min(182 / icon.width, 158 / icon.height)
        icon = icon.resize((round(icon.width * scale), round(icon.height * scale)), Image.Resampling.LANCZOS)
        board.paste(icon, (round(x + (402-icon.width)/2), round(y + 89 + (158-icon.height)/2)), icon)
        color = '#ff8d96' if label in ('キル', '正義執行') else '#f5f9ff'
        draw.text((x + 201, y + 259), label, font=font(28, True), fill=color, anchor='mt')
        for line, text in enumerate(textwrap.wrap(states, 21)):
            draw.text((x + 201, y + 298 + line * 20), text, font=font(16), fill='#a9bfce', anchor='mt')
    notes = [
        '・ヒーローの「変身」は、役職アイコンとは別の専用画像です。',
        '・メガホンレーザー／ボイスジャミンガーの通常キルは、既存のキルボタンを使います。',
        '・耳の聴取開始・解除は、設置した耳に近づいて直接クリックします（HUDボタンではありません）。',
        '・ビビリ／おしゃべり／リミットスピーチ／NGワード／ダイイングボイスは自動発動です。',
        '※ 同じボタンの状態表示は各枠の下段に記載。使用不可・クールダウン時の色や数字は省略しています。',
    ]
    for i, note in enumerate(notes):
        draw.text((45, 1977 + i * 37), note, font=font(19), fill='#a9bfce')
    OUT.parent.mkdir(parents=True, exist_ok=True)
    board.save(OUT, optimize=True)
    print(f'{len(BUTTONS)} button slots rendered: {OUT}')


if __name__ == '__main__':
    main()
