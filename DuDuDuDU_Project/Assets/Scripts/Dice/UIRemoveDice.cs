using UnityEngine;
using OJ.DI;
using VContainer;

namespace OJ.Dice
{
    public class UIRemoveDice : MonoBehaviour
    {
        // 8.3b: 배틀 스코프가 씬을 훑으며 채운다.
        [Inject] private IBattleRefs battle;

        public void RemoveDice(UIDice uIDice)
        {
            // 버튼 클릭으로만 들어오므로 창구는 이미 채워져 있다. 여기에 ?. 를 붙이면
            // 별 회수 실패가 조용해진다 — 다이스는 사라졌는데 별은 안 돌아온다.
            battle.DiceStars.OnDiceRemove(uIDice.Type, uIDice.Star);

            // 원본 다이스 제거
            Destroy(uIDice.gameObject);

            battle.Summon.AddSP(5);
        }
    }
}
