using System;

namespace OJ.Relic
{
    public enum RelicId
    {
        None = 0,
        BeginnerPouch = 1,
        BattleVault = 2,
        LuckyMagazine = 3,
        RepairHammer = 4,
        QuickHands = 5,
        LootMap = 6,
        EmberJar = 7,
        FrostNail = 8,
        LightningRodRing = 9,
        PoisonIncense = 10,
        TornadoAnchor = 11,
        ParalysisNeedle = 12,
        CrackHammer = 13,
        TailwindFeather = 14,
        MergeInsurance = 15,
        TwinSummonStone = 16,
        JustOneHit = 17,
        LuckyGesture = 18,
        KingBlueprint = 19,
        FullBoardPressure = 20,
        AdvanceDeployment = 21,
        LuckyInvitation = 22,
        CrownResonance = 23,
        LastWall = 24,
    }

    [Serializable]
    public struct RelicSummonCost
    {
        public int goldCost;
        public int ticketCost;

        public RelicSummonCost(int goldCost, int ticketCost)
        {
            this.goldCost = goldCost;
            this.ticketCost = ticketCost;
        }
    }

    public class RelicSummonResult
    {
        public RelicDefinition Definition;
        public int OldLevel;
        public int NewLevel;
        public bool IsNew => OldLevel <= 0 && NewLevel > 0;
        public bool IsLevelUp => NewLevel > OldLevel;
        public bool IsMaxDuplicate => OldLevel == NewLevel && NewLevel > 0;
    }
}
