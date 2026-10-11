from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import shutil, zipfile

root = Path(__file__).resolve().parent.parent
out = root / 'dist/icon-request'
out.mkdir(parents=True, exist_ok=True)
for name in ['bibiri-icon.png', 'talkative-icon.png']:
    shutil.copy2(root / 'dist' / name, out / name)
font_path = 'C:/Windows/Fonts/meiryo.ttc'
bold_path = 'C:/Windows/Fonts/meiryob.ttc'
def font(size, bold=False): return ImageFont.truetype(bold_path if bold else font_path, size)
im = Image.new('RGB', (1600, 1230), '#0e1520')
d = ImageDraw.Draw(im)
white, muted, tan, mint = '#f1f6fc', '#aebcce', '#c4a470', '#64dcb4'
def text(x, y, s, size=24, color=white, bold=False): d.text((x,y),s,font=font(size,bold),fill=color)
def icon(name, xy, size):
    p = Image.open(out / name).convert('RGBA').resize((size,size),Image.Resampling.LANCZOS)
    im.paste(p,xy,p)

text(48, 27, 'ビビリ / おしゃべり  アイコン制作参考', 43, bold=True)
text(50, 96, 'NoSアドオンの追加能力（モディファイア）｜現在の仮アイコンとUI配置の再現図', 23, muted)
for x, title, color, file, lines in [
    (48, 'ビビリ', tan, 'bibiri-icon.png', [
        '大声を出して、設定された声量に達すると自滅。',
        'イメージ：怯え・緊張・驚き。',
        '候補：汗、震えたポーズ、びくっとした表情。',
        '色の目安：ベージュ / 淡い茶色。']),
    (824, 'おしゃべり', mint, 'talkative-icon.png', [
        '一定時間しゃべらないと自滅。発言で時間が戻る。',
        'イメージ：おしゃべり好き・声が止まらない。',
        '候補：吹き出し、声の線、元気なポーズ。',
        '色の目安：ミント / 青緑。'])]:
    d.rounded_rectangle((x,153,x+728,550),24,fill='#172231',outline='#30425a',width=2)
    text(x+26,176,title,34,color,True)
    icon(file,(x+18,244),180)
    text(x+220,243,'現在の仮デザイン',20,muted)
    for j,line in enumerate(lines): text(x+220,289+j*48,line,18)
    d.rounded_rectangle((x+24,463,x+704,526),14,fill='#090e15',outline='#f0f5fc',width=3)
    icon(file,(x+30,466),57)
    text(x+108,475,title,25,color,True)
    text(x+429,483,'役職一覧での使用例',17,muted)

text(48,588,'ゲーム中のUI配置参考',30,bold=True)
text(48,631,'右上に表示。アイコンの下に数字やメーターを組み合わせます。',21,muted)
d.rounded_rectangle((48,687,986,1110),22,fill='#202b36',outline='#405168',width=2)
text(77,711,'UI再現図（実ゲームのスクリーンショットではありません）',18,muted)
# The two modifiers coexist: Talkative is left of Bibiri in the top-right HUD.
icon('talkative-icon.png',(587,752),104)
icon('bibiri-icon.png',(787,752),104)
text(595,863,'10s',35,white,True)
d.rounded_rectangle((823,863,857,1032),5,fill='#0e171e',outline=white,width=3)
d.rectangle((829,924,851,1026),fill='#75e3a3')
d.line((814,869,866,869),fill='#ff5f65',width=4)
text(801,1040,'60%',23,'#75e3a3',True)
text(580,1010,'おしゃべり',19,mint)
text(571,1052,'残り無言時間',18,muted)
text(896,860,'←100%',15,'#ff7b80')
text(898,911,'声量で',16,muted)
text(898,937,'上に伸びる',15,muted)
text(775,1080,'ビビリ',17,tan)

text(1030,691,'制作・納品の目安',28,bold=True)
items = [
    '・各1点、透過PNG。正方形のキャンバス。',
    '・512×512px以上推奨（現在は256px）。',
    '・編集可能な元データもあるとうれしいです。',
    '・48～96px程度でも意味が伝わる形。',
    '・太めの輪郭、暗い背景でも読める明暗。',
    '・文字や数字、メーターは画像に入れない。',
    '・通常 / 危険 / 停止はゲーム側で色を変更。',
    '・重要な形は端に寄せず、余白を確保。',
]
for j,line in enumerate(items): text(1030,750+j*43,line,18)
text(49,1151,'仮アイコンは構図の参考です。表情やポーズは描き直し歓迎。2種類を同じ絵柄で揃えてください。',22,muted)
im.save(out / 'reference-board.png')

brief = '''ビビリ・おしゃべり アイコン制作のご依頼

Among UsのMod「Nebula on the Ship」向けアドオンで使う、2種類の追加能力（モディファイア）のアイコンをお願いしたいです。
現在の仮アイコンと、ゲーム内での配置を再現した参考画像を添付します。UI画像は配置説明用の再現図で、実ゲームのスクリーンショットではありません。

【ビビリ】
設定した声量に達するような大声を出すと、自滅する能力です。
怯えている・びっくりしている雰囲気が一目で伝わる絵にしたいです。
仮案はベージュ系のクルーメイトに、汗と震えの線を添えています。
表情やポーズはお任せできます。細かすぎず、小さくても「ビビっている」と分かる形を希望します。

【おしゃべり】
指定された秒数しゃべらないと自滅し、発言すると猶予時間が戻る能力です。
よくしゃべる・会話好きな雰囲気を出したいです。
仮案はミント系のクルーメイトに、吹き出しと声の線を添えています。
ビビリと同じ絵柄で、見分けやすいシルエットを希望します。

【使用場所】
1. 役職・モディファイア一覧の名前の左側。
2. ゲーム画面の右上にある状態表示。
   ビビリ：アイコンの下に縦の声量メーターとパーセントを表示。下から上に伸び、100%が自滅ライン。
   おしゃべり：アイコンの下に「10s」などの残り無言時間を表示。残り5秒以下では危険を知らせる色に変わる。
両方が付いた場合、おしゃべりが左、ビビリが右です。会議中に表示する場合もあります。

【希望するデータ】
・各1種類の透過PNG。正方形、512×512px以上を推奨します。
・編集可能な元データも納品可能ならお願いします。形式は相談できます。
・実際は48～96px程度に縮小されるため、太めの輪郭と分かりやすい形を希望します。
・暗いゲーム背景でも見やすくし、重要な部分は画像の端から少し離してください。
・役職名、パーセント、秒数、メーターはゲーム側で描くので、絵には入れないでください。
・通常／危険／未接続や判定停止の状態はゲーム側で赤系や灰色に色を乗せます。状態別のイラストは必須ではありません。
・添付の絵は仮デザインです。構図や表情はより良い案に変更していただいて大丈夫です。

制作費、納期、使用・加工条件については別途ご相談させてください。
'''
(out / '依頼文.txt').write_text(brief, encoding='utf-8-sig')
with zipfile.ZipFile(root / 'dist/voice-role-icon-request.zip', 'w', zipfile.ZIP_DEFLATED) as z:
    for file in out.iterdir():
        if file.is_file(): z.write(file, file.name)
print(out / 'reference-board.png')
print(root / 'dist/voice-role-icon-request.zip')
