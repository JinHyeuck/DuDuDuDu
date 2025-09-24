using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class MergeSystem : MonoBehaviour
    {
        public static MergeSystem Instance;

        public const int MaxStar = 5;  // 정적 상수로 변경

        public List<DiceType> UseDices = new List<DiceType>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            for (int i = DiceType.Normal.Enum32ToInt(); i < DiceType.Max.Enum32ToInt(); ++i)
            {
                UseDices.Add(i.IntToEnum32<DiceType>());
            }

        }


        public bool TryMerge(UIDice from, UIDice to)
        {
            if (GameManager.Instance.inGameState == InGameState.Wave)
                return false;

            List<DiceType> diceTypes = new List<DiceType>();

            if (to.Star >= MaxStar)
            { // 최대 별일때는 속성 머지
                if (from.Star != to.Star)
                    return false;

                for (int i = 0; i < from.Type.Count; ++i)
                {
                    diceTypes.Add(from.Type[i]);
                }
                
                for (int i = 0; i < to.Type.Count; ++i)
                {
                    diceTypes.Add(to.Type[i]);
                }
            }
            else
            {
                if (from.Type[0] != to.Type[0] || from.Star != to.Star)
                    return false;

                diceTypes.Add(UseDices[Random.Range(0, UseDices.Count)]);
            }
            
            

            // 기존 타입에서 별 제거
            DiceTypeStarManager.Instance.OnDiceRemove(from.Type, from.Star);
            DiceTypeStarManager.Instance.OnDiceRemove(to.Type, to.Star);

            // Star 증가
            int newStar = to.Star + 1;

            // 타겟 다이스에 적용
            to.Init(diceTypes, newStar, to.SlotIndex);

            // DiceTypeStarManager 갱신
            DiceTypeStarManager.Instance.OnDiceSpawn(diceTypes, newStar);

            // 원본 다이스 제거
            Destroy(from.gameObject);

            return true;
        }
    }

}
