using System;
using UnityEngine;
using OJ.ContentUnlock;
using OJ.Core;
using OJ.Dice;
using OJ.Equipment;
using OJ.Point;

namespace OJ.Lobby
{
    /// <summary>
    /// 하단 탭(장비 · 다이스)에 "강화할 수 있다" 레드닷을 건다. <c>LobbySceneController</c> 가 코드로 붙인다 — 씬을 고치지 않는다.
    ///
    /// 0.5초마다 훑는다. 재화가 바뀔 때마다 이벤트로 받는 편이 정확하지만, 이벤트가 장비·다이스·골드·두루마리 네 갈래라 빠뜨리기 쉽고
    /// 훑는 비용이 (장비 6 + 다이스 15) 번의 비교뿐이라 싸다. <b>뉴비가 강화할 수 있는데 못 보는 것</b>이 이 컴포넌트가 막으려는 사고다.
    /// 아직 열리지 않은 탭에는 달지 않는다(잠긴 컨텐츠를 가리키지 않게).
    /// </summary>
    public sealed class LobbyUpgradeBadges : MonoBehaviour
    {
        private const float Interval = 0.5f;

        private float nextScan;
        private LobbyBottomBtn[] buttons;

        private void OnEnable()
        {
            nextScan = 0f;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScan)
                return;

            nextScan = Time.unscaledTime + Interval;

            if (buttons == null || buttons.Length == 0)
                buttons = FindObjectsByType<LobbyBottomBtn>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            bool equipment = AnyEquipmentUpgradable();
            bool dice = AnyDiceUpgradable();

            for (int i = 0; i < buttons.Length; i++)
            {
                LobbyBottomBtn button = buttons[i];
                if (button == null)
                    continue;

                if (button._tab == LobbyTab.Equipment)
                    button.SetUpgradeBadge(equipment && IsTabOpen(button._tab));
                else if (button._tab == LobbyTab.Bullet)
                    button.SetUpgradeBadge(dice && IsTabOpen(button._tab));
            }
        }

        private static bool IsTabOpen(LobbyTab tab)
        {
            LobbyContentGate gate = LobbyContentGate.Current;
            if (gate == null || !LobbyContentGate.TryGetContentOfTab(tab, out ContentId id))
                return true;

            return gate.IsAccessible(id);
        }

        private static bool AnyEquipmentUpgradable()
        {
            foreach (EquipmentType type in Enum.GetValues(typeof(EquipmentType)))
            {
                if (UIEquipmentPage.CanUpgradeEquipment(type))
                    return true;
            }

            return false;
        }

        private static bool AnyDiceUpgradable()
        {
            PointManager points = PointManager.Instance;
            DiceLevelManager levels = DiceLevelManager.Instance;
            DiceOwnershipManager ownership = DiceOwnershipManager.Instance;
            if (points == null || levels == null)
                return false;

            int gold = points.Get(PointType.Gold);
            foreach (DiceType type in Enum.GetValues(typeof(DiceType)))
            {
                if (type == DiceType.Max)
                    continue;

                if (ownership != null && !ownership.IsOwned(type))
                    continue;

                var cost = DiceMetaDataProvider.GetUpgradeCost(type, levels.GetLevel(type));
                if (cost.goldCost <= 0 && cost.scrollCost <= 0)
                    continue;

                if (UpgradeAffordability.CanUpgrade(levels.IsMaxLevel(type), gold,
                        points.Get(PointManager.ToScrollType(type)), cost.goldCost, cost.scrollCost))
                    return true;
            }

            return false;
        }
    }
}
