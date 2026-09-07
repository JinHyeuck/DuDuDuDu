using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using OJ.Core;
using OJ.Tower;

namespace OJ.EditorTools
{
    /// <summary>
    /// <see cref="TowerDatabase"/> 에셋을 만들고 검사한다.
    /// <c>BountyDatabaseAssetBuilder</c> 와 같은 자리의 도구다.
    ///
    /// <b>덮어쓰지 않는다.</b> 이미 있으면 검사만 하고 끝낸다 — 손으로 조정한 밸런스를
    /// 도구가 조용히 되돌리면, 값이 왜 바뀌었는지 아무도 못 찾는다.
    /// 기본값으로 되돌리려면 에셋을 지우고 다시 돌릴 것.
    /// </summary>
    public static class TowerDatabaseAssetBuilder
    {
        private const string AssetPath = "Assets/ScriptableObject/TowerDatabase.asset";

        [MenuItem("OJ/개발/무한의 탑/데이터베이스 에셋 만들기")]
        private static void CreateOrValidate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TowerDatabase>(AssetPath);
            if (existing != null)
            {
                Report("[탑] 이미 있다: " + AssetPath, existing);
                return;
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath));

            var database = ScriptableObject.CreateInstance<TowerDatabase>();
            database.PopulateDefaults();

            AssetDatabase.CreateAsset(database, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report("[탑] 새로 만들었다: " + AssetPath + System.Environment.NewLine +
                   "  StaticResource 빈 슬롯 채우기가 다음 컴파일에 자동으로 잇는다.", database);
        }

        [MenuItem("OJ/개발/무한의 탑/데이터베이스 검사")]
        private static void ValidateOnly()
        {
            var database = AssetDatabase.LoadAssetAtPath<TowerDatabase>(AssetPath);
            if (database == null)
            {
                Debug.LogError("[탑] 에셋이 없다: " + AssetPath +
                               " — 지금은 코드 기본값으로 돌아가고 있다.");
                return;
            }

            Report("[탑] 검사: " + AssetPath, database);
        }

        /// <summary>
        /// 층별 수치를 표로 뽑는다. <b>밸런스를 눈으로 보는 유일한 수단</b>이다 —
        /// 300층을 인스펙터에서 하나씩 눌러 볼 수는 없고, 곡선이 어디서 꺾이는지는
        /// 숫자를 나란히 놓아야 보인다.
        /// </summary>
        [MenuItem("OJ/개발/무한의 탑/밸런스 표 뽑기")]
        private static void DumpBalanceTable()
        {
            TowerDatabase database = AssetDatabase.LoadAssetAtPath<TowerDatabase>(AssetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<TowerDatabase>();
                database.PopulateDefaults();
                Debug.LogWarning("[탑] 에셋이 없어 코드 기본값으로 표를 뽑는다.");
            }

            var sb = new StringBuilder();
            sb.AppendLine("층\t구간\t콘셉트\t마리\t체력\t방어\t속도\t총체력\t목표처치\t최초골드");

            // 전부 찍으면 로그 창이 잘린다. 구간 첫 층과 마지막 층만 본다 —
            // 구간 안에서 달라지는 것은 마리 수뿐이라 그 둘이면 곡선이 다 보인다.
            for (int band = 1; band <= TowerFormula.BandCount; band++)
            {
                int first = TowerFormula.BandStartFloor(band);
                int last = TowerFormula.ClampFloor(first + TowerFormula.FloorsPerBand - 1);

                AppendRow(sb, database, first);
                if (last != first)
                    AppendRow(sb, database, last);
            }

            string path = "Temp/tower_balance.tsv";
            System.IO.Directory.CreateDirectory("Temp");
            System.IO.File.WriteAllText(path, sb.ToString(), new System.Text.UTF8Encoding(true));

            Debug.Log("[탑] 밸런스 표를 " + path + " 에 썼다 (" +
                      TowerFormula.BandCount * 2 + "줄). 표계산기에서 열어 볼 것.");
        }

        private static void AppendRow(StringBuilder sb, TowerDatabase database, int floor)
        {
            TowerFloorPlan plan = database.GetPlan(floor);

            sb.Append(plan.Floor).Append('\t')
                .Append(plan.Band).Append('\t')
                .Append(plan.DisplayName).Append('\t')
                .Append(plan.MonsterCount).Append('\t')
                .Append(plan.MonsterHp).Append('\t')
                .Append(plan.MonsterDefense).Append('\t')
                .Append(plan.MoveSpeedMultiplier.ToString("0.00")).Append('\t')
                .Append(plan.TotalHpPool).Append('\t')
                .Append(plan.KillTarget).Append('\t')
                .Append(TowerFormula.FirstClearGold(plan.Floor))
                .AppendLine();
        }

        private static void Report(string header, TowerDatabase database)
        {
            var sb = new StringBuilder(header).AppendLine();
            sb.AppendLine("  구간 " + database.Bands.Count + "개 / 해금 " + database.DiceUnlocks.Count + "개");

            // 학습 구간 넷은 기획서에 마리 수까지 적혀 있어 검증 값어치가 크다.
            for (int floor = 1; floor <= 20; floor += 5)
            {
                TowerFloorPlan plan = database.GetPlan(floor);
                sb.AppendLine("    " + floor + "층 " + plan.DisplayName +
                              " — " + plan.MonsterCount + "마리 × HP " + plan.MonsterHp +
                              " / 방어 " + plan.MonsterDefense);
            }

            TowerFloorPlan top = database.GetPlan(TowerFormula.TotalFloors);
            sb.AppendLine("    " + TowerFormula.TotalFloors + "층 " + top.DisplayName +
                          " — " + top.MonsterCount + "마리 × HP " + top.MonsterHp +
                          " / 방어 " + top.MonsterDefense);

            List<string> problems = database.Validate();
            if (problems.Count == 0)
            {
                sb.Append("  문제 없음.");
                Debug.Log(sb.ToString());
                return;
            }

            sb.AppendLine("  문제 " + problems.Count + "건:");
            foreach (string p in problems)
                sb.AppendLine("    !! " + p);

            Debug.LogError(sb.ToString());
        }
    }
}
