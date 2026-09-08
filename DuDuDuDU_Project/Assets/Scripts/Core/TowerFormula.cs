namespace OJ.Core
{
    /// <summary>
    /// 무한의 탑의 순수 산술. 엔진도 <c>DiceType</c> 같은 enum 도 쓰지 않는다.
    /// (<c>BountyFormula</c> 와 같은 자리의 파일이다.)
    ///
    /// <b>왜 300층을 표로 적지 않는가.</b> 층마다 체력·수·방어력을 손으로 적으면 900칸을
    /// 관리하게 되고, 밸런스를 한 번 만질 때마다 그 900칸이 전부 대상이 된다.
    /// 여기서는 <b>층 번호 하나에서 뽑는 성장 곡선</b> 하나와, 구간(5층)마다 붙는
    /// <b>콘셉트 배수</b> 하나만 둔다. 곡선을 바꾸면 300층이 같이 움직이고,
    /// 배수를 바꾸면 그 콘셉트를 쓰는 구간만 움직인다.
    ///
    /// <b>스테이지 성장식(<see cref="StageGrowthFormula"/>)을 재사용하지 않는 이유.</b>
    /// 저쪽은 <b>웨이브</b>가 축이라 한 스테이지(10~20웨이브) 안에서 오르내리는 곡선이고,
    /// 이쪽은 <b>층</b>이 축이라 300칸을 한 번에 훑어야 한다. 같은 식에 다른 축을 태우면
    /// 한쪽을 고칠 때마다 다른 쪽 밸런스가 조용히 바뀐다 — 그것이 기획서 2장이 말하는
    /// "두 콘텐츠가 서로를 대체하지 않게" 의 산술판이다.
    ///
    /// <b>여기 있는 값이 곧 화면에 뜨는 숫자다.</b> 층 선택 화면의 체력 미리보기,
    /// 결과 화면의 보상, 편성 화면의 슬롯 수가 전부 이 함수들에서 나온다.
    /// </summary>
    public static class TowerFormula
    {
        // ── 규모 ────────────────────────────────────────────────────────

        /// <summary>탑의 총 층수. 기획서 "약 300층".</summary>
        public const int TotalFloors = 300;

        /// <summary>구간 하나의 층수. 기획서 "기본적으로 5층마다 적 구성과 공략 핵심을 변경".</summary>
        public const int FloorsPerBand = 5;

        /// <summary>
        /// 학습 구간의 마지막 층. 기획서 6.1 — 물량 → 단일 → 속도 → 방어를 하나씩 익히는
        /// 20층까지다. 21층부터 조합을 요구한다.
        /// </summary>
        public const int TutorialLastFloor = 20;

        // ── 편성 슬롯 (기획서 4.1) ──────────────────────────────────────

        /// <summary>신화 슬롯 수.</summary>
        public const int MythicSlotCount = 1;

        /// <summary>스페셜 슬롯 수.</summary>
        public const int SpecialSlotCount = 2;

        /// <summary>기본 다이스 슬롯 수. 1·2·3·4성 각 1개라 성급 수와 같다.</summary>
        public const int BaseSlotCount = 4;

        /// <summary>기본 다이스 슬롯의 최고 성급. 4성 슬롯이 있다는 뜻이다.</summary>
        public const int MaxBaseStar = BaseSlotCount;

        /// <summary>편성 슬롯 총합. 기획서 "최대 7개 = 기본 4(고정) + 스페셜 2 + 신화 1".</summary>
        public const int TotalSlotCount = MythicSlotCount + SpecialSlotCount + BaseSlotCount;

        // ── 층 → 구간 ───────────────────────────────────────────────────

        /// <summary>
        /// 층 번호를 1..<see cref="TotalFloors"/> 로 자른다. <b>0 층이나 301 층은 없다.</b>
        /// 저장된 진행도가 깨졌을 때 그것이 배열 색인 사고로 번지지 않게 하는 자리다.
        /// </summary>
        public static int ClampFloor(int floor)
        {
            return OJMath.Clamp(floor, 1, TotalFloors);
        }

        /// <summary>
        /// 이 층이 속한 구간 번호. 1층~5층이 1, 6층~10층이 2 … 296층~300층이 60 이다.
        /// <b>1부터 센다</b> — 화면에 "3구간" 처럼 그대로 쓰이기 때문에 0 부터 세면
        /// 표시할 때마다 +1 을 하게 되고, 그 +1 을 한 곳에서 빠뜨린다.
        /// </summary>
        public static int BandOf(int floor)
        {
            return (ClampFloor(floor) - 1) / FloorsPerBand + 1;
        }

        /// <summary>구간 안에서 몇 번째 층인가. 1..<see cref="FloorsPerBand"/>.</summary>
        public static int FloorInBand(int floor)
        {
            return (ClampFloor(floor) - 1) % FloorsPerBand + 1;
        }

        /// <summary>구간의 첫 층 번호.</summary>
        public static int BandStartFloor(int band)
        {
            int validBand = OJMath.Max(1, band);
            return ClampFloor((validBand - 1) * FloorsPerBand + 1);
        }

        /// <summary>구간 수. 300층 / 5 = 60.</summary>
        public static int BandCount => (TotalFloors + FloorsPerBand - 1) / FloorsPerBand;

        /// <summary>
        /// 이 층이 구간의 마지막 층인가. <b>희귀 보상이 붙는 층</b>이다(기획서 7.1).
        /// 300층처럼 마지막 구간이 잘려 있어도 참이 되도록 총층수도 같이 본다.
        /// </summary>
        public static bool IsBandLastFloor(int floor)
        {
            int valid = ClampFloor(floor);
            return valid == TotalFloors || FloorInBand(valid) == FloorsPerBand;
        }

        /// <summary>구간 보상까지 남은 층 수. 층 선택 화면의 게이지가 쓴다(기획서 5.2).</summary>
        public static int FloorsUntilBandReward(int floor)
        {
            return FloorsPerBand - FloorInBand(floor);
        }

        // ── 성장 곡선 ───────────────────────────────────────────────────
        //
        // 세 식이 형태가 같다: 상수 + 선형항 + 거듭제곱항. 층이 300 까지 가므로
        // 순수 2차식(웨이브 성장식의 형태)을 쓰면 후반이 폭발한다 — 지수를 1.5 근처로
        // 두면 후반이 완만해지면서도 초반 20층에서 체감되는 차이가 남는다.
        //
        // 값은 스테이지 30(현재 마지막)의 웨이브 15 체력 780 · 방어력 399 를 기준선으로
        // 잡아 맞췄다. 탑은 다이스가 7개뿐이라 같은 성장 수준에서 그보다 낮아야 한다.

        private const float HpBase = 6f;
        private const float HpLinear = 0.30f;
        private const float HpPowerScale = 0.05f;
        private const float HpPowerExponent = 1.55f;

        // 방어력은 <b>곱셈 감쇄</b>라 체력보다 훨씬 위험한 손잡이다.
        // <c>IncomingDamageFormula.DefenseMultiplier</c> 가 <c>100/(100+armor)</c> 이므로
        // 방어력을 두 배로 올리면 유효 체력이 두 배가 아니라 그 이상으로 뛴다.
        //
        // 그래서 <b>본편의 마지막 스테이지를 상한으로 잡았다</b>: 30스테이지 15웨이브의
        // 일반 몬스터 방어력이 399(감쇄 0.20)이고, 보스가 998(감쇄 0.091)이다.
        // 아래 값은 300층에서 394(0.20) 가 되고, 고방어 구간(×3)이 얹혀도 1182(0.078)로
        // 본편 보스 근처에 머문다 — 그 위로 올리면 아머브레이크가 <b>선택이 아니라
        // 필수</b>가 되어 기획서 8.2 의 "특정 다이스 하나만으로 대부분의 층을 해결" 을
        // 뒤집은 형태(그것 없이는 아무 층도 못 깨는)가 된다.
        private const float DefenseBase = 3f;
        private const float DefenseLinear = 0.65f;
        private const float DefensePowerScale = 0.33f;
        private const float DefensePowerExponent = 1.12f;

        /// <summary>
        /// 이 층의 <b>기준</b> 몬스터 체력. 콘셉트 배수가 아직 안 곱해진 값이다.
        ///
        /// 1층 8 · 20층 73 · 100층 563 · 300층 2616 근처가 된다.
        /// <b>이 숫자를 바꾸면 300층 전체가 같이 움직인다.</b> 한 구간만 조정하려면
        /// 콘셉트 배수나 구간 정의의 배수를 만질 것.
        /// </summary>
        public static int BaseMonsterHp(int floor)
        {
            float f = ClampFloor(floor);
            float value = HpBase * (1f + f * HpLinear + OJMath.Pow(f, HpPowerExponent) * HpPowerScale);
            return OJMath.Max(1, OJMath.RoundToInt(value));
        }

        /// <summary>
        /// 이 층의 <b>기준</b> 몬스터 방어력.
        ///
        /// 방어력은 <see cref="IncomingDamageFormula.DefenseMultiplier"/> 로 <b>곱셈 감쇄</b>가
        /// 되므로 아무리 커져도 피해가 0 이 되지는 않는다. 그래서 상한을 걸지 않는다 —
        /// 300층 570 이면 감쇄가 0.149 배이고, 방어 감소(아머브레이크)가 그 자리에서
        /// 눈에 보이는 차이를 만든다. 그것이 "고방어 부대" 구간의 설계 의도다.
        /// </summary>
        public static int BaseMonsterDefense(int floor)
        {
            float f = ClampFloor(floor);
            float value = DefenseBase
                          + f * DefenseLinear
                          + OJMath.Pow(f, DefensePowerExponent) * DefensePowerScale;
            return OJMath.Max(0, OJMath.RoundToInt(value));
        }

        /// <summary>
        /// 콘셉트 배수를 먹인 최종 체력. <b>반올림은 마지막에 한 번만</b> 한다 —
        /// 기준 체력을 먼저 반올림해 넘기면 배수가 작을 때 계단이 생긴다.
        /// </summary>
        public static int MonsterHp(int floor, float hpMultiplier)
        {
            return OJMath.Max(1, OJMath.RoundToInt(BaseMonsterHp(floor) * OJMath.Max(0.01f, hpMultiplier)));
        }

        /// <summary>콘셉트 배수를 먹인 최종 방어력. 0 은 정상값이라 하한이 0 이다.</summary>
        public static int MonsterDefense(int floor, float defenseMultiplier)
        {
            return OJMath.Max(0, OJMath.RoundToInt(BaseMonsterDefense(floor) * OJMath.Max(0f, defenseMultiplier)));
        }

        /// <summary>
        /// 이 층에 나오는 몬스터 수.
        ///
        /// <paramref name="bandCount"/> 는 구간 정의가 적어 둔 첫 층의 마리 수,
        /// <paramref name="perFloorStep"/> 는 구간 안에서 층마다 늘어나는 수다.
        /// 기획서 6.1 의 "1~5층: 약 30마리부터 시작해 층마다 수 증가" 가 이 두 값이다.
        ///
        /// <b>1 미만이 될 수 없다.</b> 0 마리 층은 시작하자마자 클리어되어 층이 통째로
        /// 사라진 것처럼 보인다.
        /// </summary>
        public static int MonsterCount(int floor, int bandCount, int perFloorStep)
        {
            int step = (FloorInBand(floor) - 1) * perFloorStep;
            return OJMath.Max(1, bandCount + step);
        }

        /// <summary>
        /// 이 층에서 <b>잡아야 하는 총 마리 수</b>. 분열형 적은 자식까지 세야 한다.
        ///
        /// <b>웨이브 종료 판정이 이 값에 걸려 있다.</b> <c>GameManager</c> 는 처치 수가
        /// 목표에 닿아야 웨이브를 끝내는데, 분열 자식을 빼놓으면 목표를 채우고도
        /// 자식이 화면에 남고, 반대로 부모만 세면 목표가 영영 안 채워진다.
        /// 분열 수가 고정이라 <b>미리 셀 수 있다</b> — 그래서 여기서 한 번에 정한다.
        /// </summary>
        public static int WaveKillTarget(int monsterCount, int splitChildCount)
        {
            int validCount = OJMath.Max(1, monsterCount);
            int validSplit = OJMath.Max(0, splitChildCount);
            return validCount + validCount * validSplit;
        }

        /// <summary>
        /// 몬스터 사이의 등장 간격(초).
        ///
        /// <b>스테이지의 고정 2초를 쓰면 안 된다.</b> 45마리 물량 층이 등장에만 90초를
        /// 쓰게 되어, 30~60초 안에 끝난다는 이 콘텐츠의 전제가 무너진다.
        /// 여기서는 <b>총 등장 시간</b>을 고정하고 간격을 거기서 역산한다 —
        /// 마리 수가 몇이든 전부 나오는 데 걸리는 시간이 비슷해진다.
        ///
        /// 하한 0.08 은 한 프레임에 여러 마리가 겹쳐 쏟아지는 것을 막고,
        /// 상한 1.2 는 한 마리짜리 층에서 그 한 마리를 1.2초보다 오래 기다리지 않게 한다.
        /// </summary>
        public static float SpawnInterval(int monsterCount)
        {
            const float TotalSpawnSeconds = 12f;
            int validCount = OJMath.Max(1, monsterCount);
            return OJMath.Clamp(TotalSpawnSeconds / validCount, 0.08f, 1.2f);
        }

        // ── 보상 (기획서 7.1) ───────────────────────────────────────────

        /// <summary>
        /// 층 최초 클리어 골드.
        ///
        /// <b>10층 단위 계단이 들어 있다.</b> <c>StageRewardFormula.StageBonus</c> 와 같은
        /// 형태이고 이유도 같다 — 정수 나눗셈의 버림이 만드는 계단이 "열 층을 더 올랐다"를
        /// 눈에 보이게 한다. float 로 바꾸면 계단이 사라져 매 층이 똑같아 보인다.
        /// </summary>
        public static int FirstClearGold(int floor)
        {
            int valid = ClampFloor(floor);
            return 60 + valid * 8 + (valid / 10) * 40;
        }

        /// <summary>
        /// 구간 마지막 층의 다이아. <see cref="IsBandLastFloor"/> 인 층에서만 준다.
        /// 구간 번호로 오르므로 60구간을 다 오르면 128 이 된다.
        /// </summary>
        public static int BandRewardDia(int floor)
        {
            return 10 + (BandOf(floor) - 1) * 2;
        }

        /// <summary>
        /// 구간 마지막 층이 주는 <b>다이스 재료</b>의 양. 어떤 재화로 낼지는 여기서 정하지 않는다
        /// (<c>TowerRunManager.BuildClearRewards</c> 가 정한다) — 이 함수는 곡선일 뿐이다.
        ///
        /// <b>원래는 전투 강화석이었고, 그것은 틀린 선택이었다.</b> 그 재화는 판이 시작될 때
        /// 0 으로 밀린다(<c>ElementUpgradeManager.ResetRunState</c>). 받자마자 다음 층에
        /// 들어가면 사라지고 로비에는 쓸 곳조차 없어서, <b>주는 척만 하는 보상</b>이었다.
        /// 지금은 신화 스크롤로 낸다 — 탑이 여는 것이 킹 다이스이고 킹을 올리는 재료가
        /// 그것이라, "탑을 올라 다이스를 키우고 키운 다이스로 더 올라간다"는 7.2 의 순환이
        /// <b>사라지지 않는 재화로</b> 닫힌다.
        ///
        /// <b>이름에 재화를 넣지 않은 이유</b>도 같다 — 한 번 갈아 끼웠으니 또 갈아 끼울 수 있고,
        /// 그때 이름이 거짓말을 하면 안 된다.
        /// </summary>
        public static int BandRewardMaterial(int floor)
        {
            return 12 + (BandOf(floor) - 1) * 3;
        }

        // ── 진행 ────────────────────────────────────────────────────────

        /// <summary>
        /// 지금 도전할 수 있는 가장 높은 층. 기획서 3.2 "층을 클리어하면 다음 층이 열린다".
        /// 아무것도 못 깼으면 1층이다.
        /// </summary>
        public static int HighestUnlockedFloor(int highestClearedFloor)
        {
            return OJMath.Clamp(highestClearedFloor + 1, 1, TotalFloors);
        }

        /// <summary>
        /// 이 층의 정보를 보여 줄 수 있는가. <b>도전 가능과 다르다</b> —
        /// 이미 깬 층은 정보를 보여 주되 들어갈 수는 없다
        /// (<see cref="IsFloorChallengeable"/>).
        ///
        /// 층 선택 화면이 카드의 콘셉트·기록을 채울지 <c>? ? ?</c> 로 가릴지를
        /// 이 함수로 정한다.
        /// </summary>
        public static bool IsFloorRevealed(int floor, int highestClearedFloor)
        {
            if (floor < 1 || floor > TotalFloors)
                return false;

            return floor <= HighestUnlockedFloor(highestClearedFloor);
        }

        /// <summary>
        /// 이 층에 지금 들어갈 수 있는가. <b>도전 가능한 층은 언제나 하나뿐이다.</b>
        ///
        /// <b>클리어한 층은 다시 들어갈 수 없다.</b> 1층을 깨면 보상을 받고 끝이고,
        /// 그다음에는 2층만 도전한다 — 탑은 오르는 것이지 도는 것이 아니다.
        /// 실패한 층은 그 층이 아직 "다음 층" 이므로 자연히 다시 도전하게 된다.
        ///
        /// <b>기획서 5.2 와 다르다.</b> 목업에는 클리어한 층 카드에 재도전 버튼이
        /// 그려져 있지만, 반복 플레이 여부는 9장이 열어 둔 항목이었고
        /// <b>"반복 없음" 으로 확정됐다</b>(2026-09-05). 그래서 그 버튼이 빠졌다.
        /// 되살리려면 이 함수를 <see cref="IsFloorRevealed"/> 와 같게 만들면 된다.
        /// </summary>
        public static bool IsFloorChallengeable(int floor, int highestClearedFloor)
        {
            if (floor < 1 || floor > TotalFloors)
                return false;

            return floor == HighestUnlockedFloor(highestClearedFloor);
        }

        /// <summary>
        /// 해금까지의 진행도(0~1). 결과 화면의 "해금 진행도" 게이지가 쓴다(기획서 5.6).
        ///
        /// <paramref name="unlockFloor"/> 가 0 이하이면 해금 조건이 없다는 뜻이라 1 이다 —
        /// 게이지가 0 으로 남아 "영영 안 열리는 것"처럼 보이지 않게 한다.
        /// </summary>
        public static float UnlockProgress01(int highestClearedFloor, int unlockFloor)
        {
            if (unlockFloor <= 0)
                return 1f;

            return OJMath.Clamp01((float)OJMath.Max(0, highestClearedFloor) / unlockFloor);
        }
    }
}
