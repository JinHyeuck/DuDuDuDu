using System.Collections.Generic;
using System.Text;
using OJ.Core;
using OJ.Dice;

namespace OJ.Tower
{
    /// <summary>
    /// "보상 목록" 오버레이의 본문. 층 선택과 편성 두 화면이 같은 버튼을 가져서
    /// 본문을 만드는 곳을 여기 하나로 둔다 — 두 벌이 되면 한쪽만 고쳐지는 날이 온다.
    ///
    /// 구분 기호는 쉼표·쌍점만 쓴다. 가운뎃점(·)·줄표(—)는 BM HANNA 아틀라스에서 그림이 비어 빈칸으로 찍힌다.
    /// </summary>
    internal static class TowerRewardListText
    {
        /// <summary>
        /// 앞으로 받을 것들. <b>다 보여 주지 않는다</b> — 300층치를 늘어놓으면 목록이
        /// 아니라 표가 되고, 기획서 5.2 가 지키려는 "다음 한 층" 의 초점이 흐려진다.
        /// 다음 구간 보상 다섯 개와 남은 다이스 해금까지만 적는다.
        /// </summary>
        internal static string Build()
        {
            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress == null)
                return string.Empty;

            var sb = new StringBuilder();
            int floor = progress.HighestUnlockedFloor;

            sb.AppendLine("<b>구간 보상</b>");
            int band = TowerFormula.BandOf(floor);
            for (int i = 0; i < 5 && band + i <= TowerFormula.BandCount; i++)
            {
                int lastFloor = TowerFormula.ClampFloor(
                    TowerFormula.BandStartFloor(band + i) + TowerFormula.FloorsPerBand - 1);

                sb.Append("  ").Append(lastFloor).Append("층: 다이아 ")
                    .Append(TowerFormula.BandRewardDia(lastFloor))
                    .Append(", 신화 스크롤 ")
                    .Append(TowerFormula.BandRewardMaterial(lastFloor))
                    .AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("<b>다이스 해금</b>");

            // <b>기준이 층에서 보유로 바뀌었다.</b> 예전에는 "아직 안 올라간 층" 을 남은
            // 해금으로 셌지만, 이제 같은 다이스를 별·스테이지로 먼저 얻을 수 있다 —
            // 그러면 층은 안 올라갔어도 이미 갖고 있고, 목록에 남겨 두면 거짓말이 된다.
            DiceOwnershipManager ownership = DiceOwnershipManager.Instance;
            IReadOnlyList<DiceUnlockDefinition> unlocks = DiceUnlockDatabaseProvider.Database.Definitions;
            bool anyLocked = false;
            for (int i = 0; i < unlocks.Count; i++)
            {
                DiceUnlockDefinition unlock = unlocks[i];
                if (unlock == null || unlock.towerFloor <= 0)
                    continue;

                if (ownership != null && ownership.IsOwned(unlock.diceType))
                    continue;

                anyLocked = true;
                sb.Append("  ").Append(unlock.towerFloor).Append("층: ").Append(unlock.diceType).AppendLine();
            }

            if (!anyLocked)
                sb.AppendLine("  모두 사용할 수 있어요");

            sb.AppendLine();
            sb.Append("층 최초 클리어 시 골드 ")
                .Append(TowerFormula.FirstClearGold(floor))
                .Append(" (").Append(floor).Append("층 기준)");

            return sb.ToString();
        }
    }
}
