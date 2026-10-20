using UnityEngine;
using System.Collections.Generic;
using VContainer;
using OJ.Analytics;
using OJ.Battle;
using OJ.Core;
using OJ.DI;
using OJ.Hunting;
using OJ.Mission;
using OJ.Point;
using OJ.Relic;

namespace OJ.Dice
{
    public class MergeSystem : MonoBehaviour
    {
        /// <summary>
        /// 머지의 최고 성급. <b>3성이 마지막이다</b> — 3성끼리는 머지되지 않는다(<see cref="TryMerge"/>).
        /// 4성은 폐지됐다. 진화(<c>DiceEvolution.EvolveRequiredStar</c>)와 탑 편성(<c>TowerFormula.MaxBaseStar</c>)도 3성이 끝이다.
        /// </summary>
        public const int MaxStar = 3;

        // 8.3b: 배틀 스코프가 채운다. BattleScene 안에서는 null 이 아니다.
        // 단 스코프는 씬의 모든 Awake 뒤에 빌드되므로 Awake 에서는 아직 null 이다.
        [Inject] private IBattleRefs battle;

        /// <summary>
        /// 미보유 다이스를 만들어 내지 않기 위한 판정. 루트 스코프에 살아서 배틀 스코프의
        /// 자식으로 해석된다.
        ///
        /// <b><c>.Instance</c> 로 잡지 않는다.</b> 이 클래스는 이미 주입 창구를 갖고 있고,
        /// 여기에 싱글톤 호출을 하나 더 얹으면 8.3b 가 262곳에서 걷어낸 방향을 되돌리는 셈이다.
        /// </summary>
        [Inject] private DiceOwnershipManager ownership;

        public bool TryMerge(UIDice from, UIDice to)
        {
            // 무한의 탑에서는 머지가 없다. 기획서 3.2 의 금지 항목이고, 2장이 두 콘텐츠를
            // 가르는 축으로 든 것이 바로 이것이다("전투 중 성장 없음 — 편성 시 확정").
            //
            // <b>아래 웨이브 검사에 기대면 안 된다.</b> 탑은 관리 단계가 없어 전투 중에는
            // 늘 Wave 라 우연히 막히지만, 층이 끝나면 상태가 None 으로 떨어져 <b>결과창이
            // 뜬 채로 머지가 열린다.</b> 규칙을 우연에 맡기지 않는다.
            if (battle.Tower.IsActive)
                return false;

            if (battle.Game.IsBoardLockedByWave)
                return false;

            if (from == null || to == null)
                return false;

            if (!DiceMetaDataProvider.CanMerge(from.Type) || !DiceMetaDataProvider.CanMerge(to.Type))
                return false;

            // No merge beyond max star. Composite/multi-attribute dice is removed.
            if (to.Star >= MaxStar || from.Star >= MaxStar)
                return false;

            if (from.Type != to.Type || from.Star != to.Star)
                return false;

            DiceType fromType = from.Type;
            int fromStar = from.Star;
            DiceType toType = to.Type;
            int toStar = to.Star;
            // 튜토리얼 대본이 정해 둔 결과가 있으면 그것이다(노멀 둘 → 번개). 한 번 쓰면 사라진다.
            DiceType mergedType = battle.Tutorial.TryTakeForcedMergeType(out DiceType forced)
                ? forced
                : GetRandomMergedType(fromType);
            battle.DiceStars.OnDiceRemove(fromType, fromStar);
            battle.DiceStars.OnDiceRemove(toType, toStar);

            int newStar = Mathf.Min(MaxStar, to.Star + 1);

            to.Init(mergedType, newStar, to.SlotIndex);

            // 결과물 속성 색의 먼지가 터진다. 색을 <b>결과</b>에서 가져오는 것이 요점이다 —
            // 노말 둘이 불이 되는 판이 있으므로(GetRandomMergedType) 재료 색으로 뿌리면
            // 무엇이 나왔는지를 가리는 연출이 된다.
            DiceFx.PlayMerge(to.transform as RectTransform, DiceMetaDataProvider.GetColor(mergedType));

            battle.DiceStars.OnDiceSpawn(mergedType, newStar);
            // RunHistoryManager 는 루트에 사는 별개 서비스라 ?. 를 그대로 둔다.
            // 반면 웨이브 번호의 GameManager null 검사는 지웠다 — 이 메서드는 맨 위에서
            // 이미 battle.Game.inGameState 를 그냥 읽고 있고, 씬 매니저는 여기서 null 일 수 없다.
            RunHistoryManager.Instance?.RecordMerge(
                fromType,
                fromStar,
                toType,
                toStar,
                mergedType,
                newStar,
                battle.Game.CurrentWaveIndex);

            Destroy(from.gameObject);
            // 튜토리얼 판에서는 유물이 끼지 않는다(UIDiceSummonSystem 의 같은 판단).
            if (!battle.Tutorial.IsActive)
                RelicManager.Instance?.ApplyMergeInsurance();

            // 머지만 센다. 진화(TryEvolve)·교환(TryExchange)은 같은 파일에 있지만
            // 기획의 "다이스 머지 n회" 가 가리키는 행동이 아니다.
            MissionManager.Instance?.Notify(MissionAction.DiceMerge);

            return true;
        }

        private DiceType GetRandomMergedType(DiceType fallbackType)
        {
            UIDiceSummonSystem summonSystem = battle.Summon;
            // summonSystem == null 검사는 지웠다. 씬 매니저라 BattleScene 안에서 null 이면
            // 그건 사고이고 조용히 fallbackType 으로 새면 안 된다.
            // deckTypes 쪽 검사는 참조가 아니라 덱 데이터에 대한 검사라 남긴다.
            if (summonSystem.deckTypes == null || summonSystem.deckTypes.Count == 0)
                return fallbackType;

            List<DiceType> candidates = new List<DiceType>(summonSystem.deckTypes.Count);
            for (int i = 0; i < summonSystem.deckTypes.Count; i++)
            {
                DiceType type = summonSystem.deckTypes[i];
                if (!DiceMetaDataProvider.IsSummonable(type))
                    continue;

                // 지금 deckTypes 는 기본 5종뿐이라 이 줄은 아무것도 거르지 않는다. 그래도 둔다 —
                // 그건 <b>씬 데이터</b>라 언제든 특수가 들어갈 수 있고, 그때 미보유 다이스가
                // 머지 결과로 튀어나오는 것은 어떤 검사 도구도 못 잡는다.
                if (!ownership.IsOwned(type))
                    continue;

                candidates.Add(type);
            }

            if (candidates.Count == 0)
                return fallbackType;

            return candidates[Random.Range(0, candidates.Count)];
        }

        // ── 진화 · 교환 ──────────────────────────────────────────────────────────────
        //
        // 조합식을 대신하는 두 동작이다. 규칙(배선·비용·후보)은 DiceEvolution 이 들고,
        // 여기는 <b>보드를 실제로 바꾸는 일</b>만 한다 — 재화를 깎고, 재고를 갱신하고,
        // 슬롯의 다이스를 다른 다이스로 갈아 끼운다.
        //
        // <b>왜 MergeSystem 인가.</b> 진화도 교환도 "보드의 다이스를 다른 다이스로
        // 바꾼다" 는 점에서 머지와 같은 종류의 일이고, 그러려면 IBattleRefs 창구가
        // 필요하다. 새 MonoBehaviour 를 세우면 씬에 오브젝트를 놓고 BattleScope 의
        // 등록 목록까지 늘려야 하는데, 얻는 것이 파일 분리뿐이다.

        /// <summary>
        /// 상위 단계로 올린다. 성공하면 재화를 깎고 같은 슬롯의 다이스를 바꿔 끼운다.
        ///
        /// <b>웨이브 중에는 막는다.</b> 머지와 같은 규약이다 — 발사 중에 슬롯의 타입이
        /// 바뀌면 <c>PlayerController</c> 가 들고 있는 쿨타임 상태와 화면이 어긋난다.
        /// </summary>
        public bool TryEvolve(UIDice dice)
        {
            // 탑에서는 진화·교환도 없다. 위 TryMerge 와 같은 이유이고, 셋을 함께 막아야
            // "편성 = 최종 전투력"(기획서 3.2)이 성립한다.
            if (battle.Tower.IsActive)
                return false;

            if (dice == null || battle.Game.IsBoardLockedByWave)
                return false;

            // 보는 것은 <b>대상</b>의 보유다 — Normal 을 3성까지 올려도 Tornado 가 없으면 못 간다.
            if (!DiceEvolution.CanEvolve(dice.Type, dice.Star, ownership.IsOwned))
                return false;

            if (!DiceEvolution.TryGetEvolveTarget(dice.Type, out DiceType target))
                return false;

            int cost = DiceEvolution.GetEvolveCost(dice.Type);
            if (battle.BattlePoints == null || !battle.BattlePoints.TrySpend(BattlePointType.EnhanceStone, cost))
                return false;

            // 상위 단계는 성급 개념이 없다(showStarUI = false). 옛 조합도 결과를 항상
            // 1성으로 놓았고, 그 값이 곧 데미지의 pip 이자 쿨타임 배수의 지수라
            // 함부로 올리면 밸런스가 통째로 움직인다. 파워는 성급이 아니라
            // baseAttack 으로 올렸다 — DiceMetaDataDatabase 에셋 참조.
            const int evolvedStar = 1;
            ReplaceInPlace(dice, target, evolvedStar);

            if (DiceEvolution.GetTier(target) == DiceTier.King)
            {
                RelicManager.Instance?.OnMythicCrafted(target);

                // 킹이 나왔다 — 전체 화면 소환 연출. 연출이 끝나는 자리에서 다이스를 드러내고
                // 번개·팝을 터뜨린다(RevealMythic). 연출 창을 못 얻으면 그냥 바로 드러낸다.
                UIMythicSummonCinematic cinematic = GameContainer.UI?.Get<UIMythicSummonCinematic>();
                if (cinematic != null)
                {
                    HideForCinematic(dice);
                    cinematic.Play(target, dice);
                }
                else
                {
                    RevealMythic(dice);
                }
            }
            else
            {
                // 진화한 자리에 속성 색 번개가 내리친다. ReplaceInPlace 는 같은 오브젝트를
                // 그대로 두므로(새로 찍지 않는다) 연출 대상이 도중에 사라지지 않는다.
                DiceFx.PlayEvolve(dice.transform as RectTransform, DiceMetaDataProvider.GetColor(target));
            }

            RunHistoryManager.Instance?.RecordCraft(target, battle.Game.CurrentWaveIndex);
            return true;
        }

        /// <summary>날아와 자리를 잡을 때까지 킹 다이스를 보이지 않게 한다. 슬롯·쿨타임 상태는 이미 킹이다.</summary>
        private static void HideForCinematic(UIDice dice)
        {
            CanvasGroup group = dice.GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = 0f;
        }

        /// <summary>
        /// 숨겨 둔 킹 다이스를 드러내고 등장 연출을 낸다 — 팝(<see cref="DiceFx.PlaySummon"/>)과
        /// 속성 색 번개(<see cref="DiceFx.PlayEvolve"/>). 소환 연출 창이 도착하는 순간 부른다.
        /// </summary>
        public void RevealMythic(UIDice dice)
        {
            if (dice == null)
                return;

            CanvasGroup group = dice.GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = 1f;

            DiceFx.PlaySummon(dice.transform);
            DiceFx.PlayEvolve(dice.transform as RectTransform, DiceMetaDataProvider.GetColor(dice.Type));
        }

        /// <summary>
        /// 같은 단계의 다른 다이스로 바꾼다. 성급은 그대로 간다 —
        /// 3성 노말을 교환하면 3성 파이어가 되지 1성으로 떨어지지 않는다.
        /// </summary>
        public bool TryExchange(UIDice dice)
        {
            // 탑에서는 진화·교환도 없다. 위 TryMerge 와 같은 이유이고, 셋을 함께 막아야
            // "편성 = 최종 전투력"(기획서 3.2)이 성립한다.
            if (battle.Tower.IsActive)
                return false;

            if (dice == null || battle.Game.IsBoardLockedByWave)
                return false;

            if (!DiceEvolution.CanExchange(dice.Type))
                return false;

            // 후보가 0이면 여기서 돌아간다. 이 검사가 TrySpend 보다 앞이라
            // 재화를 내고 나서 바꿀 것이 없어지는 경로는 없다.
            if (!DiceEvolution.TryGetExchangeCandidates(dice.Type, exchangeBuffer, ownership.IsOwned))
                return false;

            int cost = DiceEvolution.GetExchangeCost(dice.Type);
            if (battle.BattlePoints == null || !battle.BattlePoints.TrySpend(BattlePointType.EnhanceStone, cost))
                return false;

            DiceType target = exchangeBuffer[Random.Range(0, exchangeBuffer.Count)];
            ReplaceInPlace(dice, target, dice.Star);
            return true;
        }

        // 교환 후보를 담는 재사용 버퍼. 매번 List 를 새로 만들지 않으려는 것이고,
        // 이 시스템은 씬에 하나뿐이라 재진입이 없다.
        private readonly List<DiceType> exchangeBuffer = new List<DiceType>(5);

        /// <summary>
        /// 슬롯은 그대로 두고 타입·성급만 바꿔 끼운다.
        ///
        /// <b>재고 갱신 순서가 중요하다.</b> <c>OnDiceRemove</c> → <c>OnDiceSpawn</c> 이어야
        /// <c>DiceTypeStarManager</c> 의 (타입,성급) 카운트가 맞고, 그 안에서
        /// <c>PlayerController.RefreshDice</c> 와 보드 UI 갱신이 같이 돈다.
        /// 새 오브젝트를 만들지 않으므로 슬롯 인덱스와 드래그 상태가 보존된다.
        /// </summary>
        private void ReplaceInPlace(UIDice dice, DiceType newType, int newStar)
        {
            battle.DiceStars.OnDiceRemove(dice.Type, dice.Star);
            dice.Init(newType, newStar, dice.SlotIndex);
            battle.DiceStars.OnDiceSpawn(newType, newStar);
        }
    }
}
