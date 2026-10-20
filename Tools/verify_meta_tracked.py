# -*- coding: utf-8 -*-
"""Assets 아래 <b>추적되는 에셋인데 .meta 가 추적되지 않는</b> 파일·폴더를 찾는다.

만들어진 이유: PR #40 이 .cs 4개를 .meta 없이 들여왔다. 작성한 체크아웃에서는 Unity 가
만든 .meta 가 untracked 로 남아 있어 아무 문제가 없어 보이고, <b>새 체크아웃을 열 때마다
GUID 가 새로 발급된다.</b> 씬·프리팹이 그 GUID 를 참조하고 있었다면 체크아웃마다 참조가
끊긴다 — 컴파일도 콘솔도 조용하다. 2026-10-20 새 워크트리에서 .meta 4개가 생기는 것을
보고서야 드러났다.

git 인덱스만 본다. 디스크는 보지 않는다 — 작성자 디스크에는 .meta 가 있으니
디스크를 보면 바로 그 사고를 놓친다. Unity 가 필요 없어 CI 에서도 돈다.

반대 방향(대상 없는 .meta)은 경고만 한다. 빈 폴더의 .meta 가 그렇다 — git 은 빈 폴더를
만들지 않으므로 새 체크아웃에서 Unity 가 그 .meta 를 지워 diff 가 생긴다. GUID 를
잃는 사고는 아니라서 실패로 보지 않는다.

  python Tools/verify_meta_tracked.py [리포루트]

종료 코드 0 = 통과, 1 = .meta 누락, 2 = 실행 오류.
"""
import os
import posixpath
import subprocess
import sys

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

ASSETS = 'DuDuDuDU_Project/Assets'


def tracked(root):
    out = subprocess.check_output(
        ['git', '-c', 'core.quotepath=off', 'ls-files', '-z', '--', ASSETS], cwd=root)
    return set(p for p in out.decode('utf-8').split('\0') if p)


def main(argv):
    root = os.path.abspath(argv[1] if len(argv) > 1 else '.')
    print('=== verify_meta_tracked ===')
    try:
        files = tracked(root)
    except (OSError, subprocess.CalledProcessError) as e:
        print('!! git ls-files 실패: %s' % e)
        return 2
    if not files:
        print('!! %s 아래 추적 파일이 없다. 리포 루트를 인자로 넘겨라.' % ASSETS)
        return 2

    # 폴더도 .meta 를 갖는다. Assets 자신은 예외다.
    dirs = set()
    for f in files:
        d = posixpath.dirname(f)
        while d.startswith(ASSETS + '/'):
            dirs.add(d)
            d = posixpath.dirname(d)

    assets = sorted(p for p in files | dirs if not p.endswith('.meta'))
    missing = [p for p in assets if p + '.meta' not in files]
    orphans = sorted(p for p in files
                     if p.endswith('.meta') and p[:-5] not in files and p[:-5] not in dirs)

    print('에셋 %d개 (폴더 %d개 포함) 검사' % (len(assets), len(dirs)))

    if orphans:
        print()
        print('  경고: 대상 없는 .meta %d개 — 새 체크아웃에서 Unity 가 지운다' % len(orphans))
        for p in orphans:
            print('    %s' % p)

    if missing:
        print()
        print('!! .meta 가 추적되지 않는 에셋 %d개 — 새 체크아웃마다 GUID 가 새로 발급된다' % len(missing))
        for p in missing:
            print('  !! %s' % p)
        print('  작성한 체크아웃에 Unity 가 만든 .meta 가 있으면 그 파일을 그대로 커밋할 것.')
        print('  새로 만들면 그 체크아웃의 씬·프리팹 참조와 GUID 가 어긋난다.')
        return 1

    print('통과')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
