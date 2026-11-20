"""Shared deterministic layout for the corrected, armless seated zipline art."""
from collections import deque
import statistics
from PIL import Image

GRIP = (220, 55)


def components(image, include_pixels=False):
    w, h = image.size
    alpha = image.getchannel('A')
    values = bytearray(1 if a >= 128 else 0 for a in alpha.get_flattened_data())
    found = []
    for start in range(w*h):
        if not values[start]:
            continue
        values[start] = 0
        queue = deque([start])
        left = right = start % w
        top = bottom = start // w
        area = 0
        positions = []
        while queue:
            i = queue.popleft()
            x, y = i % w, i // w
            area += 1
            if include_pixels:
                positions.append(i)
            left, right = min(left,x), max(right,x)
            top, bottom = min(top,y), max(bottom,y)
            for other in ([i-1] if x else []) + ([i+1] if x+1 < w else []) + ([i-w] if y else []) + ([i+w] if y+1 < h else []):
                if values[other]:
                    values[other] = 0
                    queue.append(other)
        if area >= 20:
            found.append({'area':area, 'bounds':(left,top,right+1,bottom+1), 'pixels':positions})
    return sorted(found,key=lambda c:c['area'],reverse=True)


def seated_frames(sheet):
    assert sheet.mode == 'RGBA' and sheet.getpixel((0,0))[3] == 0
    cells = [sheet.crop((round(i*sheet.width/4),0,round((i+1)*sheet.width/4),sheet.height)) for i in range(4)]
    anchors = []
    for cell in cells:
        parts = components(cell)
        assert len(parts) >= 2, 'Seated pose must have a detached hand, no connecting arm'
        body = parts[0]['bounds']
        candidates = [p for p in parts[1:] if p['area'] > parts[0]['area']*.01 and p['bounds'][1] < body[1]]
        assert candidates, 'Missing separate gripping hand above the seated body'
        hand = max(candidates,key=lambda p:p['area'])['bounds']
        gx, gy = (hand[0]+hand[2])/2, (hand[1]+hand[3])/2
        assert hand[3] < body[3] and gx > (body[0]+body[2])*.45
        visible = cell.getchannel('A').point(lambda a:255 if a>=128 else 0).getbbox()
        anchors.append((gx,gy,body,hand,visible))
    scale = 192/statistics.median(a[2][3]-a[2][1] for a in anchors)
    for gx,gy,body,hand,b in anchors:
        for available,extent in [(GRIP[0]-8,gx-b[0]),(300-GRIP[0]-8,b[2]-gx),(GRIP[1]-8,gy-b[1]),(375-GRIP[1]-8,b[3]-gy)]:
            if extent > 0:
                scale = min(scale,available/extent)
    output, records = [], []
    for cell, (gx,gy,body,hand,b) in zip(cells,anchors):
        tile = cell.resize((round(cell.width*scale),round(cell.height*scale)),Image.Resampling.LANCZOS)
        target = Image.new('RGBA',(300,375))
        xy = (round(GRIP[0]-gx*scale),round(GRIP[1]-gy*scale))
        target.alpha_composite(tile,xy)
        b = target.getchannel('A').point(lambda a:255 if a>=4 else 0).getbbox()
        assert b and 0 < b[0] < b[2] < 300 and 0 < b[1] < b[3] < 375
        output.append(target)
        records.append({'offset':xy,'gripSource':[gx,gy],'bodyBounds':body,'handBounds':hand})
    return output, {'scale':scale,'gripPixels':list(GRIP),'pose':'seated, both feet forward, detached hand, no long arm','frames':records}
