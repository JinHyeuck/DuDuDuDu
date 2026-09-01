# -*- coding: utf-8 -*-
"""프리팹의 `*Button` 필드가 정말 Button 컴포넌트를 가리키는지 본다.

만들어진 이유: CurrencyHud 에 도감 버튼을 복제해 넣으면서 `_collectionButton` 을
같은 오브젝트의 <b>Image</b> 컴포넌트에 물려 놨다. Unity 는 타입이 안 맞는 참조를
조용히 null 로 두므로 필드는 비고, 버튼을 눌러도 아무 일도 일어나지 않는다 —
컴파일도 콘솔도 깨끗해서 실행해 봐야만 드러난다.

  python Tools/ui/verify_field_types.py [프로젝트루트]
"""
import io
import os
import re
import sys

sys.stdout.reconfigure(encoding='utf-8', errors='replace')

ROOT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else '.')

# 필드 이름 접미사 → 그 필드가 가리켜야 하는 컴포넌트의 script guid
EXPECTED = {
    'Button': ('4e29b1a8efbd4b44bb3f3716e73f07ff', 'Button'),
}

COMPONENT = re.compile(r'(?ms)^--- !u!114 &(-?\d+)\nMonoBehaviour:\n(.*?)(?=^--- |\Z)')
FIELD = re.compile(r'^\s+(_\w+): \{fileID: (-?\d+)\}$', re.M)


def main():
    errors = []
    checked = 0

    for root, _dirs, files in os.walk(os.path.join(ROOT, 'DuDuDuDU_Project/Assets')):
        for name in files:
            if not name.endswith('.prefab'):
                continue

            path = os.path.join(root, name)
            text = io.open(path, encoding='utf-8', errors='ignore').read()

            # 이 파일 안의 컴포넌트 fileID → script guid
            owner = {}
            for m in COMPONENT.finditer(text):
                g = re.search(r'm_Script: \{fileID: \d+, guid: ([a-f0-9]{32})', m.group(2))
                if g:
                    owner[m.group(1)] = g.group(1)

            for m in FIELD.finditer(text):
                field, target = m.group(1), m.group(2)
                if target == '0':
                    continue   # 미할당은 여기서 볼 문제가 아니다

                for suffix, (guid, label) in EXPECTED.items():
                    if not field.endswith(suffix):
                        continue

                    checked += 1
                    actual = owner.get(target)
                    if actual is None or actual == guid:
                        continue   # 이 파일 밖의 참조이거나 정상

                    line = text.count('\n', 0, m.start()) + 1
                    errors.append('%s:%d  %s 가 %s 가 아닌 다른 컴포넌트를 가리킨다'
                                  % (name, line, field, label))

    for e in errors:
        print('ERROR ' + e)

    print('\n%d개 필드 검사, 오류 %d건' % (checked, len(errors)))
    sys.exit(1 if errors else 0)


if __name__ == '__main__':
    main()
