# -*- coding: utf-8 -*-
"""프리팹·씬 YAML 두 판을 <b>fileID 를 무시하고</b> 비교한다.

만들어진 이유: 12단계 게이트가 "생성기를 연속 2회 실행해도 결과가 동일(멱등)"을
요구하는데, <b>`git diff` 로는 그 판정을 할 수 없다.</b>

Unity 의 `PrefabUtility.SaveAsPrefabAsset` 은 저장할 때마다 로컬 fileID 를 새로
발급한다. 그래서 아무것도 안 바뀐 프리팹을 다시 구워도 파일 <b>전체</b>가 바뀐 것으로
나온다 — 실제로 `UIIdleRewardDialog.prefab` 은 3,564줄이 통째로 갈렸는데 값이 달라진
필드는 <b>단 하나</b>였다. 그 하나를 3,564줄 속에서 눈으로 찾는 것은 불가능하다.

그래서 물어야 할 것을 바꾼다. "파일이 같은가"가 아니라 <b>"값이 같은가"</b>다.

방법: 문서를 `--- !u!<타입> &<앵커>` 로 쪼개고, 본문의 파일 내부 `fileID` 를 전부
`*` 로 눌러 정규화한 뒤, 정규화된 문서의 <b>다중집합</b>을 비교한다. 값이 하나라도
바뀌면 집합이 갈린다. `guid` 가 붙은 fileID(외부 에셋 참조)는 <b>누르지 않는다</b> —
그건 실제로 어느 에셋을 가리키는지의 정보라 지우면 검사가 무뎌진다.

<b>잡지 못하는 것</b>: 값이 전부 같은 채로 <b>부모-자식 배선만</b> 뒤바뀐 경우.
파일 내부 fileID 를 눌렀으므로 그래프 모양은 비교되지 않는다. 그 대비로 오브젝트
이름 집합과 타입별 개수를 따로 대조해 출력한다.

  python Tools/diff_prefab.py <이전.prefab> <이후.prefab>

종료 코드 0 = 값 차이 없음, 1 = 차이 있음.
직전 커밋본과 비교하려면:

  git show HEAD:<경로> > before.prefab
  python Tools/diff_prefab.py before.prefab <경로>
"""
import collections
import io
import re
import sys

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

DOC = re.compile(r'^--- !u!(\d+) &(-?\d+)', re.M)
# guid 가 붙은 것은 외부 참조라 살린다. 파일 내부 참조만 누른다.
FID = re.compile(r'\{fileID: (-?\d+)(, guid: [0-9a-f]+, type: \d+)?\}')
NAME = re.compile(r'^  m_Name: (.*)$', re.M)


def normalize(text):
    """(타입, 정규화된 본문) 목록으로 바꾼다."""
    parts = DOC.split(text)
    docs = []
    # parts = [머리말, 타입, 앵커, 본문, 타입, 앵커, 본문, ...]
    for i in range(1, len(parts) - 2, 3):
        typ = parts[i]
        body = parts[i + 2]
        body = FID.sub(lambda m: '{fileID: *%s}' % (m.group(2) or ''), body)
        docs.append((typ, body.strip()))
    return docs


def main(argv):
    if len(argv) != 3:
        print(__doc__)
        return 2

    a_path, b_path = argv[1], argv[2]
    a = io.open(a_path, encoding='utf-8', errors='replace').read()
    b = io.open(b_path, encoding='utf-8', errors='replace').read()

    na, nb = normalize(a), normalize(b)
    print('=== diff_prefab ===')
    print('문서 수      %-6d %-6d' % (len(na), len(nb)))
    print('이름 집합    %s' % ('동일' if sorted(NAME.findall(a)) == sorted(NAME.findall(b)) else '!! 다름'))
    same_types = (collections.Counter(t for t, _ in na) == collections.Counter(t for t, _ in nb))
    print('타입별 개수  %s' % ('동일' if same_types else '!! 다름'))

    only_a = collections.Counter(na) - collections.Counter(nb)
    only_b = collections.Counter(nb) - collections.Counter(na)

    if not only_a and not only_b:
        print()
        print('fileID 를 무시하면 완전히 같다. 값 변화 0건.')
        return 0

    print()
    print('!! 값이 다르다 — 이전에만 %d문서, 이후에만 %d문서'
          % (sum(only_a.values()), sum(only_b.values())))
    for label, bag in (('---- 이전에만', only_a), ('++++ 이후에만', only_b)):
        for (typ, body), n in list(bag.items())[:5]:
            print('%s (!u!%s) x%d' % (label, typ, n))
            for line in body.splitlines()[:20]:
                print('    ' + line)
    return 1


if __name__ == '__main__':
    sys.exit(main(sys.argv))
