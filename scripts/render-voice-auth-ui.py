"""Build the console faceplate and a labelled design preview from the same art."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
SCALE = 2
SIZE = (1400, 900)
image = Image.new('RGBA', (SIZE[0]*SCALE, SIZE[1]*SCALE))
d = ImageDraw.Draw(image)
INK = '#17262c'

def box(bounds, fill, radius=18, outline=INK, width=6):
    d.rounded_rectangle(tuple(int(v*SCALE) for v in bounds), radius*SCALE,
                        fill=fill, outline=outline, width=width*SCALE)

def line(points, fill, width=5):
    d.line([(int(x*SCALE), int(y*SCALE)) for x,y in points], fill=fill, width=width*SCALE)

def circle(x,y,r,fill,outline=INK,width=5):
    d.ellipse(((x-r)*SCALE,(y-r)*SCALE,(x+r)*SCALE,(y+r)*SCALE),
              fill=fill, outline=outline, width=width*SCALE)

# Thick, slightly imperfect-looking hardware bezel and inset teal face.
box((17,30,1383,885),'#18282e',68,width=14)
box((15,15,1385,865),'#829c9b',68,width=14)
box((38,37,1362,840),'#d8e1d0',49,outline='#b9cdc4',width=8)
box((63,63,1337,177),'#557f7e',25,width=7)
line([(86,80),(1307,80)], '#81a5a0',4)
for x in (75,1325):
    for y in (211,779):
        circle(x,y,14,'#8b9d94',width=4)
        line([(x-6,y-5),(x+6,y+5)],INK,3)

# Main recessed CRT display; all readable labels are live game text.
box((273,205,1053,590),'#78968e',31,width=8)
box((290,222,1036,573),'#101e27',21,width=6)
box((302,234,1024,560),'#163b43',16,outline='#385b59',width=3)
for y in range(240,555,9):
    line([(313,y),(1013,y)],'#1b4148',1)
line([(326,251),(995,251)],'#356369',3)
line([(326,510),(995,510)],'#356369',3)
box((320,604,1020,740),'#a8bbb0',21,width=6)
box((342,650,998,721),'#233c40',12,width=4)

# Microphone, physical grille, stand and cable.
circle(167,365,91,'#668e84',width=7)
circle(167,365,78,'#b2c8ae',width=4)
box((115,289,219,430),'#dce2ca',40,width=7)
box((128,303,206,393),'#425d60',27,width=4)
for y in range(317,384,13):
    line([(139,y),(195,y)],'#98b6aa',4)
line([(99,367),(99,404),(118,437),(165,451),(213,437),(235,405),(235,367)],INK,8)
box((155,447,179,500),'#73918b',8,width=5)
box((115,494,220,513),'#456765',8,width=5)
line([(167,513),(167,552),(190,569),(239,569),(254,593),(254,663),(285,684)],INK,8)
circle(168,597,17,'#30534d',width=5)
for y in range(651,723,17):
    box((110,y,222,y+8),'#8ea69a',3,outline='#8ea69a',width=1)

# Five physical progress lamps; unused slots remain part of the hardware.
box((1080,205,1310,740),'#b9c8b5',25,width=7)
box((1098,224,1292,277),'#668c7e',12,width=4)
for i in range(5):
    y=320+i*81
    box((1100,y-30,1290,y+30),'#9caf9f',13,width=3)
    circle(1135,y,18,'#405a53',width=4)

# Lower edge: interruption is safe and the physical button closes the task.
box((292,780,1025,844),'#bdcdbb',15,outline='#8aa294',width=3)
box((1051,780,1310,844),'#355b61',18,width=6)
line([(1070,793),(1290,793)],'#729d97',3)
image=image.resize(SIZE,Image.Resampling.LANCZOS)
asset=ROOT/'addon/Resources/VoiceAuthenticationPanel.png'
asset.parent.mkdir(exist_ok=True,parents=True)
image.save(asset,optimize=True)

# Preview uses the actual shipped faceplate, with representative dynamic UI.
preview=Image.new('RGBA',SIZE,'#202c35')
preview.alpha_composite(image)
p=ImageDraw.Draw(preview)
font_path='C:/Windows/Fonts/YuGothB.ttc'
def text(x,y,value,size,color='#edf4de'):
    p.text((x,y),value,font=ImageFont.truetype(font_path,size),fill=color,anchor='mm')
text(665,119,'音 声 認 証',43)
text(1195,246,'認証進捗',23)
text(663,281,'この単語を読み上げてください',25,'#98c5c0')
text(663,381,'ロケット',78,'#ffe88e')
text(663,475,'02 / 03',25,'#b8d0c7')
text(663,538,'音声を認識中…',24,'#85e1bc')
text(670,626,'マイク入力',21,'#395c58')
text(663,812,'中断しても進捗は保存されます',23,'#385a54')
text(1180,812,'閉じる',29)
words=['バナナ','ロケット','カメラ','—','—']
for i,word in enumerate(words):
    y=320+i*81
    color='#71ddb5' if i==0 else '#ffdc78' if i==1 else '#526b60'
    p.ellipse((1122,y-13,1148,y+13),fill=color)
    text(1220,y,word,23,'#213f39' if i<3 else '#778b7d')
for i in range(16):
    x=355+i*39
    height=[12,18,27,38,48,39,57,45,33,48,29,23,18,12,8,8][i]
    p.rounded_rectangle((x,710-height,x+25,710),radius=3,fill='#77e2ae' if i<10 else '#42605c')
out=ROOT/'dist/voice-authentication-ui-preview.png'
out.parent.mkdir(exist_ok=True)
preview.convert('RGB').save(out,optimize=True)
print(asset)
print(out)
