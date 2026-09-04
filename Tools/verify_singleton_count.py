# -*- coding: utf-8 -*-
"""B무리 매니저 14개의 `X.Instance` 호출부가 <b>늘지 않았는지</b> 센다. (8.3b)

만들어진 이유: 8.3b 는 호출부 272곳을 열 개 트랜치로 나눠 지운다. 그 작업의 실패는
"에러가 난다"가 아니라 <b>"줄어야 할 숫자가 안 줄었다"</b> 또는 <b>"지우는 동안 딴 데서
늘었다"</b>이다. 둘 다 컴파일도 테스트도 통과한다 — 헤드리스 러너의 <c>minTests</c> 가
"테스트 파일이 통째로 사라져도 남은 게 전부 통과라 초록불이 나온다"를 막는 것과
정확히 같은 자리다.

<b>왜 하한이 아니라 상한인가.</b> 이 검사는 늘어난 것만 실패로 본다. 줄어드는 것은
작업이 진행된 것이므로 막을 이유가 없고, 대신 <b>기준선을 낮추라고 알려 준다</b> —
낮추지 않으면 다음 사람이 되돌려도 검사가 통과해 버린다.

<b>주석은 세지 않는다.</b> 지금도 5건이 주석 안에 있다(설명문이라 남겨야 한다).
그것까지 세면 실제 호출부가 0 이 돼도 검사가 실패한다.

  python Tools/verify_singleton_count.py [리포루트]

종료 코드 0 = 통과, 1 = 늘었다.
"""
import io
import os
import re
import sys

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

# 8.3b 착수 시점(2026-08-31)의 실측값. 트랜치를 하나 끝낼 때마다 여기를 낮춘다.
#
# 갱신 이력:
#   272 (8.3b 착수 기준선)
#   143 (T2·T4·T5 — 루트 서비스 · 씬 매니저 상호 · DiceEffect)
#   136 (T3 — 씬 상주 비매니저)
#    28 (T6·T7 — 런타임 생성물 · 다이얼로그)
#     0 (T8·T9 — 정적 provider·개발도구, static Instance 삭제)  <- 8.3b 완료
BASELINE = {
    'GameManager': 0,
    'PlayerController': 0,
    'MonsterManager': 0,
    'MonsterSpawner': 0,
    'AttackContent': 0,
    'MergeSystem': 0,
    'UIBoard': 0,
    'UIDiceBoardUI': 0,
    'UIDiceSummonSystem': 0,
    'DiceTypeStarManager': 0,
    'ElementUpgradeManager': 0,
    'BulletPool': 0,
    'BulletEffectPool': 0,
    'DamageTextPool': 0,
}

PATTERN = re.compile(r'\b(' + '|'.join(BASELINE) + r')\.Instance\b')


def is_comment(line):
    s = line.lstrip()
    return s.startswith('//') or s.startswith('*') or s.startswith('/*')


def main(argv):
    root = os.path.abspath(argv[1] if len(argv) > 1 else '.')
    scripts = os.path.join(root, 'DuDuDuDU_Project', 'Assets', 'Scripts')
    print('=== verify_singleton_count ===')
    if not os.path.isdir(scripts):
        print('!! %s 가 없다. 리포 루트를 인자로 넘겨라.' % scripts)
        return 2

    counts = dict.fromkeys(BASELINE, 0)
    where = {k: [] for k in BASELINE}
    files = 0
    for d, _, fs in os.walk(scripts):
        for f in fs:
            if not f.endswith('.cs'):
                continue
            files += 1
            path = os.path.join(d, f)
            rel = os.path.relpath(path, root).replace(os.sep, '/')
            for i, line in enumerate(io.open(path, encoding='utf-8', errors='replace'), 1):
                if is_comment(line):
                    continue
                for name in PATTERN.findall(line):
                    counts[name] += 1
                    where[name].append('%s:%d' % (rel, i))

    total = sum(counts.values())
    base_total = sum(BASELINE.values())
    print('스크립트 %d개 검사 — 호출부 %d건 (기준선 %d건)' % (files, total, base_total))

    grew = [(k, counts[k], BASELINE[k]) for k in BASELINE if counts[k] > BASELINE[k]]
    shrank = [(k, counts[k], BASELINE[k]) for k in BASELINE if counts[k] < BASELINE[k]]

    if grew:
        print()
        print('!! 늘어난 매니저 %d개 — 8.3b 는 이 숫자를 줄이는 작업이다' % len(grew))
        for name, now, base in grew:
            print('  !! %-24s %d → %d  (+%d)' % (name, base, now, now - base))
            for loc in where[name][-6:]:
                print('       %s' % loc)
        return 1

    if shrank:
        print()
        print('  줄었다 — 아래 값으로 BASELINE 을 낮출 것 (낮추지 않으면 되돌려도 통과한다):')
        for name, now, base in shrank:
            print("    '%s': %d,   # was %d" % (name, now, base))
        print('  합계 %d → %d' % (base_total, total))

    print('통과')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
