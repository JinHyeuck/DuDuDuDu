using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.UI;

namespace OJ.Hunting
{
    /// <summary>
    /// 보상 한 칸. 재화든 다이스든 <b>이 칸 하나로 그린다.</b>
    ///
    /// <b>왜 다이스용 칸을 따로 안 만드나.</b> 이 프리팹
    /// (<c>Assets/Prefab/Hunting/UIRewardElement.prefab</c>)은 이미 결과창·스테이지 보상·
    /// 별 보상·스테이지 결과 <b>네 화면이 함께 참조</b>한다. 여기에 능력을 더하면 네 곳이
    /// 한 번에 따라오지만, 새 칸을 만들면 네 화면에 각각 배선을 심어야 하고
    /// 그중 하나를 빠뜨리면 <b>그 화면에서만 다이스가 안 보이는</b> 상태가 된다.
    /// </summary>
    public class UIRewardElement : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text amountText;

        /// <summary>
        /// 아이콘 위에 겹치는 작은 아이콘. <b>이미 가진 다이스가 재화로 돌아온 경우</b>에만 켜진다.
        ///
        /// 환급 재화만 덩그러니 보여 주면 "무엇 때문에 받은 재화인지" 를 알 수 없다.
        /// 그래서 <b>다이스를 딤 처리해 밑에 깔고</b> 그 위에 재화 아이콘을 얹는다 —
        /// "이 다이스 대신 이걸 받았다" 가 그림 하나로 읽힌다.
        /// </summary>
        [SerializeField] private Image overlayIcon;

        /// <summary>이미 가진 다이스를 그릴 때의 색. 흑백에 가깝게 눌러 "이건 못 받았다" 를 말한다.</summary>
        private static readonly Color DimmedColor = new Color(0.42f, 0.42f, 0.46f, 1f);

        // ── 등장 연출 ────────────────────────────────────────────────

        /// <summary>튀어나오기 시작하는 배율.</summary>
        private const float AppearStartScale = 0.4f;

        private const float AppearDuration = 0.26f;

        /// <summary>수량이 0 에서 목표까지 올라가는 시간(초).</summary>
        private const float CountDuration = 0.34f;

        /// <summary>
        /// 이 수를 넘어야 카운트업을 한다. <b>x1 이 0 에서 1 로 올라가는 것은 우스꽝스럽다</b> —
        /// 숫자가 도는 맛은 자릿수가 있을 때 난다.
        /// </summary>
        private const int MinCountUpAmount = 10;

        private CanvasGroup appearGroup;
        private int appearSequence;

        /// <summary>카운트업이 끝나면 써야 할 최종 문구. 중간에 끊겨도 이 값으로 맞춘다.</summary>
        private int pendingAmount;
        private string pendingFormat;

        public void Construct(Image icon, TMP_Text amount)
        {
            iconImage = icon;
            amountText = amount;
        }

        public void Bind(Sprite iconSprite, int amount)
        {
            Bind(iconSprite, amount, "{0:#,##0}");
        }

        /// <summary>재화 한 칸. 다이스로 쓰였던 칸이 재사용될 수 있어 <b>상태를 되돌린다.</b></summary>
        public void Bind(Sprite iconSprite, int amount, string amountFormat)
        {
            ResetVisual();

            if (iconImage != null)
                iconImage.sprite = iconSprite;

            if (amountText != null)
            {
                amountText.gameObject.SetActive(true);

                pendingAmount = amount;
                pendingFormat = string.IsNullOrEmpty(amountFormat) ? "{0:#,##0}" : amountFormat;
                WriteAmount(amount);
            }
        }

        private void WriteAmount(int value)
        {
            if (amountText == null)
                return;

            amountText.SetText(string.IsNullOrEmpty(pendingFormat) ? "{0:#,##0}" : pendingFormat, value);
        }

        /// <summary>
        /// 이 칸을 튀어나오게 한다. <paramref name="delay"/> 만큼 늦게 시작해서
        /// 칸들이 <b>하나씩</b> 나타나게 한다.
        ///
        /// <b>Bind 안에서 하지 않는 이유.</b> 이 칸은 네 화면이 함께 쓰는데, 목록을 채우는
        /// 시점과 창이 열리는 시점이 화면마다 다르다. 창이 스스로 "지금 보여 준다" 고
        /// 말하게 두면 그 차이를 각 화면이 알아서 정한다.
        /// </summary>
        public void PlayAppear(float delay)
        {
            if (!gameObject.activeInHierarchy)
                return;

            AppearAsync(++appearSequence, delay).Forget();
        }

        /// <summary>
        /// 연출을 건너뛰고 최종 상태로 못박는다. 창이 닫히거나 목록이 다시 채워질 때 쓴다.
        /// </summary>
        public void SkipAppear()
        {
            appearSequence++;

            transform.localScale = Vector3.one;

            if (appearGroup != null)
                appearGroup.alpha = 1f;

            WriteAmount(pendingAmount);
        }

        private async UniTaskVoid AppearAsync(int mine, float delay)
        {
            CanvasGroup group = EnsureAppearGroup();

            // 시작 상태를 이 프레임에 세운다. 한 프레임이라도 또렷하게 보이면
            // 칸이 깜빡였다가 다시 나타나는 것처럼 보인다.
            transform.localScale = Vector3.one * AppearStartScale;
            if (group != null)
                group.alpha = 0f;

            bool counts = pendingAmount >= MinCountUpAmount;
            if (counts)
                WriteAmount(0);

            float waited = 0f;
            while (waited < delay)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (this == null || mine != appearSequence)
                    return;

                waited += Time.unscaledDeltaTime;
            }

            float total = counts ? Mathf.Max(AppearDuration, CountDuration) : AppearDuration;
            float elapsed = 0f;

            while (elapsed < total)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (this == null || mine != appearSequence)
                    return;

                elapsed += Time.unscaledDeltaTime;

                float pop = Mathf.Clamp01(elapsed / AppearDuration);
                transform.localScale =
                    Vector3.one * Mathf.LerpUnclamped(AppearStartScale, 1f, UIEase.OutBack(pop));

                if (group != null)
                    group.alpha = UIEase.OutQuad(pop);

                if (counts)
                {
                    float c = Mathf.Clamp01(elapsed / CountDuration);
                    // 끝을 향해 느려진다. 마지막 자리가 천천히 맞춰져야 결과가 읽힌다.
                    WriteAmount(Mathf.RoundToInt(pendingAmount * UIEase.OutCubic(c)));
                }
            }

            if (this == null || mine != appearSequence)
                return;

            SkipAppear();
        }

        private CanvasGroup EnsureAppearGroup()
        {
            if (appearGroup != null)
                return appearGroup;

            appearGroup = GetComponent<CanvasGroup>();
            if (appearGroup == null)
                appearGroup = gameObject.AddComponent<CanvasGroup>();

            return appearGroup;
        }

        /// <summary>
        /// 처음 얻은 다이스. 개수는 언제나 하나라 숫자를 적지 않는다 —
        /// "x1" 은 알려 주는 것이 없고 아이콘만 좁힌다.
        /// </summary>
        public void BindDice(Sprite diceSprite)
        {
            ResetVisual();

            if (iconImage != null)
                iconImage.sprite = diceSprite;

            if (amountText != null)
                amountText.gameObject.SetActive(false);

            // 숫자가 없으니 카운트업도 없다. 남아 있던 앞 번 값이 새 다이스 칸에 찍히지
            // 않도록 여기서 지운다.
            pendingAmount = 0;
            pendingFormat = null;
        }

        /// <summary>
        /// 이미 가진 다이스라 재화로 돌아온 경우.
        /// 딤 처리한 다이스 + 그 위의 재화 아이콘 + 아래의 수량.
        /// </summary>
        public void BindDice(Sprite diceSprite, Sprite refundSprite, int refundAmount)
        {
            ResetVisual();

            if (iconImage != null)
            {
                iconImage.sprite = diceSprite;
                iconImage.color = DimmedColor;
            }

            if (overlayIcon != null)
            {
                overlayIcon.sprite = refundSprite;
                overlayIcon.gameObject.SetActive(true);
            }

            if (amountText != null)
            {
                amountText.gameObject.SetActive(true);

                pendingAmount = refundAmount;
                pendingFormat = "x{0:#,##0}";
                WriteAmount(refundAmount);
            }
        }

        /// <summary>
        /// 칸을 기본 상태로 되돌린다.
        ///
        /// <b>없으면 조용히 틀린다.</b> 이 칸들은 목록에서 <c>Instantiate</c> 후 재사용되므로,
        /// 앞 번에 딤 처리된 칸이 다음 번 재화 보상에 그대로 쓰이면 <b>골드가 회색으로</b> 뜬다.
        /// </summary>
        private void ResetVisual()
        {
            if (iconImage != null)
                iconImage.color = Color.white;

            if (overlayIcon != null)
                overlayIcon.gameObject.SetActive(false);
        }
    }
}
