using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using OJ.Dice;

namespace OJ.Tower
{
    /// <summary>
    /// 편성 화면 후보 그리드의 칸 하나. 기획서 5.4 의 다섯 가지 상태를 전부 여기서 그린다.
    ///
    /// <b>짧은 탭은 언제나 선택/해제다.</b> 기획서 5.5 가 못 박은 규칙이고, 상세 보기는
    /// 길게 누르기로 뺐다 — 편성은 서른 칸을 훑으며 고르는 화면이라 탭의 뜻이
    /// 상황마다 달라지면 속도가 통째로 죽는다.
    /// </summary>
    public class UITowerDiceCell : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Image selectionEdge;

        /// <summary>
        /// 칸 바탕 두 장. 선택되면 노란 테두리 그림(Bg_Prs)으로 바꾼다 — 시안은 선택을
        /// 테두리 색으로만 말한다. 비어 있으면(옛 프리팹) 아래 <see cref="selectionEdge"/> 가 대신한다.
        /// </summary>
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite selectedSprite;

        /// <summary>
        /// 다이스의 <b>정체는 아이콘이 나른다.</b> 인게임 <c>UIDice</c>·
        /// <c>UIDiceGrowthItem</c> 이 그렇고, 탑만 글자로 적으면 같은 다이스가
        /// 화면마다 다르게 보인다. 아래 <see cref="label"/> 은 아이콘 밑의 이름이다.
        /// </summary>
        [SerializeField] private Image icon;

        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text starLabel;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private GameObject newBadge;
        [SerializeField] private Image checkMark;

        /// <summary>
        /// 이 칸이 맡은 다이스. <b>굽는 시점에 정해져 프리팹에 저장된다.</b>
        ///
        /// <c>[SerializeField]</c> 가 반드시 필요하다 — 없으면 Unity 가 저장하지 않아
        /// 프리팹을 다시 열었을 때 서른 칸이 전부 <c>Normal</c>(enum 기본값)이 되고,
        /// 그 화면은 "일반 다이스만 서른 개" 로 보인다. 컴파일도 굽기도 조용히 통과한다.
        /// </summary>
        [SerializeField] private DiceType diceType;

        [SerializeField] private int star = 1;

        private Action<DiceType, int> tapHandler;
        private Action<DiceType, int> holdHandler;

        private float pointerDownTime;
        private bool pointerDown;

        /// <summary>길게 누르기로 판정하는 시간(초).</summary>
        private const float HoldSeconds = 0.5f;

        public DiceType DiceType => diceType;
        public int Star => star;

        /// <summary>
        /// 칸을 채운다.
        /// </summary>
        /// <param name="unlocked">보유했는가. false 면 자물쇠가 뜨고 탭이 해금 안내로 바뀐다.</param>
        /// <param name="selected">지금 편성에 들어 있는가.</param>
        /// <param name="tierFull">이 계층이 꽉 찼는가. 기획서 5.4 의 "한도 도달 — 흐리게".</param>
        /// <param name="isNew">획득 직후인가. 기획서 5.4 의 NEW 배지.</param>
        public void Bind(
            DiceType diceType,
            int star,
            bool unlocked,
            bool selected,
            bool tierFull,
            bool isNew,
            Action<DiceType, int> onTap,
            Action<DiceType, int> onHold)
        {
            this.diceType = diceType;
            this.star = star;
            tapHandler = onTap;
            holdHandler = onHold;

            // <b>이 칸은 스스로 막지 않는다.</b> 잠긴 칸도 한도 도달 칸도 탭은 받고,
            // 무엇을 할지는 UITowerLoadoutDialog.OnCellTapped 이 정한다 — 거기서
            // 해금 조건이나 슬롯 규칙을 토스트로 알려 줘야 하고(기획서 5.4),
            // 그 판단에 필요한 것을 칸이 다 알고 있지는 않기 때문이다.

            if (label != null)
                label.SetText(TowerDiceText.NameOf(diceType));

            // 성급 표기는 인게임과 같은 "x4" 이고, 붙일지 말지도 인게임과 같은
            // ShowStarUI 로 정한다(TowerDiceText.StarOf 주석 참조).
            if (starLabel != null)
            {
                string starText = TowerDiceText.StarOf(diceType, star);
                starLabel.gameObject.SetActive(!string.IsNullOrEmpty(starText));
                starLabel.SetText(starText);
            }

            // 정체는 아이콘이 나른다. 스프라이트가 아직 없는 다이스는 아이콘을 끄고
            // 이름만 남긴다 — 빈 흰 사각형이 뜨는 것보다 낫다.
            Sprite iconSprite = DiceMetaDataProvider.GetIcon(diceType);
            if (icon != null)
            {
                icon.enabled = iconSprite != null;
                icon.sprite = iconSprite;
            }

            if (background != null)
            {
                // 배경은 <b>다이스 색으로 칠하지 않는다.</b> 인게임 보드의 UIDice 도,
                // 로비 목록의 UIDiceGrowthItem 도 배경에 색을 먹이지 않는다
                // (저쪽은 그 줄이 아예 주석 처리돼 있다). 색은 아이콘이 들고 있다.
                //
                // 여기서 칠하는 것은 <b>상태</b>뿐이다 — 잠긴 칸은 어둡게, 한도 도달 칸은
                // 흐리게. 둘을 다르게 그려야 "못 가진 것" 과 "지금은 못 넣는 것" 이
                // 구별되고, 그래야 수집 목표가 규칙 안내에 묻히지 않는다.
                bool hasArt = normalSprite != null;
                if (hasArt)
                    background.sprite = selected && selectedSprite != null ? selectedSprite : normalSprite;

                // 그림이 있으면 흰색이 원래 색이다. 상태는 밝기로만 누른다.
                Color baseColor = hasArt ? Color.white : UITowerUIFactory.CardColor;
                Color dimColor = hasArt ? new Color(0.45f, 0.45f, 0.5f, 1f) : UITowerUIFactory.CardDimColor;

                Color color = baseColor;
                if (!unlocked)
                    color = dimColor;
                else if (tierFull && !selected)
                    color = Color.Lerp(baseColor, dimColor, 0.55f);

                background.color = color;
            }

            // 아이콘과 글자도 같이 흐려야 칸 전체가 한 덩어리로 읽힌다. 배경만 어둡게
            // 하면 잠긴 칸의 아이콘이 오히려 도드라진다.
            float contentAlpha = unlocked ? 1f : 0.4f;
            SetAlpha(icon, contentAlpha);
            SetAlpha(label, contentAlpha);
            SetAlpha(starLabel, contentAlpha);

            if (selectionEdge != null)
                selectionEdge.enabled = selected;

            if (checkMark != null)
                checkMark.enabled = selected;

            if (lockIcon != null)
                lockIcon.SetActive(!unlocked);

            if (newBadge != null)
                newBadge.SetActive(isNew && unlocked && !selected);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
                return;

            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        // ── 입력 ────────────────────────────────────────────────────────
        //
        // <b>Button 을 쓰지 않는다.</b> uGUI 의 Button 은 짧은 탭만 알고, 길게 누르기를
        // 얹으려면 결국 이 인터페이스 셋을 직접 구현하게 된다. 둘을 섞으면 길게 누른 뒤
        // 손을 뗄 때 onClick 까지 같이 터져 <b>상세를 보려다 선택까지 되는</b> 일이 난다.

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerDown = true;
            pointerDownTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pointerDown)
                return;

            pointerDown = false;

            // 길게 누르기는 상세. 잠긴 칸에서도 열린다 — 무엇을 얻게 되는지 보는 것이
            // 수집 동기의 절반이다(기획서 8.3 "장기 목표 노출").
            if (Time.unscaledTime - pointerDownTime >= HoldSeconds)
            {
                holdHandler?.Invoke(diceType, star);
                return;
            }

            tapHandler?.Invoke(diceType, star);
        }

        /// <summary>
        /// 손가락이 칸 밖으로 나가면 없던 일로 한다. 그리드가 스크롤되는 화면이라
        /// 스크롤하려고 끈 손가락이 선택으로 잡히면 편성이 제멋대로 바뀐다.
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            pointerDown = false;
        }

        /// <summary>
        /// 에디터 굽기 전용. 이 칸이 맡을 다이스를 확정한다.
        ///
        /// <b><see cref="Bind"/> 를 태우지 않는다 — 이유가 있다.</b>
        /// <c>Bind</c> 는 색을 <c>DiceMetaDataProvider.GetColor</c> 에서 받아오는데,
        /// 그것이 <c>StaticResource.Instance</c> 를 깨운다. 굽기는 <b>플레이 중이 아니라</b>
        /// 그 싱글톤이 "플레이 중이 아닐 때 요청했다" 로 <c>LogError</c> 를 뱉고,
        /// 이어서 <c>DiceMetaDataProvider</c> 가 "데이터베이스가 없다" 로 한 번 더 뱉는다.
        ///
        /// 굽기가 성공했는데 빨간 줄이 두 개 뜨는 것도 문제지만, 진짜 문제는 그 두 로그가
        /// <b>한 번만 나오게 잠긴다</b>는 것이다 — 같은 에디터 세션에서 나중에 일어나는
        /// <b>진짜</b> 배선 사고를 대신 삼킨다. 이 리포가 여러 곳에 적어 둔 그 함정이다.
        ///
        /// 색을 굽지 않아도 되는 이유는 간단하다. <c>UITowerLoadoutDialog.RefreshGrid</c> 가
        /// 화면을 열 때마다 서른 칸 전부에 <c>Bind</c> 를 태우므로, 프리팹에 저장된 색은
        /// <b>런타임에 반드시 덮인다.</b> 굽기가 저장해야 하는 것은 "이 칸이 어떤 다이스냐"
        /// 하나뿐이다.
        /// </summary>
        internal void BakeAssign(DiceType assignedType, int assignedStar)
        {
            diceType = assignedType;
            star = assignedStar;

            // 이름도 성급도 <b>에셋을 안 보는 값</b>으로 굽는다. 진짜 이름(displayName)과
            // 성급 표시 여부(ShowStarUI)는 전부 에셋에서 오고, 그것을 굽기 시점에 읽으면
            // 위에 적은 거짓 에러가 난다. 런타임 Bind 가 반드시 덮으므로 여기 값은
            // 프리팹을 열었을 때 칸이 구별되어 보이기만 하면 된다.
            if (label != null)
                label.SetText(assignedType.ToString());

            if (starLabel != null)
            {
                bool showStar = TowerLoadoutRules.TierOf(assignedType) == TowerSlotTier.Base;
                starLabel.gameObject.SetActive(showStar);
                if (showStar)
                    starLabel.SetText("x{0}", assignedStar);
            }

            if (icon != null)
                icon.enabled = false;

            // 프리팹에 저장되는 기본 모습: 보유·미선택. 잠금과 배지는 꺼 둔다.
            if (selectionEdge != null)
                selectionEdge.enabled = false;

            if (checkMark != null)
                checkMark.enabled = false;

            if (lockIcon != null)
                lockIcon.SetActive(false);

            if (newBadge != null)
                newBadge.SetActive(false);
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 아이콘 Image 크기. 시안 "스킬 아이콘 1.7배" — 아이콘 스프라이트가 64px 안에 45px 그림이라
        /// 64 x 1.7 = 109 로 잡아야 보이는 그림이 약 76 이 된다.
        /// </summary>
        private const float IconSize = 109f;

        /// <summary>
        /// 칸 하나. 시안 Infinity_Layout_Skill — 바탕 Infinity_Skill_Selcet_Bg_Normal x2 (96x96),
        /// 선택 시 Bg_Prs 로 바뀌고 오른쪽 위에 체크 배지가 뜬다. 이름·성급 글자는 없다 —
        /// 성급은 줄 왼쪽 라벨이, 정체는 아이콘이 말한다.
        /// </summary>
        internal static UITowerDiceCell Create(
            Transform parent, Vector2 size, Vector2 position, TMP_FontAsset font)
        {
            Sprite normal = UITowerUIFactory.LoadSprite("InfinityMode/Infinity_Skill_Selcet_Bg_Normal");

            // size 는 보이는 칸(96). 그림은 사방 7px 여백이 있어 x2 로 14px 씩 더 크다.
            Image background = UITowerUIFactory.CreateSprite("Cell", parent, normal, 2f,
                size + new Vector2(28f, 28f), position, true);
            background.raycastTarget = true;

            var cell = background.gameObject.AddComponent<UITowerDiceCell>();
            cell.background = background;
            cell.normalSprite = normal;
            cell.selectedSprite = UITowerUIFactory.LoadSprite("InfinityMode/Infinity_Skill_Selcet_Bg_Prs");

            cell.icon = UITowerUIFactory.CreateImage("Icon", background.transform, Color.white);
            UITowerUIFactory.SetRect(cell.icon.rectTransform, new Vector2(IconSize, IconSize), Vector2.zero);
            cell.icon.preserveAspect = true;
            cell.icon.raycastTarget = false;

            // 잠김: 칸을 어둡게 덮고 자물쇠를 얹는다.
            Image lockDim = UITowerUIFactory.CreateImage("Lock", background.transform, new Color(0f, 0f, 0f, 0.5f));
            UITowerUIFactory.SetRect(lockDim.rectTransform, size - new Vector2(12f, 12f), Vector2.zero);
            lockDim.raycastTarget = false;
            cell.lockIcon = lockDim.gameObject;

            UITowerUIFactory.CreateSprite("LockIcon", lockDim.transform,
                UITowerUIFactory.LoadSprite("Upgrade/Icon_lock"), 2f,
                new Vector2(64f, 64f), Vector2.zero, false);

            // 선택 배지 — 오른쪽 위 모서리에 걸친다.
            cell.checkMark = UITowerUIFactory.CreateSprite("Check", background.transform,
                UITowerUIFactory.LoadSprite("InfinityMode/Infinity_Skill_Selcet_Check_Bg"), 2f,
                new Vector2(64f, 64f), new Vector2(size.x * 0.5f - 15f, size.y * 0.5f - 15f), false);

            // NEW 배지 — 획득 직후 한 번(기획서 5.4). 시안에 없지만 기존 기능이라 남긴다.
            Image badge = UITowerUIFactory.CreateImage("NewBadge", background.transform,
                new Color(0.95f, 0.35f, 0.42f, 1f));
            UITowerUIFactory.SetRect(badge.rectTransform,
                new Vector2(52f, 28f), new Vector2(-size.x * 0.5f + 22f, -size.y * 0.5f + 14f));
            badge.raycastTarget = false;
            cell.newBadge = badge.gameObject;

            TMP_Text badgeText = UITowerUIFactory.CreateText("Text", badge.transform, "NEW", 18f,
                TextAlignmentOptions.Center, Color.white, font);
            UITowerUIFactory.SetRect(badgeText.rectTransform, new Vector2(52f, 28f), Vector2.zero);

            return cell;
        }
    }

    /// <summary>
    /// 다이스를 한두 글자로 적는다. 편성 그리드의 칸이 작아 이름을 다 넣을 수 없다.
    ///
    /// <b>기획서 목업의 글자를 그대로 쓴다</b>(普·火·氷·雷·毒, 王火 …).
    /// 아이콘 아트가 붙으면 이 글자는 아이콘 뒤로 물러나지만, 그때까지도 칸이
    /// 서로 구별되어야 편성 화면이 제 일을 한다.
    /// </summary>
    /// <summary>
    /// 다이스를 화면에 적는 방법. <b>인게임 표기를 그대로 따른다.</b>
    ///
    /// <b>기획서 목업의 글자(普·火·王火 …)를 쓰지 않는다.</b> 그것은 목업을 그리려고
    /// 임시로 넣은 표기이고, 이 게임에는 <b>이미 다이스를 적는 방식이 있다</b> —
    /// 아이콘 스프라이트 + <c>DiceMeta.displayName</c> + <c>"x4"</c> 성급 표기.
    /// <c>UIDiceGrowthItem</c>·<c>UIBattleDiceDetailPanel</c>·<c>UIDice</c> 셋이 같은 식을
    /// 쓰고 있고, 탑만 다르게 적으면 같은 다이스가 화면마다 다른 이름으로 보인다.
    ///
    /// <b>표기를 새로 정하는 곳이 아니다.</b> 여기 있는 것은 저 셋이 쓰는 두 줄짜리 식을
    /// 한 곳에 모아 둔 것뿐이다 — 탑 UI 세 곳(칸·슬롯·기여도 막대)이 각자 적으면
    /// 세 벌이 되고, 셋이 어긋나는 순간 원인을 화면으로는 못 찾는다.
    /// </summary>
    public static class TowerDiceText
    {
        /// <summary>
        /// 인게임과 같은 이름. 에셋의 <c>displayName</c> 이 비어 있으면 enum 이름으로
        /// 물러서는 것까지 <c>UIDiceGrowthItem</c> 과 같다.
        /// </summary>
        public static string NameOf(DiceType diceType)
        {
            DiceMetaDataDatabase.DiceMeta meta = DiceMetaDataProvider.GetMeta(diceType);
            return meta != null && !string.IsNullOrEmpty(meta.displayName)
                ? meta.displayName
                : diceType.ToString();
        }

        /// <summary>
        /// 성급 표기. <c>UIDice.Refresh</c> 와 같은 <c>"x4"</c> 형식이고,
        /// 성급 개념이 없는 다이스(스페셜·신화)는 빈 문자열이다.
        ///
        /// <b>계층이 아니라 <c>ShowStarUI</c> 로 판정한다.</b> 인게임이 그 값을 보므로
        /// 여기서 다르게 판정하면 같은 다이스의 별이 화면마다 붙었다 안 붙었다 한다.
        /// </summary>
        public static string StarOf(DiceType diceType, int star)
        {
            return DiceMetaDataProvider.ShowStarUI(diceType) ? "x" + star : string.Empty;
        }

        /// <summary>
        /// 이름과 성급을 한 줄로. 슬롯 바 툴팁·기여도 막대처럼 한 줄만 쓸 수 있는 곳.
        /// </summary>
        public static string NameWithStar(DiceType diceType, int star)
        {
            string starText = StarOf(diceType, star);
            return string.IsNullOrEmpty(starText) ? NameOf(diceType) : NameOf(diceType) + " " + starText;
        }
    }
}
