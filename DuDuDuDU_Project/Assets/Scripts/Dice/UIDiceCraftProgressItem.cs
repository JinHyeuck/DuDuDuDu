using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using VContainer;

namespace OJ.Dice
{
    public class UIDiceCraftProgressItem : MonoBehaviour
    {
        // 8.3b: 이 프리팹은 씬에 상주하지 않고 제작 다이얼로그가 런타임에 찍는다.
        // 그래서 BattleScope 의 씬 순회로는 닿지 않고, 생성부의 Instantiate 가 채워 준다.
        //
        // 주입은 Instantiate 호출 '안에서' Awake 뒤에 일어난다 — 그러니 아래 Awake 에서
        // 이 창구를 읽으면 안 된다. 실제로 창구를 읽는 곳은 Refresh 하나뿐이고,
        // Refresh 는 Bind(= Instantiate 다음 줄) 이후에만 불린다.
        [Inject] private IBattleRefs battle;

        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text percentText;
        [SerializeField] private GameObject selectedFrame;

        private DiceType mythicType;
        private System.Func<DiceType, int> percentProvider;
        private System.Action<DiceType> clickCallback;
        private bool hideWhenZero = true;
        public DiceType MythicType => mythicType;

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

        public void Bind(
            DiceType type,
            System.Func<DiceType, int> getPercent,
            System.Action<DiceType> onClick,
            bool hideZero = true)
        {
            mythicType = type;
            percentProvider = getPercent;
            clickCallback = onClick;
            hideWhenZero = hideZero;
            Refresh();
        }

        public void Refresh()
        {
            int percent = percentProvider != null ? percentProvider(mythicType) : 0;
            bool visible = !hideWhenZero || percent > 0;
            gameObject.SetActive(visible);
            if (!visible)
                return;

            if (iconImage != null)
                iconImage.sprite = DiceMetaDataProvider.GetIcon(mythicType);

            if (percentText != null)
            {
                IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe =
                    DiceMetaDataProvider.GetRecipeMaterials(mythicType);
                int readyCount = 0;
                int requirementCount = recipe != null ? recipe.Count : 0;

                // 원본의 DiceTypeStarManager.Instance != null 가드를 그대로 옮긴 것이다.
                // 이 항목을 찍는 제작 다이얼로그는 UIService 카탈로그에서 로드되므로 전투 밖
                // (로비·타이틀)에서도 Refresh 가 돈다. 그때 창구가 비어 있는 것은 사고가 아니라
                // 정상이고, 원본도 그 경우 readyCount 를 0 으로 두고 넘어갔다.
                // IsActive 가 true 면 DiceStars 는 BattleContext 가 통째로 채웠으니 반드시 있다.
                //
                // battle 자체는 검사하지 않는다. 여기서 battle 이 null 이면 찍는 쪽이
                // resolver 를 안 태웠다는 뜻 — 그건 정상 상태가 아니라 배선 사고이고,
                // 가려 두면 "0/3 45%" 라는 조용히 틀린 숫자로 남는다. 크게 터지는 편이 낫다.
                // (두 생성부 모두 이미 리졸버를 탄다 — UIDiceCraftPanelDialog.cs:167 ·
                //  UIDiceCraftProgressDialog.cs:132. 그래서 창구는 항상 채워져 있다.)
                if (recipe != null && battle.IsActive)
                {
                    for (int i = 0; i < recipe.Count; i++)
                    {
                        DiceMetaDataDatabase.DiceRecipeMaterial req = recipe[i];
                        if (battle.DiceStars.GetTypeStarCount(req.diceType, req.star) >= req.count)
                            readyCount++;
                    }
                }

                percentText.SetText(requirementCount > 0
                    ? $"{readyCount}/{requirementCount}  {percent}%"
                    : $"{percent}%");
            }
        }

        public void SetSelected(bool selected)
        {
            if (selectedFrame != null)
                selectedFrame.SetActive(selected);
        }

        private void HandleClick()
        {
            clickCallback?.Invoke(mythicType);
        }
    }
}
