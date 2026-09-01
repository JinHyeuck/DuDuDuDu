using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;   // resolver.Instantiate 확장 메서드

namespace OJ.Dice
{
    public class UIDiceBoardUI : MonoBehaviour
    {
        public Transform TypeUIParent;
        public GameObject TypeUIPrefab;

        // 여기서 찍는 TypeUIComponent 는 씬 로드 순회가 끝난 뒤에 태어나므로
        // BattleScope 의 InjectGameObject 가 닿지 않는다. 리졸버로 찍어야
        // 그 프리팹 안의 [Inject] 가 채워진다.
        //
        // 이 컴포넌트는 씬에서 비활성이라 Start 가 바로 돌지 않는다. 다행히
        // BattleScope 는 sceneLoaded 에서 비활성 자식까지 훑어 주입하므로,
        // 나중에 켜져서 Start 가 도는 시점에 resolver 는 이미 채워져 있다.
        [Inject] private IObjectResolver resolver;

        private Dictionary<DiceType, TypeUIComponent> typeUIDict = new Dictionary<DiceType, TypeUIComponent>();

        private void Start()
        {
            foreach (DiceType type in System.Enum.GetValues(typeof(DiceType)))
            {
                if (type == DiceType.Max)
                    continue;

                // 부모(TypeUIParent)를 반드시 넘긴다. 부모 없는 오버로드는 스코프 아래
                // 만들었다가 SetParent(null) 하는 분기를 타서 오브젝트가 엉뚱한 곳에 남는다.
                GameObject go = resolver.Instantiate(TypeUIPrefab, TypeUIParent);
                TypeUIComponent comp = go.GetComponent<TypeUIComponent>();
                comp.Init(type);
                comp.UpdateVisual();
                typeUIDict[type] = comp;
            }
        }

        public void UpdateTypeStars()
        {
            foreach (var kvp in typeUIDict)
                kvp.Value.UpdateVisual();
        }
    }

}
