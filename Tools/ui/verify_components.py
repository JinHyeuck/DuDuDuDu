"""프리팹의 UI 컴포넌트가 script guid 와 직렬화 필드가 서로 맞는지 본다.

만들어진 이유: GridLayoutGroup 을 손으로 써 넣으면서 필드는 Grid 것(m_CellSize/m_Constraint)을,
script guid 는 HorizontalLayoutGroup 것을 박은 적이 있다. Unity 는 guid 로 타입을 정하므로
Grid 필드를 통째로 무시하고 HorizontalLayoutGroup 의 기본값(ChildControlHeight=on)을 썼고,
그 결과 자식 셀의 높이가 0 이 되어 **레이캐스트에 안 걸려 클릭이 전부 죽었다.**
컴파일도 통과하고 콘솔도 깨끗해서 실행 전까지 아무도 모른다.

  python Tools/ui/verify_components.py [프로젝트루트]
"""
import io
import os
import re
import sys

ROOT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")

# guid: (표시명, 반드시 있어야 하는 필드, 있으면 안 되는 필드)
KNOWN = {
    "8a8695521f0d02e499659fee002a26c2": (
        "GridLayoutGroup", ["m_CellSize", "m_Constraint"], ["m_ChildControlHeight"]),
    "30649d3a9faa99c48a7b1166b86bf2a0": (
        "HorizontalLayoutGroup", ["m_ChildControlHeight"], ["m_CellSize"]),
    "59f8146938fff824cb5fd77236b75775": (
        "VerticalLayoutGroup", ["m_ChildControlHeight"], ["m_CellSize"]),
    "3245ec927659c4140ac4f8d17403cc18": (
        "ContentSizeFitter", ["m_HorizontalFit", "m_VerticalFit"], ["m_CellSize"]),
    "1aa08ab6e0800fa44ae55d278d1423e3": (
        "ScrollRect", ["m_Content", "m_Viewport"], []),
    "fe87c0e1cc204ed48ad3b37840f39efc": (
        "Image", ["m_RaycastTarget"], ["m_Interactable"]),
    "4e29b1a8efbd4b44bb3f3716e73f07ff": (
        "Button", ["m_Interactable"], ["m_CellSize"]),
    "3312d7739989d2b4e91e6319e9a96d76": ("RectMask2D", [], ["m_CellSize"]),
}

BLOCK = re.compile(
    r"m_Script: \{fileID: 11500000, guid: (\w+), type: 3\}\n"
    r"((?:  m_\w+:[^\n]*\n|    m_\w+:[^\n]*\n)*)")

# 프리팹 인스턴스의 스트립된 컴포넌트 스텁. 값은 PrefabInstance 의 m_Modifications 에 있고
# 이 블록 자체는 필드를 안 갖는 것이 정상이라 "필수 필드 없음" 규칙에서 뺀다.
STUB = re.compile(r"m_CorrespondingSourceObject: \{fileID: (?!0\})")


def check(path):
    text = io.open(path, encoding="utf-8", errors="replace").read()
    bad = []
    for m in BLOCK.finditer(text):
        spec = KNOWN.get(m.group(1))
        if not spec:
            continue
        name, required, forbidden = spec
        body = m.group(2)
        line = text.count("\n", 0, m.start()) + 1

        head = text[max(0, m.start() - 400):m.start()]
        is_stub = bool(STUB.search(head))

        missing = [] if is_stub else [f for f in required if f not in body]
        present = [f for f in forbidden if f in body]
        if missing:
            bad.append("%s:%d  %s 인데 %s 가 없다" % (path, line, name, ", ".join(missing)))
        if present:
            bad.append("%s:%d  %s 인데 다른 컴포넌트 필드 %s 가 있다 — guid 를 잘못 박았을 가능성"
                       % (path, line, name, ", ".join(present)))
    return bad


def main():
    failures = []
    scanned = 0
    for dp, _dn, fns in os.walk(os.path.join(ROOT, "DuDuDuDU_Project/Assets")):
        for fn in fns:
            if fn.endswith(".prefab") or fn.endswith(".unity"):
                scanned += 1
                failures.extend(check(os.path.join(dp, fn)))

    for f in failures:
        print("  FAIL " + f)
    print("  %d개 파일 검사, 문제 %d건" % (scanned, len(failures)))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
