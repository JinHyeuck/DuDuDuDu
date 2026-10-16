using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Bounty;
using OJ.Core;
using OJ.UI;

namespace OJ.Economy
{
    /// <summary>
    /// 행운 주사위의 확률표. "?" 를 누르면 열리고, 바깥을 누르거나 뒤로가기를 누르면 닫힌다.
    ///
    /// <b>줄은 <see cref="LuckyDiceRules"/> 에서 읽는다.</b> 화면의 표와 실제 굴림이 같은 표를 보므로
    /// "표기와 다르다" 는 사고가 없다 — 열 때마다 다시 채운다.
    /// </summary>
    public sealed class UILuckyDiceOddsTooltip : DialogBase
    {
        [SerializeField] private Button outsideButton;
        [SerializeField] private List<TMP_Text> stoneTexts = new List<TMP_Text>();
        [SerializeField] private List<TMP_Text> chanceTexts = new List<TMP_Text>();
        [SerializeField] private TMP_Text footer;

        protected override void OnEnter()
        {
            // 현상금 선택창처럼 같은 캔버스의 다른 창 위에 오게 한다.
            transform.SetAsLastSibling();
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < stoneTexts.Count; i++)
            {
                int stones = LuckyDiceRules.MinStones + i;
                stoneTexts[i].SetText(stones + "개");
                chanceTexts[i].SetText(LuckyDiceRules.Chance(stones) + "%");
            }

            footer.SetText(LuckyDiceRules.CostSp + " SP / 평균 " + (LuckyDiceRules.ExpectedStonesTimes100() / 100f).ToString("0.0") + "개");
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        private static readonly Vector2 PanelVisible = new Vector2(430f, 640f);
        private static readonly Vector2 PanelCenter = new Vector2(190f, -380f);
        private const float RowHeight = 46f;

        /// <summary>에디터 굽기 전용.</summary>
        public static UILuckyDiceOddsTooltip Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UILuckyDiceOddsTooltip", parent);
            UIBountyUIFactory.Stretch(root.GetComponent<RectTransform>());

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UILuckyDiceOddsTooltip>();
            dialog.dialogView = view;
            dialog.UseBackBtn = true;
            dialog.BakeOpenStyle(DialogOpenStyle.None);

            // 바깥은 거의 투명하게 둔다 — 말풍선 정도의 안내라 전투를 가리지 않는다. 누르면 닫힌다.
            Image blocker = UIBountyUIFactory.CreateImage("OutsideCatcher", view.transform, new Color(0f, 0f, 0f, 0.25f));
            UIBountyUIFactory.Stretch(blocker.rectTransform);
            dialog.outsideButton = blocker.gameObject.AddComponent<Button>();
            dialog.outsideButton.targetGraphic = blocker;
            dialog.outsideButton.transition = Selectable.Transition.None;
            dialog.AddExitButton(dialog.outsideButton);

            Image panelImage = UIBountyUIFactory.CreateSprite("Panel", view.transform,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_Bg"), 4f,
                PanelVisible + new Vector2(24f, 24f), PanelCenter, true);
            panelImage.raycastTarget = true;
            RectTransform panel = panelImage.rectTransform;
            float top = PanelVisible.y / 2f;

            TMP_Text title = UIBountyUIFactory.CreateText("Title", panel, "행운 주사위 확률", 36f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(title.rectTransform, new Vector2(PanelVisible.x - 40f, 50f), new Vector2(0f, top - 52f));

            for (int i = 0; i < 10; i++)
            {
                float y = top - 120f - i * RowHeight;

                TMP_Text stone = UIBountyUIFactory.CreateText("Stone" + i, panel, (i + 1) + "개", 30f,
                    TextAlignmentOptions.Left, Color.white, font);
                UIBountyUIFactory.SetRect(stone.rectTransform, new Vector2(150f, RowHeight), new Vector2(-100f, y));
                dialog.stoneTexts.Add(stone);

                TMP_Text chance = UIBountyUIFactory.CreateText("Chance" + i, panel, LuckyDiceRules.Chance(i + 1) + "%", 30f,
                    TextAlignmentOptions.Right, new Color(1f, 0.92f, 0.4f, 1f), font);
                UIBountyUIFactory.SetRect(chance.rectTransform, new Vector2(150f, RowHeight), new Vector2(100f, y));
                dialog.chanceTexts.Add(chance);
            }

            dialog.footer = UIBountyUIFactory.CreateText("Footer", panel, "", 26f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.footer.rectTransform, new Vector2(PanelVisible.x - 40f, 40f), new Vector2(0f, -top + 40f));

            return dialog;
        }
    }
}
