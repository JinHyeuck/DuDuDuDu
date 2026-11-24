using System.Collections.Generic;

namespace OJ.StageReward
{
    /// <summary>
    /// 1~10스테이지 클리어 보상표 (2026-10-22 사용자 지시로 다시 책정, 2026-11 카피 개편 반영).
    ///
    /// <b>원칙.</b> 보상은 "그 스테이지를 깨면 <b>이 레벨까지 올릴 수 있는 재화</b>" 다. 장비 강화 비용(골드+두루마리)과
    /// 다이스 레벨업 비용(<b>카피 장수</b>, <c>DiceCopyCurve</c>)을 그대로 계산해서 넣었다 — 그래서 뉴비가 보상을 받고도
    /// 강화를 못 하는 일이 없다. 1~3스테이지는 장비·다이스·고기·핀볼 티켓만 준다.
    /// 1스테이지 5웨이브에서 다이스 가이드가 노멀 다이스를 1→3 으로 올려 주므로(재화를 채워 준다) 노멀은 3레벨에서 시작한다.
    ///
    /// <b>카피 개편 후 달라진 것.</b> 다이스 레벨업에 골드가 들지 않는다(카피만). 그래서 골드는 <b>장비 강화 비용만</b> 맞춘다 —
    /// 예전 골드에 섞여 있던 다이스 몫과 킹 몫(300)이 빠졌다. 다이스 재료는 속성 스크롤(= 기본 카피) 장수 그대로이고,
    /// 레어·킹 몫은 등급 공용이던 레어석·신화석 대신 <b>랜덤 레어·킹 카피</b>(지급 시 다이스 하나로 풀림)로 준다.
    ///
    /// <b>세 칸에 나눠 준다</b>(웨이브 1/3 · 2/3 · 전부 클리어): 첫 칸은 장비 두루마리, 둘째 칸은 다이스 카피·고기·핀볼 티켓,
    /// 마지막 칸은 랜덤 레어 카피(4스테이지부터 랜덤 킹 카피도). 골드는 세 칸에 35% · 30% · 35% 로 나뉜다.
    /// 한 칸에 아이콘이 열 개를 넘지 않게 하려는 것이다. 세 칸을 다 받으면 아래 목표 레벨에 정확히 닿는다.
    ///
    /// 장비 비용 상수는 <c>EquipmentUpgradeFormula.Rule</c> 에서 왔다. 그 값이 바뀌면 <c>StageRewardPlanTests</c> 가 깨진다.
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
                //   5웨이브  = 다이스 재료만 (번개 1, 불 1 — 둘 다 1→2). 노멀 1→3 은 다이스 가이드가 재화를 채워 주며 올려 준다(LobbyUnlockDirector)
                //   10웨이브 = 장비 재료 (무기 3, 투구·갑옷·반지·신발·목걸이 2) + 고기
                //   15웨이브 = 랜덤 레어 카피 6장 — 레어 다이스 두 개를 3레벨까지 (3장씩: 1+2)
                case 1:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.ThunderScroll, 1), E(PointType.FireScroll, 1) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 787), E(PointType.WeaponScroll, 7), E(PointType.HelmetScroll, 2), E(PointType.ArmorScroll, 2), E(PointType.RingScroll, 3), E(PointType.ShoesScroll, 2), E(PointType.NecklaceScroll, 2), E(PointType.Stamina, 30) }); break;
                        default: list.AddRange(new[] { E(PointType.RandomRareCopy, 6) }); break;
                    }
                    break;
                // 2스테이지: 투구 3, 갑옷 3, 반지 3, 신발 3, 목걸이 3, 독다이스 2 (노멀 3 은 이미 올라 있어 노멀 2장은 보너스)
                case 2:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 261), E(PointType.HelmetScroll, 3), E(PointType.ArmorScroll, 3), E(PointType.RingScroll, 4), E(PointType.ShoesScroll, 3), E(PointType.NecklaceScroll, 3) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 224), E(PointType.NormalScroll, 2), E(PointType.PoisonScroll, 1), E(PointType.Stamina, 30), E(PointType.PinballTicket, 30) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 260), E(PointType.RandomRareCopy, 1) }); break;
                    }
                    break;
                // 3스테이지: 무기 4, 반지 4, 번개다이스 3, 얼음다이스 2
                case 3:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 152), E(PointType.WeaponScroll, 5), E(PointType.RingScroll, 5) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 131), E(PointType.ThunderScroll, 2), E(PointType.IceScroll, 1), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 152), E(PointType.RandomRareCopy, 1) }); break;
                    }
                    break;
                // 4스테이지: 투구 4, 갑옷 4, 불다이스 3, 얼음다이스 3
                case 4:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 137), E(PointType.HelmetScroll, 4), E(PointType.ArmorScroll, 4) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 117), E(PointType.FireScroll, 2), E(PointType.IceScroll, 2), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 136), E(PointType.RandomRareCopy, 1), E(PointType.RandomKingCopy, 2) }); break;
                    }
                    break;
                // 5스테이지: 신발 4, 목걸이 4, 독다이스 3, 노멀다이스 4
                case 5:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 135), E(PointType.ShoesScroll, 4), E(PointType.NecklaceScroll, 4) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 116), E(PointType.PoisonScroll, 2), E(PointType.NormalScroll, 4), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 134), E(PointType.RandomRareCopy, 1), E(PointType.RandomKingCopy, 2) }); break;
                    }
                    break;
                // 6스테이지: 무기 5, 반지 5, 번개다이스 4, 불다이스 4
                case 6:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 189), E(PointType.WeaponScroll, 6), E(PointType.RingScroll, 6) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 162), E(PointType.ThunderScroll, 4), E(PointType.FireScroll, 4), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 189), E(PointType.RandomRareCopy, 1), E(PointType.RandomKingCopy, 2) }); break;
                    }
                    break;
                // 7스테이지: 투구 5, 갑옷 5, 얼음다이스 4, 독다이스 4
                case 7:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 172), E(PointType.HelmetScroll, 5), E(PointType.ArmorScroll, 5) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 147), E(PointType.IceScroll, 4), E(PointType.PoisonScroll, 4), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 171), E(PointType.RandomRareCopy, 1), E(PointType.RandomKingCopy, 2) }); break;
                    }
                    break;
                // 8스테이지: 신발 5, 목걸이 5, 노멀다이스 5, 불다이스 5
                case 8:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 168), E(PointType.ShoesScroll, 5), E(PointType.NecklaceScroll, 5) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 144), E(PointType.NormalScroll, 5), E(PointType.FireScroll, 5), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 168), E(PointType.RandomRareCopy, 1), E(PointType.RandomKingCopy, 2) }); break;
                    }
                    break;
                // 9스테이지: 무기 6, 반지 6, 번개다이스 5, 얼음다이스 5
                case 9:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 224), E(PointType.WeaponScroll, 7), E(PointType.RingScroll, 7) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 192), E(PointType.ThunderScroll, 5), E(PointType.IceScroll, 5), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 224), E(PointType.RandomRareCopy, 1), E(PointType.RandomKingCopy, 2) }); break;
                    }
                    break;
                // 10스테이지: 투구 6, 갑옷 6, 독다이스 5, 번개다이스 6
                case 10:
                    switch (tier)
                    {
                        case 0: list.AddRange(new[] { E(PointType.Gold, 205), E(PointType.HelmetScroll, 6), E(PointType.ArmorScroll, 6) }); break;
                        case 1: list.AddRange(new[] { E(PointType.Gold, 176), E(PointType.PoisonScroll, 5), E(PointType.ThunderScroll, 7), E(PointType.Stamina, 30), E(PointType.PinballTicket, 20) }); break;
                        default: list.AddRange(new[] { E(PointType.Gold, 204), E(PointType.RandomRareCopy, 1), E(PointType.RandomKingCopy, 2) }); break;
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
