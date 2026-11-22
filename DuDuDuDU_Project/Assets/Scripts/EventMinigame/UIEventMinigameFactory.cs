using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Mission;

namespace OJ.EventMinigame
{
    /// <summary>
    /// 이벤트 화면 셋(낚시·보물찾기·래프팅)이 함께 쓰는 색과 조립 도구.
    /// 낱개 헬퍼(<c>CreateText</c>·<c>CreateButton</c>·<c>CreateScrollList</c>…)는 퀘스트 화면 것을 그대로 쓴다 —
    /// 같은 함수를 한 벌 더 두면 두 화면의 여백이 조금씩 어긋난다.
    ///
    /// <b>런타임에 쓰지 않는다.</b> 전부 에디터 굽기 경로에서만 불린다.
    /// 시안이 없어 판·버튼은 단색이다. 그림이 생기면 이 파일의 색 자리만 스프라이트로 바꾸면 된다.
    /// </summary>
    internal static class UIEventMinigameFactory
    {
        internal static readonly Color RowIdle = UIMissionUIFactory.Hex(0x575973);
        internal static readonly Color RowNext = UIMissionUIFactory.Hex(0x7a6a2e);
        internal static readonly Color RowReached = UIMissionUIFactory.Hex(0x3c6b4a);
        internal static readonly Color RowPending = UIMissionUIFactory.Hex(0x8a4f8f);

        internal static readonly Color ButtonOn = UIMissionUIFactory.Hex(0xf5b83d);
        internal static readonly Color ButtonOff = UIMissionUIFactory.Hex(0x6b6c80);
        internal static readonly Color ButtonSelected = UIMissionUIFactory.Hex(0x4fb3e8);

        internal static readonly Color DarkText = UIMissionUIFactory.Hex(0x2a2330);
        internal static readonly Color MutedText = UIMissionUIFactory.Hex(0xc3c4e2);
        internal static readonly Color GoldText = UIMissionUIFactory.Hex(0xfcf900);

        internal static readonly Color TrackColor = UIMissionUIFactory.Hex(0x2b2c40);
        internal static readonly Color GoodZone = UIMissionUIFactory.Hex(0x2f78a8);
        internal static readonly Color PerfectZone = UIMissionUIFactory.Hex(0x3fbf6b);
        internal static readonly Color MarkerColor = UIMissionUIFactory.Hex(0xffffff);

        internal static readonly Color TileHidden = UIMissionUIFactory.Hex(0x8a6a45);
        internal static readonly Color TileEmpty = UIMissionUIFactory.Hex(0x34364a);
        internal static readonly Color TileTreasure = UIMissionUIFactory.Hex(0xe6b422);
        internal static readonly Color TileFound = UIMissionUIFactory.Hex(0x58b368);

        internal static readonly Color CellIdle = UIMissionUIFactory.Hex(0x45465f);
        internal static readonly Color CellActive = UIMissionUIFactory.Hex(0xf5b83d);
        internal static readonly Color CellFree = UIMissionUIFactory.Hex(0x3fbf6b);
        internal static readonly Color CellJackpot = UIMissionUIFactory.Hex(0xe6574a);

        /// <summary>
        /// 창 공통 틀을 짓는다: 어두운 바탕, 판, 제목, 닫기, 남은 시간, 재화, 한 줄 상태, (선택) 마일스톤 목록.
        /// 좌표는 1080x1920 화면 중심 기준이다. 게임별 영역은 호출한 쪽이 이 위에 올린다.
        /// </summary>
        internal static Transform BakeFrame(UIEventMinigameDialog dialog, Transform parent, TMP_FontAsset font,
            string title, bool withMilestoneList)
        {
            GameObject view = UIMissionUIFactory.CreateRect("View", parent);
            UIMissionUIFactory.Stretch(view.GetComponent<RectTransform>());
            dialog.dialogView = view;
            Transform p = view.transform;

            Image backdrop = UIMissionUIFactory.CreateImage("Backdrop", p, UIMissionUIFactory.Backdrop);
            UIMissionUIFactory.Stretch(backdrop.rectTransform);

            // 바깥을 눌러도 닫힌다(퀘스트 창과 같다).
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.transition = Selectable.Transition.None;
            dialog.AddExitButton(backdropButton);

            Image panel = UIMissionUIFactory.Sliced(p, "Panel", "Upgrade/Ui_Popup_Bg", 4f,
                new Vector2(948f, 1702f), new Vector2(-3f, -43f), Color.white);
            panel.raycastTarget = true;

            dialog.SetTitle(UIMissionUIFactory.CreateText(
                "Title", p, title, 45f, TextAlignmentOptions.Center, Color.white, font));
            UIMissionUIFactory.SetRect(dialog.TitleText.rectTransform, new Vector2(600f, 60f), new Vector2(0f, 731f));

            Image close = UIMissionUIFactory.Picture(p, "CloseButton", "OffLineReward/FromPsd/FromPsd_CloseButton",
                new Vector2(112f, 111f), new Vector2(371f, 779.5f));
            dialog.AddExitButton(UIMissionUIFactory.SpriteButton(close));

            dialog.SetTimer(UIMissionUIFactory.CreateText(
                "Timer", p, string.Empty, 30f, TextAlignmentOptions.Center, MutedText, font));
            UIMissionUIFactory.SetRect(dialog.TimerText.rectTransform, new Vector2(700f, 44f), new Vector2(0f, 668f));

            dialog.SetCurrency(UIMissionUIFactory.CreateText(
                "Currency", p, string.Empty, 38f, TextAlignmentOptions.Center, GoldText, font));
            UIMissionUIFactory.SetRect(dialog.CurrencyText.rectTransform, new Vector2(700f, 52f), new Vector2(0f, 612f));

            dialog.SetStatus(UIMissionUIFactory.CreateText(
                "Status", p, string.Empty, 30f, TextAlignmentOptions.Center, Color.white, font));
            dialog.StatusTextInternal.textWrappingMode = TextWrappingModes.Normal;
            UIMissionUIFactory.SetRect(dialog.StatusTextInternal.rectTransform, new Vector2(840f, 170f), new Vector2(0f, -210f));

            if (withMilestoneList)
            {
                Image listBack = UIMissionUIFactory.CreateImage("ListBack", p, TrackColor);
                UIMissionUIFactory.SetRect(listBack.rectTransform, new Vector2(884f, 560f), new Vector2(0f, -560f));
                listBack.raycastTarget = false;

                RectTransform content = UIMissionUIFactory.CreateScrollList(
                    "List", p, new Vector2(868f, 540f), new Vector2(0f, -560f), 6f, 6);
                dialog.SetList(listBack.gameObject, content, UIEventMilestoneRow.Create(content, font));
            }

            return p;
        }

        /// <summary>글자가 들어 있는 단색 버튼. 반환값의 라벨은 자식 "Label" 이다.</summary>
        internal static Button TextButton(Transform parent, string name, string label, Vector2 size, Vector2 position,
            Color background, float fontSize, TMP_FontAsset font)
        {
            return UIMissionUIFactory.CreateButton(name, parent, label, size, position, background, DarkText, fontSize, font);
        }
    }
}
