"""프리팹 RectTransform 계층의 실제 사각형을 계산한다.

에디터를 못 여는 상태에서 "버튼이 화면 밖으로 나갔다 / 팝업 밖이다 / 서로 겹친다"를
눈대중 대신 숫자로 판정하기 위한 것이다. 앵커·피벗·오프셋을 Unity와 같은 식으로 푼다.

  부모 폭 W, 자식 anchorMin.x=a0, anchorMax.x=a1, sizeDelta.x=sw, anchoredPosition.x=px, pivot.x=pv
    앵커 구간 폭 = W*(a1-a0)
    실제 폭      = 앵커 구간 폭 + sw          (a0==a1 이면 앵커 구간 폭이 0이라 실제 폭 = sw)
    중심         = W*a0 + 앵커구간폭*pv + px  ... 를 부모 좌하단 기준으로 환산
"""
import io
import os
import re
import sys

ROOT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")


def read(rel):
    return io.open(os.path.join(ROOT, rel), encoding="utf-8").read()


def parse(text):
    docs = {}
    for m in re.finditer(r"(?m)^--- !u!(\d+) &(-?\d+)\n((?:(?!^--- ).*\n)*)", text):
        docs[m.group(2)] = (m.group(1), m.group(3))
    return docs


def vec(body, key):
    m = re.search(r"%s: \{x: ([-\d.eE]+), y: ([-\d.eE]+)" % key, body)
    return (float(m.group(1)), float(m.group(2))) if m else (0.0, 0.0)


class Node:
    __slots__ = ("rect_id", "name", "active", "x", "y", "w", "h", "children", "depth")


def build(text, canvas=(1080.0, 1920.0)):
    docs = parse(text)

    rects = {fid: body for fid, (t, body) in docs.items() if t == "224"}

    def name_of(rect_id):
        go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", rects[rect_id]).group(1)
        if go not in docs:
            return "?", True
        body = docs[go][1]
        n = re.search(r"m_Name: ([^\n]*)", body)
        a = re.search(r"m_IsActive: (\d)", body)
        return (n.group(1).strip() if n else "?"), (a.group(1) == "1" if a else True)

    root = None
    for fid, body in rects.items():
        if re.search(r"m_Father: \{fileID: 0\}", body):
            root = fid
            break

    out = []

    def walk(rect_id, px, py, pw, ph, depth):
        body = rects[rect_id]
        amin = vec(body, "m_AnchorMin")
        amax = vec(body, "m_AnchorMax")
        size = vec(body, "m_SizeDelta")
        pos = vec(body, "m_AnchoredPosition")
        pivot = vec(body, "m_Pivot")

        span_w = pw * (amax[0] - amin[0])
        span_h = ph * (amax[1] - amin[1])
        w = span_w + size[0]
        h = span_h + size[1]

        # 앵커 구간의 좌하단(부모 좌하단 기준) + 오프셋 - 피벗 보정
        left = px + pw * amin[0] + pos[0] - w * pivot[0] + span_w * pivot[0]
        bottom = py + ph * amin[1] + pos[1] - h * pivot[1] + span_h * pivot[1]

        n = Node()
        n.rect_id = rect_id
        n.name, n.active = name_of(rect_id)
        n.x, n.y, n.w, n.h = left, bottom, w, h
        n.depth = depth
        n.children = []
        out.append(n)

        kids = re.search(r"m_Children:\n((?:  - \{fileID: -?\d+\}\n)*)", body)
        for kid in re.findall(r"fileID: (-?\d+)", kids.group(1)) if kids else []:
            if kid in rects:
                n.children.append(walk(kid, left, bottom, w, h, depth + 1))
        return n

    walk(root, 0.0, 0.0, canvas[0], canvas[1], 0)
    return out


def overlaps(a, b):
    return not (a.x + a.w <= b.x or b.x + b.w <= a.x or a.y + a.h <= b.y or b.y + b.h <= a.y)


def report(rel, focus=None):
    print("=== %s ===" % os.path.basename(rel))
    nodes = build(read(rel))
    by_id = {n.rect_id: n for n in nodes}

    for n in nodes:
        if focus and not any(f in n.name for f in focus):
            continue
        mark = "" if n.active else "  (비활성)"
        print("   %s%-22s x=%7.1f y=%7.1f w=%6.1f h=%6.1f%s"
              % ("  " * n.depth, n.name, n.x, n.y, n.w, n.h, mark))

    print("   --- 부모 밖으로 나간 자식 ---")
    found = False
    for n in nodes:
        for c in n.children:
            if not c.active:
                continue
            if c.x < n.x - 0.5 or c.y < n.y - 0.5 or c.x + c.w > n.x + n.w + 0.5 or c.y + c.h > n.y + n.h + 0.5:
                print("     %s 가 부모 %s 밖 (자식 y %.0f~%.0f / 부모 y %.0f~%.0f)"
                      % (c.name, n.name, c.y, c.y + c.h, n.y, n.y + n.h))
                found = True
    if not found:
        print("     없음")

    print("   --- 형제 겹침 ---")
    found = False
    for n in nodes:
        kids = [c for c in n.children if c.active and c.w > 1 and c.h > 1]
        for i in range(len(kids)):
            for j in range(i + 1, len(kids)):
                a, b = kids[i], kids[j]
                # 배경/오버레이(부모 면적 80% 이상)는 겹쳐도 정상이라 뺀다.
                if a.w * a.h > n.w * n.h * 0.8 or b.w * b.h > n.w * n.h * 0.8:
                    continue
                if overlaps(a, b):
                    print("     %s ↔ %s" % (a.name, b.name))
                    found = True
    if not found:
        print("     없음")
    print()


if __name__ == "__main__":
    targets = sys.argv[2:] if len(sys.argv) > 2 else []
    for t in targets:
        report(t)
