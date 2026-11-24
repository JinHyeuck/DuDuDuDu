using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Mission;

namespace OJ.EventMinigame
{
    /// <summary>킷 버튼 종류. 색은 다른 화면(퀘스트·현상금·장비)이 쓰는 킷 버튼과 같다.</summary>
    public enum EventButtonKind
    {
        /// <summary>노란 큰 버튼. 이 화면의 주 행동(던지기·돌리기·고르기).</summary>
        Primary = 0,

        /// <summary>회색 버튼. 보조 행동·고르지 않은 배율·꺼진 버튼.</summary>
        Secondary,

        /// <summary>초록 버튼. 선택된 배율·열린 칸.</summary>
        Positive,

        /// <summary>빨간 버튼. 대박 칸.</summary>
        Danger,
    }

    /// <summary>
    /// 이벤트 화면 셋(낚시·보물찾기·래프팅)이 함께 쓰는 스프라이트·색·조립 도구.
    /// 낱개 헬퍼(<c>CreateText</c>·<c>CreateScrollList</c>·<c>Sliced</c>…)는 퀘스트 화면 것을 그대로 쓴다 —
    /// 같은 함수를 한 벌 더 두면 두 화면의 여백이 조금씩 어긋난다.
    ///
    /// <b>스프라이트는 새로 그리지 않고 이미 있는 것을 쓴다</b>: 킷 버튼(<c>Ingame/Big_Btn_*</c>, <c>Btn_Gray</c>),
    /// 판(<c>Upgrade/Ui_Popup_*</c>, <c>InfinityMode/Infinity_Popup_Bg_*</c>), 게이지(<c>Upgrade/Upgrade_Gauge_Bg</c>),
    /// 칸(<c>ItemSlot/Itme_Slot_*</c>). 이 화면만의 그림이 생기면 이 파일의 경로만 바꾼다.
    ///
    /// <b>런타임에 쓰지 않는다.</b> 전부 에디터 굽기 경로에서만 불린다.
    /// </summary>
    internal static class UIEventMinigameFactory
    {
        internal const string ButtonPrimary = "Ingame/Big_Btn_Yellow";
        internal const string ButtonSecondary = "Ingame/Btn_Gray";
        internal const string ButtonPositive = "Ingame/Big_Btn_Green";
        internal const string ButtonDanger = "Ingame/Big_Btn_Red";

        internal const string SmallBox = "Upgrade/Ui_Popup_SmallBox";
        internal const string PanelDark = "InfinityMode/Infinity_Popup_Bg_Dark";
        internal const string PanelLight = "InfinityMode/Infinity_Popup_Bg_Light";
        internal const string GaugeBack = "Upgrade/Upgrade_Gauge_Bg";

        internal const string SlotGrey = "ItemSlot/Itme_Slot_0";
        internal const string SlotGreen = "ItemSlot/Itme_Slot_1";
        internal const string SlotGold = "ItemSlot/Itme_Slot_5";

        // 마일스톤 줄: 흰 둥근 상자(SmallBox)에 곱하는 색. 줄 글씨가 읽히는 어두운 바탕이다.
        internal static readonly Color RowIdle = UIMissionUIFactory.Hex(0x575973);
        internal static readonly Color RowNext = UIMissionUIFactory.Hex(0x7a6a2e);
        internal static readonly Color RowReached = UIMissionUIFactory.Hex(0x3c6b4a);
        internal static readonly Color RowPending = UIMissionUIFactory.Hex(0x8a4f8f);

        internal static readonly Color DisabledTint = new Color(0.62f, 0.62f, 0.66f, 1f);

        internal static readonly Color MutedText = UIMissionUIFactory.Hex(0xc3c4e2);
        internal static readonly Color GoldText = UIMissionUIFactory.Hex(0xfcf900);
        internal static readonly Color DarkText = UIMissionUIFactory.Hex(0x2a2330);

        internal static readonly Color GoodZone = UIMissionUIFactory.Hex(0x2f8fd0);
        internal static readonly Color PerfectZone = UIMissionUIFactory.Hex(0x3fe06b);
        internal static readonly Color MarkerColor = Color.white;

        /// <summary>보물찾기 칸 색. 칸 그림(회색/초록/주황 슬롯)에 곱한다.</summary>
        internal static readonly Color TileHidden = Color.white;
        internal static readonly Color TileEmpty = new Color(0.38f, 0.38f, 0.46f, 1f);
        internal static readonly Color TileTreasure = Color.white;
        internal static readonly Color TileFound = Color.white;

        internal static string ButtonPath(EventButtonKind kind)
        {
            switch (kind)
            {
                case EventButtonKind.Secondary: return ButtonSecondary;
                case EventButtonKind.Positive: return ButtonPositive;
                case EventButtonKind.Danger: return ButtonDanger;
                default: return ButtonPrimary;
            }
        }

        internal static Sprite ButtonSprite(EventButtonKind kind)
        {
            return UIMissionUIFactory.LoadSprite(ButtonPath(kind));
        }

        /// <summary>
        /// 킷 버튼(32² 그림을 9조각으로 늘린다). 글자는 흰색이다 — 이 게임의 버튼 글자는 전부 흰 글자에 외곽선이다.
        /// 꺼지면 그림만 어둡게 눌린다(글자는 그대로).
        /// </summary>
        internal static Button KitButton(Transform parent, string name, string label, EventButtonKind kind,
            Vector2 size, Vector2 position, float fontSize, TMP_FontAsset font)
        {
            float pixelScale = size.y >= 100f ? 3f : 2.5f;
            Image image = UIMissionUIFactory.Sliced(parent, name, ButtonPath(kind), pixelScale, size, position, Color.white);
            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.disabledColor = DisabledTint;
            button.colors = colors;

            TMP_Text text = UIMissionUIFactory.CreateText(
                "Label", image.transform, label, fontSize, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.Stretch(text.rectTransform);

            // 킷 버튼은 아래 모서리가 두꺼워서 글자를 조금 올린다.
            text.rectTransform.offsetMin = new Vector2(8f, 10f);
            text.rectTransform.offsetMax = new Vector2(-8f, -2f);
            return button;
        }

        /// <summary>글자 뒤에 까는 작은 어두운 상자(퀘스트의 갱신 타이머 상자와 같은 그림·색).</summary>
        internal static Image Chip(Transform parent, string name, Vector2 size, Vector2 position)
        {
            Image chip = UIMissionUIFactory.Sliced(parent, name, SmallBox, 1.5f, size, position, UIMissionUIFactory.DarkBoxColor);
            chip.raycastTarget = false;
            return chip;
        }

        /// <summary>
        /// 이벤트 화면 공통 틀(레퍼런스의 구성): <b>전체 화면 배경 장면</b> + 위쪽 제목·정보 배지 둘 + 아래쪽 조작부(독)와 보상 버튼.
        /// 가운데 장면과 독 가운데 버튼은 게임별 화면이 올리고, 보상 목록은 따로 <see cref="BakeRewardSheet"/> 로 지어 마지막에 얹는다.
        /// 좌표는 1080x1920 화면 중심 기준이다.
        /// </summary>
        internal static Transform BakeScreen(UIEventMinigameDialog dialog, Transform parent, TMP_FontAsset font,
            string title, string backgroundPath, bool withDock = true)
        {
            GameObject view = UIMissionUIFactory.CreateRect("View", parent);
            UIMissionUIFactory.Stretch(view.GetComponent<RectTransform>());
            dialog.dialogView = view;
            Transform p = view.transform;

            // 배경: 스테이지 지도 그림을 화면에 꽉 채운다(941x1672 ≈ 9:16). 뒤 화면 클릭을 막으려고 레이캐스트를 받는다.
            Image background = UIMissionUIFactory.CreateImage("Background", p, Color.white);
            background.sprite = UIMissionUIFactory.LoadSprite(backgroundPath);
            UIMissionUIFactory.Stretch(background.rectTransform);

            Image dim = UIMissionUIFactory.CreateImage("Dim", p, new Color(0f, 0f, 0f, 0.1f));
            UIMissionUIFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = false;

            // 제목 판
            Image plate = UIMissionUIFactory.Sliced(p, "TitlePlate", PanelDark, 3f,
                new Vector2(420f, 88f), new Vector2(0f, 872f), Color.white);
            plate.raycastTarget = false;
            dialog.SetTitle(UIMissionUIFactory.CreateText(
                "Title", plate.transform, title, 46f, TextAlignmentOptions.Center, Color.white, font));
            UIMissionUIFactory.SetRect(dialog.TitleText.rectTransform, new Vector2(380f, 60f), Vector2.zero);

            Image close = UIMissionUIFactory.Picture(p, "CloseButton", "OffLineReward/FromPsd/FromPsd_CloseButton",
                new Vector2(104f, 103f), new Vector2(452f, 872f));
            dialog.AddExitButton(UIMissionUIFactory.SpriteButton(close));

            // 위 정보 배지: 왼쪽은 게임별 줄을, 오른쪽은 남은 시간 + 게임별 줄을 올린다.
            Image left = UIMissionUIFactory.Sliced(p, "InfoLeft", PanelDark, 3f,
                new Vector2(500f, 150f), new Vector2(-270f, 750f), Color.white);
            left.raycastTarget = false;

            Image right = UIMissionUIFactory.Sliced(p, "InfoRight", PanelDark, 3f,
                new Vector2(500f, 150f), new Vector2(270f, 750f), Color.white);
            right.raycastTarget = false;

            dialog.SetTimer(UIMissionUIFactory.CreateText(
                "Timer", right.transform, string.Empty, 30f, TextAlignmentOptions.Center, MutedText, font));
            UIMissionUIFactory.SetRect(dialog.TimerText.rectTransform, new Vector2(460f, 44f), new Vector2(0f, 34f));

            // 아래 조작부. 낚시는 독 없이 장면 위에 버튼을 바로 얹는다(withDock = false) — 보상 버튼도 그쪽에서 둔다.
            if (withDock)
            {
                Image dock = UIMissionUIFactory.Sliced(p, "Dock", PanelDark, 3f,
                    new Vector2(1120f, 340f), new Vector2(0f, -810f), Color.white);
                dock.raycastTarget = true;
            }

            dialog.SetCurrency(UIMissionUIFactory.CreateText(
                "Currency", p, string.Empty, 36f, TextAlignmentOptions.Center, GoldText, font));
            UIMissionUIFactory.SetRect(dialog.CurrencyText.rectTransform, new Vector2(520f, 52f), new Vector2(0f, -905f));

            dialog.SetStatus(UIMissionUIFactory.CreateText(
                "Status", p, string.Empty, 36f, TextAlignmentOptions.Center, Color.white, font));
            dialog.StatusTextInternal.textWrappingMode = TextWrappingModes.Normal;
            UIMissionUIFactory.SetRect(dialog.StatusTextInternal.rectTransform, new Vector2(860f, 220f), new Vector2(0f, -40f));

            // 런타임에 버튼 그림을 바꿔 끼우기(선택된 배율·활성 칸) 위한 그림 모음.
            dialog.SetButtonSprites(
                ButtonSprite(EventButtonKind.Primary), ButtonSprite(EventButtonKind.Secondary),
                ButtonSprite(EventButtonKind.Positive), ButtonSprite(EventButtonKind.Danger));

            if (withDock)
            {
                dialog.SetRewardButton(KitButton(p, "RewardButton", "보상", EventButtonKind.Secondary,
                    new Vector2(220f, 116f), new Vector2(385f, -770f), 38f, font));
            }

            return p;
        }

        /// <summary>왼쪽 정보 배지에 글자 한 줄을 올린다(0 = 위, 1 = 아래).</summary>
        internal static TMP_Text BadgeLine(Transform screen, string badgeName, string name, int line, float size,
            Color color, TMP_FontAsset font)
        {
            Transform badge = screen.Find(badgeName);
            TMP_Text text = UIMissionUIFactory.CreateText(name, badge, string.Empty, size,
                TextAlignmentOptions.Center, color, font);
            UIMissionUIFactory.SetRect(text.rectTransform, new Vector2(470f, 48f), new Vector2(0f, line == 0 ? 34f : -30f));
            return text;
        }

        /// <summary>
        /// 보상 목록 판(처음엔 꺼져 있다). 독의 "보상" 버튼이 연다. <b>화면의 맨 마지막 자식으로 지어야</b>
        /// 독·장면 위에 그려진다 — 게임별 화면이 자기 요소를 다 올린 뒤에 부른다.
        /// </summary>
        internal static void BakeRewardSheet(UIEventMinigameDialog dialog, Transform screen, TMP_FontAsset font)
        {
            Image veil = UIMissionUIFactory.CreateImage("RewardSheet", screen, new Color(0f, 0f, 0f, 0.62f));
            UIMissionUIFactory.Stretch(veil.rectTransform);

            Image panel = UIMissionUIFactory.Sliced(veil.transform, "Panel", "Upgrade/Ui_Popup_Bg", 4f,
                new Vector2(960f, 1460f), new Vector2(0f, 10f), Color.white);
            panel.raycastTarget = true;

            TMP_Text title = UIMissionUIFactory.CreateText(
                "SheetTitle", veil.transform, "보상", 46f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(title.rectTransform, new Vector2(500f, 64f), new Vector2(0f, 640f));

            Image listBack = UIMissionUIFactory.Sliced(veil.transform, "ListBack", PanelDark, 4f,
                new Vector2(884f, 1120f), new Vector2(0f, 10f), Color.white);
            listBack.raycastTarget = false;

            RectTransform content = UIMissionUIFactory.CreateScrollList(
                "List", veil.transform, new Vector2(868f, 1100f), new Vector2(0f, 10f), 6f, 6);
            dialog.SetList(veil.gameObject, content, UIEventMilestoneRow.Create(content, font));

            dialog.SetSheetClose(KitButton(veil.transform, "SheetClose", "닫기", EventButtonKind.Primary,
                new Vector2(280f, 92f), new Vector2(0f, -625f), 38f, font));

            veil.gameObject.SetActive(false);
        }

        /// <summary>독 가운데의 큰 행동 버튼(던지기·돌리기).</summary>
        internal static Button DockMainButton(Transform screen, string name, string label, TMP_FontAsset font)
        {
            return KitButton(screen, name, label, EventButtonKind.Positive,
                new Vector2(300f, 200f), new Vector2(0f, -765f), 52f, font);
        }

        /// <summary>독 왼쪽 보조 버튼(도감 같은 것).</summary>
        internal static Button DockLeftButton(Transform screen, string name, string label, TMP_FontAsset font)
        {
            return KitButton(screen, name, label, EventButtonKind.Secondary,
                new Vector2(220f, 116f), new Vector2(-385f, -770f), 38f, font);
        }

        /// <summary>독의 배율 버튼. 누를 때마다 x1 → x10 → x100 순환(레퍼런스의 "x1" 배지와 같은 자리).</summary>
        internal static Button DockMultiplierButton(Transform screen, TMP_FontAsset font)
        {
            return KitButton(screen, "MultiplierButton", "x1", EventButtonKind.Primary,
                new Vector2(150f, 84f), new Vector2(205f, -700f), 36f, font);
        }
    }
}
