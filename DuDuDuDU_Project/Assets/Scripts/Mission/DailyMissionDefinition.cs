using System;
using System.Collections.Generic;
using UnityEngine;
using OJ.Core;

namespace OJ.Mission
{
    /// <summary>
    /// 일일 미션 한 줄.
    ///
    /// <b><see cref="id"/> 가 세이브 키다.</b> 바꾸면 그날의 수령 기록만 날아가고
    /// (다음 날이면 어차피 리셋되므로) 사고는 아니지만, 이유 없이 바꾸지 말 것.
    /// </summary>
    [Serializable]
    public sealed class DailyMissionDefinition
    {
        [Tooltip("세이브에 들어가는 키. 목록 안에서 유일해야 한다.")]
        public string id = "daily_play";

        [Tooltip("무엇을 세는가.")]
        public MissionAction action = MissionAction.GamePlay;

        [Tooltip("비워 두는 것이 보통이다. 일일 미션은 종류를 가르지 않는다.")]
        public string subKey = string.Empty;

        [Min(1)] public int requiredCount = 1;

        [Tooltip("{0} 자리에 요구 횟수가 들어간다. 예: 게임 플레이 {0}회")]
        public string titleFormat = "게임 플레이 {0}회";

        /// <summary>
        /// 목록에 띄울 것인가.
        ///
        /// <b>지우는 대신 끄는 자리다.</b> 광고·결제처럼 <b>SDK 가 붙기 전에는 달성할 수
        /// 없는</b> 항목을 잠시 빼야 할 때 쓴다. 지워 버리면 SDK 를 붙이는 날 수치와 보상을
        /// 다시 만들어야 하고, 그러면 그때의 기획이 지금과 달라져 있다.
        /// </summary>
        public bool enabled = true;

        public List<MissionReward> rewards = new List<MissionReward>();

        /// <summary>이 미션이 보는 카운터 키.</summary>
        public string CounterKey => MissionRules.CounterKey(action, subKey);

        /// <summary>화면에 적을 제목.</summary>
        public string DisplayTitle => MissionText.Title(titleFormat, requiredCount);
    }

    /// <summary>
    /// "오늘 n개 깨면 추가로 준다" 한 칸. 화면의 게이지 위에 놓인 선물 상자 하나다.
    /// </summary>
    [Serializable]
    public sealed class DailyMissionTier
    {
        [Tooltip("오늘 깬 미션 수가 이 값 이상이면 열린다.")]
        [Min(1)] public int requiredClearCount = 3;

        public List<MissionReward> rewards = new List<MissionReward>();
    }
}
