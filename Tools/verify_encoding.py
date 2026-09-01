# -*- coding: utf-8 -*-
"""모든 `.cs` 가 UTF-8 로 디코딩되는지, 파괴된 문자가 없는지 검사한다.

만들어진 이유: 이 리포에는 두 종류의 인코딩 사고가 섞여 있었다.

1. **CP949 원시 바이트** — `TypeUIComponent.cs`, `UIRemoveDice.cs`.
   되살릴 수 있다. 바이트가 온전하므로 디코딩만 바꾸면 된다.
2. **이미 U+FFFD 로 파괴됨** — `Define.cs`, `Unity3dBuilder.cs`.
   CP949 로 저장된 파일을 UTF-8 로 읽어 다시 UTF-8 로 저장한 결과다.
   **한 번 U+FFFD 가 박히면 원래 글자는 파일에 남아 있지 않다.**

2번이 무서운 이유는 조용하기 때문이다. 컴파일도 콘솔도 아무 말이 없고
주석만 소리 없이 사라진다. 그래서 1번 상태를 발견하면 **고치기 전에**
잡아야 하고, 2번이 새로 생기면 즉시 알아야 한다.

`Assets/Plugins` 는 벤더링된 서드파티(UniTask 등)라 검사하지 않는다.

  python Tools/verify_encoding.py [프로젝트루트]
"""
import os
import sys

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

ROOT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else '.')
SCAN = os.path.join(ROOT, 'DuDuDuDU_Project/Assets')

# 벤더링된 서드파티. 우리가 고칠 대상이 아니다.
SKIP_DIRS = {'Plugins'}

REPLACEMENT = '�'


def main():
    broken, damaged, scanned = [], [], 0

    for base, dirs, files in os.walk(SCAN):
        dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
        for name in files:
            if not name.endswith('.cs'):
                continue

            path = os.path.join(base, name)
            rel = os.path.relpath(path, ROOT).replace(os.sep, '/')
            scanned += 1

            raw = open(path, 'rb').read()
            body = raw[3:] if raw.startswith(b'\xef\xbb\xbf') else raw

            try:
                text = body.decode('utf-8')
            except UnicodeDecodeError as e:
                # 어떤 인코딩이면 살아나는지까지 알려준다. 복구 가능 여부가 갈린다.
                hint = 'utf-8 아님'
                for enc in ('cp949', 'utf-16'):
                    try:
                        body.decode(enc)
                        hint = '%s 로는 디코딩됨 — 무손실 변환 가능' % enc
                        break
                    except (UnicodeDecodeError, UnicodeError):
                        pass
                broken.append('%s (%d번째 바이트에서 실패, %s)' % (rel, e.start, hint))
                continue

            if REPLACEMENT in text:
                lines = sorted({i + 1 for i, ln in enumerate(text.splitlines())
                                if REPLACEMENT in ln})
                damaged.append('%s (U+FFFD %d개, %s번째 줄)' % (
                    rel, text.count(REPLACEMENT),
                    ', '.join(str(n) for n in lines)))

    print('=== verify_encoding ===')
    print('스크립트 %d개 검사 (Plugins 제외)' % scanned)

    for b in broken:
        print('  ERROR UTF-8 아님: %s' % b)
    for d in damaged:
        print('  ERROR 글자가 이미 파괴됨: %s' % d)

    if broken or damaged:
        total = len(broken) + len(damaged)
        print('실패: %d건 — 컴파일도 콘솔도 조용하므로 여기서 잡지 않으면 드러나지 않는다.'
              % total)
        if damaged:
            print('      U+FFFD 는 되돌릴 수 없다. git 히스토리에서 마지막으로 성한')
            print('      리비전을 찾거나(있으면 무손실), 없으면 주석을 다시 쓰는 수밖에 없다.')
        sys.exit(1)

    print('통과')


if __name__ == '__main__':
    main()
