using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ.UI
{
    /// <summary>
    /// 다이얼로그 프리팹 목록. (MIGRATION_BASELINE 10.3)
    ///
    /// <b>왜 문자열 경로가 아니라 직접 참조인가.</b> <c>Resources.Load("UI/Xxx")</c> 방식은
    /// 프리팹을 옮기거나 이름을 바꿔도 <b>컴파일에 걸리지 않고</b> 런타임에 null 로만 드러난다.
    /// 게다가 <c>Resources/</c> 폴더는 안에 있는 것이 전부 빌드에 들어가서, 쓰지 않는 프리팹도
    /// 앱 크기에 남는다. 에셋 참조로 두면 옮겨도 연결이 유지되고 인스펙터에서 빈 칸이 보인다.
    ///
    /// <b>왜 씬이 아니라 여기인가.</b> 지금은 다이얼로그 15개가 씬에 인스턴스로 상주한다.
    /// 그래서 씬을 열 때마다 전부 만들어지고, 한 번도 열지 않아도 <c>Awake</c> 가 돈다.
    /// 게다가 <b>어떤 오프너가 어떤 다이얼로그를 여는지가 씬 참조에 흩어져 있어</b>
    /// 코드만 읽어서는 알 수 없다. 목록을 한 곳에 두면 그것이 곧 문서가 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "DialogCatalog", menuName = "OJ/Dialog Catalog")]
    public sealed class DialogCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("이 프리팹의 루트에 붙은 다이얼로그 컴포넌트 타입 이름. 비면 프리팹 이름을 쓴다.")]
            public string typeName;

            public GameObject prefab;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>
        /// 타입 이름으로 프리팹을 찾는다. 없으면 null — <b>부르는 쪽이 시끄럽게 실패해야 한다.</b>
        /// 여기서 기본 프리팹으로 흐르게 만들면 배선 사고가 "엉뚱한 창이 뜬다"로 바뀐다.
        /// </summary>
        public GameObject Find(string typeName)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e == null || e.prefab == null)
                    continue;

                if (KeyOf(e) == typeName)
                    return e.prefab;
            }

            return null;
        }

        /// <summary>
        /// 항목의 키. <c>typeName</c> 이 비어 있으면 프리팹 이름을 쓴다 —
        /// 이 프로젝트는 프리팹 이름과 컴포넌트 이름이 같다는 규칙을 지키고 있어서
        /// 대부분의 항목은 손으로 적을 것이 없다.
        /// </summary>
        public static string KeyOf(Entry e)
        {
            if (e == null)
                return null;

            return string.IsNullOrWhiteSpace(e.typeName)
                ? (e.prefab != null ? e.prefab.name : null)
                : e.typeName.Trim();
        }

        /// <summary>
        /// 목록이 성한지 본다. 편집기 도구와 진단(F9)이 같이 쓴다.
        ///
        /// <b>중복 키를 사고로 취급한다.</b> 같은 이름이 둘이면 <see cref="Find"/> 가
        /// 먼저 나온 것을 돌려주는데, 그 "먼저"가 목록 순서에 달려 있어서 항목을 옮기는
        /// 것만으로 열리는 창이 바뀐다.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e == null)
                {
                    problems.Add(i + "번 항목이 비어 있다.");
                    continue;
                }

                if (e.prefab == null)
                {
                    problems.Add(i + "번 항목(" + (e.typeName ?? "이름 없음") + ")에 프리팹이 없다.");
                    continue;
                }

                string key = KeyOf(e);
                if (string.IsNullOrEmpty(key))
                {
                    problems.Add(i + "번 항목의 키를 정할 수 없다.");
                    continue;
                }

                if (!seen.Add(key))
                    problems.Add("키가 중복이다: " + key);

                if (e.prefab.GetComponent(key) == null)
                {
                    // 프리팹 루트에 그 이름의 컴포넌트가 없으면, 열더라도 다이얼로그로
                    // 동작하지 않는다. 이름만 맞고 알맹이가 다른 경우를 잡는다.
                    problems.Add(key + " 프리팹 루트에 같은 이름의 컴포넌트가 없다.");
                }
            }

            return problems;
        }
    }
}
