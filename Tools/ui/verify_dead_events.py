# -*- coding: utf-8 -*-
"""프리팹 버튼에 남은 <b>옛 UI 시절 persistent onClick</b>을 찾는다.

만들어진 이유: 옛 _P Popup 을 새 다이얼로그로 변환할 때 Missing 스크립트는 지웠지만
UnityEvent 의 persistent call 은 프리팹에 그대로 남았다. 대상이 사라진 호출(target=0)은
조용히 무시되지만, <b>같은 프리팹 안의 오브젝트를 가리키는 호출은 살아서 실행된다.</b>

실제로 코스튬 다이얼로그의 부위 탭 7개가 옛 배선을 물고 있어, 탭을 누르면
다이얼로그 루트를 SetActive(false) 해서 창이 사라졌다. 코드는 정상이고 컴파일도
콘솔도 깨끗해서 눌러 보기 전에는 드러나지 않는다.

새 다이얼로그의 버튼 동작은 전부 런타임에 코드가 붙이므로 persistent call 은 남길 이유가 없다.

  python Tools/ui/verify_dead_events.py [프로젝트루트]
"""
import io
import os
import re
import sys

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

ROOT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else '.')
SCAN = os.path.join(ROOT, 'DuDuDuDU_Project/Assets')

BUTTON_GUID = '4e29b1a8efbd4b44bb3f3716e73f07ff'
TOGGLE_GUID = '9085046f02f69544eb97fd06b6048fe2'

# 변환 <b>원본</b>인 옛 팝업들. 새 프리팹을 만들어 낸 뒤로는 아무도 세우지 않으므로
# 배선이 남아 있어도 무해하다. _P 정리 때 함께 사라질 자리다.
LEGACY = {
}

DOC = re.compile(r'(?m)^--- !u!(\d+) &(-?\d+).*$')


def docs_of(text):
    out, cur = {}, None
    for line in text.split('\n'):
        m = DOC.match(line)
        if m:
            cur = m.group(2)
            out[cur] = {'type': m.group(1), 'lines': []}
        elif cur:
            out[cur]['lines'].append(line)
    return {k: (v['type'], '\n'.join(v['lines'])) for k, v in out.items()}


def name_of(docs, fid):
    if fid not in docs:
        return None
    m = re.search(r'^  m_Name: (.*)$', docs[fid][1], re.M)
    return m.group(1).strip() if m else ''


def owner_name(docs, comp_body):
    go = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', comp_body)
    return name_of(docs, go.group(1)) if go else '?'


def main():
    errors, notes = [], []
    scanned = 0

    for base, _dirs, files in os.walk(SCAN):
        for name in files:
            if not name.endswith('.prefab'):
                continue

            path = os.path.join(base, name)
            rel = os.path.relpath(path, ROOT).replace(os.sep, '/')
            scanned += 1

            docs = docs_of(io.open(path, encoding='utf-8', errors='ignore').read())
            for fid, (dtype, body) in docs.items():
                if dtype != '114':
                    continue
                guid = re.search(r'm_Script: \{fileID: \d+, guid: ([0-9a-f]+)', body)
                if not guid or guid.group(1) not in (BUTTON_GUID, TOGGLE_GUID):
                    continue

                block = re.search(r'^      m_Calls:\n(.+)$', body, re.M | re.S)
                if not block or not block.group(1).strip():
                    continue

                for target, method in re.findall(
                        r'- m_Target: \{fileID: (-?\d+)\}(?:.|\n)*?m_MethodName: (\w+)', block.group(1)):
                    # 대상이 없는(=이미 사라진) 호출은 실행되지 않는다. 지저분할 뿐 무해하다.
                    if target == '0' or target not in docs:
                        continue

                    # <b>이 도구는 메서드가 실제로 있는지는 모른다.</b> 프리팹 YAML 만 읽으므로
                    # 대상 컴포넌트의 타입에 그 이름의 public 메서드가 있는지 확인할 수 없다.
                    # 그래서 결과 문구는 "실행된다"가 아니라 "확인이 필요하다"여야 한다 —
                    # 실제로 UIDice.prefab 이 그 차이에 걸렸다. 대상은 살아 있었지만
                    # UIDice 에 OnClick 이 아예 없어서 리스너 0개로 아무 일도 안 하고 있었고,
                    # 문서에는 "실제로 실행된다"고 단정돼 있어 없는 버그를 쫓게 만들었다.
                    line = '%s 의 %s → %s(%s)' % (
                        rel, owner_name(docs, body), method, name_of(docs, target))
                    (notes if rel in LEGACY else errors).append(line)

    print('=== verify_dead_events ===')
    print('프리팹 %d개 검사' % scanned)

    for n in notes:
        print('  note  변환 원본이라 무해: %s' % n)

    if errors:
        for e in errors:
            print('  ERROR 대상이 살아 있는 옛 배선: %s' % e)
        print('실패: %d건 — 눌렀을 때 <b>의도하지 않은 동작이 함께 실행될 수 있다.</b>' % len(errors))
        print('       이 도구는 프리팹 YAML 만 읽어 <b>그 메서드가 실제로 있는지는 모른다.</b>')
        print('       대상 클래스에 그 이름의 public 메서드가 있는지 확인할 것 —')
        print('       없으면 리스너 0개로 아무 일도 안 하지만, 그래도 지우는 것이 맞다')
        print('       (인스펙터에 Missing 으로 뜨고, 나중에 같은 이름을 만들면 그때 살아난다).')
        sys.exit(1)

    print('통과')


if __name__ == '__main__':
    main()
