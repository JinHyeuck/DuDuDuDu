# -*- coding: utf-8 -*-
"""스프라이트 여러 장을 한 장의 목록 이미지로 묶고, 장마다 크기·투명 여백·9슬라이스 추정값을 찍는다.

    python Tools/ui/art/sprite_sheet.py <png 또는 glob ...> [--out sheet.png]

예)
    python Tools/ui/art/sprite_sheet.py "DuDuDuDU_Project/Assets/Resources/Art/InfinityMode/*.png" --out C:/tmp/sheet.png

왜: 시안 주석의 리소스 이름을 실제 파일과 맞출 때, 스프라이트를 한 장씩 열면 느리고 비싸다.
    한 장으로 묶어 한 번 보고, 수치는 텍스트로 받는다.

출력 수치
  size      — 파일 픽셀 크기
  visible   — 불투명 영역 (x0, y0, x1, y1). **Image 크기 = 보이는 크기 + 여백 x 배율** 을 계산할 때 쓴다
  border    — 9슬라이스 추정 (왼, 아래, 오른, 위). 가운데 줄과 같은 줄이 이어지는 곳까지를 늘어나는 구간으로 본다.
              **추정일 뿐이다 — 테두리는 사람이 정한다.** (Unity spriteBorder 순서와 같다)
  meta      — 현재 .meta 의 spriteBorder
"""
import argparse
import glob
import os
import re
import sys

from PIL import Image, ImageDraw

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')


def estimate_border(im):
    w, h = im.size
    px = im.load()
    col = lambda x: [px[x, y] for y in range(h)]
    row = lambda y: [px[x, y] for x in range(w)]
    cx, cy = w // 2, h // 2
    l = cx
    while l > 0 and col(l - 1) == col(cx):
        l -= 1
    r = cx
    while r < w - 1 and col(r + 1) == col(cx):
        r += 1
    t = cy
    while t > 0 and row(t - 1) == row(cy):
        t -= 1
    b = cy
    while b < h - 1 and row(b + 1) == row(cy):
        b += 1
    return (l, h - 1 - b, w - 1 - r, t)


def meta_border(path):
    meta = path + '.meta'
    if not os.path.exists(meta):
        return None
    m = re.search(r'spriteBorder: \{x: (\d+), y: (\d+), z: (\d+), w: (\d+)\}', open(meta, encoding='utf-8').read())
    return tuple(int(v) for v in m.groups()) if m else None


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('inputs', nargs='+')
    ap.add_argument('--out', help='목록 이미지 저장 경로(생략하면 수치만)')
    ap.add_argument('--cell', type=int, default=200)
    a = ap.parse_args()

    files = []
    for pattern in a.inputs:
        files += sorted(glob.glob(pattern, recursive=True)) if any(c in pattern for c in '*?[') else [pattern]

    cells = []
    for f in files:
        im = Image.open(f).convert('RGBA')
        print(f'{os.path.basename(f):45s} size={im.size} visible={im.getbbox()} '
              f'border~={estimate_border(im)} meta={meta_border(f)}')
        if a.out:
            limit = a.cell - 20
            scale = max(1, limit // max(im.size))
            thumb = im.resize((im.size[0] * scale, im.size[1] * scale), Image.NEAREST) if scale > 1 else im.copy()
            thumb.thumbnail((limit, limit))
            cells.append((os.path.basename(f)[:-4], thumb))

    if a.out and cells:
        cols = 6
        rows = (len(cells) + cols - 1) // cols
        cw, ch = a.cell, a.cell + 10
        sheet = Image.new('RGBA', (cw * cols, ch * rows), (255, 0, 255, 255))  # 마젠타 = 투명
        draw = ImageDraw.Draw(sheet)
        for i, (name, thumb) in enumerate(cells):
            x, y = (i % cols) * cw, (i // cols) * ch
            sheet.alpha_composite(thumb, (x + (cw - thumb.size[0]) // 2, y + 5))
            draw.text((x + 3, y + ch - 18), name[:30], fill=(0, 0, 0, 255))
        sheet.save(a.out)
        print('저장:', a.out)
    return 0


if __name__ == '__main__':
    sys.exit(main())
