# -*- coding: utf-8 -*-
"""TMP 폰트 아틀라스가 문자셋 파일을 <b>전부</b> 담고 있는지 검사한다.

만들어진 이유: 문자셋을 1,206자에서 2,915자로 늘렸는데 아틀라스가
2048x1024 그대로였다. TMP 는 텍스처가 차면 **남은 글자를 조용히 버린다** —
에러도 경고도 런타임 로그도 없다. 결과는 1,223자만 구워졌고, 유니코드 순서상
뒤쪽인 '소'·'스'·'테' 가 통째로 빠져서 "소탕"·"스테이지" 가 화면에서 깨졌다.

이 고장이 고약한 이유는 셋이다.

1. **조용하다.** 굽는 사람이 Font Asset Creator 의 경고를 놓치면 끝이다.
2. **부분적이다.** 흔한 글자가 남아 있어서 대충 보면 멀쩡해 보인다.
3. **늦게 드러난다.** 그 글자를 쓰는 화면을 열어야 보이고, 그때는 이미
   커밋된 뒤다.

정본은 문자셋 파일이고 아틀라스는 그 결과물이다. 둘이 어긋나면 아틀라스를
다시 구워야 한다 — 이 스크립트는 "얼마나 어긋났는지"와 "무엇이 빠졌는지"를
말한다.

  python Tools/verify_font_atlas.py [프로젝트루트]
"""
import os
import re
import sys

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

ROOT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else '.')
ASSETS = os.path.join(ROOT, 'DuDuDuDU_Project/Assets')

# (문자셋 파일, SDF 에셋). 짝이 늘면 여기 한 줄 추가한다.
PAIRS = [
    ('BMHANNAProOTF/BMHANNAProOTF_CharacterSet.txt',
     'BMHANNAProOTF/BMHANNAProOTF SDF.asset'),
]

# 아틀라스가 Dynamic 이면 런타임에 글자를 채우므로 검사 대상이 아니다.
DYNAMIC = '1'


def read_charset(path):
    with open(path, 'rb') as f:
        text = f.read().decode('utf-8')
    # 개행은 구분자일 뿐 글자가 아니다.
    return set(ord(c) for c in text if c not in '\r\n')


def read_atlas(path):
    with open(path, 'rb') as f:
        text = f.read().decode('utf-8', errors='replace')

    mode = re.search(r'm_AtlasPopulationMode: (\d+)', text)
    size = re.search(r'm_AtlasWidth: (\d+)[\s\S]*?m_AtlasHeight: (\d+)', text)
    return (
        set(int(m) for m in re.findall(r'm_Unicode: (\d+)', text)),
        mode.group(1) if mode else '?',
        (size.group(1), size.group(2)) if size else ('?', '?'),
    )


def main():
    failures = 0

    for charset_rel, atlas_rel in PAIRS:
        charset_path = os.path.join(ASSETS, charset_rel)
        atlas_path = os.path.join(ASSETS, atlas_rel)
        name = os.path.basename(atlas_rel)

        if not os.path.isfile(charset_path) or not os.path.isfile(atlas_path):
            print('  건너뜀 — 파일 없음: %s' % name)
            continue

        wanted = read_charset(charset_path)
        have, mode, (w, h) = read_atlas(atlas_path)

        if mode == DYNAMIC:
            print('  %s — Dynamic 이라 검사하지 않는다 (런타임에 채운다)' % name)
            continue

        missing = sorted(wanted - have)
        print('  %s  %sx%s  문자셋 %d자 / 아틀라스 %d자'
              % (name, w, h, len(wanted), len(have)))

        if not missing:
            continue

        failures += 1
        shown = ''.join(chr(cp) for cp in missing[:40])
        print('    빠진 글자 %d개: %s%s' % (len(missing), shown, ' …' if len(missing) > 40 else ''))
        print('    아틀라스가 문자셋을 다 못 담았다. Font Asset Creator 에서 '
              'Atlas Resolution 을 키우고 다시 구울 것 (기존 에셋에 덮어쓰기).')

    print()
    if failures:
        print('실패: 폰트 %d개가 문자셋과 어긋났다 — 그 글자는 화면에서 빈칸이 된다.' % failures)
        return 1

    print('통과')
    return 0


if __name__ == '__main__':
    print('=== verify_font_atlas ===')
    sys.exit(main())
