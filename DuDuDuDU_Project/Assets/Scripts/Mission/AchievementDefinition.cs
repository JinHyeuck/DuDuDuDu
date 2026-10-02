using System;
using System.Collections.Generic;
using UnityEngine;
using OJ.Core;

namespace OJ.Mission
{
    /// <summary>
    /// 업적 한 단계.
    ///
    /// <b>같은 <see cref="CounterKey"/> 를 가진 것들이 한 계열이다.</b> 계열 안에서는
    /// 요구 횟수 오름차순으로 한 번에 하나만 화면에 뜨고, 받으면 다음 단계로 넘어간다.
    /// 계열을 묶는 필드를 따로 두지 않은 것은 <b>묶음과 카운터가 갈라질 수 있기 때문</b>이다 —
    /// 그러면 "같은 그룹인데 다른 카운터를 보는" 단계를 만들 수 있고, 그건 화면에서
    /// 숫자가 거꾸로 가는 것으로만 드러난다.
    ///
    /// <b><see cref="id"/> 는 영구 세이브 키다.</b> 일일 미션과 달리 리셋이 없으므로,
    /// 바꾸면 그 단계를 이미 받은 사람이 <b>다시 받을 수 있게 된다.</b> 바꾸지 말 것.
    /// </summary>
    [Serializable]
    public sealed class AchievementDefinition
    {
        [Tooltip("세이브에 들어가는 키. 전체에서 유일해야 하고, 한 번 정하면 바꾸지 않는다.")]
        public string id = "ach_play_10";

        [Tooltip("무엇을 세는가.")]
        public MissionAction action = MissionAction.GamePlay;

        [Tooltip("종류를 가를 때만 채운다. 예: 다이스 등급(Normal/Rare/Mythic), 보석 재료 등급(Rare)")]
        public string subKey = string.Empty;

        [Min(1)] public int requiredCount = 10;

        [Tooltip("{0} 자리에 요구 횟수가 들어간다. 예: 핀볼 발사 {0}회")]
        public string titleFormat = "게임 플레이 {0}회";

        public List<MissionReward> rewards = new List<MissionReward>();

        /// <summary>이 업적이 보는 누적 카운터 키. 계열을 가르는 축이기도 하다.</summary>
        public string CounterKey => MissionRules.CounterKey(action, subKey);

        /// <summary>화면에 적을 제목.</summary>
        public string DisplayTitle => MissionText.Title(titleFormat, requiredCount);
    }

    /// <summary>
    /// 한 계열(같은 카운터 키)의 단계들. <b>요구 횟수 오름차순</b>으로 정렬된 상태다 —
    /// <c>AchievementDatabase</c> 가 로딩 때 한 번 정렬하고, 그 뒤로는 순서가 곧 진행 순서다.
    /// </summary>
    public sealed class AchievementSeries
    {
        public AchievementSeries(string counterKey, List<AchievementDefinition> tiers)
        {
            CounterKey = counterKey;
            Tiers = tiers;
        }

        public string CounterKey { get; }

        public List<AchievementDefinition> Tiers { get; }
    }
}
