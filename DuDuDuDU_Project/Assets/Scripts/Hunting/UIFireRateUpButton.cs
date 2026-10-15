using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Core;
using OJ.DI;
using OJ.Dice;
using OJ.Point;

namespace OJ.Hunting
{
    /// <summary>
    /// 공속 UP 버튼(테스트 모드). 인게임 우하단 <b>삭제 버튼 자리</b>를 차지한다.
    ///
    /// <b>씬을 고치지 않고 런타임에 만든다.</b> <c>.unity</c> 를 손으로 편집하지 않는 규칙이라,
    /// 삭제 버튼을 복제해 같은 자리·같은 크기·같은 배경을 물려받고 원본은 끈다.
    /// 그래서 이 모드가 켜져 있는 동안은 <b>다이스 삭제가 없다</b>.
    ///
    /// 컴포넌트가 <c>AddComponent</c> 로 태어나 <c>[Inject]</c> 가 안 채워지므로
    /// 창구는 <see cref="Create"/> 가 직접 넘긴다.
    /// </summary>
    public sealed class UIFireRateUpButton : MonoBehaviour
    {
        private IBattleRefs battle;
        private Button button;
        private Image background;
        private TMP_Text label;

        private static readonly Color EnabledColor = new Color(0.20f, 0.55f, 0.95f, 1f);
        private static readonly Color DisabledColor = new Color(0.30f, 0.32f, 0.37f, 1f);

        /// <summary>삭제 버튼을 복제해 공속 UP 버튼을 띄운다. 삭제 버튼이 없으면 아무 일도 하지 않는다.</summary>
        public static void Create(IBattleRefs battle)
        {
            UIRemoveDice template = FindFirstObjectByType<UIRemoveDice>(FindObjectsInactive.Include);
            if (template == null)
            {
                Debug.LogWarning("[공속UP] 삭제 버튼을 찾지 못해 버튼을 만들지 않았다.");
                return;
            }

            GameObject clone = Instantiate(template.gameObject, template.transform.parent);
            clone.name = "FireRateUp";

            // 복제본이 삭제 기능을 물려받으면 안 된다.
            Destroy(clone.GetComponent<UIRemoveDice>());

            // 삭제 아이콘은 가린다. 배경만 쓴다.
            foreach (Transform child in clone.transform)
                child.gameObject.SetActive(false);

            template.gameObject.SetActive(false);

            var view = clone.AddComponent<UIFireRateUpButton>();
            view.Init(battle);
        }

        private void Init(IBattleRefs refs)
        {
            battle = refs;

            background = GetComponent<Image>();
            if (background == null)
                background = gameObject.AddComponent<Image>();

            button = gameObject.GetComponent<Button>();
            if (button == null)
                button = gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(OnClick);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)labelObject.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            label = labelObject.GetComponent<TextMeshProUGUI>();
            if (battle.Summon != null && battle.Summon.spText != null)
                label.font = battle.Summon.spText.font;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 30f;
            label.color = Color.white;
            label.raycastTarget = false;

            Refresh();
        }

        private void Update()
        {
            Refresh();
        }

        private void OnClick()
        {
            if (!CanUpgrade(out int cost))
                return;

            PlayerController player = battle.Player;
            if (!battle.BattlePoints.TrySpend(BattlePointType.SummonPoint, cost))
                return;

            player.SetFireRateLevel(player.FireRateLevel + 1);
            Refresh();
        }

        private bool CanUpgrade(out int cost)
        {
            cost = -1;

            PlayerController player = battle.Player;
            if (player == null || battle.Game == null || battle.BattlePoints == null)
                return false;

            cost = TestModeRules.FireRateUpgradeCost(player.FireRateLevel);
            if (cost < 0)
                return false;

            // 소환·머지와 같은 규칙이다 — 관리 단계(연속 웨이브 중에는 웨이브에서도).
            if (!battle.Game.CanManageBoard)
                return false;

            return battle.BattlePoints.Get(BattlePointType.SummonPoint) >= cost;
        }

        private void Refresh()
        {
            PlayerController player = battle != null ? battle.Player : null;
            if (player == null || label == null)
                return;

            int level = player.FireRateLevel;
            int cost = TestModeRules.FireRateUpgradeCost(level);

            if (cost < 0)
            {
                label.text = "공속 MAX\n" + player.fireRate.ToString("0.0#") + "초";
                button.interactable = false;
                background.color = DisabledColor;
                return;
            }

            float next = TestModeRules.FireRateDelay(level + 1, player.BaseFireRate);
            // 화살표 글자는 폰트에 없다(PORTING.md). 다음 단계의 딜레이만 적는다.
            label.text = "공속 UP " + next.ToString("0.0#") + "초\n" + cost + " SP";

            bool can = CanUpgrade(out _);
            button.interactable = can;
            background.color = can ? EnabledColor : DisabledColor;
        }
    }
}
