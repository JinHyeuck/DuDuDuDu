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
