# -*- coding: utf-8 -*-
"""열린 Unity 에디터에서 이 폴더의 C# 스크립트를 돌린다 (Unity CLI `eval_file`).

    python Tools/ui/unity/run.py <스크립트> [인자...] [--project <Unity 프로젝트 경로>]

예)
    python Tools/ui/unity/run.py render_prefab Assets/Prefab/Refactory/Tower/UITowerLoadoutDialog.prefab C:/tmp/loadout.png
    python Tools/ui/unity/run.py screen_check C:/tmp/shot.png
    python Tools/ui/unity/run.py click UITowerEntryButton
    python Tools/ui/unity/run.py back_key

`eval_file` 은 인자를 받지 못한다. 그래서 스크립트 맨 앞에 `var ARGS = new string[] {...};`
한 줄을 끼운 사본을 임시 폴더에 만들어 그것을 돌린다. 스크립트는 `ARGS` 를 읽는다.

스크립트가 이 폴더(Assets 밖)에 있는 이유: Assets 안에 두면 Unity 가 게임 코드로 컴파일한다.
Unity 의 `Temp/` 에 두지 말 것 — 에디터를 다시 켜면 비워진다.
"""
import argparse
import json
import pathlib
import subprocess
import sys
import tempfile

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

HERE = pathlib.Path(__file__).resolve().parent


def cs_string(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('script', help='이 폴더의 스크립트 이름(.cs 생략 가능)')
    ap.add_argument('args', nargs='*')
    ap.add_argument('--project', help='Unity 프로젝트 경로. 에디터가 하나만 떠 있으면 생략')
    ap.add_argument('--timeout', default='120')
    a = ap.parse_args()

    name = a.script if a.script.endswith('.cs') else a.script + '.cs'
    src = HERE / name
    if not src.exists():
        print('!! 스크립트가 없다:', src)
        return 2

    header = 'var ARGS = new string[] { ' + ', '.join(cs_string(x) for x in a.args) + ' };\n'
    body = src.read_text(encoding='utf-8')
    tmp = pathlib.Path(tempfile.gettempdir()) / ('oj_ui_' + name)
    tmp.write_text(header + body, encoding='utf-8')

    cmd = ['unity', 'command', 'eval_file', '--file', str(tmp), '--result-only', '--timeout', a.timeout]
    if a.project:
        cmd += ['--project-path', a.project]

    out = subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8', errors='replace')
    try:
        data = json.loads(out.stdout)
    except ValueError:
        print(out.stdout or out.stderr)
        return 3

    if data.get('result') is not None:
        print(data['result'])
        return 0

    # 컴파일 오류(diagnostics)나 실행 실패
    print(json.dumps(data, ensure_ascii=False, indent=1))
    return 1


if __name__ == '__main__':
    sys.exit(main())
