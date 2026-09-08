using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using OJ.Point;

namespace OJ.Dice
{
    public class UIDiceGrowthItem : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image bgImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private GameObject canUpgradeDot;

        /// <summary>
        /// 미보유일 때 덮는 것(딤 + 자물쇠 + 언락 안내). <b>비어 있어도 동작은 맞다</b> —
        /// 아래 Refresh 가 강화 정보를 끄는 것으로 이미 구별이 되고, 이 참조는 그 위에
        /// 얹는 표시일 뿐이다. 프리팹을 굽기 전에도 화면이 거짓말을 하지 않아야 한다.
        /// </summary>
        [SerializeField] private GameObject lockedRoot;

        // 아래 셋은 <see cref="lockedRoot"/> 의 자식이라 보유하면 부모가 꺼지면서 같이 사라진다.
        // 따로 껐다 켤 필요가 없다.

        /// <summary>언락 재화 아이콘. 어떤 재화가 필요한지는 그림이 제일 빨리 말한다.</summary>
        [SerializeField] private Image unlockCostIcon;

        /// <summary>"보유 / 가격". <b>가격만 적으면</b> 얼마나 남았는지를 유저가 계산해야 한다.</summary>
        [SerializeField] private TMP_Text unlockCostText;

        /// <summary>컨텐츠 해금 조건 한 줄. 칸이 좁아 짧은 형태를 쓴다.</summary>
        [SerializeField] private TMP_Text unlockConditionText;
        [SerializeField] private Image scrollCountFillImage;
        [SerializeField] private TMP_Text scrollCountText;

        private DiceType diceType;
        private System.Action<DiceType> onClick;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        public void Bind(DiceType type, System.Action<DiceType> clickCallback)
        {
            diceType = type;
            onClick = clickCallback;
            Refresh();
        }

        public void Refresh()
        {
            var meta = DiceMetaDataProvider.GetMeta(diceType);
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(diceType) : 1;

            // 보유하지 않았으면 강화 정보를 전부 끈다. 레벨과 스크롤 진행도는 <b>보유한 뒤에나
            // 뜻이 있는 숫자</b>인데, 미보유 칸에 그대로 두면 "이미 쓰고 있는 다이스" 로 읽힌다.
            // 레벨 자체는 지우지 않는다 — 언락 전에 올려 둔 레벨은 실재하고, 그것을 버리는 것은
            // DiceLevelManager 주석이 말하는 되돌릴 수 없는 손실이다.
            DiceOwnershipManager ownership = DiceOwnershipManager.Instance;
            bool owned = ownership == null || ownership.IsOwned(diceType);

            if (lockedRoot != null)
                lockedRoot.SetActive(!owned);

            if (!owned)
            {
                if (iconImage != null)
                    iconImage.sprite = DiceMetaDataProvider.GetIcon(diceType);

                if (nameText != null)
                    nameText.SetText(meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : diceType.ToString());

                // 강화 줄은 비운다. 딤 밑에 깔려 어차피 안 읽히는데 값이 남아 있으면
                // <b>미보유 안내와 겹쳐 보인다</b>.
                if (levelText != null)
                    levelText.SetText(string.Empty);

                if (scrollCountText != null)
                    scrollCountText.SetText(string.Empty);

                if (scrollCountFillImage != null)
                    scrollCountFillImage.fillAmount = 0f;

                if (canUpgradeDot != null)
                    canUpgradeDot.SetActive(false);

                RefreshUnlockInfo();
                return;
            }

            // if (bgImage != null)
            //     bgImage.color = DiceMetaDataProvider.GetColor(diceType);

            if (iconImage != null)
                iconImage.sprite = DiceMetaDataProvider.GetIcon(diceType);

            if (nameText != null)
                nameText.SetText(meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : diceType.ToString());

            if (levelText != null)
                levelText.SetText("Lv.{0}", level);

            var cost = DiceLevelManager.Instance != null
                ? DiceLevelManager.Instance.GetNextUpgradeCost(diceType)
                : DiceMetaDataProvider.GetUpgradeCost(diceType, level);

            PointType scrollType = PointManager.ToScrollType(diceType);
            int ownedScroll = PointManager.Instance != null ? PointManager.Instance.Get(scrollType) : 0;
            int requiredScroll = Mathf.Max(0, cost.scrollCost);

            // <b>보유/필요 순서다.</b> 재화 표기는 프로젝트 전체가 이 순서를 쓴다 —
            // 앞이 "지금 얼마 있나", 뒤가 "얼마가 드는가".
            if (scrollCountText != null)
                scrollCountText.SetText("{0}/{1}", ownedScroll, requiredScroll);

            if (scrollCountFillImage != null)
            {
                float fill = requiredScroll <= 0 ? 1f : Mathf.Clamp01((float)ownedScroll / requiredScroll);
                scrollCountFillImage.fillAmount = fill;
            }

            if (canUpgradeDot != null)
            {
                int ownedGold = PointManager.Instance != null ? PointManager.Instance.Get(PointType.Gold) : 0;
                bool canUpgrade = ownedGold >= cost.goldCost && ownedScroll >= requiredScroll;
                canUpgradeDot.SetActive(canUpgrade);
            }
        }

        /// <summary>
        /// 자물쇠 아래에 필요 재화와 해금 조건을 적는다.
        ///
        /// <b>여기서 값을 다시 계산하지 않는다.</b> 가격은 <c>DiceUnlockDatabase</c>,
        /// 재화 종류는 <c>PointManager.ToScrollType</c>, 조건 문구는 <c>DiceUnlockText</c> 가
        /// 정본이다 — 칸이 자기 식을 하나 더 가지면 팝업과 다른 말을 하게 된다.
        /// </summary>
        private void RefreshUnlockInfo()
        {
            DiceUnlockDefinition definition = DiceUnlockDatabaseProvider.Database.Get(diceType);

            if (unlockCostIcon != null || unlockCostText != null)
            {
                PointType currency = DiceOwnershipManager.GetPriceCurrency(diceType);
                int price = definition != null ? definition.price : 0;
                int owned = PointManager.Instance != null ? PointManager.Instance.Get(currency) : 0;

                if (unlockCostIcon != null)
                    unlockCostIcon.sprite = PointRewardUtility.GetPointIcon(currency);

                if (unlockCostText != null)
                    unlockCostText.SetText("{0}/{1}", owned, price);
            }

            if (unlockConditionText != null)
            {
                string sources = DiceUnlockText.DescribeShortSources(definition);
                unlockConditionText.SetText(string.IsNullOrEmpty(sources) ? "재화로만 열림" : sources);
            }
        }

        private void HandleClick()
        {
            onClick?.Invoke(diceType);
        }

    }
}
