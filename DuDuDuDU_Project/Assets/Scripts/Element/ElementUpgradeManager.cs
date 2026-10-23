using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OJ.Battle;
using OJ.Bounty;
using OJ.Core;
using OJ.DI;
using OJ.Dice;
using OJ.UI;
using OJ.Utils;
using VContainer;

namespace OJ.Element
{
    public class ElementUpgradeManager : MonoBehaviour
    {
        // 배틀 스코프가 채운다. inGameState 를 읽는 것 말고는 쓰지 않으므로 Awake 에서는
        // 건드리지 않는다 — 창구는 모든 Awake 뒤에 채워진다.
        [Inject] private IBattleRefs battle;

        [SerializeField] private Button ElementUpgrade;
        [SerializeField] private Image coinIconImage;
        [SerializeField] private TMP_Text coinAmountText;

        private readonly Dictionary<ElementType, int> levels = new Dictionary<ElementType, int>();

        public event Action<ElementType, int> OnElementLevelChanged;

        private static readonly Color PayableColor = Color.white;
        private static readonly Color CannotPayColor = new Color(1f, 0.35f, 0.3f, 1f);
        private static readonly Color BonusColor = new Color(1f, 0.87f, 0.35f, 1f);

        private UIEnhanceStoneChip chip;
        private TMP_Text bonusText;

        void Awake()
        {
            // <b>여기서 battle 을 건드리면 안 된다.</b> VContainer 의 주입은 Awake 뒤에
            // 붙으므로 이 시점의 battle 은 null 이다 — 예전에 BindUI 가 여기 있어서
            // battle.BattlePoints 에서 NullReferenceException 이 났다.
            // (같은 함정을 UIBoard·MonsterSpawner 주석이 먼저 적어 두었다.)
            //
            // ResetAll 은 levels 딕셔너리만 채우므로 주입과 무관하다.
            ResetAll(false);
        }

        private void Start()
        {
            BuildUI();
            BindUI();
            RefreshUI();
        }

        private void OnDestroy()
        {
            UnbindUI();

        }

        /// <summary>강화 버튼(보드 왼쪽 "강화" 칸). 튜토리얼이 그 자리만 열어 두고, 연출이 이 칸을 튕긴다.</summary>
        public RectTransform UpgradeButtonRect => ElementUpgrade != null ? ElementUpgrade.transform as RectTransform : null;

        /// <summary>보유 마석 수 칸. 마석이 날아와 닿는 자리다. 칸을 못 만들었으면 null.</summary>
        public RectTransform StoneRect => chip != null ? chip.Rect : null;

        /// <summary>
        /// 날아오는 마석을 화면 숫자에 넣지 않고 쥐어 둔다. 아이콘이 닿을 때 <see cref="ReleaseStone"/> 으로 푼다.
        /// 데이터는 이미 올랐고, 바뀌는 것은 보이는 숫자뿐이다.
        /// </summary>
        public void HoldStone(int amount)
        {
            if (chip != null)
                chip.Hold(amount);
        }

        public void ReleaseStone(int amount)
        {
            if (chip != null)
                chip.Release(amount);
        }

        /// <summary>
        /// 지금까지 강화한 횟수. 전 속성이 같은 레벨로 오르므로 가장 높은 레벨이 곧 횟수다.
        /// 따로 세지 않는다 — 되돌리기가 레벨을 복원해도 가격이 어긋날 곳이 없다.
        /// </summary>
        public int UpgradeCount
        {
            get
            {
                int count = 0;
                foreach (int level in levels.Values)
                {
                    if (level > count)
                        count = level;
                }

                return count;
            }
        }

        /// <summary>다음 강화에 드는 마석.</summary>
        public int NextUpgradeCost => ElementUpgradeRules.NextCost(UpgradeCount);

        public int GetLevel(ElementType elementType)
        {
            if (elementType == ElementType.Max)
                return 0;

            return levels.TryGetValue(elementType, out int level) ? Mathf.Max(0, level) : 0;
        }

        public void SetLevel(ElementType elementType, int level)
        {
            if (elementType == ElementType.Max)
                return;

            int clamped = Mathf.Max(0, level);
            levels[elementType] = clamped;
            OnElementLevelChanged?.Invoke(elementType, clamped);
            RefreshUI();
        }

        /// <summary>
        /// 모든 속성을 한 단계씩 올린다(한 단계 = 위력 <see cref="ElementUpgradeRules.BonusPercentPerUpgrade"/>%).
        /// 마석이 모자라거나 지금 보드를 못 만지는 단계면 아무것도 바꾸지 않고 false.
        /// </summary>
        public bool TryUpgradeAll()
        {
            if (battle == null || battle.Game == null || battle.BattlePoints == null)
                return false;

            // <b>관리 단계에서만 올린다.</b> 소환(UIDiceSummonSystem)·머지(MergeSystem)·
            // 진화와 교환(UIBattleDiceDetailPanel)이 전부 이 규칙을 지키는데 속성강화만
            // 빠져 있었다 — 같은 강화석을 쓰면서 혼자만 전투 중에 열려 있었다는 뜻이다.
            //
            // <b>버튼을 숨기는 것만으로는 부족하다.</b> 그건 프리팹 한 번 잘못 만지면
            // 사라지는 규칙이고, 규칙이 사라진 사실은 화면에 안 나타난다. 값을 실제로
            // 깎는 자리에서 막아야 한다.
            if (!battle.Game.CanManageBoard)
                return false;

            if (!battle.BattlePoints.TrySpend(BattlePointType.EnhanceStone, NextUpgradeCost))
                return false;

            int next = UpgradeCount + 1;
            foreach (ElementType elementType in Enum.GetValues(typeof(ElementType)))
            {
                if (elementType != ElementType.Max)
                    SetLevel(elementType, next);
            }

            return true;
        }

        /// <summary>
        /// 판을 새로 시작할 때 부른다.
        ///
        /// <b>강화석을 여기서 0 으로 밀던 줄이 사라졌다.</b> 이제 그것은
        /// <c>RunState.BeginRun</c> 의 몫이다 — 런 범위 값을 한 곳에서 세우지 않으면
        /// 하나만 빠뜨려도 이전 판 값이 다음 판으로 새는데, 그 사고를
        /// <c>RunStateSnapshotTests</c> 가 리플렉션으로 막고 있다.
        /// 이 매니저가 자기 몫(원소 레벨)만 보는 것이 맞다.
        /// </summary>
        public void ResetRunState()
        {
            ResetAll();

            // 날아오던 마석이 있었다면 그 연출은 새 판에서 의미가 없다. 쥔 채로 두면 숫자가 영영 모자란다.
            if (chip != null)
                chip.ReleaseAll();

            RefreshUI();
        }

        public void ResetAll(bool notify = true)
        {
            foreach (ElementType elementType in Enum.GetValues(typeof(ElementType)))
            {
                if (elementType == ElementType.Max)
                    continue;

                levels[elementType] = 0;
                if (notify)
                    OnElementLevelChanged?.Invoke(elementType, 0);
            }
        }

        public float GetTotalBonusMultiplier(DiceType diceType)
        {
            DiceMetaDataDatabase.DiceMeta meta = DiceMetaDataProvider.GetMeta(diceType);
            if (meta == null || meta.elementType == null || meta.elementType.Length == 0)
                return 1f;

            int totalLevel = 0;
            for (int i = 0; i < meta.elementType.Length; i++)
            {
                ElementType elementType = meta.elementType[i];
                if (elementType == ElementType.Max)
                    continue;

                totalLevel += GetLevel(elementType);
            }

            return 1f + (totalLevel * (ElementUpgradeRules.BonusPercentPerUpgrade / 100f));
        }

        public float GetTotalBonusPercent(DiceType diceType)
        {
            return Mathf.Max(0f, (GetTotalBonusMultiplier(diceType) - 1f) * 100f);
        }

        /// <summary>웨이브 중 강화 버튼의 투명도. 눌리는 것과 확실히 구분되는 선이다.</summary>
        private const float DimmedAlpha = 0.45f;

        private CanvasGroup upgradeButtonGroup;

        /// <summary>
        /// 전투 상태에 맞춰 강화 버튼을 <b>딤드</b> 처리한다.
        /// <see cref="GameManager.ChangeState"/> 가 부른다 — 상태를 아는 곳이 거기 하나라서다.
        /// 여기서 매 프레임 <c>inGameState</c> 를 들여다보면 다른 전투 UI 와 규칙이 갈라지고,
        /// 갈라진 것은 나중에 한쪽만 고쳐진다.
        ///
        /// <b>끄지 않고 흐리게만 둔다.</b> <c>SetActive(false)</c> 로 치우면 웨이브 중에는
        /// 버튼이 있던 자리가 빈 칸이 되어 <b>기능이 사라진 것처럼</b> 보인다. 흐리게 두면
        /// "지금은 못 쓴다"가 화면에 남아, 관리 단계로 돌아가면 다시 쓸 수 있다는 것까지
        /// 같이 읽힌다. 레이아웃도 웨이브 경계마다 들썩이지 않는다.
        ///
        /// <c>CanvasGroup</c> 을 쓰는 것은 버튼 아래에 아이콘·글자가 몇 개나 붙어 있는지
        /// 이 코드가 모르기 때문이다. 하나씩 색을 만지면 씬에서 자식이 하나 늘어날 때마다
        /// 조용히 어긋난다. 런타임에 붙이는 방식은 <c>UIDice</c> 가 쓰는 것과 같다.
        /// </summary>
        public void SetUpgradeUIAvailable(bool available)
        {
            if (ElementUpgrade == null)
                return;

            if (upgradeButtonGroup == null)
            {
                upgradeButtonGroup = ElementUpgrade.GetComponent<CanvasGroup>();
                if (upgradeButtonGroup == null)
                    upgradeButtonGroup = ElementUpgrade.gameObject.AddComponent<CanvasGroup>();
            }

            upgradeButtonGroup.alpha = available ? 1f : DimmedAlpha;

            // 흐리게만 두면 <b>여전히 눌린다.</b> 보이는 것과 눌리는 것을 따로 꺼야 하고,
            // blocksRaycasts 까지 꺼야 그 아래(보드)가 정상적으로 클릭을 받는다.
            upgradeButtonGroup.interactable = available;
            upgradeButtonGroup.blocksRaycasts = available;

            // 예전에 SetActive(false) 로 치웠던 흔적이 남아 있으면 딤드가 보이지 않는다.
            // 껐던 오브젝트는 아무도 다시 켜 주지 않으므로 여기서 되살린다.
            if (!ElementUpgrade.gameObject.activeSelf)
                ElementUpgrade.gameObject.SetActive(true);
        }

        private void BindUI()
        {
            if (ElementUpgrade != null)
            {
                ElementUpgrade.onClick.RemoveListener(OnClickElementUpgrade);
                ElementUpgrade.onClick.AddListener(OnClickElementUpgrade);
            }

            // battle 이 아직 없을 수 있다 — 주입 전에 파괴되거나, 이 컴포넌트가
            // 스코프 밖에서 찍힌 경우다. 그때는 이벤트를 걸 대상 자체가 없다.
            if (battle != null && battle.BattlePoints != null)
            {
                battle.BattlePoints.OnBattlePointChanged -= OnBattlePointChanged;
                battle.BattlePoints.OnBattlePointChanged += OnBattlePointChanged;
            }
        }

        private void UnbindUI()
        {
            if (ElementUpgrade != null)
                ElementUpgrade.onClick.RemoveListener(OnClickElementUpgrade);

            // OnDestroy 는 Start 전에도 돌 수 있다(씬이 바로 바뀌는 경우).
            if (battle != null && battle.BattlePoints != null)
                battle.BattlePoints.OnBattlePointChanged -= OnBattlePointChanged;
        }

        /// <summary>
        /// 누르면 곧바로 모든 속성을 한 단계 올린다. 팝업을 거치지 않는다.
        /// 성공하면 버튼이 튕기며 "+10%" 가 뜨고, 마석이 모자라면 보유 마석 칸이 흔들린다.
        /// 지금 보드를 못 만지는 때는 버튼이 눌리지 않으므로(<see cref="SetUpgradeUIAvailable"/>) 여기까지 오지 않는다.
        /// </summary>
        private void OnClickElementUpgrade()
        {
            if (TryUpgradeAll())
            {
                UIRewardFly.Punch(UpgradeButtonRect);
                UIRewardFly.FloatText(UpgradeButtonRect, "+" + ElementUpgradeRules.BonusPercentPerUpgrade + "%",
                    coinAmountText, BonusColor);
                return;
            }

            if (chip != null && battle != null && battle.BattlePoints != null &&
                battle.BattlePoints.Get(BattlePointType.EnhanceStone) < NextUpgradeCost)
            {
                chip.Shake();
            }
        }

        // 종류를 거르지 않는다. 창구가 전투 재화만 내보내고 그중 강화석 말고는
        // 이 UI 가 신경 쓸 것이 없어서, 걸러도 결과가 같고 안 걸러야 나중에
        // 전투 재화가 늘었을 때 이 줄이 막지 않는다.
        private void OnBattlePointChanged(BattlePointType battlePointType, int value)
        {
            RefreshUI();
        }

        /// <summary>
        /// 강화 버튼 칸에 다음 강화의 값과 지금까지 쌓인 위력을 붙이고, 보유 마석 칸을 만든다.
        /// 씬을 고치지 않고 코드로 얹는다(<c>UpgradeRedDot</c> 와 같은 방식).
        /// </summary>
        private void BuildUI()
        {
            RectTransform button = UpgradeButtonRect;
            if (button == null)
                return;

            if (coinAmountText != null)
            {
                // 값이 세 자리(25, 30 … 100 이상)로 늘어도 칸 안에 들어오게 한다.
                coinAmountText.textWrappingMode = TextWrappingModes.NoWrap;
                coinAmountText.enableAutoSizing = true;
                coinAmountText.fontSizeMax = coinAmountText.fontSize;
                coinAmountText.fontSizeMin = 20f;
            }

            if (coinAmountText != null && bonusText == null)
            {
                bonusText = UIBountyUIFactory.CreateText("Bonus", button, string.Empty, 24f,
                    TextAlignmentOptions.Center, BonusColor, coinAmountText.font, coinAmountText.fontSharedMaterial);
                UIBountyUIFactory.SetRect(bonusText.rectTransform, new Vector2(104f, 64f), new Vector2(0f, -124f));
                bonusText.raycastTarget = false;
                bonusText.textWrappingMode = TextWrappingModes.NoWrap;
            }

            // 칸의 자리는 화면 비율로 정해지므로 부모는 전투 HUD 캔버스의 뿌리여야 한다.
            Canvas canvas = button.GetComponentInParent<Canvas>();
            if (chip == null && coinAmountText != null && canvas != null)
            {
                chip = UIEnhanceStoneChip.Create((RectTransform)canvas.rootCanvas.transform, coinAmountText,
                    BattlePointUtility.GetIcon(BattlePointType.EnhanceStone));
            }
        }

        private void RefreshUI()
        {
            int owned = battle != null && battle.BattlePoints != null
                ? battle.BattlePoints.Get(BattlePointType.EnhanceStone)
                : 0;

            if (chip != null)
                chip.SetOwned(owned);

            int cost = NextUpgradeCost;
            if (coinAmountText != null)
            {
                coinAmountText.SetText("{0}", cost);
                coinAmountText.color = owned >= cost ? PayableColor : CannotPayColor;
            }

            if (coinIconImage != null)
                coinIconImage.sprite = BattlePointUtility.GetIcon(BattlePointType.EnhanceStone);

            if (bonusText != null)
                bonusText.SetText("전 속성\n+{0}%", ElementUpgradeRules.TotalBonusPercent(UpgradeCount));
        }
    }
}
