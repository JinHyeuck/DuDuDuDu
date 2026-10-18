using System.Collections.Generic;
using UnityEngine;
using OJ.Core;

namespace OJ.ContentUnlock
{
    /// <summary>
    /// 컨텐츠 해금 순서의 <b>유일한 정본</b> — 무엇이 몇 스테이지 클리어에 열리고, 어떻게 알리는가.
    /// (AGENTS 확정사항 3, Docs/ContentUnlock.md)
    ///
    /// <b>규칙은 여기 없다.</b> 기본표·검사·대기열은 <see cref="ContentUnlockRules"/>(OJ.Core)에 있고
    /// 이 SO 는 목록을 들기만 한다. 헤드리스 러너에서 SO 를 못 만들기 때문이다
    /// (<c>DiceUnlockDatabase</c> 와 같은 판단).
    /// </summary>
    [CreateAssetMenu(fileName = "ContentUnlockDatabase", menuName = "OJ/Content Unlock Database")]
    public sealed class ContentUnlockDatabase : ScriptableObject
    {
        [Tooltip("해금 순서대로 적는다. 같은 클리어 안에서는 연출을 가이드보다 앞에 — 가이드에서 그 진입이 끊긴다.")]
        [SerializeField] private List<ContentUnlockDefinition> definitions = new List<ContentUnlockDefinition>();

        private readonly Dictionary<ContentId, ContentUnlockDefinition> map =
            new Dictionary<ContentId, ContentUnlockDefinition>();

        public IReadOnlyList<ContentUnlockDefinition> Definitions => definitions;

        private void OnEnable()
        {
            if (definitions == null)
                definitions = new List<ContentUnlockDefinition>();

            // 비었을 때만 채운다 — 로드마다 덮으면 인스펙터에서 고친 값이 조용히 사라진다.
            if (definitions.Count == 0)
                definitions.AddRange(ContentUnlockRules.BuildDefaults());

            RebuildMap();
        }

        private void RebuildMap()
        {
            map.Clear();
            for (int i = 0; i < definitions.Count; i++)
            {
                ContentUnlockDefinition definition = definitions[i];
                if (definition != null)
                    map[definition.contentId] = definition;
            }
        }

        /// <summary>그 컨텐츠의 정의. 표에 없으면 null — <c>Validate</c> 가 먼저 잡는다.</summary>
        public ContentUnlockDefinition Get(ContentId id)
        {
            if (map.Count == 0)
                RebuildMap();

            return map.TryGetValue(id, out ContentUnlockDefinition definition) ? definition : null;
        }

        public List<string> Validate(int stageCount)
        {
            return ContentUnlockRules.Validate(definitions, stageCount);
        }

        /// <summary>에디터 도구가 에셋을 처음 만들 때 쓴다.</summary>
        public void PopulateDefaults()
        {
            definitions.Clear();
            definitions.AddRange(ContentUnlockRules.BuildDefaults());
            RebuildMap();
        }
    }
}
