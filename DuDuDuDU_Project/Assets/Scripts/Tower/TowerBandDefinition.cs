using System;
using System.Collections.Generic;
using UnityEngine;
using OJ.Stage;

namespace OJ.Tower
{
    /// <summary>
    /// 5층 구간 하나의 정의. 300층을 60줄로 적는 단위다.
    ///
    /// <b>층을 줄로 적지 않는 이유.</b> 기획서 6장이 "기본적으로 5층마다 적 구성과 공략
    /// 핵심을 변경한다" 라고 못 박았다. 구간이 설계 단위이므로 데이터도 구간 단위여야
    /// 한다 — 층 단위로 두면 같은 값을 다섯 번 적게 되고, 다섯 중 하나만 고치는 사고가
    /// 생긴다. 층마다 달라지는 것은 <see cref="countPerFloorStep"/> 하나뿐이고,
    /// 그것도 <b>왜</b> 달라지는지가 기획서에 적혀 있다("층마다 수 증가").
    /// </summary>
    [Serializable]
    public sealed class TowerBandDefinition
    {
        [Tooltip("구간 번호. 1이 1~5층, 2가 6~10층 …")]
        [Min(1)] public int band = 1;

        [Tooltip("층 카드와 편성 화면에 뜨는 이름.")]
        public string displayName = "구간";

        [Tooltip("이 구간의 주 콘셉트. 비어 있을 수 없다.")]
        public TowerFloorConcept primaryConcept = TowerFloorConcept.Swarm;

        [Tooltip("겹칠 콘셉트. 비면 단독 구간이다. 21층 이후에만 채운다(기획서 6.2).")]
        public List<TowerFloorConcept> extraConcepts = new List<TowerFloorConcept>();

        [Tooltip("배경·몬스터 프리팹 테마. 본편 스테이지 테마를 빌린다.")]
        public StageTheme theme = StageTheme.DarkForest;

        [Header("마리 수")]
        [Tooltip("구간 첫 층의 마리 수. 콘셉트 배수가 곱해지기 전 값이다.")]
        [Min(1)] public int monsterCount = 20;

        [Tooltip("구간 안에서 층마다 늘어나는 마리 수. 0이면 다섯 층이 같은 수다.")]
        [Min(0)] public int countPerFloorStep = 0;

        [Header("구간 배수")]
        [Tooltip("이 구간에만 먹이는 체력 배수. 콘셉트 배수 위에 한 번 더 곱해진다.")]
        [Min(0.05f)] public float hpMultiplier = 1f;

        [Tooltip("이 구간에만 먹이는 방어력 배수.")]
        [Min(0f)] public float defenseMultiplier = 1f;

        /// <summary>
        /// 이 구간에서 실제로 쓰이는 콘셉트 전부. 주 콘셉트가 항상 첫 번째다.
        ///
        /// <b>중복은 걸러 낸다.</b> 인스펙터에서 같은 것을 두 번 고르면 배수가 제곱이 되어
        /// 그 구간만 이유 없이 두 배 어려워진다 — 그 사고가 화면에서는 "여기가 왜 이렇게
        /// 세지" 로만 보인다.
        /// </summary>
        public List<TowerFloorConcept> ResolveConcepts()
        {
            var list = new List<TowerFloorConcept> { primaryConcept };

            if (extraConcepts == null)
                return list;

            for (int i = 0; i < extraConcepts.Count; i++)
            {
                if (!list.Contains(extraConcepts[i]))
                    list.Add(extraConcepts[i]);
            }

            return list;
        }
    }
}
