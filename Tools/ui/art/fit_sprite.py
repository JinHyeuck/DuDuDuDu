# -*- coding: utf-8 -*-
"""시안 PNG 에서 스프라이트의 배율·위치를 픽셀 대조로 찾는다(템플릿 매칭).

    python Tools/ui/art/fit_sprite.py <시안.png> <스프라이트.png> --center X Y [--scales 3,3.5,4] [--radius 40]

예)
    python Tools/ui/art/fit_sprite.py Art/Layout/Infinity_Layout2.png \\
        DuDuDuDU_Project/Assets/Resources/Art/InfinityMode/Dungeon_Fire_Bg.png --center 80 480 --scales 4

왜: 시안 주석의 배율이 실제와 다를 때가 있다(무한의 탑: 프레임 x3 → 실제 x3.5, 불꽃 x2 → x3).
    눈대중 대신 "스프라이트를 이 배율로 이 자리에 놓으면 시안과 평균 색 차이가 얼마인가" 를 잰다.

--center 는 시안에서 그 그림이 대략 있는 중심(시안 픽셀, 좌상단 원점). --radius 안을 1px 단위로 뒤진다.
출력은 오차가 작은 순 상위 5개 — (평균 색 오차, 배율, 중심 x, 중심 y). 오차 5 이하면 거의 정확하다.

큰 그림(성 프레임 등)은 --mask 로 비교 영역을 줄여야 빠르고 정확하다. UI 가 덮은 곳을 빼고
테두리 기둥 같은 곳만 남기는 식 — 예: --mask "0,0,160,1920;925,0,1080,1920"
"""
import argparse
import sys

import numpy as np
from PIL import Image

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('design')
    ap.add_argument('sprite')
    ap.add_argument('--center', nargs=2, type=float, required=True)
    ap.add_argument('--scales', default='1,2,3,4')
    ap.add_argument('--radius', type=int, default=40)
    ap.add_argument('--mask', help='비교할 사각형들 "x0,y0,x1,y1;..." (생략하면 전부)')
    a = ap.parse_args()

    design = np.asarray(Image.open(a.design).convert('RGB')).astype(np.int16)
    H, W, _ = design.shape
    mask = np.ones((H, W), bool)
    if a.mask:
        mask[:] = False
        for box in a.mask.split(';'):
            x0, y0, x1, y1 = (int(v) for v in box.split(','))
            mask[y0:y1, x0:x1] = True

    src = Image.open(a.sprite).convert('RGBA')
    cx, cy = a.center
    results = []
    for s in (float(v) for v in a.scales.split(',')):
        w, h = int(round(src.size[0] * s)), int(round(src.size[1] * s))
        im = np.asarray(src.resize((w, h), Image.NEAREST)).astype(np.int16)
        for oy in range(int(cy - h / 2 - a.radius), int(cy - h / 2 + a.radius) + 1):
            for ox in range(int(cx - w / 2 - a.radius), int(cx - w / 2 + a.radius) + 1):
                y0, y1, x0, x1 = max(0, oy), min(H, oy + h), max(0, ox), min(W, ox + w)
                if y1 <= y0 or x1 <= x0:
                    continue
                sub = im[y0 - oy:y1 - oy, x0 - ox:x1 - ox]
                m = (sub[..., 3] > 200) & mask[y0:y1, x0:x1]
                if m.sum() < 50:
                    continue
                err = np.abs(sub[..., :3] - design[y0:y1, x0:x1])[m].mean()
                results.append((round(float(err), 2), s, ox + w / 2, oy + h / 2))

    results.sort()
    for r in results[:5]:
        print('오차 %.2f  배율 %g  중심 (%.1f, %.1f)' % r)
    return 0 if results else 1


if __name__ == '__main__':
    sys.exit(main())
