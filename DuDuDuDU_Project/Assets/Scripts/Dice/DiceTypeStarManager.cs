using System.Collections.Generic;
using UnityEngine;
using VContainer;
using OJ.DI;
// OJ.Hunting 은 PlayerController.Instance 를 이름으로 부를 때만 필요했다.
// 이제 battle.Player 로 받으므로 이 파일에서 그 네임스페이스의 이름을 쓰지 않는다.

namespace OJ.Dice
{
    public class DiceTypeStarManager : MonoBehaviour
    {
        public event System.Action OnDiceInventoryChanged;

        // 8.3b: 배틀 스코프가 채운다. BattleScene 안에서는 null 이 아니다.
        // 이 매니저 자체가 BattleScene 에서만 사는 놈이라, 이 코드가 도는 시점에는
        // 스코프 빌드가 이미 끝나 있다(스코프는 모든 Awake 뒤·모든 Start 앞에 빌드된다).
        [Inject] private IBattleRefs battle;

        public Dictionary<DiceType, int> typeCountTotals = new Dictionary<DiceType, int>();
        private Dictionary<DiceType, int> typeStarTotals = new Dictionary<DiceType, int>();
        private Dictionary<(DiceType type, int star), int> typeStarCounts = new Dictionary<(DiceType type, int star), int>();

        private void Awake()
        {
            foreach (DiceType type in System.Enum.GetValues(typeof(DiceType)))
            {
                if (type == DiceType.Max)
                    continue;
                typeCountTotals[type] = 0;
                typeStarTotals[type] = 0;

                for (int star = 1; star <= MergeSystem.MaxStar; star++)
                    typeStarCounts[(type, star)] = 0;
            }
        }

        public void OnDiceSpawn(DiceType type, int star)
        {
            AddStars(type, star);
            // 창구가 주는 참조는 씬 안에서 null 이 될 수 없으므로 ?. 를 지웠다.
            // 여기서 터진다면 그것은 배선 사고이고, 조용히 넘어가는 대신 울어야 한다.
            battle.BoardUI.UpdateTypeStars();
            battle.Player.RefreshDice();
            OnDiceInventoryChanged?.Invoke();
        }

        public void OnDiceRemove(DiceType type, int star)
        {
            RemoveStars(type, star);
            battle.BoardUI.UpdateTypeStars();
            battle.Player.RefreshDice();
            OnDiceInventoryChanged?.Invoke();
        }

        private void AddStars(DiceType type, int stars)
        {
            if (stars <= 0)
                return;

            if (typeCountTotals.ContainsKey(type))
                typeCountTotals[type] += 1;

            if (typeStarTotals.ContainsKey(type))
                typeStarTotals[type] += stars;

            var key = (type, stars);
            typeStarCounts.TryGetValue(key, out int count);
            typeStarCounts[key] = count + 1;
        }

        private void RemoveStars(DiceType type, int stars)
        {
            if (stars <= 0)
                return;

            if (typeCountTotals.ContainsKey(type))
            {
                typeCountTotals[type] -= 1;
                if (typeCountTotals[type] < 0) typeCountTotals[type] = 0;
            }

            if (!typeStarTotals.ContainsKey(type)) return;
            typeStarTotals[type] -= stars;
            if (typeStarTotals[type] < 0) typeStarTotals[type] = 0;

            var key = (type, stars);
            if (typeStarCounts.TryGetValue(key, out int count))
            {
                count--;
                if (count < 0) count = 0;
                typeStarCounts[key] = count;
            }
        }

        public int GetTypeCount(DiceType type)
        {
            if (!typeCountTotals.ContainsKey(type)) return 0;
            return typeCountTotals[type];
        }

        public int GetTypeStars(DiceType type)
        {
            if (!typeStarTotals.ContainsKey(type)) return 0;
            return typeStarTotals[type];
        }

        public int GetTypeStarCount(DiceType type, int star)
        {
            if (star <= 0)
                return 0;

            typeStarCounts.TryGetValue((type, star), out int count);
            return count;
        }

        public int GetTypeBaseEquivalent(DiceType type)
        {
            int total = 0;
            for (int star = 1; star <= MergeSystem.MaxStar; star++)
            {
                int count = GetTypeStarCount(type, star);
                total += count * GetBaseUnitFromStar(star);
            }

            return total;
        }

        public bool CanCraft(IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe)
        {
            if (recipe == null || recipe.Count == 0)
                return false;

            for (int i = 0; i < recipe.Count; i++)
            {
                DiceMetaDataDatabase.DiceRecipeMaterial req = recipe[i];
                if (GetTypeStarCount(req.diceType, req.star) < req.count)
                    return false;
            }

            return true;
        }

        public int GetRecipeProgressPercent(IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe)
        {
            if (recipe == null || recipe.Count == 0)
                return 0;

            long totalRequiredBase = 0;
            long satisfiedBase = 0;

            for (int i = 0; i < recipe.Count; i++)
            {
                DiceMetaDataDatabase.DiceRecipeMaterial req = recipe[i];
                long requiredBase = (long)req.count * GetBaseUnitFromStar(req.star);
                totalRequiredBase += requiredBase;

                int haveExact = GetTypeStarCount(req.diceType, req.star);
                int usedCount = Mathf.Min(haveExact, req.count);
                satisfiedBase += (long)usedCount * GetBaseUnitFromStar(req.star);
            }

            if (totalRequiredBase <= 0)
                return 0;

            float ratio = (float)satisfiedBase / totalRequiredBase;
            return Mathf.Clamp(Mathf.RoundToInt(ratio * 100f), 0, 100);
        }

        public void ResetAll()
        {
            // 키 컬렉션을 순회하면서 그 딕셔너리를 건드리면 Mono 에서 열거자가 깨진다
            // (InvalidOperationException). 값만 바꿔도 마찬가지다 — .NET Core 는
            // 봐주지만 Unity 가 쓰는 런타임은 아니다. 그래서 enum 을 돈다.
            // Awake 가 채울 때 쓰는 방식과 같게 맞췄다.
            foreach (DiceType type in System.Enum.GetValues(typeof(DiceType)))
            {
                if (type == DiceType.Max)
                    continue;

                typeCountTotals[type] = 0;
                typeStarTotals[type] = 0;
            }

            // (타입, 성급) 조합은 enum 으로 돌 수 없다. 성급이 몇까지 쓰였는지는
            // 실행 중에만 알기 때문이다. 그래서 <b>키를 먼저 복사해</b> 순회한다 —
            // 그냥 Clear() 하지 않는 이유는 이 딕셔너리가 "없는 키 = 0" 과
            // "0 인 키" 를 구별하는 곳이 있어서다(GetTypeStarCount 의 TryGetValue).
            // 키를 지우면 그 자리가 조용히 기본값으로 흐른다.
            var starKeys = new List<(DiceType type, int star)>(typeStarCounts.Keys);
            for (int i = 0; i < starKeys.Count; i++)
                typeStarCounts[starKeys[i]] = 0;

            battle.BoardUI.UpdateTypeStars();
            OnDiceInventoryChanged?.Invoke();
        }

        private static int GetBaseUnitFromStar(int star)
        {
            int s = Mathf.Max(1, star);
            return 1 << (s - 1);
        }

    }
}
