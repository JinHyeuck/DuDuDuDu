# -*- coding: utf-8 -*-
"""시안 PNG 의 한 행/열을 훑어 어두운 외곽선(또는 지정 색)이 이어지는 구간을 찍는다.

    python Tools/ui/art/scan_edges.py <시안.png> [--row Y ...] [--col X ...] [--color R,G,B] [--tol 20]

예)
    python Tools/ui/art/scan_edges.py Art/Layout/Infinity_Layout2.png --row 740 --col 600
    python Tools/ui/art/scan_edges.py Art/Layout/Infinity_Layout_Skill2.png --row 795 --color 108,98,141

왜: 픽셀 아트 패널은 검은 외곽선(x4 면 폭 8px 안팎)으로 둘러싸여 있다. 한 줄만 훑으면
    패널·버튼의 <b>보이는</b> 경계가 숫자로 나온다. 이미지를 다시 열 필요가 없다.

출력: (시작, 끝) 목록. 폭이 7~12px 인 구간이 보통 외곽선이다(x4 기준).
--color 를 주면 그 색(±tol)인 구간을 찾는다 — 외곽선이 검정이 아닌 그림(격자 칸 등)에 쓴다.
"""
import argparse
import sys

import numpy as np
from PIL import Image

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')


def runs(line, color, tol, dark):
    hit = (np.abs(line - np.array(color)).sum(axis=1) < tol) if color else (line.max(axis=1) < dark)
    out, start = [], None
    for i, v in enumerate(hit):
        if v and start is None:
            start = i
        elif not v and start is not None:
            out.append((start, i - 1))
            start = None
    if start is not None:
        out.append((start, len(hit) - 1))
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('design')
    ap.add_argument('--row', type=int, nargs='*', default=[])
    ap.add_argument('--col', type=int, nargs='*', default=[])
    ap.add_argument('--color', help='R,G,B — 생략하면 어두운 픽셀(max<dark)')
    ap.add_argument('--tol', type=int, default=20)
    ap.add_argument('--dark', type=int, default=40)
    ap.add_argument('--maxwidth', type=int, default=0, help='이 폭보다 긴 구간은 뺀다(0=모두)')
    a = ap.parse_args()

    img = np.asarray(Image.open(a.design).convert('RGB')).astype(int)
    color = [int(v) for v in a.color.split(',')] if a.color else None

    def show(label, line):
        r = runs(line, color, a.tol, a.dark)
        if a.maxwidth:
            r = [x for x in r if x[1] - x[0] + 1 <= a.maxwidth]
        print(label, r)

    for y in a.row:
        show('row y=%d:' % y, img[y])
    for x in a.col:
        show('col x=%d:' % x, img[:, x])
    return 0


if __name__ == '__main__':
    sys.exit(main())
