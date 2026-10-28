using System;
using System.Collections.Generic;
using OJ.Core;
using OJ.Save;
using OJ.Dice;
using OJ.Point;
using OJ.Stage;
using OJ.Tower;
using UnityEngine;
using UnityEngine.Scripting;

namespace OJ.Synergy
{
    /// <summary>
    /// 시너지의 영구 상태 — 계열별 프리셋 4개, 장착 프리셋, Best 보정 누적, 리롤 횟수, 누적 발동.
    ///
    /// 규칙(비용·Best·개방)은 전부 <see cref="SynergyRules"/> 에 있고 여기는 상태를 들고 재화를 낸다.
    /// 루트에 사는 영구 서비스라 로비와 전투 양쪽에서 불린다.
    /// </summary>
    [Preserve]
    public sealed class SynergyManager : ISaveStateOwner
    {
        /// <summary>과도기 다리. <b>대입은 <c>GameContainer</c> 에서만 한다.</b></summary>
        public static SynergyManager Instance { get; internal set; }

        /// <summary>프리셋·잠금·장착이 바뀌었다.</summary>
        public event Action OnChanged;

        private sealed class CategoryState
        {
            public int EquippedPreset;
            public int BestSpent;
            public int Rerolls;
            public readonly SynergyPreset[] Presets = NewPresets();
        }

        private readonly CategoryState[] categories = NewCategories();
        private int totalFires;

        public int TotalFires => totalFires;

        // ── 개방 ───────────────────────────────────────────────

        public bool IsOpen(SynergyCategory category)
        {
            int floor = TowerProgressManager.Instance != null ? TowerProgressManager.Instance.HighestClearedFloor : 0;
            return SynergyRules.IsCategoryOpen(category, Get(SynergyCategory.Dice).Rerolls, totalFires, floor);
        }

        /// <summary>열리지 않은 계열의 개방 조건과 진행. 예: "다이스 시너지 리롤 12/30".</summary>
        public string UnlockHint(SynergyCategory category)
        {
            switch (category)
            {
                case SynergyCategory.Combat:
                    return "다이스 시너지 리롤 " + Get(SynergyCategory.Dice).Rerolls + "/" + SynergyRules.CombatUnlockDiceRerolls;
                case SynergyCategory.Action:
                    return "시너지 누적 발동 " + totalFires + "/" + SynergyRules.ActionUnlockTotalFires;
                case SynergyCategory.Unified:
                    int floor = TowerProgressManager.Instance != null ? TowerProgressManager.Instance.HighestClearedFloor : 0;
                    return "무한의 탑 " + SynergyRules.UnifiedUnlockTowerFloor + "층 클리어 (" + floor + "층)";
                default:
                    return string.Empty;
            }
        }

        public int OpenEffectLines
        {
            get
            {
                int last = StageProgressManager.Instance != null ? StageProgressManager.Instance.GetLastClearedStageIndex() : 0;
                return SynergyRules.OpenEffectLines(last);
            }
        }

        // ── 조회 ───────────────────────────────────────────────

        public int GetEquippedIndex(SynergyCategory category) => Get(category).EquippedPreset;

        public int GetBestSpent(SynergyCategory category) => Get(category).BestSpent;

        public int GetRerolls(SynergyCategory category) => Get(category).Rerolls;

        /// <summary>
        /// 프리셋. <b>리롤하기 전에는 비어 있다</b> — 기본 옵션을 아무거나 넣지 않는다(사용자 결정 2026-10-28).
        /// 빈 프리셋을 장착한 계열은 전투에서 슬롯이 뜨지 않는다.
        /// </summary>
        public SynergyPreset GetPreset(SynergyCategory category, int index)
        {
            return Get(category).Presets[Mathf.Clamp(index, 0, SynergyRules.PresetCount - 1)];
        }

        public SynergyPreset GetEquipped(SynergyCategory category) => GetPreset(category, GetEquippedIndex(category));

        // ── 조작 ───────────────────────────────────────────────

        public void SetEquipped(SynergyCategory category, int index)
        {
            index = Mathf.Clamp(index, 0, SynergyRules.PresetCount - 1);
            CategoryState state = Get(category);
            if (state.EquippedPreset == index)
                return;

            state.EquippedPreset = index;
            Save();
            OnChanged?.Invoke();
        }

        /// <summary>잠금을 뒤집는다. <paramref name="line"/> -1 은 조건, 0~2 는 효과. 빈 줄·안 열린 줄은 못 잠근다.</summary>
        public bool ToggleLock(SynergyCategory category, int presetIndex, int line)
        {
            SynergyPreset preset = GetPreset(category, presetIndex);
            SynergyLine target;
            if (line < 0)
            {
                target = preset.Condition;
            }
            else
            {
                if (line >= OpenEffectLines)
                    return false;

                target = preset.Effects[line];
            }

            if (target.IsEmpty)
                return false;

            target.Locked = !target.Locked;
            Save();
            OnChanged?.Invoke();
            return true;
        }

        public int GetRerollCost(SynergyCategory category, int presetIndex)
        {
            return SynergyRules.RerollCost(GetPreset(category, presetIndex), OpenEffectLines);
        }

        public bool CanReroll(SynergyCategory category, int presetIndex)
        {
            return IsOpen(category) && SynergyRules.CanReroll(GetPreset(category, presetIndex), OpenEffectLines);
        }

        /// <summary>티켓을 내고 굴린다. 티켓이 모자라거나 굴릴 줄이 없으면 아무것도 안 한다.</summary>
        public SynergyRerollResult Reroll(SynergyCategory category, int presetIndex)
        {
            var none = new SynergyRerollResult { BestLine = -2 };
            if (!CanReroll(category, presetIndex))
                return none;

            SynergyPreset preset = GetPreset(category, presetIndex);
            int lines = OpenEffectLines;
            int cost = SynergyRules.RerollCost(preset, lines);

            PointManager points = PointManager.Instance;
            if (points == null || !points.TrySpend(PointType.SynergyTicket, cost, false))
                return none;

            CategoryState state = Get(category);
            SynergyRerollResult result = SynergyRules.Reroll(preset, category, lines, cost, ref state.BestSpent, Next, Targets);
            if (result.Rolled)
                state.Rerolls++;

            Save();
            OnChanged?.Invoke();
            return result;
        }

        /// <summary>전투가 센 발동을 더한다. 저장은 판이 끝날 때의 저장에 실린다.</summary>
        public void AddFires(int fires)
        {
            if (fires > 0)
                totalFires += fires;
        }

        // ── 난수·대상 ───────────────────────────────────────────

        private static int Next(int n) => n <= 1 ? 0 : UnityEngine.Random.Range(0, n);

        private static readonly List<int> elementTargets = new List<int>
        {
            (int)ElementType.Normal, (int)ElementType.Fire, (int)ElementType.Water, (int)ElementType.Light, (int)ElementType.Dark,
        };

        private static readonly List<int> diceTargets = new List<int>();

        /// <summary>대상 후보. 다이스는 지금 쓸 수 있는 것만 — 못 가진 킹이 걸리면 영영 안 켜진다.</summary>
        private static IReadOnlyList<int> Targets(SynergyTargetKind kind)
        {
            if (kind == SynergyTargetKind.Element)
                return elementTargets;

            diceTargets.Clear();
            AddOwned(DiceEvolution.BaseTypes);
            AddOwned(DiceEvolution.SpecialTypes);
            AddOwned(DiceEvolution.KingTypes);
            return diceTargets;
        }

        private static void AddOwned(IReadOnlyList<DiceType> types)
        {
            foreach (DiceType type in types)
            {
                bool owned = DiceOwnershipManager.Instance != null
                    ? DiceOwnershipManager.Instance.IsOwned(type)
                    : DiceEvolution.IsOwnedFromStart(type);
                if (owned)
                    diceTargets.Add((int)type);
            }
        }

        // ── 저장 ───────────────────────────────────────────────

        public void WriteTo(SaveState state)
        {
            if (state == null)
                return;

            SynergySave save = state.Synergy;
            save.TotalFires = totalFires;
            save.Categories.Clear();

            for (int c = 0; c < SynergyRules.CategoryCount; c++)
            {
                CategoryState source = categories[c];

                // 한 번도 안 건드린 계열은 적지 않는다. 적으면 옛 세이브(키 없음)를 읽은 직후의 상태가
                // 파일과 달라져 SaveService 의 로드 왕복 대조가 매번 운다.
                if (IsUntouched(source))
                    continue;

                var target = new SynergyCategorySave
                {
                    EquippedPreset = source.EquippedPreset,
                    BestSpent = source.BestSpent,
                    Rerolls = source.Rerolls,
                };

                foreach (SynergyPreset preset in source.Presets)
                {
                    var presetSave = new SynergyPresetSave { Condition = WriteLine(preset.Condition, true) };
                    foreach (SynergyLine line in preset.Effects)
                        presetSave.Effects.Add(WriteLine(line, false));

                    target.Presets.Add(presetSave);
                }

                save.Categories[((SynergyCategory)c).ToString()] = target;
            }
        }

        public void ReadFrom(SaveState state)
        {
            for (int c = 0; c < categories.Length; c++)
                categories[c] = new CategoryState();

            totalFires = 0;
            if (state == null)
                return;

            totalFires = Mathf.Max(0, state.Synergy.TotalFires);
            foreach (KeyValuePair<string, SynergyCategorySave> pair in state.Synergy.Categories)
            {
                if (!Enum.TryParse(pair.Key, out SynergyCategory category) || pair.Value == null)
                    continue;

                CategoryState target = categories[(int)category];
                target.EquippedPreset = Mathf.Clamp(pair.Value.EquippedPreset, 0, SynergyRules.PresetCount - 1);
                target.BestSpent = Mathf.Max(0, pair.Value.BestSpent);
                target.Rerolls = Mathf.Max(0, pair.Value.Rerolls);

                for (int p = 0; p < SynergyRules.PresetCount && p < pair.Value.Presets.Count; p++)
                {
                    SynergyPresetSave presetSave = pair.Value.Presets[p];
                    if (presetSave == null)
                        continue;

                    ReadLine(presetSave.Condition, target.Presets[p].Condition, true);
                    for (int e = 0; e < SynergyRules.EffectLineCount && e < presetSave.Effects.Count; e++)
                        ReadLine(presetSave.Effects[e], target.Presets[p].Effects[e], false);
                }
            }
        }

        private static bool IsUntouched(CategoryState state)
        {
            if (state.EquippedPreset != 0 || state.BestSpent != 0 || state.Rerolls != 0)
                return false;

            foreach (SynergyPreset preset in state.Presets)
            {
                if (!preset.Condition.IsEmpty)
                    return false;

                foreach (SynergyLine line in preset.Effects)
                {
                    if (!line.IsEmpty)
                        return false;
                }
            }

            return true;
        }

        private static SynergyLineSave WriteLine(SynergyLine line, bool condition)
        {
            SynergyOption option = SynergyRules.Resolve(line, condition);
            if (option == null)
                return new SynergyLineSave();

            return new SynergyLineSave
            {
                Option = condition ? ((SynergyConditionId)line.OptionId).ToString() : ((SynergyEffectId)line.OptionId).ToString(),
                Target = SynergyText.TargetToSave(option.Target, line.Target),
                Value = line.Value,
                Locked = line.Locked,
            };
        }

        private static void ReadLine(SynergyLineSave save, SynergyLine line, bool condition)
        {
            if (save == null || string.IsNullOrEmpty(save.Option))
                return;

            // 없어진 옵션 이름은 빈 줄로 둔다. 그 한 줄 때문에 로드 전체가 죽으면 안 된다.
            int id;
            if (condition)
                id = Enum.TryParse(save.Option, out SynergyConditionId c) ? (int)c : 0;
            else
                id = Enum.TryParse(save.Option, out SynergyEffectId e) ? (int)e : 0;

            line.OptionId = id;
            SynergyOption option = SynergyRules.Resolve(line, condition);
            if (option == null)
            {
                line.OptionId = 0;
                return;
            }

            line.Target = SynergyText.TargetFromSave(option.Target, save.Target);
            line.Value = Mathf.Clamp(save.Value, option.Low, option.High);
            line.Locked = save.Locked;
        }

        private static void Save() => OJ.DI.GameContainer.SaveService?.SaveAll();

        private CategoryState Get(SynergyCategory category) => categories[(int)category];

        private static CategoryState[] NewCategories()
        {
            var array = new CategoryState[SynergyRules.CategoryCount];
            for (int i = 0; i < array.Length; i++)
                array[i] = new CategoryState();
            return array;
        }

        private static SynergyPreset[] NewPresets()
        {
            var array = new SynergyPreset[SynergyRules.PresetCount];
            for (int i = 0; i < array.Length; i++)
                array[i] = new SynergyPreset();
            return array;
        }
    }
}
