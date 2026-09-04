# -*- coding: utf-8 -*-
"""씬·프리팹·SO 에 남은 <b>Missing script</b> 를 에디터를 열지 않고 센다.

만들어진 이유: 11단계에서 폴더를 재배치하고 asmdef 를 나눈다. 그때
"Missing 이 늘지 않았다"를 판정하려면 <b>손대기 전 숫자</b>가 있어야 한다.
리팩토링이 끝난 뒤에 세면 원래 있던 것과 방금 만든 것을 구분할 수 없다.

무엇을 Missing 으로 보는가:

* `m_Script: {fileID: 0}` — 참조 자체가 비어 있다. 에디터에서
  "The associated script can not be loaded" 로 뜨는 그것이다.
* guid 가 프로젝트 어디에도 없다 — 스크립트 파일이 지워졌거나 `.meta` 없이
  옮겨져 GUID 가 재발급된 경우다.
* guid 는 있는데 가리키는 대상이 `.cs` 도 어셈블리도 아니다 — 배선 사고.

<b>잡지 못하는 것</b>: guid 가 살아 있는 `.cs` 를 가리키지만 그 안의 클래스명이
파일명과 다르면 Unity 는 로드에 실패한다. YAML 만 봐서는 알 수 없으므로
이 도구는 통과시킨다. 그건 에디터에서만 드러난다.

`Library/PackageCache` 를 함께 읽는다. TMP·uGUI 컴포넌트의 guid 가 거기 있어서,
빼면 정상 참조가 전부 Missing 으로 오탐된다. 그래서 캐시가 없으면 <b>중단</b>한다.

  python Tools/verify_missing_scripts.py [프로젝트루트] [--baseline N]
"""
import io
import os
import re
import sys

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

# --baseline 은 값을 뒤에 하나 먹는다. 그 값까지 걸러 내지 않으면
# `--baseline 0` 의 0 이 위치 인자(리포 루트)로 새어 들어가 엉뚱한 폴더를 훑는다.
# 실제로 그렇게 물려서 "PackageCache 가 없다"는 잘못된 진단이 나왔다.
TAKES_VALUE = {'--baseline'}

BASELINE = None
argv = []
skip = False
for i, a in enumerate(sys.argv[1:], start=1):
    if skip:
        skip = False
        continue
    if a in TAKES_VALUE:
        if i + 1 < len(sys.argv):
            BASELINE = int(sys.argv[i + 1])
            skip = True
        continue
    if a.startswith('--'):
        continue
    argv.append(a)

ROOT = os.path.abspath(argv[0] if argv else '.')
PROJECT = os.path.join(ROOT, 'DuDuDuDU_Project')
SCAN = os.path.join(PROJECT, 'Assets')

# guid 를 공급하는 곳. PackageCache 가 빠지면 오탐이 폭발한다.
GUID_SOURCES = [
    os.path.join(PROJECT, 'Assets'),
    os.path.join(PROJECT, 'Packages'),
    os.path.join(PROJECT, 'Library', 'PackageCache'),
]

TARGETS = ('.prefab', '.unity', '.asset')
SCRIPT_HOSTS = ('.cs', '.dll', '.js', '.asmdef')

DOC = re.compile(r'(?m)^--- !u!(\d+) &(-?\d+).*$')
GUID_LINE = re.compile(r'^guid: ([0-9a-f]{32})\s*$', re.M)
M_SCRIPT = re.compile(r'm_Script: \{fileID: (-?\d+)(?:, guid: ([0-9a-f]{32}), type: (\d+))?\}')


def load_guids():
    """guid -> 짝이 되는 에셋 경로."""
    table = {}
    for source in GUID_SOURCES:
        if not os.path.isdir(source):
            continue
        for base, _dirs, files in os.walk(source):
            for name in files:
                if not name.endswith('.meta'):
                    continue
                path = os.path.join(base, name)
                try:
                    head = io.open(path, encoding='utf-8', errors='ignore').read(400)
                except OSError:
                    continue
                m = GUID_LINE.search(head)
                if m:
                    table[m.group(1)] = path[:-5]
    return table


def docs_of(text):
    out, cur = [], None
    for line in text.split('\n'):
        m = DOC.match(line)
        if m:
            cur = {'type': m.group(1), 'fid': m.group(2), 'lines': []}
            out.append(cur)
        elif cur is not None:
            cur['lines'].append(line)
    return [(d['type'], d['fid'], '\n'.join(d['lines'])) for d in out]


def owner_of(text, body):
    """MonoBehaviour 가 붙은 GameObject 이름. 어디를 봐야 하는지 알려준다."""
    go = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', body)
    if not go:
        return ''
    m = re.search(r'^--- !u!1 &%s$(.*?)(?=^--- !u!|\Z)' % re.escape(go.group(1)),
                  text, re.M | re.S)
    if not m:
        return ''
    n = re.search(r'^  m_Name: (.*)$', m.group(1), re.M)
    return n.group(1).strip() if n else ''


def main():
    if not os.path.isdir(os.path.join(PROJECT, 'Library', 'PackageCache')):
        print('=== verify_missing_scripts ===')
        print('  ERROR Library/PackageCache 가 없다. 패키지 스크립트의 guid 를 못 찾아')
        print('        정상 참조까지 Missing 으로 세게 된다. 에디터로 프로젝트를 한 번')
        print('        열어 패키지를 복원한 뒤 다시 실행할 것.')
        sys.exit(2)

    guids = load_guids()
    findings = []
    scanned = 0

    for base, _dirs, files in os.walk(SCAN):
        for name in files:
            if not name.endswith(TARGETS):
                continue

            path = os.path.join(base, name)
            rel = os.path.relpath(path, ROOT).replace(os.sep, '/')
            scanned += 1

            text = io.open(path, encoding='utf-8', errors='ignore').read()
            if not text.startswith('%YAML'):
                continue                       # 바이너리 직렬화 — 텍스트로 못 읽는다

            for dtype, fid, body in docs_of(text):
                if dtype != '114':
                    continue

                m = M_SCRIPT.search(body)
                if m is None:
                    findings.append((rel, fid, '', 'm_Script 줄이 없다'))
                    continue

                file_id, guid, _kind = m.group(1), m.group(2), m.group(3)
                if guid is None or file_id == '0':
                    findings.append((rel, fid, owner_of(text, body),
                                     '참조가 비어 있다 (fileID: 0)'))
                    continue

                target = guids.get(guid)
                if target is None:
                    findings.append((rel, fid, owner_of(text, body),
                                     'guid %s 가 프로젝트에 없다' % guid))
                elif not target.endswith(SCRIPT_HOSTS):
                    findings.append((rel, fid, owner_of(text, body),
                                     'guid %s 가 스크립트가 아닌 %s 를 가리킨다'
                                     % (guid, os.path.basename(target))))

    print('=== verify_missing_scripts ===')
    print('guid %d개 수집, 에셋 %d개 검사 (.prefab/.unity/.asset)' % (len(guids), scanned))

    by_file = {}
    for rel, fid, owner, why in findings:
        by_file.setdefault(rel, []).append((fid, owner, why))

    for rel in sorted(by_file):
        print('\n  %s — %d건' % (rel, len(by_file[rel])))
        for fid, owner, why in by_file[rel]:
            where = (' [%s]' % owner) if owner else ''
            print('    &%s%s  %s' % (fid, where, why))

    total = len(findings)
    print('\nMissing script 합계: %d건' % total)

    if BASELINE is None:
        print('기준선이 주어지지 않았다. 이 숫자를 MIGRATION_BASELINE.md 1.5 에 적어 둘 것.')
        return

    print('기준선: %d건' % BASELINE)
    if total > BASELINE:
        print('실패: 기준선 대비 %d건 늘었다 — 이번 작업이 참조를 끊었다.' % (total - BASELINE))
        sys.exit(1)
    if total < BASELINE:
        print('참고: 기준선보다 %d건 줄었다. 기준선을 낮춰 갱신할 것.' % (BASELINE - total))
    print('통과')


if __name__ == '__main__':
    main()
