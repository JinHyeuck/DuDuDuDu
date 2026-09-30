using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Bounty;
using OJ.UI;
using OJ.Utils;

namespace OJ.Hunting
{
    /// <summary>경고 띠의 종류. 그림·색·문구만 다르고 움직임은 같다.</summary>
    public enum BattleWarningKind
    {
        /// <summary>보스 웨이브의 보스가 나왔다. 빨강 + "WARNING!".</summary>
        Boss,

        /// <summary>현상금 몬스터가 나왔다. 보라.</summary>
        Bounty,
    }

    /// <summary>
    /// 보스·현상금이 나온 순간 화면 가운데를 가로지르는 경고 띠
    /// (시안 <c>Art/Layout/Warning_Layout.png</c> = 보스, <c>Warning_Layout2.png</c> = 현상금).
    ///
    /// <b>왜 필요한가.</b> 둘 다 일반 몬스터 스무 마리 틈에 섞여 나온다. 덩치로 구분은 되지만
    /// <b>나온 순간</b>은 놓치기 쉽고, 매 웨이브 반복되면 배경이 된다. 한 번 크게 짚어 주면 환기된다.
    ///
    /// <b>한 창이 두 종류를 맡는다.</b> 배치·움직임이 같고 그림·색·문구만 다르다. 둘로 나누면
    /// 연출을 고칠 때 한쪽만 고치는 사고가 난다. 종류별 차이는 <see cref="Theme"/> 한 곳에 있다.
    ///
    /// <b>스스로 사라지고 아무것도 가리지 않는다.</b> 닫는 버튼도 백키도 없고 레이캐스트도
    /// 전부 꺼 둔다 — 하필 전투가 가장 바쁜 순간에 뜨는 물건이다.
    ///
    /// <b>연출은 실제 시간으로 돈다.</b> 배속은 전투를 빨리 돌리라는 뜻이지 글을 빨리 읽으라는
    /// 뜻이 아니다. 트위닝 라이브러리가 없어서(AGENTS) 경과 시간 하나로 모든 값을 계산한다 —
    /// 중간에 끊겨도 다음 재생이 처음부터 다시 세우므로 남는 값이 없다.
    /// </summary>
    public class UIBattleWarning : DialogBase
    {
        [Serializable]
        private sealed class Theme
        {
            public Sprite stripe;
            public Sprite box;
            public Sprite skull;
            public Color bandColor = Color.black;
            public Vector2 skullPosition;
            public string message = string.Empty;
            public bool showWarningFont;
        }

        [SerializeField] private Theme bossTheme = new Theme();
        [SerializeField] private Theme bountyTheme = new Theme();

        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform band;
        [SerializeField] private Image bandFill;
        [SerializeField] private RectTransform stripeTopRow;
        [SerializeField] private RectTransform stripeBottomRow;
        [SerializeField] private Image[] stripeTiles = new Image[0];
        [SerializeField] private RectTransform emblem;
        [SerializeField] private Image box;
        [SerializeField] private Image skull;
        [SerializeField] private Image warningFont;
        [SerializeField] private TMP_Text messageText;

        [Tooltip("다 나타난 뒤 떠 있는 시간(초). 실제 시간이다.")]
        [SerializeField, Min(0.2f)] private float holdSeconds = 1.6f;

        // ── 연출 시간표(초). 띠가 먼저 열리고, 문장이 박히고, 글자가 마지막에 찍힌다. ──
        private const float BandOpen = 0.18f;
        private const float EmblemStart = 0.08f;
        private const float EmblemDuration = 0.3f;
        private const float WarningStart = 0.16f;
        private const float WarningDuration = 0.22f;
        private const float MessageStart = 0.26f;
        private const float MessageDuration = 0.26f;
        private const float FadeDuration = 0.3f;

        /// <summary>줄무늬가 흐르는 속도(px/초). 한 주기가 96px(24px x4)이다.</summary>
        private const float StripeSpeed = 160f;
        private const float StripePeriod = 96f;

        /// <summary>WARNING! 글자의 기울기. 시안에서 오른쪽이 약 10도 올라가 있다.</summary>
        private const float WarningAngle = 10f;

        /// <summary>새 요청이 오면 올라간다. 앞선 연출은 자기 번호가 밀린 것을 보고 물러난다.</summary>
        private int playSequence;

        private Vector2 stripeTopBase;
        private Vector2 stripeBottomBase;

        public void Play(BattleWarningKind kind)
        {
            Theme theme = kind == BattleWarningKind.Boss ? bossTheme : bountyTheme;
            ApplyTheme(theme);

            Enter();
            PlayAsync(++playSequence).Forget();
        }

        protected override void OnExit()
        {
            // 연출 도중에 거둬지면(웨이브가 먼저 끝남) 남은 프레임이 값을 덮어쓰지 않게 한다.
            playSequence++;
        }

        private void ApplyTheme(Theme theme)
        {
            if (bandFill != null)
                bandFill.color = theme.bandColor;

            for (int i = 0; i < stripeTiles.Length; i++)
            {
                if (stripeTiles[i] != null)
                    stripeTiles[i].sprite = theme.stripe;
            }

            if (box != null)
                box.sprite = theme.box;

            if (skull != null)
            {
                skull.sprite = theme.skull;
                skull.rectTransform.anchoredPosition = theme.skullPosition;
            }

            if (warningFont != null)
                warningFont.gameObject.SetActive(theme.showWarningFont);

            if (messageText != null)
                messageText.SetText(theme.message);
        }

        private async UniTaskVoid PlayAsync(int sequence)
        {
            stripeTopBase = stripeTopRow != null ? stripeTopRow.anchoredPosition : Vector2.zero;
            stripeBottomBase = stripeBottomRow != null ? stripeBottomRow.anchoredPosition : Vector2.zero;

            float total = holdSeconds + FadeDuration;
            float elapsed = 0f;

            try
            {
                Apply(0f, total);

                while (elapsed < total)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);

                    // 파괴된 오브젝트는 == null 이 true 인 가짜 null 이라 이 검사에 걸린다.
                    if (this == null || sequence != playSequence)
                        return;

                    elapsed += Time.unscaledDeltaTime;
                    Apply(elapsed, total);
                }
            }
            finally
            {
                // 줄무늬 자리는 끊겼을 때도 돌려놓는다. 안 그러면 다음 재생의 기준점이 밀린다.
                if (this != null)
                {
                    if (stripeTopRow != null) stripeTopRow.anchoredPosition = stripeTopBase;
                    if (stripeBottomRow != null) stripeBottomRow.anchoredPosition = stripeBottomBase;
                }
            }

            if (this != null && sequence == playSequence)
                Exit();
        }

        /// <summary>경과 시간 하나로 모든 값을 세운다.</summary>
        private void Apply(float t, float total)
        {
            if (group != null)
            {
                float fade = Mathf.Clamp01((total - t) / FadeDuration);
                group.alpha = fade;
            }

            if (band != null)
                band.localScale = new Vector3(1f, EaseOutCubic(Progress(t, 0f, BandOpen)), 1f);

            float offset = Mathf.Repeat(t * StripeSpeed, StripePeriod);
            if (stripeTopRow != null)
                stripeTopRow.anchoredPosition = stripeTopBase + new Vector2(-offset, 0f);
            if (stripeBottomRow != null)
                stripeBottomRow.anchoredPosition = stripeBottomBase + new Vector2(offset, 0f);

            if (emblem != null)
            {
                float p = Progress(t, EmblemStart, EmblemDuration);
                emblem.localScale = Vector3.one * EaseOutBack(p);
            }

            if (warningFont != null && warningFont.gameObject.activeSelf)
            {
                // 크게 찍혀 들어와 제자리에 박히고, 그 뒤로는 살짝 맥박친다.
                float p = Progress(t, WarningStart, WarningDuration);
                float settle = Mathf.Lerp(2.2f, 1f, EaseOutCubic(p));
                float pulse = p >= 1f ? 1f + 0.04f * Mathf.Sin((t - WarningStart - WarningDuration) * 12f) : 1f;

                RectTransform rect = warningFont.rectTransform;
                rect.localScale = Vector3.one * settle * pulse;
                rect.localRotation = Quaternion.Euler(0f, 0f, WarningAngle + (1f - p) * 12f);

                Color c = warningFont.color;
                c.a = p;
                warningFont.color = c;
            }

            if (messageText != null)
            {
                float p = Progress(t, MessageStart, MessageDuration);
                messageText.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, EaseOutBack(p));
                messageText.alpha = Mathf.Clamp01(p * 3f);
            }
        }

        private static float Progress(float t, float start, float duration)
        {
            return Mathf.Clamp01((t - start) / duration);
        }

        private static float EaseOutCubic(float x)
        {
            float inv = 1f - x;
            return 1f - inv * inv * inv;
        }

        /// <summary>끝에서 살짝 넘쳤다 돌아온다. 0 → 1.1 → 1.</summary>
        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float m = x - 1f;
            return 1f + c3 * m * m * m + c1 * m * m;
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용. 시안: Art/Layout/Warning_Layout(2).png — 수치는 Docs/WantedUIArtPort.md
        // ──────────────────────────────────────────────────────────────

        /// <summary>띠 전체(줄무늬 두 줄 + 가운데 판)의 세로 범위 — 시안 y592~879.</summary>
        private const float BandCenterY = 735.5f;
        private const float BandHeight = 288f;

        /// <summary>가운데 판(기본박스) — 시안 y636~835.</summary>
        private const float FillHeight = 200f;

        /// <summary>
        /// 줄무늬 그림(144x72, 보이는 줄은 y31~41) x4 = 576x288. 그림 중심이 띠 중심에서
        /// 위로 123.5 · 아래로 120.5 에 있다(보이는 줄 y592~635 · 836~879).
        /// </summary>
        private const float StripeTopY = 123.5f;
        private const float StripeBottomY = -120.5f;
        private const float StripeTileWidth = 576f;
        private const float StripeTileHeight = 288f;

        /// <summary>테두리 마름모 — 시안 대조로 x7.25(주석은 x8), 중심 (527, 725).</summary>
        private static readonly Vector2 BoxCenter = new Vector2(527f, 725f);
        private const float BoxSize = 64f * 7.25f;

        /// <summary>해골 x8 — 템플릿 매칭 오차 0. 보스 (527, 714) · 현상금 (522, 730).</summary>
        private static readonly Vector2 BossSkullCenter = new Vector2(527f, 714f);
        private static readonly Vector2 BountySkullCenter = new Vector2(522f, 730f);

        /// <summary>경고 삼각형 x4 — 템플릿 매칭 오차 1 미만.</summary>
        private static readonly float[] TriangleX = { 68f, 195f, 876f, 1003f };
        private const float TriangleY = 734f;

        /// <summary>WARNING! 글자(256² 그림) x3 — 시안에서 중심 약 (470, 590).</summary>
        private static readonly Vector2 WarningFontCenter = new Vector2(470f, 590f);

        /// <summary>등장 문구 — PSD 글자 레이어 BM HANNA 95.5pt ffc000 + 외곽선, 중심 약 (527, 893).</summary>
        private static readonly Vector2 MessageCenter = new Vector2(527f, 893f);

        /// <summary>에디터 굽기 전용.</summary>
        public static UIBattleWarning Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UIBattleWarning", parent);
            UIBountyUIFactory.Stretch(root.GetComponent<RectTransform>());

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var warning = root.AddComponent<UIBattleWarning>();
            warning.dialogView = view;

            // 백키 스택에 얹지 않는다. 스스로 사라지는 물건이 스택에 남으면 이미 없어진
            // 창이 백키 한 번을 조용히 먹는다.
            warning.UseBackBtn = false;

            // 제 연출이 있으므로 DialogBase 의 팝업 연출(축소→원래)을 끈다. 둘이 겹치면
            // 띠가 가운데로 쪼그라들었다 펴지는 엉뚱한 움직임이 된다.
            warning.BakeOpenStyle(DialogOpenStyle.None);

            GameObject content = UIBountyUIFactory.CreateRect("Content", view.transform);
            UIBountyUIFactory.Stretch(content.GetComponent<RectTransform>());
            warning.group = content.AddComponent<CanvasGroup>();

            // 읽히기만 하면 된다. 이 띠가 떠 있는 동안에도 아래 보드는 눌려야 한다.
            warning.group.blocksRaycasts = false;
            warning.group.interactable = false;

            Sprite bossStripe = UIBountyUIFactory.LoadSprite("Ingame/Warning/Warning_Boss_Bg");

            // ── 띠 ────────────────────────────────────────────────────
            GameObject bandGo = UIBountyUIFactory.CreateRect("Band", content.transform);
            warning.band = bandGo.GetComponent<RectTransform>();
            UIBountyUIFactory.StretchHorizontal(warning.band, BandHeight, 960f - BandCenterY);

            warning.bandFill = UIBountyUIFactory.CreateImage("Fill", warning.band, UIBountyUIFactory.Hex(0x720000));
            UIBountyUIFactory.StretchHorizontal(warning.bandFill.rectTransform, FillHeight, 0f);
            warning.bandFill.raycastTarget = false;

            var tiles = new System.Collections.Generic.List<Image>();
            warning.stripeTopRow = CreateStripeRow("StripeTop", warning.band, StripeTopY, bossStripe, tiles);
            warning.stripeBottomRow = CreateStripeRow("StripeBottom", warning.band, StripeBottomY, bossStripe, tiles);
            warning.stripeTiles = tiles.ToArray();

            // 경고 삼각형 넷. 두 장을 번갈아 깜빡인다 — 이웃끼리 박자를 엇갈려 번쩍이는 느낌을 낸다.
            var triangleFrames = new[]
            {
                UIBountyUIFactory.LoadSprite("Ingame/Warning/Warning_Icon_1"),
                UIBountyUIFactory.LoadSprite("Ingame/Warning/Warning_Icon_2"),
            };

            for (int i = 0; i < TriangleX.Length; i++)
            {
                Image triangle = UIBountyUIFactory.CreateSprite("Triangle" + i, warning.band, triangleFrames[i % 2], 4f,
                    new Vector2(104f, 104f), new Vector2(TriangleX[i] - 540f, BandCenterY - TriangleY), false);

                var flipbook = triangle.gameObject.AddComponent<UIImageFlipbook>();
                flipbook.BakeSetup(triangleFrames, 0.18f, i % 2);
            }

            // ── 가운데 문장(마름모 + 해골) ─────────────────────────────
            GameObject emblemGo = UIBountyUIFactory.CreateRect("Emblem", content.transform);
            warning.emblem = emblemGo.GetComponent<RectTransform>();
            UIBountyUIFactory.SetRect(warning.emblem, new Vector2(BoxSize, BoxSize),
                UIBountyUIFactory.Pos(BoxCenter.x, BoxCenter.y));

            warning.box = UIBountyUIFactory.CreateSprite("Box", warning.emblem,
                UIBountyUIFactory.LoadSprite("Ingame/Warning/Warning_Boss_Box"), 7.25f,
                new Vector2(BoxSize, BoxSize), Vector2.zero, false);

            warning.skull = UIBountyUIFactory.CreateSprite("Skull", warning.emblem,
                UIBountyUIFactory.LoadSprite("Ingame/Warning/Warning_Boss"), 8f,
                new Vector2(256f, 256f), SkullOffset(BossSkullCenter), false);

            // ── 문구 ─────────────────────────────────────────────────
            warning.messageText = UIBountyUIFactory.CreateText("Message", content.transform, "보스등장!!!", 95.5f,
                TextAlignmentOptions.Center, UIBountyUIFactory.Hex(0xFFC000), font);
            UIBountyUIFactory.SetRect(warning.messageText.rectTransform, new Vector2(1000f, 130f),
                UIBountyUIFactory.Pos(MessageCenter.x, MessageCenter.y));
            warning.messageText.textWrappingMode = TextWrappingModes.NoWrap;

            // WARNING! 은 맨 앞(시안 주석 "젤 앞") — 마지막 형제로 둔다.
            warning.warningFont = UIBountyUIFactory.CreateSprite("WarningFont", content.transform,
                UIBountyUIFactory.LoadSprite("Ingame/Warning/Warning_Font"), 3f,
                new Vector2(768f, 768f), UIBountyUIFactory.Pos(WarningFontCenter.x, WarningFontCenter.y), false);
            warning.warningFont.rectTransform.localRotation = Quaternion.Euler(0f, 0f, WarningAngle);

            warning.bossTheme = new Theme
            {
                stripe = bossStripe,
                box = UIBountyUIFactory.LoadSprite("Ingame/Warning/Warning_Boss_Box"),
                skull = UIBountyUIFactory.LoadSprite("Ingame/Warning/Warning_Boss"),
                bandColor = UIBountyUIFactory.Hex(0x720000),
                skullPosition = SkullOffset(BossSkullCenter),
                message = "보스등장!!!",
                showWarningFont = true,
            };

            warning.bountyTheme = new Theme
            {
                stripe = UIBountyUIFactory.LoadSprite("Ingame/Warning/Warning_Wanted_Bg"),
                box = UIBountyUIFactory.LoadSprite("Ingame/Warning/Warning_Wanted_Box"),
                skull = UIBountyUIFactory.LoadSprite("Ingame/Warning/Warning_Wanted"),
                bandColor = UIBountyUIFactory.Hex(0x640092),
                skullPosition = SkullOffset(BountySkullCenter),
                message = "현상범 등장!",
                showWarningFont = false,
            };

            return warning;
        }

        private static Vector2 SkullOffset(Vector2 designCenter)
        {
            return new Vector2(designCenter.x - BoxCenter.x, BoxCenter.y - designCenter.y);
        }

        /// <summary>
        /// 줄무늬 한 줄. 그림(576 폭)을 세 장 이어 붙여 1728 폭으로 깐다. 그림 한 장 안에서
        /// 무늬가 딱 여섯 주기라 이음매가 없고, 한 주기(96)만큼만 흘렸다 되돌리므로 빈틈이 안 보인다.
        /// </summary>
        private static RectTransform CreateStripeRow(
            string name, Transform parent, float y, Sprite sprite, System.Collections.Generic.List<Image> tiles)
        {
            GameObject row = UIBountyUIFactory.CreateRect(name, parent);
            RectTransform rect = row.GetComponent<RectTransform>();
            UIBountyUIFactory.SetRect(rect, new Vector2(StripeTileWidth * 3f, StripeTileHeight), new Vector2(0f, y));

            for (int i = -1; i <= 1; i++)
            {
                Image tile = UIBountyUIFactory.CreateSprite("Tile" + (i + 1), row.transform, sprite, 4f,
                    new Vector2(StripeTileWidth, StripeTileHeight), new Vector2(StripeTileWidth * i, 0f), false);
                tiles.Add(tile);
            }

            return rect;
        }
    }
}
