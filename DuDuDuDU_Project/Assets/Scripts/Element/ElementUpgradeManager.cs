using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OJ.DI;
using OJ.Dice;
using OJ.Point;
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

        void Awake()
        {
            ResetAll(false);
            BindUI();
            RefreshCoinUI();
        }

        private void OnDestroy()
        {
            UnbindUI();

        }

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
        }

        public int GetNextUpgradeCost(ElementType elementType)
        {
            return GetLevel(elementType) + 1;
        }

        public bool TryLevelUp(ElementType elementType)
        {
            if (elementType == ElementType.Max || PointManager.Instance == null)
                return false;

            // <b>관리 단계에서만 올린다.</b> 소환(UIDiceSummonSystem)·머지(MergeSystem)·
            // 진화와 교환(UIBattleDiceDetailPanel)이 전부 이 규칙을 지키는데 속성강화만
            // 빠져 있었다 — 같은 강화석을 쓰면서 혼자만 전투 중에 열려 있었다는 뜻이다.
            //
            // <b>버튼을 숨기는 것만으로는 부족하다.</b> 그건 프리팹 한 번 잘못 만지면
            // 사라지는 규칙이고, 규칙이 사라진 사실은 화면에 안 나타난다. 값을 실제로
            // 깎는 자리에서 막아야 한다.
            if (battle.Game.inGameState != InGameState.Setting)
                return false;

            int cost = GetNextUpgradeCost(elementType);
            if (!PointManager.Instance.TrySpend(PointType.BattleEnhanceStone, cost))
                return false;

            SetLevel(elementType, GetLevel(elementType) + 1);
            return true;
        }

        public void ResetRunState()
        {
            ResetAll();

            if (PointManager.Instance != null)
                PointManager.Instance.Set(PointType.BattleEnhanceStone, 0);
            else
                RefreshCoinUI();
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

            return 1f + (totalLevel * 0.1f);
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

            if (PointManager.Instance != null)
            {
                PointManager.Instance.OnPointChanged -= OnPointChanged;
                PointManager.Instance.OnPointChanged += OnPointChanged;
            }
        }

        private void UnbindUI()
        {
            if (ElementUpgrade != null)
                ElementUpgrade.onClick.RemoveListener(OnClickElementUpgrade);

            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= OnPointChanged;
        }

        /// <summary>
        /// 카탈로그에서 꺼내 띄운다. (10.4)
        ///
        /// 예전에는 씬 인스턴스를 <c>[SerializeField]</c> 로 직접 가리켰고, 그 참조가 <c>None</c> 이면
        /// <c>?.</c> 가 그대로 삼켜 <b>아무 로그 없이</b> 버튼이 먹통이 됐다.
        /// <see cref="UIService"/> 는 못 열면 사유를 로그로 남긴다.
        /// </summary>
        private void OnClickElementUpgrade()
        {
            GameContainer.UI?.Show<UIElementUpgradePanel>();
        }

        private void OnPointChanged(PointType pointType, int value)
        {
            if (pointType != PointType.BattleEnhanceStone)
                return;

            RefreshCoinUI();
        }

        private void RefreshCoinUI()
        {
            if (coinAmountText != null)
                coinAmountText.SetText("{0}", PointManager.Instance != null ? PointManager.Instance.Get(PointType.BattleEnhanceStone) : 0);

            if (coinIconImage != null && StaticResource.Instance != null && StaticResource.Instance.PointMetadataDatabase != null)
            {
                PointMetadataDatabase.PointMetadata metadata = StaticResource.Instance.PointMetadataDatabase.Get(PointType.BattleEnhanceStone);
                coinIconImage.sprite = metadata != null ? metadata.icon : null;
            }
        }
    }
}
