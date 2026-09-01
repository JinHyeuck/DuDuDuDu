# -*- coding: utf-8 -*-
"""`Assets/Scripts` 아래 모든 `.cs` 의 네임스페이스가 폴더와 일치하는지 검사한다.

만들어진 이유: 11.4 에서 172개 파일을 평평한 `namespace OJ` 에서 폴더에 맞춘 21개
계층으로 쪼갰다. 그런데 <b>이 규약은 컴파일러가 강제하지 않는다.</b> 새 파일이 엉뚱한
네임스페이스로 들어와도 잘 컴파일되고, 아무도 모르는 사이에 규약이 무너진다.
문서에만 적힌 규약은 <b>지켜지지 않는 규약</b>이다.

검사하는 것 두 가지:

<b>1. 폴더 ↔ 네임스페이스 일치.</b> `Scripts/Equipment/Foo.cs` → `namespace OJ.Equipment`.
   예외 두 건은 아래 EXCEPTIONS 에 이유와 함께 박아 뒀다.

<b>2. 폴더 이름이 타입 이름과 겹치지 않는가.</b> 이게 진짜 함정이다. `Scripts/Bullet`
   폴더를 만들면 `namespace OJ.Bullet` 이 되는데, 그러면 다른 `OJ.*` 안에서
   <b>`Bullet` 이라는 이름이 클래스가 아니라 네임스페이스로 먼저 잡힌다</b>
   (CS0118: 'Bullet' is a namespace but is used like a type). 같은 이유로
   `namespace OJ.Editor` 는 `UnityEditor.Editor` 를 가린다.

   컴파일러가 잡아 주긴 하지만 <b>그 타입을 실제로 쓰는 파일이 생겨야</b> 터진다.
   폴더를 만든 날이 아니라 몇 주 뒤 엉뚱한 파일에서 터지고, 그때는 폴더를 되돌리기가
   훨씬 비싸다. 여기서 먼저 잡는다.

`Assets/Plugins`(벤더링 서드파티)와 `Assets/Tests`(별도 어셈블리 규약)는 검사하지 않는다.

  python Tools/verify_namespaces.py [리포루트]

인자는 <b>리포 루트</b>다 (`verify_encoding.py` · `verify_missing_scripts.py` 와 같다).
생략하면 현재 폴더를 쓴다. 종료 코드 0 = 통과, 1 = 위반 있음.
"""
import collections
import io
import os
import re
import sys

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

ROOT_NS = 'OJ'

# 폴더명 -> 네임스페이스 조각. 이유는 위 독스트링과 AGENTS.md 참조.
FOLDER_TO_NS = {
    'Editor': 'EditorTools',
}

# 네임스페이스가 없어도 되는 파일. 이유를 함께 적는다.
NO_NAMESPACE_OK = {
    'AssemblyInfo.cs': '어셈블리 특성만 담는다. 네임스페이스가 없는 것이 맞다',
}

NS_RE = re.compile(r'^namespace\s+([\w.]+)\s*$', re.M)
TYPE_RE = re.compile(r'^(?P<indent> *).*?\b(?:class|struct|enum|interface)\s+(?P<name>[A-Za-z_]\w*)')


def expected_ns(rel_dir):
    """`Scripts` 기준 상대 폴더 경로 -> 기대 네임스페이스."""
    if rel_dir in ('.', ''):
        return ROOT_NS
    parts = [FOLDER_TO_NS.get(p, p) for p in rel_dir.replace('\\', '/').split('/')]
    return ROOT_NS + '.' + '.'.join(parts)


def main(argv):
    root = os.path.abspath(argv[1] if len(argv) > 1 else '.')
    scripts = os.path.join(root, 'DuDuDuDU_Project', 'Assets', 'Scripts')
    print('=== verify_namespaces ===')
    if not os.path.isdir(scripts):
        print('!! %s 가 없다. 리포 루트를 인자로 넘겨라.' % scripts)
        return 2

    files = []
    for d, dirnames, fs in os.walk(scripts):
        for f in fs:
            if f.endswith('.cs'):
                files.append(os.path.join(d, f))
    files.sort()

    problems = []
    declared = {}          # 타입 이름 -> 첫 정의 파일
    folders = set()

    for path in files:
        rel_dir = os.path.relpath(os.path.dirname(path), scripts)
        folders.update(p for p in rel_dir.replace('\\', '/').split('/') if p != '.')
        want = expected_ns(rel_dir)
        text = io.open(path, encoding='utf-8', errors='replace').read()
        shown = os.path.relpath(path, root).replace(os.sep, '/')

        found = NS_RE.findall(text)
        if not found:
            why = NO_NAMESPACE_OK.get(os.path.basename(path))
            if not why:
                problems.append('%s\n     네임스페이스가 없다. `namespace %s` 여야 한다.' % (shown, want))
            continue

        for ns in set(found):
            if ns != want:
                problems.append('%s\n     `namespace %s` 인데 폴더상 `namespace %s` 여야 한다.'
                                % (shown, ns, want))

        for line in text.splitlines():
            m = TYPE_RE.match(line)
            if m and len(m.group('indent')) <= 4:
                declared.setdefault(m.group('name'), shown)

    # 폴더 이름이 타입 이름과 겹치면 그 네임스페이스가 타입을 가린다 (CS0118).
    for folder in sorted(folders):
        ns_part = FOLDER_TO_NS.get(folder, folder)
        if ns_part in declared:
            problems.append('폴더 `%s` (→ `%s.%s`) 가 타입 `%s` 를 가린다 — %s\n'
                            '     그 이름은 다른 OJ.* 안에서 타입이 아니라 네임스페이스로 먼저 잡힌다 (CS0118).\n'
                            '     폴더를 합치거나 이름을 바꿔라. AGENTS.md 의 "네임스페이스" 절 참조.'
                            % (folder, ROOT_NS, ns_part, ns_part, declared[ns_part]))

    print('스크립트 %d개 검사 (Plugins/Tests 제외), 폴더 %d개' % (len(files), len(folders)))
    if not problems:
        by_ns = collections.Counter(expected_ns(os.path.relpath(os.path.dirname(p), scripts))
                                    for p in files)
        print('통과 — 네임스페이스 %d개' % len(by_ns))
        return 0

    print()
    print('!! 위반 %d건' % len(problems))
    for p in problems:
        print('  !! ' + p)
    return 1


if __name__ == '__main__':
    sys.exit(main(sys.argv))
