using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OJ.DI;
using VContainer;

namespace OJ.Dice
{
    public class TypeUIComponent : MonoBehaviour
    {
        public Image BGImage;        // 타입 UI 배경
        public Image Icon;
        public TMP_Text TypeLabel;
        public TMP_Text Star;

        private DiceType type;

        // 8.3b: UIDiceBoardUI 가 런타임에 찍는 프리팹이라 배틀 스코프의 씬 루트 순회로는
        // 닿지 않는다. 그 순회는 sceneLoaded 때 한 번뿐이고 이 오브젝트는 그 뒤에 태어난다.
        // 생성부의 resolver.Instantiate 가 Instantiate 호출 안에서 채워 준다 —
        // 이 컴포넌트는 UIDiceBoardUI 가 resolver.Instantiate 로 찍는 런타임 생성물이라
        // <b>주입이 Awake 보다 먼저다</b> — VContainer 가 프리팹을 SetActive(false) 로 껐다
        // 찍고 주입한 뒤에 켜기 때문이다(ObjectResolverUnityExtensions.cs:78-91).
        // 그래도 Awake 에서 읽지 않는다: 씬에 놓인 컴포넌트는 반대(자기 Awake 뒤)라
        // 두 규칙을 섞어 기억하는 것이 사고의 원천이고, 늦게 읽어서 손해 볼 것이 없다.
        [Inject] private IBattleRefs battle;

        public void Init(DiceType t)
        {
            type = t;
            if (TypeLabel != null)
                TypeLabel.text = type.ToString();
        }

        private void Start()
        {
            UpdateVisual();
        }

        public void UpdateVisual()
        {
            // 8.3b: 예전 DiceTypeStarManager.Instance == null 검사를 옮긴 것이다.
            // 그 매니저는 전투 씬에만 사는 물건이라 "Instance 가 null" 은 곧 "전투가 없다"
            // 는 뜻이었고, 창구에서 같은 것을 묻는 이름이 IsActive 다.
            //
            // battle 이나 battle.DiceStars 를 대신 검사하지 않았다. 전투 중에 그것이
            // null 이면 그리기를 건너뛸 상황이 아니라 주입이 빠진 사고이고, 조용히
            // 넘어가면 별 개수가 안 바뀌는 것으로만 드러나 원인에서 한참 멀어진다.
            if (!battle.IsActive) return;

            //Color typeColor = DiceMetaDataProvider.GetColor(type);
            Sprite typeSprite = DiceMetaDataProvider.GetIcon(type);

            // BG 색상만 변경
            //if (BGImage != null)
            //    BGImage.color = typeColor;

            if (Icon != null)
            {
                if (typeSprite != null) Icon.sprite = typeSprite;
            }

            if (Star != null)
                Star.text = battle.DiceStars.GetTypeStars(type).ToString();
        }
    }

}
