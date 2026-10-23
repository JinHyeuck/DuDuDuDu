using System.Collections.Generic;

namespace OJ.StageReward
{
    /// <summary>
    /// 1~10스테이지 클리어 보상표 (2026-10-22 사용자 지시로 다시 책정).
    ///
    /// <b>원칙.</b> 보상은 "그 스테이지를 깨면 <b>이 레벨까지 올릴 수 있는 재화</b>" 다. 장비·다이스 강화 비용(골드+두루마리)을 그대로
    /// 계산해서 넣었다 — 그래서 뉴비가 보상을 받고도 강화를 못 하는 일이 없다. 1~3스테이지는 장비·다이스·고기·핀볼 티켓만 준다.
    /// 1스테이지 5웨이브에서 다이스 가이드가 노멀 다이스를 1→3 으로 올려 주므로(재화를 채워 준다) 노멀은 3레벨에서 시작한다.
    ///
    /// <b>세 칸에 나눠 준다</b>(웨이브 1/3 · 2/3 · 전부 클리어): 첫 칸은 장비 두루마리, 둘째 칸은 다이스 두루마리·고기·핀볼 티켓,
    /// 마지막 칸은 레어석(4스테이지부터 신화석). 골드는 세 칸에 35% · 30% · 35% 로 나뉜다. 한 칸에 아이콘이 열 개를 넘지 않게 하려는 것이다.
    /// 세 칸을 다 받으면 아래 목표 레벨에 정확히 닿는다.
    ///
    /// <b>4스테이지부터는 킹 다이스 강화 재화</b>(신화석 10 + 골드 300)가 더해진다 — 킹 썬더를 키우게 하려는 것이다.
    ///
    /// 비용 상수는 <c>EquipmentUpgradeFormula.Rule</c> 과 <c>DiceMetaDataDatabase.asset</c>(기본 다이스 130/55 골드, 10/20 두루마리)에서 왔다.
    /// 그 값이 바뀌면 <c>StageRewardPlanTests</c> 가 깨진다.
    /// </summary>
    public static class StageRewardPlan
    {
        public const int LastPlannedStage = 10;

        /// <summary>칸 번호: 0 = 첫 칸(웨이브 1/3), 1 = 둘째 칸(2/3), 2 = 마지막 칸(전부 클리어).</summary>
        public const int TierCount = 3;

        public static bool Covers(int stageIndex)
        {
            return stageIndex >= 1 && stageIndex <= LastPlannedStage;
        }

        public static List<StageRewardEntry> Build(int stageIndex, int tier)
        {
            var list = new List<StageRewardEntry>();
            if (!Covers(stageIndex) || tier < 0 || tier >= TierCount)
                return list;

            switch (stageIndex)
            {
                // 1스테이지 (시험 모드 — 5 · 10 · 15웨이브 보스, 웨이브로 해금):
                //   5웨이브  = 다이스 재료만 (번개 2, 불 2). 노멀 1→3 은 다이스 가이드가 재화를 채워 주며 올려 준다(LobbyUnlockDirector)
                //   10웨이브 = 장비 재료 (무기 3, 투구·갑옷·반지·신발·목걸이 2) + 고기
                //   15웨이브 = 레어석 — 레어 다이스 두 개를 3레벨까지 (11개씩)
                case 1:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 260), E(PointType.ThunderScroll, 10), E(PointType.FireScroll, 10) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 787), E(PointType.WeaponScroll, 7), E(PointType.HelmetScroll, 2), E(PointType.ArmorScroll, 2), E(PointType.RingScroll, 3), E(PointType.ShoesScroll, 2), E(PointType.NecklaceScroll, 2), E(PointType.Stamina, 30) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 724), E(PointType.RareStone, 22) }); break;
                    }
                    break;
                // 2스테이지: 투구 3, 갑옷 3, 반지 3, 신발 3, 목걸이 3, 노멀다이스 3, 독다이스 2
                case 2:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 370), E(PointType.HelmetScroll, 3), E(PointType.ArmorScroll, 3), E(PointType.RingScroll, 4), E(PointType.ShoesScroll, 3), E(PointType.NecklaceScroll, 3) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 320), E(PointType.NormalScroll, 30), E(PointType.PoisonScroll, 10), E(PointType.Stamina, 30), E(PointType.PinballTicket, 30) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 370), E(PointType.RareStone, 3) }); break;
                    }
                    break;
                // 3스테이지: 무기 4, 반지 4, 번개다이스 3, 얼음다이스 2
                case 3:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 260), E(PointType.WeaponScroll, 5), E(PointType.RingScroll, 5) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 220), E(PointType.ThunderScroll, 30), E(PointType.IceScroll, 10), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 270), E(PointType.RareStone, 3) }); break;
                    }
                    break;
                // 4스테이지: 투구 4, 갑옷 4, 불다이스 3, 얼음다이스 3
                case 4:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 370), E(PointType.HelmetScroll, 4), E(PointType.ArmorScroll, 4) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 320), E(PointType.FireScroll, 30), E(PointType.IceScroll, 30), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 370), E(PointType.RareStone, 3), E(PointType.MythicStone, 10) }); break;
                    }
                    break;
                // 5스테이지: 신발 4, 목걸이 4, 독다이스 3, 노멀다이스 4
                case 5:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 390), E(PointType.ShoesScroll, 4), E(PointType.NecklaceScroll, 4) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 330), E(PointType.PoisonScroll, 30), E(PointType.NormalScroll, 50), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 390), E(PointType.RareStone, 3), E(PointType.MythicStone, 10) }); break;
                    }
                    break;
                // 6스테이지: 무기 5, 반지 5, 번개다이스 4, 불다이스 4
                case 6:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 460), E(PointType.WeaponScroll, 6), E(PointType.RingScroll, 6) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 400), E(PointType.ThunderScroll, 50), E(PointType.FireScroll, 50), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 460), E(PointType.RareStone, 3), E(PointType.MythicStone, 10) }); break;
                    }
                    break;
                // 7스테이지: 투구 5, 갑옷 5, 얼음다이스 4, 독다이스 4
                case 7:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 440), E(PointType.HelmetScroll, 5), E(PointType.ArmorScroll, 5) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 380), E(PointType.IceScroll, 50), E(PointType.PoisonScroll, 50), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 450), E(PointType.RareStone, 3), E(PointType.MythicStone, 10) }); break;
                    }
                    break;
                // 8스테이지: 신발 5, 목걸이 5, 노멀다이스 5, 불다이스 5
                case 8:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 480), E(PointType.ShoesScroll, 5), E(PointType.NecklaceScroll, 5) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 410), E(PointType.NormalScroll, 70), E(PointType.FireScroll, 70), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 480), E(PointType.RareStone, 3), E(PointType.MythicStone, 10) }); break;
                    }
                    break;
                // 9스테이지: 무기 6, 반지 6, 번개다이스 5, 얼음다이스 5
                case 9:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 540), E(PointType.WeaponScroll, 7), E(PointType.RingScroll, 7) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 460), E(PointType.ThunderScroll, 70), E(PointType.IceScroll, 70), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 530), E(PointType.RareStone, 3), E(PointType.MythicStone, 10) }); break;
                    }
                    break;
                // 10스테이지: 투구 6, 갑옷 6, 독다이스 5, 번개다이스 6
                case 10:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 540), E(PointType.HelmetScroll, 6), E(PointType.ArmorScroll, 6) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 460), E(PointType.PoisonScroll, 70), E(PointType.ThunderScroll, 90), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 530), E(PointType.RareStone, 3), E(PointType.MythicStone, 10) }); break;
                    }
                    break;
            }

            return list;
        }

        private static StageRewardEntry E(PointType type, int amount)
        {
            return new StageRewardEntry(type, amount);
        }
    }
}
