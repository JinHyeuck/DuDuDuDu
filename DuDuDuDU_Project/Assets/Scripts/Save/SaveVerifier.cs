#if UNITY_EDITOR || DEV_DEFINE
using System;
using System.Collections.Generic;
using System.Text;
using OJ.Core;
using UnityEngine;
using OJ.Dice;
using OJ.Equipment;
using OJ.IdleReward;
using OJ.Point;
using OJ.Relic;
using OJ.Stage;
using OJ.StageReward;
using OJ.StageStar;

namespace OJ.Save
{
    /// <summary>
    /// 통합 세이브 매핑이 맞는지 대조한다. (F10)
    ///
    /// <b>무엇을 증명하나.</b> "지금 화면에 보이는 값"과 "파일에 쓰일 값"이 같은지다.
    /// 이것이 통과해야 <see cref="SaveService.ReadFromFileEnabled"/> 를 켤 수 있다.
    ///
    /// <b>왜 이 방식인가.</b> 매핑의 진짜 위험은 <c>WriteTo</c> 가 빠뜨리는 것이다 —
    /// 빠진 필드는 파일에서 기본값이 되고, 읽기를 전환하는 순간 그 진행도가 사라진다.
    /// 그런데 빠진 것은 <b>정의상 눈에 안 보인다.</b> 그래서 매니저의 실제 값을 따로
    /// 읽어 와 파일에 담긴 값과 <i>하나씩 이름을 대며</i> 비교한다.
    ///
    /// <b>이 도구는 아무것도 바꾸지 않는다.</b> 파일도, 메모리 상태도 건드리지 않는다.
    /// <c>ReadFrom</c> 을 실제로 불러 보면 더 강한 검증이 되겠지만, 그건 살아 있는
    /// 매니저의 상태를 덮는 행위다. 매핑이 틀린 상태에서 그걸 하면 <b>검증하려던 바로 그
    /// 진행도를 검증 중에 날린다.</b> 그래서 읽기만 한다.
    /// </summary>
    public static class SaveVerifier
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("================ [세이브 대조] ================");

            SaveState state;
            try
            {
                state = BuildFromOwners(sb);
            }
            catch (Exception ex)
            {
                sb.AppendLine("  !! 상태를 모으지 못했다: " + ex);
                Debug.LogError(sb.ToString());
                return;
            }

            int mismatch = 0;
            sb.AppendLine();
            sb.AppendLine("--- 매니저 실제 값  vs  세이브에 담긴 값 ---");

            mismatch += Compare(sb, "골드",
                () => PointManager.Instance.Get(PointType.Gold),
                () => Get(state.Points, "Gold"));

            mismatch += Compare(sb, "다이아",
                () => PointManager.Instance.Get(PointType.Dia),
                () => Get(state.Points, "Dia"));

            mismatch += Compare(sb, "주사위 Normal 레벨",
                () => DiceLevelManager.Instance.GetLevel(DiceType.Normal),
                () => Get(state.DiceLevels, "Normal"));

            mismatch += Compare(sb, "주사위 Fire 레벨",
                () => DiceLevelManager.Instance.GetLevel(DiceType.Fire),
                () => Get(state.DiceLevels, "Fire"));

            mismatch += Compare(sb, "무기 레벨",
                () => EquipmentManager.Instance.GetLevel(EquipmentType.Weapon),
                () => Get(state.Equipment.Levels, "Weapon"));

            mismatch += Compare(sb, "투구 레벨",
                () => EquipmentManager.Instance.GetLevel(EquipmentType.Helmet),
                () => Get(state.Equipment.Levels, "Helmet"));

            mismatch += Compare(sb, "유물 소환 횟수",
                () => RelicManager.Instance.SummonCount,
                () => state.Relics.SummonCount);

            mismatch += Compare(sb, "선택 스테이지",
                () => StageProgressManager.Instance.GetSelectedStageIndex(),
                () => state.Stage.SelectedIndex);

            mismatch += Compare(sb, "해금 스테이지",
                () => StageProgressManager.Instance.GetHighestUnlockedStageIndex(),
                () => state.Stage.HighestUnlockedIndex);

            mismatch += Compare(sb, "자동전투 시작 tick",
                () => IdleRewardManager.Instance.AutoBattleStartUtcTicksForDiagnostics,
                () => state.Idle.AutoBattleStartUtcTicks);

            // 개수만 보는 것들. 내용까지 대조하면 출력이 길어지는데, 개수가 맞고
            // 위 항목들이 맞으면 매핑이 통째로 어긋난 경우는 걸러진다.
            mismatch += Compare(sb, "재화 종류 수(0 아닌 것)",
                () => CountNonZeroPoints(),
                () => CountNonZero(state.Points));

            mismatch += Compare(sb, "유물 종류 수",
                () => CountOwnedRelics(),
                () => state.Relics.Levels.Count);

            mismatch += Compare(sb, "수령한 누적보상 수",
                () => CountClaimedStageRewards(),
                () => state.Stage.ClaimedRewardIds.Count);

            mismatch += Compare(sb, "스테이지 기록 수",
                () => CountStageRecords(),
                () => state.Stage.Records.Count);

            sb.AppendLine();
            if (mismatch == 0)
            {
                sb.AppendLine("  전부 일치. SaveService.ReadFromFileEnabled 를 켜도 되는 상태다.");
                sb.AppendLine("  세이브 크기: " + SaveSerializer.Serialize(state).Length + "바이트");
            }
            else
            {
                sb.AppendLine("  !! 불일치 " + mismatch + "건. 읽기 전환하면 그만큼 진행도가 사라진다.");
            }

            sb.AppendLine("==============================================");

            if (mismatch == 0)
                Debug.LogWarning(sb.ToString());
            else
                Debug.LogError(sb.ToString());
        }

        private static SaveState BuildFromOwners(StringBuilder sb)
        {
            var state = new SaveState();
            var owners = new List<ISaveStateOwner>();

            Collect(owners, PointManager.Instance);
            Collect(owners, DiceLevelManager.Instance);
            Collect(owners, EquipmentManager.Instance);
            Collect(owners, RelicManager.Instance);
            Collect(owners, StageProgressManager.Instance);
            Collect(owners, StageRewardManager.Instance);
            Collect(owners, StageStarManager.Instance);
            Collect(owners, IdleRewardManager.Instance);

            sb.AppendLine("  세이브 조각 소유자 " + owners.Count + "개");

            for (int i = 0; i < owners.Count; i++)
                owners[i].WriteTo(state);

            return state;
        }

        private static void Collect(List<ISaveStateOwner> list, object candidate)
        {
            if (candidate is ISaveStateOwner owner)
                list.Add(owner);
        }

        private static int Compare(StringBuilder sb, string name, Func<long> actual, Func<long> saved)
        {
            long a, s;
            try
            {
                a = actual();
                s = saved();
            }
            catch (Exception ex)
            {
                sb.AppendFormat("  !! {0,-24} 읽다 예외: {1}", name, ex.GetType().Name).AppendLine();
                return 1;
            }

            bool same = a == s;
            sb.AppendFormat("  {0} {1,-24} 실제 {2}  세이브 {3}", same ? "OK" : "!!", name, a, s).AppendLine();
            return same ? 0 : 1;
        }

        private static long Get(SortedDictionary<string, int> map, string key)
        {
            return map.TryGetValue(key, out int value) ? value : 0;
        }

        private static long CountNonZero(SortedDictionary<string, int> map)
        {
            int n = 0;
            foreach (KeyValuePair<string, int> pair in map)
            {
                if (pair.Value != 0)
                    n++;
            }

            return n;
        }

        private static long CountNonZeroPoints()
        {
            int n = 0;
            foreach (PointType type in Enum.GetValues(typeof(PointType)))
            {
                if (type == PointType.Max)
                    continue;

                if (PointManager.Instance.Get(type) != 0)
                    n++;
            }

            return n;
        }

        private static long CountOwnedRelics()
        {
            int n = 0;
            foreach (RelicId id in Enum.GetValues(typeof(RelicId)))
            {
                if (id == RelicId.None)
                    continue;

                if (RelicManager.Instance.GetLevel(id) > 0)
                    n++;
            }

            return n;
        }

        private static long CountClaimedStageRewards()
        {
            IReadOnlyList<StageRewardMilestone> milestones = StageRewardManager.Instance.GetMilestones();
            int n = 0;
            for (int i = 0; i < milestones.Count; i++)
            {
                if (StageRewardManager.Instance.IsClaimed(milestones[i]))
                    n++;
            }

            return n;
        }

        private static long CountStageRecords()
        {
            int n = 0;
            int max = StageDatabaseProvider.GetDatabase().StageCount;
            for (int i = 1; i <= max; i++)
            {
                if (StageProgressManager.Instance.GetBestClearGrade(i) > StageClearGrade.None ||
                    StageProgressManager.Instance.GetBestClearedWave(i) > 0)
                {
                    n++;
                }
            }

            return n;
        }
    }
}
#endif
