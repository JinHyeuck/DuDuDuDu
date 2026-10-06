#if UNITY_EDITOR || DEV_DEFINE
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OJ.Analytics;
using OJ.DI;
using OJ.Dice;
using OJ.Equipment;
using OJ.Hunting;
using OJ.IdleReward;
using OJ.Mission;
using OJ.Point;
using OJ.SeasonPass;
using OJ.Save;
using OJ.SceneFlow;
using OJ.Tower;

namespace OJ.Dev
{
    /// <summary>
    /// 탭별 내용. <see cref="DevCheatPanel"/> 과 갈라 둔 것은 길이 때문이다 —
    /// 창의 뼈대와 치트 항목이 한 파일에 있으면 둘 다 읽기 어려워진다.
    ///
    /// <b>치트 동작은 여기서 새로 만들지 않는다.</b> 전부 이미 있는 경로를 부른다
    /// (<c>PointManager.Add</c>·<c>MissionCheat</c>·<c>TowerProgressCheat</c>·
    /// <c>SaveResetCheat</c>·<c>IdleRewardManager.DevAdvanceTime</c>).
    /// 치트가 제 나름의 지름길을 뚫으면 <b>게임에서 안 일어나는 상태</b>를 만들게 되고,
    /// 거기서 나온 버그는 시간 낭비다.
    /// </summary>
    internal static class DevCheatSections
    {
        internal static void Build(DevCheatPanel panel, DevCheatTab tab, Transform root)
        {
            switch (tab)
            {
                case DevCheatTab.Currency: BuildCurrency(panel, root); break;
                case DevCheatTab.Gem: BuildGem(panel, root); break;
                case DevCheatTab.Dice: BuildDice(panel, root); break;
                case DevCheatTab.Battle: BuildBattle(panel, root); break;
                case DevCheatTab.Progress: BuildProgress(panel, root); break;
                case DevCheatTab.Time: BuildTime(panel, root); break;
                case DevCheatTab.Mode: BuildMode(panel, root); break;
            }
        }

        // ── 재화 ───────────────────────────────────────────────────────

        private static void BuildCurrency(DevCheatPanel panel, Transform root)
        {
            PointType[] types = SelectablePointTypes();
            string[] names = new string[types.Length];
            for (int i = 0; i < types.Length; i++)
                names[i] = types[i] + " (" + Current(types[i]) + ")";

            DevCheatUI.CreateSectionTitle(root, "재화");
            DevCheatUI.CreateDropdown(root, names, panel.CurrencyIndex, v => panel.CurrencyIndex = v);

            RectTransform amountRow = DevCheatUI.CreateRow(root);
            DevCheatUI.CreateLabel("AmountLabel", amountRow, "수량", 32f, DevCheatUI.MutedColor);
            var field = DevCheatUI.CreateNumberField(amountRow, panel.Amount.ToString());
            field.onValueChanged.AddListener(v => panel.Amount = ParseInt(v, panel.Amount));

            RectTransform actionRow = DevCheatUI.CreateRow(root);
            DevCheatUI.CreateButton("Add", actionRow, "지급", DevCheatUI.ButtonColor,
                () => Apply(panel, types, +1));
            DevCheatUI.CreateButton("Sub", actionRow, "차감", DevCheatUI.ButtonColor,
                () => Apply(panel, types, -1));
            DevCheatUI.CreateButton("Set", actionRow, "설정", DevCheatUI.ButtonColor,
                () => Apply(panel, types, 0));

            DevCheatUI.CreateSectionTitle(root, "한 번에");
            DevCheatUI.CreateHelpText(root,
                "모든 재화를 수량만큼 지급한다. 전투 재화(SP/강화석)는 판마다 0 으로 밀려 여기 없다.");
            DevCheatUI.CreateButton("All", root, "전 재화 지급", DevCheatUI.AccentColor,
                () => GrantAll(panel, types));
        }

        private static void Apply(DevCheatPanel panel, PointType[] types, int sign)
        {
            PointManager points = PointManager.Instance;
            if (points == null || types.Length == 0)
                return;

            PointType type = types[Mathf.Clamp(panel.CurrencyIndex, 0, types.Length - 1)];

            if (sign > 0)
                points.Add(type, panel.Amount);
            else if (sign < 0)
                points.Set(type, Mathf.Max(0, points.Get(type) - panel.Amount));
            else
                points.Set(type, Mathf.Max(0, panel.Amount));

            panel.Rebuild();
        }

        private static void GrantAll(DevCheatPanel panel, PointType[] types)
        {
            PointManager points = PointManager.Instance;
            if (points == null)
                return;

            for (int i = 0; i < types.Length; i++)
                points.Add(types[i], panel.Amount, false);

            points.SaveAll();
            panel.Rebuild();
        }

        private static int Current(PointType type)
        {
            return PointManager.Instance != null ? PointManager.Instance.Get(type) : 0;
        }

        private static PointType[] SelectablePointTypes()
        {
            var all = (PointType[])Enum.GetValues(typeof(PointType));
            var list = new List<PointType>(all.Length);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != PointType.Max)
                    list.Add(all[i]);
            }

            return list.ToArray();
        }

        // ── 보석 ───────────────────────────────────────────────────────

        private static void BuildGem(DevCheatPanel panel, Transform root)
        {
            DevCheatUI.CreateSectionTitle(root, "보석");

            EquipmentManager equipment = EquipmentManager.Instance;
            IReadOnlyList<GemDefinition> gems = equipment != null ? equipment.GetGemDefinitions() : null;

            if (gems == null || gems.Count == 0)
            {
                DevCheatUI.CreateHelpText(root, "보석 데이터베이스가 비어 있다. GemDefinitionDatabase 를 확인할 것.");
                return;
            }

            string[] names = new string[gems.Count];
            for (int i = 0; i < gems.Count; i++)
                names[i] = gems[i].displayName + " [" + gems[i].rarity + "] x" + equipment.GetGemCount(gems[i].gemId);

            DevCheatUI.CreateDropdown(root, names, panel.GemIndex, v => panel.GemIndex = v);

            RectTransform row = DevCheatUI.CreateRow(root);
            DevCheatUI.CreateLabel("GemAmountLabel", row, "개수", 32f, DevCheatUI.MutedColor);
            var field = DevCheatUI.CreateNumberField(row, panel.GemAmount.ToString());
            field.onValueChanged.AddListener(v => panel.GemAmount = ParseInt(v, panel.GemAmount));

            DevCheatUI.CreateButton("AddGem", root, "지급", DevCheatUI.ButtonColor, () =>
            {
                int index = Mathf.Clamp(panel.GemIndex, 0, gems.Count - 1);
                equipment.AddGem(gems[index].gemId, Mathf.Max(1, panel.GemAmount));
                panel.Rebuild();
            });

            DevCheatUI.CreateHelpText(root,
                "합성은 같은 등급 4개가 필요하다. 합성을 보려면 한 종류를 4개 이상 넣을 것.");
        }

        // ── 다이스 ─────────────────────────────────────────────────────

        private static void BuildDice(DevCheatPanel panel, Transform root)
        {
            DiceType[] types = SelectableDiceTypes();
            string[] names = new string[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(types[i]) : 0;
                bool owned = DiceOwnershipManager.Instance != null && DiceOwnershipManager.Instance.IsOwned(types[i]);
                names[i] = types[i] + " Lv" + level + (owned ? "" : " (미보유)");
            }

            DevCheatUI.CreateSectionTitle(root, "다이스");
            DevCheatUI.CreateDropdown(root, names, panel.DiceIndex, v => panel.DiceIndex = v);

            RectTransform levelRow = DevCheatUI.CreateRow(root);
            DevCheatUI.CreateLabel("LevelLabel", levelRow, "레벨", 32f, DevCheatUI.MutedColor);
            var levelField = DevCheatUI.CreateNumberField(levelRow, panel.DiceLevel.ToString());
            levelField.onValueChanged.AddListener(v => panel.DiceLevel = ParseInt(v, panel.DiceLevel));

            RectTransform actionRow = DevCheatUI.CreateRow(root);
            DevCheatUI.CreateButton("SetLevel", actionRow, "레벨 적용", DevCheatUI.ButtonColor, () =>
            {
                DiceType type = Pick(types, panel.DiceIndex);
                DiceLevelManager.Instance?.SetLevel(type, Mathf.Clamp(panel.DiceLevel, 1, Define.MaxDiceLevel));
                panel.Rebuild();
            });
            DevCheatUI.CreateButton("Unlock", actionRow, "보유 처리", DevCheatUI.ButtonColor, () =>
            {
                DiceOwnershipManager.Instance?.GrantFromContent(Pick(types, panel.DiceIndex), null);
                panel.Rebuild();
            });

            DevCheatUI.CreateButton("UnlockAll", root, "전부 보유 처리", DevCheatUI.AccentColor, () =>
            {
                for (int i = 0; i < types.Length; i++)
                    DiceOwnershipManager.Instance?.GrantFromContent(types[i], null);

                panel.Rebuild();
            });

            DevCheatUI.CreateSectionTitle(root, "전투 중 소환");
            DevCheatUI.CreateHelpText(root, "빈 슬롯에 바로 꽂는다. 관리 단계/웨이브 어디서나 된다.");

            RectTransform starRow = DevCheatUI.CreateRow(root);
            DevCheatUI.CreateLabel("StarLabel", starRow, "성급", 32f, DevCheatUI.MutedColor);
            var starField = DevCheatUI.CreateNumberField(starRow, panel.DiceStar.ToString());
            starField.onValueChanged.AddListener(v => panel.DiceStar = ParseInt(v, panel.DiceStar));

            Button summon = DevCheatUI.CreateButton("Summon", root, "선택한 다이스 소환",
                DevCheatUI.ButtonColor, () => Summon(Pick(types, panel.DiceIndex), panel.DiceStar));
            DevCheatPanel.MarkBattleOnly(summon);
        }

        /// <summary>
        /// 보드에 꽂는다. 예전 치트의 <c>SummonSelectedDice</c> 와 같은 순서다 —
        /// 재고(<c>DiceStars</c>)를 먼저 올리고 보드를 바꿔야 둘이 어긋나지 않는다.
        /// </summary>
        private static void Summon(DiceType type, int star)
        {
            IBattleRefs battle = GameContainer.Battle;
            if (battle == null || !battle.IsActive)
                return;

            int slot = FindEmptySlot(battle);
            if (slot < 0)
            {
                Debug.LogWarning("[Dev] 빈 슬롯이 없다.");
                return;
            }

            int clamped = Mathf.Clamp(star, 1, MergeSystem.MaxStar);
            battle.DiceStars.OnDiceSpawn(type, clamped);
            battle.Board.SpawnDice(type, clamped, slot);

            RunHistoryManager.Instance?.RecordSummon(
                type, clamped, battle.Game.CurrentWaveIndex, 0, battle.Summon.currentSP);
        }

        private static int FindEmptySlot(IBattleRefs battle)
        {
            for (int i = 0; i < 24; i++)
            {
                if (battle.Board.GetDice(i) == null)
                    return i;
            }

            return -1;
        }

        private static DiceType[] SelectableDiceTypes()
        {
            var all = (DiceType[])Enum.GetValues(typeof(DiceType));
            var list = new List<DiceType>(all.Length);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != DiceType.Max)
                    list.Add(all[i]);
            }

            return list.ToArray();
        }

        private static DiceType Pick(DiceType[] types, int index)
        {
            return types.Length == 0 ? DiceType.Normal : types[Mathf.Clamp(index, 0, types.Length - 1)];
        }

        // ── 전투 ───────────────────────────────────────────────────────

        private static void BuildBattle(DevCheatPanel panel, Transform root)
        {
            DevCheatUI.CreateSectionTitle(root, "전투");

            if (!DevCheatPanel.IsBattle)
            {
                DevCheatUI.CreateHelpText(root,
                    "전투 밖이다. 아래 항목은 BattleScene 에서만 동작한다 - 모드 탭의 '전투로' 로 들어갈 수 있다.");
            }

            RectTransform hpRow = DevCheatUI.CreateRow(root);
            DevCheatUI.CreateLabel("HpLabel", hpRow, "몬스터 HP", 32f, DevCheatUI.MutedColor);
            var hpField = DevCheatUI.CreateNumberField(hpRow, panel.MonsterHp.ToString());
            hpField.onValueChanged.AddListener(v => panel.MonsterHp = ParseInt(v, panel.MonsterHp));

            Button setHp = DevCheatUI.CreateButton("SetHp", root, "지금 나온 몬스터 HP 설정",
                DevCheatUI.ButtonColor, () => SetMonsterHp(panel.MonsterHp));
            DevCheatPanel.MarkBattleOnly(setHp);

            Button kill = DevCheatUI.CreateButton("Kill", root, "지금 나온 몬스터 전부 처치",
                DevCheatUI.ButtonColor, KillAllMonsters);
            DevCheatPanel.MarkBattleOnly(kill);

            DevCheatUI.CreateSectionTitle(root, "벽");
            DevCheatUI.CreateToggleButton(
                "Invincible", root,
                DevCheatFlags.WallInvincible ? "벽 무적 : 켬" : "벽 무적 : 끔",
                DevCheatFlags.WallInvincible,
                () =>
                {
                    DevCheatFlags.WallInvincible = !DevCheatFlags.WallInvincible;
                    panel.Rebuild();
                });

            // 무적 토글은 전투 밖에서 미리 켜 둘 수 있다 — 값이 static 이라 전투가 시작되면
            // 그대로 적용된다. 그래서 여기는 MarkBattleOnly 를 걸지 않는다.
            DevCheatUI.CreateHelpText(root, "무적은 전투 밖에서 미리 켜 둘 수 있다. 다음 판에 그대로 적용된다.");
        }

        private static void SetMonsterHp(int hp)
        {
            IBattleRefs battle = GameContainer.Battle;
            if (battle == null || !battle.IsActive)
                return;

            int clamped = Mathf.Max(1, hp);
            List<Monster> monsters = battle.Monsters.activeMonsters;

            for (int i = 0; i < monsters.Count; i++)
            {
                Monster monster = monsters[i];
                if (monster != null && monster.gameObject.activeInHierarchy)
                    monster.SetHp(clamped);
            }
        }

        private static void KillAllMonsters()
        {
            IBattleRefs battle = GameContainer.Battle;
            if (battle == null || !battle.IsActive)
                return;

            // 목록을 복사하고 돈다. TakeDamage 가 죽으면서 원본에서 자기를 빼기 때문에
            // 그대로 순회하면 절반만 죽는다.
            var snapshot = new List<Monster>(battle.Monsters.activeMonsters);
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i] != null)
                    snapshot[i].TakeDamage(int.MaxValue);
            }
        }

        // ── 진행 ───────────────────────────────────────────────────────

        private static void BuildProgress(DevCheatPanel panel, Transform root)
        {
            DevCheatUI.CreateSectionTitle(root, "무한의 탑");

            RectTransform floorRow = DevCheatUI.CreateRow(root);
            DevCheatUI.CreateLabel("FloorLabel", floorRow, "클리어 층", 32f, DevCheatUI.MutedColor);
            var floorField = DevCheatUI.CreateNumberField(floorRow, panel.TowerFloor.ToString());
            floorField.onValueChanged.AddListener(v => panel.TowerFloor = ParseInt(v, panel.TowerFloor));

            DevCheatUI.CreateButton("SetFloor", root, "그 층까지 깬 것으로", DevCheatUI.ButtonColor, () =>
            {
                TowerProgressCheat.SetClearedFloor(Mathf.Max(0, panel.TowerFloor));
                panel.Rebuild();
            });
            DevCheatUI.CreateHelpText(root, "다이스 해금은 층 번호의 함수라 따로 켤 것이 없다 - 같이 열린다.");

            DevCheatUI.CreateSectionTitle(root, "시즌 패스");
            DevCheatUI.CreateHelpText(root,
                "포인트는 쓴 고기다. 여기서는 고기를 쓰지 않고 바로 넣는다.");

            RectTransform passRow = DevCheatUI.CreateRow(root);
            DevCheatUI.CreateButton("Pass1", passRow, "+1레벨", DevCheatUI.ButtonColor, () =>
            {
                SeasonPassManager.Instance?.DevAddPoints(
                    SeasonPassDatabaseProvider.Database.PointsPerLevel);
                panel.Rebuild();
            });
            DevCheatUI.CreateButton("Pass10", passRow, "+10레벨", DevCheatUI.ButtonColor, () =>
            {
                SeasonPassManager.Instance?.DevAddPoints(
                    SeasonPassDatabaseProvider.Database.PointsPerLevel * 10);
                panel.Rebuild();
            });

            RectTransform passRow2 = DevCheatUI.CreateRow(root);
            DevCheatUI.CreateButton("PassPremium", passRow2, "프리미엄 토글", DevCheatUI.ButtonColor, () =>
            {
                SeasonPassManager pass = SeasonPassManager.Instance;
                if (pass != null)
                {
                    pass.DevSetPremium(!pass.PremiumUnlocked);
                    panel.Rebuild();
                }
            });
            DevCheatUI.CreateButton("PassReset", passRow2, "시즌 초기화", DevCheatUI.DangerColor, () =>
            {
                SeasonPassManager.Instance?.DevResetSeason();
                panel.Rebuild();
            });

            DevCheatUI.CreateSectionTitle(root, "미션/업적");
            DevCheatUI.CreateHelpText(root, "재화/스테이지 진행도는 건드리지 않는다. 미션 기록만 비운다.");

            RectTransform missionRow = DevCheatUI.CreateRow(root);
            DevCheatUI.CreateButton("WipeMission", missionRow, "전부 지우기", DevCheatUI.DangerColor,
                () => { MissionCheat.ResetAll(); panel.Rebuild(); });
            DevCheatUI.CreateButton("NewDay", missionRow, "오늘치만 (자정 흉내)", DevCheatUI.ButtonColor,
                () => { MissionCheat.ResetToday(); panel.Rebuild(); });
        }

        // ── 시간 ───────────────────────────────────────────────────────

        private static void BuildTime(DevCheatPanel panel, Transform root)
        {
            DevCheatUI.CreateSectionTitle(root, "방치 보상 당기기");
            DevCheatUI.CreateHelpText(root,
                "기기 시계를 돌리지 않는다. 방치 타이머의 시작점만 과거로 민다 - 시계를 돌리면 상점 일일 리셋까지 같이 움직인다.");

            RectTransform row = DevCheatUI.CreateRow(root);
            DevCheatUI.CreateButton("Idle2", row, "+2시간", DevCheatUI.ButtonColor,
                () => AdvanceIdle(TimeSpan.FromHours(2)));
            DevCheatUI.CreateButton("Idle8", row, "+8시간", DevCheatUI.ButtonColor,
                () => AdvanceIdle(TimeSpan.FromHours(8)));
            DevCheatUI.CreateButton("Idle24", row, "+24시간", DevCheatUI.ButtonColor,
                () => AdvanceIdle(TimeSpan.FromHours(24)));

            DevCheatUI.CreateSectionTitle(root, "일일 리셋");
            DevCheatUI.CreateHelpText(root,
                "미션의 '오늘치' 를 비워 자정이 지난 것처럼 만든다. 상점 일일 기록은 그대로다.");
            DevCheatUI.CreateButton("NewDay", root, "미션 자정 흉내", DevCheatUI.ButtonColor,
                () => { MissionCheat.ResetToday(); panel.Rebuild(); });
        }

        private static void AdvanceIdle(TimeSpan amount)
        {
            IdleRewardManager manager = IdleRewardManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[Dev] IdleRewardManager 가 없다.");
                return;
            }

            manager.DevAdvanceTime(amount);
        }

        // ── 모드 ───────────────────────────────────────────────────────

        private static void BuildMode(DevCheatPanel panel, Transform root)
        {
            DevCheatUI.CreateSectionTitle(root, "씬 이동");

            RectTransform sceneRow = DevCheatUI.CreateRow(root);
            DevCheatUI.CreateButton("Title", sceneRow, "타이틀", DevCheatUI.ButtonColor,
                () => SceneFlowManager.LoadTitle());
            DevCheatUI.CreateButton("Lobby", sceneRow, "로비", DevCheatUI.ButtonColor,
                () => SceneFlowManager.LoadLobby());
            DevCheatUI.CreateButton("Battle", sceneRow, "전투로", DevCheatUI.ButtonColor,
                () => SceneFlowManager.LoadBattle());

            DevCheatUI.CreateSectionTitle(root, "세이브");
            DevCheatUI.CreateButton("Save", root, "즉시 저장", DevCheatUI.ButtonColor,
                () => GameContainer.SaveService?.SaveAll());

            DevCheatUI.CreateHelpText(root,
                "아래는 되돌릴 수 없다. 지운 뒤에는 매니저가 들고 있는 값이 다시 저장되지 않도록 바로 타이틀로 갈 것.");
            DevCheatUI.CreateButton("WipeSave", root, "세이브 전부 지우기", DevCheatUI.DangerColor,
                () => SaveResetCheat.WipeAll());
        }

        // ── 공통 ───────────────────────────────────────────────────────

        /// <summary>
        /// 입력칸의 글자를 수로 읽는다. <b>읽히지 않으면 이전 값을 지킨다</b> —
        /// 지우는 도중(빈 문자열)에 0 으로 떨어지면 다시 칠 때마다 앞자리가 사라진다.
        /// </summary>
        private static int ParseInt(string text, int fallback)
        {
            return int.TryParse(text, out int value) ? value : fallback;
        }
    }
}
#endif
