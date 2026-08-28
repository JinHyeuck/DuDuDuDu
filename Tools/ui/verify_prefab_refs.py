# -*- coding: utf-8 -*-
"""프리팹이 <b>다른 프리팹의 컴포넌트</b>를 직접 참조하는 곳을 찾는다.

Companion_1~4 가 FollowerMoveAgent._position 으로 Companion_0.prefab 의 BasePosition 을
가리키고 있어 이동이 통째로 죽어 있었다(프리팹 복제 시 참조가 원본에 남은 것).
변형(variant)이면 base 참조가 정상이므로 제외하고, 순수 복제본만 본다.

이 종류는 컴파일도 콘솔도 조용해서 실행해 봐야만 드러난다.
"""
import io
import os
import re
import sys

sys.stdout.reconfigure(encoding='utf-8', errors='replace')

ROOT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else '.')
SCAN = os.path.join(ROOT, 'DuDuDuDU_Project/Assets')

def sibling(a, b):
    """Foo_1.prefab 과 Foo_0.prefab 처럼 끝 숫자만 다른 이름인가."""
    strip = re.compile(r'_?\d+\.prefab$')
    return a != b and strip.sub('', a) == strip.sub('', b)


REFERENCE = re.compile(r'\{fileID: -?\d+, guid: ([a-f0-9]{32}), type: 3\}')
SOURCE_PREFAB = re.compile(r'm_SourcePrefab: \{fileID: \d+, guid: ([a-f0-9]{32})')


def prefab_index():
    """프리팹 guid → 경로."""
    out = {}
    for root, _dirs, files in os.walk(SCAN):
        for f in files:
            if not f.endswith('.prefab.meta'):
                continue
            meta = os.path.join(root, f)
            text = io.open(meta, encoding='utf-8', errors='ignore').read()
            m = re.search(r'guid: ([a-f0-9]{32})', text)
            if m:
                out[m.group(1)] = meta[:-len('.meta')]
    return out


def main():
    prefabs = prefab_index()
    print('프리팹 %d개 검사' % len(prefabs))

    found = 0
    for guid, path in prefabs.items():
        text = io.open(path, encoding='utf-8', errors='ignore').read()

        # 이 프리팹이 인스턴스로 품고 있는 프리팹들 — 그쪽 참조는 정상이다.
        nested = set(SOURCE_PREFAB.findall(text))

        for m in REFERENCE.finditer(text):
            target = m.group(1)
            if target == guid or target in nested:
                continue
            if target not in prefabs:
                continue   # 스프라이트·스파인 등 다른 에셋 종류

            # 정상 참조: 다이얼로그가 셀 프리팹을, 전투체가 VFX 프리팹을 <b>템플릿</b>으로 든다.
            # 잡아야 하는 것은 "번호만 다른 복제본이 원본의 컴포넌트를 물고 있는" 경우다 —
            # Enemy_1 → Enemy_0, Companion_1 → Companion_0 처럼.
            if not sibling(os.path.basename(path), os.path.basename(prefabs[target])):
                continue

            line = text.count('\n', 0, m.start()) + 1
            name = os.path.basename(path)
            print('  %s:%d → %s 의 컴포넌트를 직접 참조' % (name, line, os.path.basename(prefabs[target])))
            found += 1

    print('\n의심 참조 %d건' % found)


if __name__ == '__main__':
    main()
