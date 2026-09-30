# -*- coding: utf-8 -*-
"""게임 캡처와 시안을 좌우로 붙인 한 장을 만든다.

    python Tools/ui/art/compare.py <캡처.png> <시안.png> <out.png> [--crop X0 Y0 X1 Y1] [--scale 0.5]

예)
    python Tools/ui/art/compare.py C:/tmp/shot.png Art/Layout/Infinity_Layout2.png C:/tmp/cmp.png
    python Tools/ui/art/compare.py C:/tmp/shot.png Art/Layout/Infinity_Layout2.png C:/tmp/cmp_top.png --crop 140 250 960 720 --scale 1

순서: 전체를 절반 크기로 한 번 본 뒤, 다른 곳만 --crop 원본 크기로 확대해서 본다.
      (전체를 원본 크기로 반복해서 여는 것이 가장 큰 시간 낭비였다.)
두 이미지는 같은 해상도여야 한다(1080x1920). 가운데 마젠타 띠가 경계다.
"""
import argparse
import sys

from PIL import Image

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('capture')
    ap.add_argument('design')
    ap.add_argument('out')
    ap.add_argument('--crop', nargs=4, type=int)
    ap.add_argument('--scale', type=float, default=0.5)
    a = ap.parse_args()

    left = Image.open(a.capture).convert('RGB')
    right = Image.open(a.design).convert('RGB')
    if left.size != right.size:
        print('!! 크기가 다르다:', left.size, right.size)
    if a.crop:
        left, right = left.crop(a.crop), right.crop(a.crop)

    gap = 10
    out = Image.new('RGB', (left.width + right.width + gap, max(left.height, right.height)), 'magenta')
    out.paste(left, (0, 0))
    out.paste(right, (left.width + gap, 0))
    if a.scale != 1:
        out = out.resize((int(out.width * a.scale), int(out.height * a.scale)))
    out.save(a.out)
    print('저장:', a.out, out.size)
    return 0


if __name__ == '__main__':
    sys.exit(main())
