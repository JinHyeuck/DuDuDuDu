using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.DI;
using OJ.Dice;
using OJ.Point;
using OJ.Save;

namespace OJ.Tower
{
    /// <summary>
    /// 이번에 들어갈 층과 편성. 로비에서 채우고 전투 씬이 읽는다.
    ///
    /// <b>왜 정적 전역이 아니라 매니저의 필드인가.</b> 정적으로 두면 씬을 두 번 오갈 때
    /// 지난 요청이 남아 있는지 아무도 모른다. 매니저가 들고 있으면 소비 시점
    /// (<see cref="TowerProgressManager.ConsumePendingRun"/>)이 한 곳으로 모인다.
    /// </summary>
    public sealed class TowerRunRequest
    {
        public int Floor { get; }
        public TowerLoadout Loadout { get; }

        public TowerRunRequest(int floor, TowerLoadout loadout)
        {
            Floor = TowerFormula.ClampFloor(floor);

            // 사본을 만든다. 화면이 들고 있는 편성을 그대로 참조하면, 결과 화면에서
            // "편성 변경" 을 눌러 고치는 순간 <b>방금 끝난 판의 기록까지 같이 바뀐다.</b>
            Loadout = new TowerLoadout();
            Loadout.CopyFrom(loadout);
        }
    }

    /// <summary>층 하나의 기록. 세이브의 <see cref="TowerFloorRecordSave"/> 와 짝이다.</summary>
    public sealed class TowerFloorRecord
    {
        public bool Cleared;

        /// <summary>최고 기록(밀리초). 0 이면 아직 클리어한 적이 없다.</summary>
        public int BestClearMilliseconds;

        /// <summary>실패 기록 중 가장 좋은 것. 남은 적 체력 비율(0~100).</summary>
        public int BestRemainingHpPercent = 100;
    }

    /// <summary>
    /// 무한의 탑의 진행도·해금·편성. 루트에 사는 영구 서비스다.
    /// (<c>StageProgressManager</c> 와 같은 자리·같은 형태)
    ///
    /// <b>왜 <c>StageProgressManager</c> 에 얹지 않았나.</b> 저쪽은 "스테이지 → 웨이브 →
    /// 등급별 보상 플래그" 라는 축을 갖고, 이쪽은 "층 → 클리어 시간 → 다이스 해금" 이라는
    /// 다른 축을 갖는다. 얹으면 스테이지 기록마다 탑 전용 필드가 빈칸으로 따라다니고,
    /// 무엇보다 <b>탑만 초기화하는 것이 불가능해진다</b> — 기획서 9장이 시즌제·초기화를
    /// 열어 둔 항목으로 적고 있어서 그 여지를 미리 없애면 안 된다.
    /// </summary>
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고 — 에디터에서는 안 드러난다.
    [Preserve]
    public sealed class TowerProgressManager : ISaveStateOwner
    {
        /// <summary>
        /// 과도기 다리. <b>대입은 <see cref="GameContainer"/> 에서만 한다.</b>
        /// <c>StageProgressManager.Instance</c> 와 같은 성격이고, 호출부가 전부 주입으로
        /// 옮겨지면 사라진다.
        /// </summary>
        public static TowerProgressManager Instance { get; internal set; }

        /// <summary>진행도가 바뀌었다. 로비의 탑 버튼과 층 선택 화면이 구독한다.</summary>
        public event Action OnProgressChanged;

        /// <summary>
        /// 편성에 넣을 수 있는지 묻는 곳. <b>층 진행이 아니라 보유가 기준이다.</b>
        ///
        /// 생성자로 받는 것이 중요하다 — <c>.Instance</c> 로 잡으면 이 클래스가
        /// <c>BeforeSceneLoad</c> 에서 만들어지는데 그때 다리가 아직 안 이어져 있을 수 있다.
        /// 컨테이너는 의존 순서를 알아서 정하므로 여기서는 없을 수가 없다.
        /// </summary>
        private readonly DiceOwnershipManager ownership;

        public TowerProgressManager(DiceOwnershipManager ownership)
        {
            this.ownership = ownership;
        }

        private readonly Dictionary<int, TowerFloorRecord> records = new Dictionary<int, TowerFloorRecord>();

        /// <summary>
        /// 마지막으로 출전한 편성을 <b>토큰 그대로</b> 들고 있다. 기획서 5.5 "최근 편성 저장".
        ///
        /// <b>왜 파싱해 두지 않나 — 이것이 이 클래스에서 가장 미묘한 자리다.</b>
        /// 파싱하려면 그 다이스가 해금됐는지 물어야 하고, 그 판정은
        /// <see cref="TowerDatabaseProvider"/> 를 거쳐 <c>StaticResource</c> 를 깨운다.
        /// 그런데 <see cref="ReadFrom"/> 은 <c>GameContainer</c> 부트스트랩
        /// (<c>BeforeSceneLoad</c>) 안에서 돈다 — <b>씬이 아직 하나도 없는 시점</b>이다.
        /// 거기서 <c>StaticResource</c> 를 건드리면 Resources 프리팹이 그 자리에서
        /// 인스턴스화되고, 못 찾으면 "데이터베이스가 없다" 는 <b>거짓 에러</b>가 한 번 찍힌다.
        /// 그 로그는 한 번만 나오게 잠기므로 나중의 진짜 사고를 대신 삼킨다.
        /// <c>StageProgressManager</c> 생성자가 <c>ClampStageIndices()</c> 를 일부러
        /// 부르지 않는 것과 <b>정확히 같은 함정</b>이다.
        ///
        /// 그래서 파싱을 <see cref="CloneLastLoadout"/> 까지 미룬다 — 그것을 부르는 것은
        /// 편성 화면뿐이고, 그때는 씬이 이미 서 있다.
        /// </summary>
        private readonly List<string> lastLoadoutTokens = new List<string>();

        private int highestClearedFloor;
        private bool emptySlotNoticeShown;

        /// <summary>
        /// 다음 전투가 탑인가. 전투 씬이 <see cref="ConsumePendingRun"/> 으로 가져간다.
        ///
        /// <b>씬 로드가 실패해도 남지 않게</b> 소비는 한 번뿐이다. 남아 있으면 다음에
        /// 본편 스테이지로 들어갈 때 탑 층이 열린다.
        /// </summary>
        private TowerRunRequest pendingRun;

        public int HighestClearedFloor => Mathf.Clamp(highestClearedFloor, 0, TowerFormula.TotalFloors);

        /// <summary>지금 도전할 수 있는 가장 높은 층.</summary>
        public int HighestUnlockedFloor => TowerFormula.HighestUnlockedFloor(HighestClearedFloor);

        /// <summary>탑을 한 번이라도 열어 봤는가. 로비 버튼의 NEW 표시가 쓴다.</summary>
        public bool HasAnyClear => HighestClearedFloor > 0;

        public bool EmptySlotNoticeShown => emptySlotNoticeShown;

        // "선택한 층" 을 들고 있지 않다. <b>고를 것이 없기 때문이다</b> —
        // 도전 가능한 층은 언제나 HighestUnlockedFloor 하나뿐이다(반복 없음).
        // 필드를 남겨 두면 아무도 안 읽는 값이 세이브에 굳고, 읽는 사람마다
        // "이게 기능인지 사고인지" 를 다시 확인하게 된다.

        // ── 기록 ────────────────────────────────────────────────────────

        public bool IsFloorCleared(int floor)
        {
            return records.TryGetValue(floor, out TowerFloorRecord record) && record.Cleared;
        }

        /// <summary>이 층의 정보를 보여 줄 수 있는가. 이미 깬 층도 참이다.</summary>
        public bool IsFloorRevealed(int floor)
        {
            return TowerFormula.IsFloorRevealed(floor, HighestClearedFloor);
        }

        /// <summary>
        /// 이 층에 지금 들어갈 수 있는가. <b>언제나 한 층뿐이다</b> —
        /// 클리어한 층은 다시 못 들어간다(<see cref="TowerFormula.IsFloorChallengeable"/>).
        /// </summary>
        public bool IsFloorChallengeable(int floor)
        {
            return TowerFormula.IsFloorChallengeable(floor, HighestClearedFloor);
        }

        /// <summary>최고 기록(밀리초). 클리어한 적이 없으면 0.</summary>
        public int GetBestClearMilliseconds(int floor)
        {
            return records.TryGetValue(floor, out TowerFloorRecord record) ? record.BestClearMilliseconds : 0;
        }

        /// <summary>실패 기록 중 가장 좋은 잔여 체력 비율. 기록이 없으면 100.</summary>
        public int GetBestRemainingHpPercent(int floor)
        {
            return records.TryGetValue(floor, out TowerFloorRecord record) ? record.BestRemainingHpPercent : 100;
        }

        /// <summary>
        /// 클리어를 기록한다.
        ///
        /// <b>최초 클리어인지를 돌려준다.</b> 보상이 최초 한 번뿐이므로(기획서 7.1
        /// "각 층 <i>최초 클리어</i> 시 기본 재화를 제공한다") 지급 여부의 판단이
        /// 여기 한 곳에서만 나와야 한다 — 호출부가 따로 세면 두 번 주는 사고가 난다.
        ///
        /// <b>반복이 없으므로 지금은 언제나 true 다.</b> 그래도 판정을 남기는 이유는
        /// 이것이 <b>재화를 주는 유일한 잠금장치</b>이기 때문이다 — 같은 층의 결과가
        /// 두 번 들어오는 경로(중복 호출, 나중에 생길 재도전 모드)가 열리는 날
        /// 이 한 줄이 이중 지급을 막는다.
        /// </summary>
        /// <returns>이번이 이 층의 최초 클리어면 true.</returns>
        /// <param name="refundInto">
        /// 이미 보유한 다이스를 이 층이 열었을 때 <b>환급 재화가 담기는 곳</b>.
        /// 호출부가 자기 보상 목록에 합쳐 한 번에 지급한다 — 지급 경로가 둘이 되면
        /// 이중 지급을 막을 자리가 없어진다. null 이면 환급을 버린다(치트 경로).
        /// </param>
        public bool RecordClear(int floor, int clearMilliseconds, out bool isNewRecord,
            List<PointRewardEntry> refundInto = null)
        {
            isNewRecord = false;

            int validFloor = TowerFormula.ClampFloor(floor);
            int validMs = Mathf.Max(1, clearMilliseconds);

            TowerFloorRecord record = GetOrCreateRecord(validFloor);
            bool firstClear = !record.Cleared;

            record.Cleared = true;
            record.BestRemainingHpPercent = 0;

            if (record.BestClearMilliseconds <= 0 || validMs < record.BestClearMilliseconds)
            {
                // 첫 클리어도 기록 갱신으로 센다. 결과 화면의 NEW RECORD 배지가
                // "이 층에서 처음 세운 기록" 에도 떠야 자연스럽다(기획서 5.6).
                record.BestClearMilliseconds = validMs;
                isNewRecord = true;
            }

            if (validFloor > highestClearedFloor)
                highestClearedFloor = validFloor;

            // 층을 올린 것만으로는 다이스가 열리지 않는다. 보유는 진행도의 함수가 아니라
            // 별개 상태이고, 실제 지급은 보상 배관(GameManager 의 탑 결과 처리)이 한다.
            // 여기서는 편성 화면의 NEW 배지가 쓸 "방금 열렸다" 만 남긴다 —
            // 그것은 계산으로 알 수 없는 <b>사건</b>이기 때문이다.
            if (firstClear)
            {
                DiceType granted = GetUnlockGrantedByFloor(validFloor);

                // <b>세우는 자리가 하나다.</b> 실제로 새로 얻었을 때만 pendingNewUnlock 이 서므로,
                // 이미 보유였다면 결과창의 "해금!" 배너와 편성 화면의 NEW 배지가 같이 조용해지고
                // 대신 환급이 보상 목록에 뜬다. 둘이 어긋날 수가 없다.
                if (granted != DiceType.Max && ownership.GrantFromContent(granted, refundInto))
                    pendingNewUnlock = granted;
            }

            Save();
            OnProgressChanged?.Invoke();
            return firstClear;
        }

        /// <summary>
        /// 실패를 기록한다. <b>실패도 진척으로 읽히게</b> 하는 것이 목적이라
        /// 남은 적 체력이 이전보다 줄었으면 갱신한다(기획서 5.6).
        /// </summary>
        /// <returns>이전 기록보다 나아졌으면 true.</returns>
        public bool RecordFail(int floor, int remainingHpPercent)
        {
            int validFloor = TowerFormula.ClampFloor(floor);
            int validPercent = Mathf.Clamp(remainingHpPercent, 0, 100);

            TowerFloorRecord record = GetOrCreateRecord(validFloor);

            // 이미 클리어한 층에서 실패해도 기록을 되돌리지 않는다. 클리어는 되돌릴 수
            // 없는 사실이고, 재도전 실패로 "0% → 40%" 가 되면 결과 화면이 후퇴를 보여 준다.
            //
            // 반복이 없어 지금은 도달하지 않는 가지다. 지우지 않는 이유는 위
            // RecordClear 의 최초 클리어 판정과 같다 — 기록을 지키는 잠금장치다.
            if (record.Cleared)
            {
                Save();
                return false;
            }

            bool improved = validPercent < record.BestRemainingHpPercent;
            if (improved)
                record.BestRemainingHpPercent = validPercent;

            Save();
            OnProgressChanged?.Invoke();
            return improved;
        }

        private TowerFloorRecord GetOrCreateRecord(int floor)
        {
            if (records.TryGetValue(floor, out TowerFloorRecord record))
                return record;

            record = new TowerFloorRecord();
            records.Add(floor, record);
            return record;
        }

        // ── 해금 ────────────────────────────────────────────────────────

        /// <summary>
        /// 이 층을 클리어하면 열리는 다이스. 없으면 <c>DiceType.Max</c>.
        ///
        /// <b>사다리의 정본이 <c>DiceUnlockDatabase</c> 로 옮겨 갔다.</b> 가격과 세 컨텐츠의
        /// 보상처가 한 목록에 있어야 중복 환급이 성립하기 때문이다 — 그쪽 클래스 주석 참조.
        /// 여기 남은 것은 "탑이 그 목록의 층 칸을 읽는다" 는 사실뿐이다.
        ///
        /// 에셋을 깨우므로 씬이 선 뒤에만 부를 것.
        /// </summary>
        public DiceType GetUnlockGrantedByFloor(int floor)
        {
            return DiceUnlockDatabaseProvider.Database.GetUnlockAtFloor(floor);
        }

        /// <summary>
        /// 다음에 열릴 탑 해금. 층 선택 화면의 목표 배너와 결과 화면의 진행도가 쓴다.
        /// 전부 열었으면 null.
        /// </summary>
        public DiceUnlockDefinition GetNextUnlock()
        {
            return DiceUnlockDatabaseProvider.Database.GetNextTowerUnlock(HighestClearedFloor);
        }

        // ── 편성 ────────────────────────────────────────────────────────

        /// <summary>
        /// 마지막 편성의 <b>사본</b>. 화면이 이것을 받아 편집한다.
        /// 원본을 넘기면 편집 도중의 상태가 그대로 저장 대상이 되어, 취소가 불가능해진다.
        /// </summary>
        public TowerLoadout CloneLastLoadout()
        {
            // 여기서 처음 파싱한다. 위 lastLoadoutTokens 주석 참조 — 해금 판정이
            // 데이터베이스를 깨우므로 씬이 선 뒤에만 할 수 있다.
            //
            // 해금이 취소되는 경로는 지금 없지만(해금은 층 번호의 함수다) 사다리를
            // 고치면 생긴다. 그때 못 고르는 다이스가 편성에 남아 있으면, 화면에서는
            // 지울 수도 없는 칸으로 보인다.
            var copy = new TowerLoadout();
            copy.LoadTokens(lastLoadoutTokens, ownership.IsOwned);
            return copy;
        }

        /// <summary>
        /// 편성을 저장한다. 기획서 5.5 "직전 출전 편성을 자동 저장한다".
        /// 출전 버튼을 누른 순간 불린다 — 편성 화면을 열어 보기만 한 것은 저장하지 않는다.
        /// </summary>
        public void SaveLoadout(TowerLoadout loadout)
        {
            lastLoadoutTokens.Clear();
            if (loadout != null)
                lastLoadoutTokens.AddRange(loadout.ToTokens());

            Save();
        }

        /// <summary>빈 슬롯 안내를 봤다고 기록한다. 기획서 5.5 "최초 출전 시 1회만".</summary>
        public void MarkEmptySlotNoticeShown()
        {
            if (emptySlotNoticeShown)
                return;

            emptySlotNoticeShown = true;
            Save();
        }

        /// <summary>
        /// 층 특성에 맞춰 자동 편성한다. 기획서 5.5 "추천 편성 — 층 특성 기반으로 자동 구성".
        ///
        /// <b>정답을 내는 함수가 아니다.</b> 보유한 것 중에서 그 층의 대응 키워드에
        /// 가까운 것을 채우는 <b>출발점</b>이고, 유저가 거기서 바꾸는 것이 이 콘텐츠의
        /// 재미다(기획서 1.3 "적 정보를 읽고 조합을 설계하는 단계").
        /// 그래서 스페셜·신화도 남는 칸을 그냥 채우지, 최적해를 찾지 않는다.
        /// </summary>
        public TowerLoadout BuildRecommendedLoadout(TowerFloorPlan plan)
        {
            var loadout = new TowerLoadout();
            if (plan == null)
                return loadout;

            // 기본 4칸: 층의 주 콘셉트가 선호하는 속성을 4성부터 채우고, 나머지 성급은
            // 서로 다른 속성으로 흩어 놓는다. 한 속성으로 몰면 상성이 어긋났을 때
            // 통째로 무력해진다.
            DiceType preferred = PreferredBaseFor(plan.Concepts[0]);
            IReadOnlyList<DiceType> baseTypes = TowerLoadoutRules.BaseTypes;

            for (int star = TowerFormula.MaxBaseStar; star >= 1; star--)
            {
                int offset = TowerFormula.MaxBaseStar - star;
                DiceType diceType = offset == 0
                    ? preferred
                    : baseTypes[(IndexOf(baseTypes, preferred) + offset) % baseTypes.Count];

                loadout.Toggle(diceType, star);
            }

            // 스페셜·신화: 해금한 것 중 앞에서부터. 없으면 칸이 빈 채로 남는다 —
            // 그것이 기획서 4.2 의 "미보유 = 빈 슬롯" 이 화면에서 보이는 방식이다.
            FillTier(loadout, TowerLoadoutRules.MythicTypes);
            FillTier(loadout, TowerLoadoutRules.SpecialTypes);

            return loadout;
        }

        private void FillTier(TowerLoadout loadout, IReadOnlyList<DiceType> candidates)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                DiceType diceType = candidates[i];
                if (!ownership.IsOwned(diceType))
                    continue;

                if (loadout.IsTierFull(diceType, TowerLoadoutRules.NonBaseStar))
                    break;

                loadout.Toggle(diceType, TowerLoadoutRules.NonBaseStar);
            }
        }

        /// <summary>
        /// 콘셉트별로 먼저 넣어 볼 기본 속성. 기획서 6.2 의 "유효한 대응" 칸을
        /// 기본 다이스 다섯 종에 대응시킨 것이다.
        /// </summary>
        private static DiceType PreferredBaseFor(TowerFloorConcept concept)
        {
            switch (concept)
            {
                // 폭발·범위 → 화염
                case TowerFloorConcept.Swarm:
                    return DiceType.Fire;

                // 빙결·둔화 → 냉기
                case TowerFloorConcept.Rush:
                    return DiceType.Ice;

                // 지속 피해 → 독
                case TowerFloorConcept.Elite:
                case TowerFloorConcept.Regenerating:
                    return DiceType.Poison;

                // 다단·연쇄 → 전기
                case TowerFloorConcept.Shielded:
                case TowerFloorConcept.Splitting:
                    return DiceType.Thunder;

                // 단일 고화력 → 일반
                default:
                    return DiceType.Normal;
            }
        }

        private static int IndexOf(IReadOnlyList<DiceType> list, DiceType diceType)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == diceType)
                    return i;
            }

            return 0;
        }

        // ── 출전 요청 ───────────────────────────────────────────────────

        /// <summary>
        /// 이 층으로 들어간다고 예약한다. 편성 화면의 "전투 시작" 이 부른다.
        /// 씬 전환은 부르는 쪽이 한다 — 여기서 하면 이 매니저가 씬 흐름을 알게 된다.
        /// </summary>
        public void RequestRun(int floor, TowerLoadout loadout)
        {
            pendingRun = new TowerRunRequest(floor, loadout);
            SaveLoadout(loadout);
        }

        /// <summary>
        /// 예약을 가져간다. <b>한 번만 나온다</b> — 두 번째부터는 null 이다.
        /// 전투 씬이 <c>Start</c> 에서 한 번 부르고, 남겨 두면 다음에 본편 스테이지로
        /// 들어갈 때 탑이 열린다.
        /// </summary>
        public TowerRunRequest ConsumePendingRun()
        {
            TowerRunRequest request = pendingRun;
            pendingRun = null;
            return request;
        }

        /// <summary>예약을 버린다. 층 선택을 취소하고 로비로 돌아갈 때 쓴다.</summary>
        public void CancelPendingRun()
        {
            pendingRun = null;
        }

        /// <summary>
        /// 로비로 돌아가면 이 층의 편성 화면을 바로 열어 달라. 0 이면 열지 않는다.
        ///
        /// <b>왜 필요한가.</b> 결과 화면의 "편성 변경" · "다음 층 도전" 은 전투 씬에 있는데,
        /// 탑의 편성은 로비에서 한다(기획서 5.1). 그 사이에 씬 전환이 끼므로 <b>의사를
        /// 넘길 그릇</b>이 필요하다. 결과창이 로비의 UI 를 직접 열 수는 없다 —
        /// 그 순간 로비가 아직 존재하지 않는다.
        ///
        /// <b>세이브에 넣지 않는다.</b> 앱을 껐다 켜면 사라져야 하는 값이다.
        /// 남으면 다음 실행에서 로비를 열자마자 편성 화면이 튀어나온다.
        /// </summary>
        private int pendingLobbyFloor;

        public void RequestLobbyLoadout(int floor)
        {
            pendingLobbyFloor = TowerFormula.ClampFloor(floor);
        }

        /// <summary>
        /// 로비가 가져간다. <b>한 번만 나온다</b> — 남겨 두면 탭을 옮길 때마다
        /// 편성 화면이 다시 열린다.
        /// </summary>
        public int ConsumePendingLobbyFloor()
        {
            int floor = pendingLobbyFloor;
            pendingLobbyFloor = 0;
            return floor;
        }

        /// <summary>
        /// 방금 열린 다이스. 편성 화면의 NEW 배지가 쓴다(기획서 5.4).
        ///
        /// <b>진행도에서 계산할 수 없는 유일한 값이다.</b> "이 다이스가 열려 있는가" 는
        /// 층 번호의 함수지만 "방금 열렸는가" 는 사건이라, 클리어한 순간에만 알 수 있다.
        /// 세이브에 넣지 않는 것도 그래서다 — 앱을 껐다 켜면 더 이상 "방금" 이 아니다.
        /// </summary>
        private DiceType pendingNewUnlock = DiceType.Max;

        /// <summary>
        /// 가져간다. <b>한 번만 나온다</b> — 남겨 두면 배지가 영영 붙어 있고,
        /// 그러면 배지가 배경이 되어 다음 획득을 못 알린다.
        /// </summary>
        /// <summary>
        /// <b>소비하지 않고</b> 들여다본다. 결과 화면이 "해금!" 을 그릴 때 쓴다 —
        /// 거기서 소비해 버리면 편성 화면의 NEW 배지가 영영 안 뜬다.
        /// </summary>
        public DiceType PendingNewUnlock => pendingNewUnlock;

        public DiceType ConsumeNewUnlock()
        {
            DiceType diceType = pendingNewUnlock;
            pendingNewUnlock = DiceType.Max;
            return diceType;
        }

        // ── 세이브 ──────────────────────────────────────────────────────

        public void WriteTo(SaveState state)
        {
            state.Tower.HighestClearedFloor = HighestClearedFloor;
            state.Tower.EmptySlotNoticeShown = emptySlotNoticeShown;

            state.Tower.Records.Clear();
            foreach (KeyValuePair<int, TowerFloorRecord> pair in records)
            {
                TowerFloorRecord record = pair.Value;
                if (record == null)
                    continue;

                // 키는 반드시 불변 문화권으로 만든다. 비교자가 Ordinal 이라 다른 자형의
                // 숫자로 찍히면 같은 층이 다른 칸에 들어간다(StageProgressManager 와 같은 이유).
                state.Tower.Records[pair.Key.ToString(CultureInfo.InvariantCulture)] = new TowerFloorRecordSave
                {
                    Cleared = record.Cleared,
                    BestClearMilliseconds = record.BestClearMilliseconds,
                    BestRemainingHpPercent = record.BestRemainingHpPercent,
                };
            }

            state.Tower.LastLoadout.Clear();
            state.Tower.LastLoadout.AddRange(lastLoadoutTokens);
        }

        public void ReadFrom(SaveState state)
        {
            highestClearedFloor = Mathf.Clamp(state.Tower.HighestClearedFloor, 0, TowerFormula.TotalFloors);
            emptySlotNoticeShown = state.Tower.EmptySlotNoticeShown;

            records.Clear();
            foreach (KeyValuePair<string, TowerFloorRecordSave> pair in state.Tower.Records)
            {
                TowerFloorRecordSave saved = pair.Value;
                if (saved == null)
                    continue;

                // 키가 숫자로 안 읽히거나 층이 범위 밖이면 조용히 버린다. 손상된 기록 하나
                // 때문에 나머지 진행도까지 잃으면 안 된다(StageProgressManager 와 같은 판단).
                if (!int.TryParse(pair.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int floor))
                    continue;

                if (floor < 1 || floor > TowerFormula.TotalFloors || records.ContainsKey(floor))
                    continue;

                records.Add(floor, new TowerFloorRecord
                {
                    Cleared = saved.Cleared,
                    BestClearMilliseconds = Mathf.Max(0, saved.BestClearMilliseconds),
                    BestRemainingHpPercent = Mathf.Clamp(saved.BestRemainingHpPercent, 0, 100),
                });
            }

            // 토큰을 그대로 옮겨 담는다. <b>여기서 파싱하지 않는다</b> —
            // 파싱은 해금 판정을 부르고, 그것이 데이터베이스를 깨우는데 이 메서드는
            // 씬이 서기 전에 돈다(위 lastLoadoutTokens 주석 참조).
            lastLoadoutTokens.Clear();
            lastLoadoutTokens.AddRange(state.Tower.LastLoadout);

        }

#if UNITY_EDITOR || DEV_DEFINE
        /// <summary>
        /// 개발용. 진행도를 이 층까지 깬 상태로 만든다. <see cref="TowerProgressCheat"/> 만 부른다.
        ///
        /// <b>기록도 같이 만든다.</b> 진행도만 올리면 층 목록의 카드가 "미도전" 으로 뜨는데,
        /// 그건 실제로 깨고 올라온 상태와 다르게 보인다 — 테스트 도구가 만드는 상태는
        /// <b>진짜와 구별되지 않아야</b> 그 상태에서 나온 버그를 믿을 수 있다.
        /// 클리어 시간은 0 으로 둔다(없는 기록을 지어내지 않는다).
        ///
        /// <b>해금도 같이 켠다.</b> 예전에는 해금이 층 번호의 함수라 진행도만 올리면 따라왔지만,
        /// 이제 보유는 별개 상태다. 안 켜면 "250층을 깼는데 편성이 텅 빈" 상태가 되고,
        /// 그건 진짜와 구별되는 상태다 — 이 메서드의 존재 이유에 반한다.
        ///
        /// <c>internal</c> 인 것이 중요하다 — 이 메서드는 정상 경로에 존재해선 안 된다.
        /// </summary>
        internal void DevSetClearedFloor(int clearedFloor)
        {
            int target = Mathf.Clamp(clearedFloor, 0, TowerFormula.TotalFloors);
            highestClearedFloor = target;

            // 통째로 다시 만든다. 층을 <b>내리는</b> 경우에도 위쪽 기록이 남으면
            // "0층인데 50층 기록이 있는" 앞뒤가 안 맞는 상태가 된다.
            records.Clear();
            for (int floor = 1; floor <= target; floor++)
            {
                records.Add(floor, new TowerFloorRecord
                {
                    Cleared = true,
                    BestClearMilliseconds = 0,
                    BestRemainingHpPercent = 0,
                });
            }

            // 그 층들이 열어 줬을 다이스를 실제로 보유시킨다. 환급은 받지 않는다 —
            // 치트가 만든 상태에 재화까지 얹으면 밸런스를 볼 때 숫자가 오염된다.
            for (int floor = 1; floor <= target; floor++)
            {
                DiceType granted = DiceUnlockDatabaseProvider.Database.GetUnlockAtFloor(floor);
                if (granted != DiceType.Max)
                    ownership.GrantFromContent(granted, null);
            }

            Save();
            OnProgressChanged?.Invoke();
        }
#endif

        /// <summary>
        /// 지금 상태를 통합 세이브 파일에 즉시 쓴다.
        ///
        /// <c>?.</c> 가 필요하다. 매니저 <b>생성자가 도는 시점에는 SaveService 가 아직 없다</b> —
        /// 컨테이너가 매니저를 만든 뒤에 SaveService 를 해석하기 때문이다.
        /// (<c>StageProgressManager.Save</c> 와 같은 이유이고 같은 형태다.)
        /// </summary>
        private void Save() => GameContainer.SaveService?.SaveAll();
    }
}
