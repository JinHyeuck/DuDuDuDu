using System;
using System.Collections.Generic;

namespace OJ.Core
{
    /// <summary>
    /// 저장되는 영구 상태 전부. (MIGRATION_BASELINE 7.2)
    ///
    /// <b>왜 하나로 모으나.</b> 지금은 매니저 9개가 각자 PlayerPrefs 키를 들고 각자 저장한다 —
    /// <c>OJ.Point.*</c>, <c>OJ.Bullet.Level.*</c>, <c>OJ.Equipment.Save</c>, <c>OJ.Relic.Save</c>,
    /// <c>OJ.Stage.Progress</c>, <c>OJ.StageReward.Progress</c>, <c>OJ.StageStar.Progress</c>,
    /// <c>OJ.IdleReward.*</c>. 그래서 <b>세이브의 일부만 쓰이다 중단되면 서로 어긋난 상태로 남는다</b> —
    /// 재화는 빠졌는데 장비는 안 올라간, 되돌릴 수 없는 상태가 나온다. 파일 하나를 원자적으로
    /// 쓰면(7.4) 전부 반영되거나 전부 안 되거나 둘 중 하나가 된다.
    ///
    /// <b>여기 안 오는 것: <c>RunHistoryManager</c>.</b> 그건 진단용 로그(런 30개 × 이벤트 400개)라
    /// 크기도 수명도 다르다. 세이브가 커지면 원자 쓰기 비용이 그만큼 오르고, 로그가 깨졌다고
    /// 진행도까지 잃을 이유가 없다. 별도 파일로 남긴다.
    ///
    /// <b>키가 문자열인 이유.</b> <c>OJ.Core</c> 는 <c>DiceType</c>·<c>PointType</c> 같은 enum 을
    /// 못 본다(asmdef 경계). 그런데 그 제약이 오히려 맞다 — 기존 코드는 enum 을 <b>정수로</b>
    /// 저장했고, 그래서 enum 에 값을 끼워 넣으면 저장된 값이 조용히 다른 것을 가리킨다.
    /// 실제로 이 리포에서 보석 <c>targetDiceType</c> 이 그렇게 어긋나 효과 52개가 죽어 있었다.
    /// 이름으로 저장하면 순서를 바꿔도 안전하고, 없어진 이름은 로드할 때 눈에 보인다.
    ///
    /// <b>컬렉션이 전부 get-only 인 이유.</b> Newtonsoft 는 set 할 수 없는 컬렉션 속성을
    /// <i>기존 인스턴스에 채워 넣는다.</i> 그래서 역직렬화 뒤에도 null 이 될 수 없다.
    /// 기존 매니저 4곳에 있던 <c>if (saveData.xxx == null) saveData.xxx = new List&lt;&gt;()</c>
    /// 방어 코드가 통째로 필요 없어진다. 비교자를 <see cref="StringComparer.Ordinal"/> 로
    /// 고정한 것도 같이 유지된다.
    ///
    /// <b><see cref="SortedDictionary{TKey,TValue}"/> 인 이유.</b> 출력이 넣은 순서와 무관하게
    /// 항상 같다. 세이브 파일을 diff 로 볼 수 있고, "저장→로드→저장" 이 바이트까지 같은지
    /// 테스트할 수 있다.
    /// </summary>
    public sealed class SaveState
    {
        /// <summary>
        /// 이 빌드가 쓰는 스키마 버전.
        ///
        /// 올려야 하는 때: 필드의 <b>의미</b>가 바뀔 때. 필드를 <i>더하는</i> 것은 올리지 않아도
        /// 된다 — 옛 세이브에는 그 키가 없고 기본값이 들어간다.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>이 세이브가 쓰여질 때의 스키마 버전. 반드시 JSON 첫 필드로 나간다.</summary>
        public int Version { get; set; } = CurrentVersion;

        /// <summary><c>PointType</c> 이름 → 보유량. (<c>OJ.Point.*</c>)</summary>
        public SortedDictionary<string, int> Points { get; } = NewIntMap();

        /// <summary><c>DiceType</c> 이름 → 레벨. (<c>OJ.Bullet.Level.*</c>)</summary>
        public SortedDictionary<string, int> DiceLevels { get; } = NewIntMap();

        /// <summary>유물. (<c>OJ.Relic.Save</c>)</summary>
        public RelicSave Relics { get; } = new RelicSave();

        /// <summary>장비·보석. (<c>OJ.Equipment.Save</c>)</summary>
        public EquipmentSave Equipment { get; } = new EquipmentSave();

        /// <summary>스테이지 진행도·보상. (<c>OJ.Stage.Progress</c> 외 2개)</summary>
        public StageSave Stage { get; } = new StageSave();

        /// <summary>
        /// 무한의 탑 진행도·해금·편성.
        ///
        /// <b>버전을 올리지 않았다.</b> 위 <see cref="CurrentVersion"/> 주석이 적어 둔 규칙
        /// 그대로다 — 필드를 <i>더하는</i> 것은 옛 세이브에 그 키가 없을 뿐이고 기본값이
        /// 들어간다. 여기서는 "탑을 한 번도 안 해 본 사람"과 정확히 같은 상태다.
        /// </summary>
        public TowerSave Tower { get; } = new TowerSave();

        /// <summary>방치 보상 타이머. (<c>OJ.IdleReward.*</c>)</summary>
        public IdleSave Idle { get; } = new IdleSave();

        internal static SortedDictionary<string, int> NewIntMap()
        {
            // Ordinal 을 못 박는다. 기본 비교자는 문화권을 타서 정렬 순서가 기계마다 달라질 수 있다.
            return new SortedDictionary<string, int>(StringComparer.Ordinal);
        }
    }

    /// <summary>유물 저장분.</summary>
    public sealed class RelicSave
    {
        /// <summary>유물 해금 판정에 쓰는 누적 소환 횟수.</summary>
        public int SummonCount { get; set; }

        /// <summary><c>RelicId</c> 이름 → 레벨. 레벨 0 은 넣지 않는다.</summary>
        public SortedDictionary<string, int> Levels { get; } = SaveState.NewIntMap();
    }

    /// <summary>장비·보석 저장분.</summary>
    public sealed class EquipmentSave
    {
        /// <summary><c>EquipmentType</c> 이름 → 강화 레벨.</summary>
        public SortedDictionary<string, int> Levels { get; } = SaveState.NewIntMap();

        /// <summary>
        /// <c>EquipmentType</c> 이름 → 슬롯에 낀 보석 id 배열.
        ///
        /// <b>빈 슬롯은 빈 문자열로 남긴다. 빼지 않는다.</b> 위치가 곧 슬롯 번호라
        /// 하나를 빼면 뒤가 전부 한 칸씩 당겨져 다른 슬롯에 낀 것이 된다.
        /// </summary>
        public SortedDictionary<string, List<string>> GemSlots { get; }
            = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

        /// <summary>보석 id → 보유 개수. 0 개는 넣지 않는다.</summary>
        public SortedDictionary<string, int> GemInventory { get; } = SaveState.NewIntMap();
    }

    /// <summary>스테이지 진행도 저장분. 원래 매니저 3개에 흩어져 있던 것이다.</summary>
    public sealed class StageSave
    {
        /// <summary>스테이지 선택 화면에서 마지막으로 고른 스테이지.</summary>
        public int SelectedIndex { get; set; } = 1;

        /// <summary>해금된 가장 높은 스테이지.</summary>
        public int HighestUnlockedIndex { get; set; } = 1;

        /// <summary>
        /// 스테이지 번호 → 기록.
        ///
        /// 키가 int 가 아니라 문자열인 것은 JSON 객체의 키가 원래 문자열이기 때문이다.
        /// int 키 딕셔너리로 두면 직렬화기마다 다르게 처리해 왕복이 깨질 수 있다.
        /// </summary>
        public SortedDictionary<string, StageRecordSave> Records { get; }
            = new SortedDictionary<string, StageRecordSave>(StringComparer.Ordinal);

        /// <summary>수령한 스테이지 보상 id. (<c>OJ.StageReward.Progress</c>)</summary>
        public List<string> ClaimedRewardIds { get; } = new List<string>();

        /// <summary>수령한 별 보상 인덱스. (<c>OJ.StageStar.Progress</c>)</summary>
        public List<int> ClaimedStarRewardIndices { get; } = new List<int>();
    }

    /// <summary>스테이지 1개의 기록.</summary>
    public sealed class StageRecordSave
    {
        /// <summary>수령한 보상 비트플래그.</summary>
        public int ClaimedRewardFlags { get; set; }

        /// <summary>최고 클리어 등급.</summary>
        public int BestClearGrade { get; set; }

        /// <summary>도달한 가장 높은 웨이브.</summary>
        public int BestClearedWave { get; set; }
    }

    /// <summary>
    /// 무한의 탑 저장분.
    ///
    /// <b>스테이지 저장분과 합치지 않는다.</b> 둘은 같은 "진행도" 로 보이지만 축이 다르다 —
    /// 스테이지는 웨이브 기록과 등급별 보상 플래그를 들고, 탑은 클리어 시간과 해금 목록을
    /// 든다. 합치면 한쪽에만 있는 필드가 다른 쪽 기록마다 빈칸으로 따라다니고, 무엇보다
    /// <b>둘 중 하나를 초기화할 방법이 없어진다</b>(기획서 9장의 "300층 이후 시즌제·초기화"가
    /// 열려 있는 항목이라 그 여지를 미리 없애면 안 된다).
    /// </summary>
    public sealed class TowerSave
    {
        /// <summary>클리어한 가장 높은 층. 0 이면 아직 1층도 못 깼다는 뜻이다.</summary>
        public int HighestClearedFloor { get; set; }

        // "선택한 층" 은 저장하지 않는다. 고를 것이 없기 때문이다 —
        // 도전 가능한 층은 언제나 HighestClearedFloor + 1 하나뿐이다(반복 없음).

        /// <summary>
        /// 층 번호 → 기록. 키가 문자열인 이유는 <see cref="StageSave.Records"/> 와 같다.
        /// <b>클리어한 층만 들어간다</b> — 300칸을 미리 만들어 두면 세이브가 이유 없이 커진다.
        /// </summary>
        public SortedDictionary<string, TowerFloorRecordSave> Records { get; }
            = new SortedDictionary<string, TowerFloorRecordSave>(StringComparer.Ordinal);

        // 해금 목록은 저장하지 않는다. <b>층 번호의 함수</b>이기 때문이다 —
        // "몇 층까지 깼는가" 하나에서 어떤 스페셜·신화가 열렸는지가 결정된다
        // (TowerProgressManager.IsDiceUnlocked). 따로 저장하면 정본이 둘이 되고,
        // 둘이 어긋났을 때(세이브 손상, 해금 사다리 수정) 어느 쪽이 맞는지
        // 판단할 근거가 없어진다.

        /// <summary>
        /// 마지막으로 출전한 편성. <c>"DiceType이름:성급"</c> 형태의 문자열이다.
        ///
        /// 기획서 5.5 "최근 편성 저장 — 직전 출전 편성을 자동 저장한다". 문자열인 것은
        /// <see cref="SaveState"/> 가 enum 을 볼 수 없기 때문이고, 그 제약이 여기서도 맞다 —
        /// 없어진 다이스 이름은 불러올 때 걸러지고 빈 슬롯으로 남는다.
        /// </summary>
        public List<string> LastLoadout { get; } = new List<string>();

        /// <summary>
        /// 빈 슬롯 안내를 이미 봤는가. 기획서 5.5 "빈 슬롯 안내는 최초 출전 시 1회만 노출".
        /// </summary>
        public bool EmptySlotNoticeShown { get; set; }
    }

    /// <summary>층 하나의 기록.</summary>
    public sealed class TowerFloorRecordSave
    {
        /// <summary>
        /// 최고 기록(밀리초). <b>초가 아니라 밀리초인 것이 중요하다.</b> 화면에는
        /// "42.3초" 처럼 소수 한 자리로 나가는데(기획서 5.6), 초를 정수로 저장하면
        /// 그 소수가 저장 왕복에서 사라져 기록 갱신이 0.1초 단위로 뭉개진다.
        /// float 로 두지 않는 것은 왕복 비교가 정확해야 하기 때문이다.
        /// </summary>
        public int BestClearMilliseconds { get; set; }

        /// <summary>이 층을 클리어한 적이 있는가. 최초 클리어 보상 중복 지급을 막는 기준이다.</summary>
        public bool Cleared { get; set; }

        /// <summary>
        /// 실패했을 때 남은 적 체력의 비율(0~100). 기획서 5.6 의 "보스 체력 12% 잔여 ·
        /// 이전보다 6%p 개선" 이 이 값의 차이다. <b>실패도 진척으로 읽히게</b> 하려면
        /// 실패 기록도 남아야 한다.
        /// </summary>
        public int BestRemainingHpPercent { get; set; } = 100;
    }

    /// <summary>
    /// 방치 보상 저장분.
    ///
    /// <b>UTC tick 이다.</b> 로컬 시각으로 저장하면 시간대를 넘나들거나 서머타임이 바뀔 때
    /// 경과 시간이 음수가 되거나 몇 시간씩 뛴다. 기존 코드도 UTC 였고 그대로 유지한다.
    /// </summary>
    public sealed class IdleSave
    {
        /// <summary>자동 전투 누적 시작 시각(UTC tick).</summary>
        public long AutoBattleStartUtcTicks { get; set; }

        /// <summary>고기 축제 누적 시작 시각(UTC tick).</summary>
        public long MeatFestivalStartUtcTicks { get; set; }
    }
}
