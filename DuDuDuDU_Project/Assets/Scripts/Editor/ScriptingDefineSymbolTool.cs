using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using OJ.Utils;

namespace OJ.EditorTools
{
    /// <summary>
    /// 스크립팅 심볼을 플랫폼 전체에 넣고 뺀다. (MIGRATION_BASELINE 8.1)
    ///
    /// <b>왜 도구로 만드나.</b> 심볼은 <c>ProjectSettings/ProjectSettings.asset</c> 에 플랫폼별로
    /// 따로 저장된다. 그 파일을 직접 고치면 두 가지가 문제가 된다 —
    /// 에디터가 켜져 있으면 자기가 들고 있던 값으로 <b>덮어써서 수정이 사라지고</b>,
    /// 한 플랫폼에만 넣으면 <b>에디터에서는 되는데 안드로이드 빌드에서만 코드가 사라진다.</b>
    /// 지금 이 프로젝트가 딱 그 상태다: <c>DEV_DEFINE</c> 이 Android 에만 들어 있다.
    ///
    /// 모듈이 설치되지 않은 플랫폼은 건너뛴다. 설치 안 된 플랫폼에 넣으려 하면 예외가 나는데,
    /// 그것 때문에 나머지 플랫폼 설정까지 중단되면 절반만 적용된 상태가 된다.
    /// </summary>
    public static class ScriptingDefineSymbolTool
    {
        /// <summary>VContainer 의 UniTask 연동 스위치. (8.1)</summary>
        public const string VContainerUniTask = "VCONTAINER_UNITASK_INTEGRATION";

        /// <summary>
        /// 이 프로젝트가 실제로 빌드하는 대상.
        ///
        /// "전 플랫폼"이라고 해서 <c>NamedBuildTarget</c> 을 전부 도는 것은 오히려 위험하다 —
        /// 쓰지도 않는 콘솔 플랫폼 설정까지 건드려 diff 를 키우고, 모듈이 없으면 예외가 난다.
        /// 실제로 쓰는 것만 적고, 늘어나면 여기 추가한다.
        /// </summary>
        private static IEnumerable<NamedBuildTarget> Targets
        {
            get
            {
                yield return NamedBuildTarget.Standalone; // 에디터가 따르는 기본 집합
                yield return NamedBuildTarget.Android;    // 실제 출시 대상
                yield return NamedBuildTarget.iOS;
            }
        }

        [MenuItem("OJ/개발/스크립팅 심볼/VCONTAINER_UNITASK_INTEGRATION 켜기")]
        private static void AddVContainerUniTask() => Add(VContainerUniTask);

        [MenuItem("OJ/개발/스크립팅 심볼/VCONTAINER_UNITASK_INTEGRATION 끄기")]
        private static void RemoveVContainerUniTask() => Remove(VContainerUniTask);

        [MenuItem("OJ/개발/스크립팅 심볼/현재 심볼 보기")]
        private static void DumpCurrent()
        {
            var lines = new List<string> { "[심볼] 플랫폼별 스크립팅 정의 심볼" };

            foreach (NamedBuildTarget target in Targets)
            {
                string[] defines;
                if (!TryGet(target, out defines, out string error))
                {
                    lines.Add(string.Format("  {0,-12} (건너뜀: {1})", target.TargetName, error));
                    continue;
                }

                lines.Add(string.Format("  {0,-12} {1}", target.TargetName,
                    defines.Length == 0 ? "(없음)" : string.Join(";", defines)));
            }

            Debug.Log(string.Join(Environment.NewLine, lines));
        }

        public static void Add(string symbol) => Apply(symbol, add: true);

        public static void Remove(string symbol) => Apply(symbol, add: false);

        private static void Apply(string symbol, bool add)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("심볼이 비어 있다.", nameof(symbol));

            symbol = symbol.Trim();
            var report = new List<string> { (add ? "[심볼] 추가: " : "[심볼] 제거: ") + symbol };
            bool anyChanged = false;

            foreach (NamedBuildTarget target in Targets)
            {
                string[] defines;
                if (!TryGet(target, out defines, out string error))
                {
                    report.Add(string.Format("  {0,-12} 건너뜀 — {1}", target.TargetName, error));
                    continue;
                }

                // 순서를 지킨다. 심볼 순서가 컴파일에 영향을 주지는 않지만, 매번 뒤섞이면
                // ProjectSettings.asset diff 가 실제 변경과 잡음을 구별할 수 없게 된다.
                var updated = defines.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
                bool has = updated.Contains(symbol);

                if (add && !has)
                    updated.Add(symbol);
                else if (!add && has)
                    updated.RemoveAll(d => d == symbol);
                else
                {
                    report.Add(string.Format("  {0,-12} 이미 그 상태다", target.TargetName));
                    continue;
                }

                try
                {
                    PlayerSettings.SetScriptingDefineSymbols(target, updated.ToArray());
                }
                catch (Exception ex)
                {
                    report.Add(string.Format("  {0,-12} 실패 — {1}", target.TargetName, ex.Message));
                    continue;
                }

                anyChanged = true;
                report.Add(string.Format("  {0,-12} -> {1}", target.TargetName, string.Join(";", updated)));
            }

            if (anyChanged)
            {
                // 껐다 켜도 남도록 즉시 디스크에 쓴다. 이걸 빼면 에디터가 비정상 종료했을 때
                // 심볼만 사라지고 코드는 그 심볼이 있다고 가정한 채로 남는다.
                AssetDatabase.SaveAssets();
                report.Add("  (ProjectSettings 저장 · 스크립트가 다시 컴파일된다)");
            }

            Debug.Log(string.Join(Environment.NewLine, report));
        }

        private static bool TryGet(NamedBuildTarget target, out string[] defines, out string error)
        {
            try
            {
                PlayerSettings.GetScriptingDefineSymbols(target, out defines);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                // 플랫폼 모듈이 설치돼 있지 않으면 여기로 온다. 나머지 플랫폼은 계속 처리해야
                // 하므로 예외를 위로 올리지 않는다 — 절반만 적용된 상태가 제일 나쁘다.
                defines = Array.Empty<string>();
                error = ex.GetType().Name;
                return false;
            }
        }
    }
}
