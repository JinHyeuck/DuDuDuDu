#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using OJ.Dice;

namespace OJ.EditorTools
{
    /// <summary>
    /// 기본 다이스 5종(Normal/Fire/Ice/Thunder/Poison)에 남아 있는 <b>낡은 조합식</b>을 지운다.
    ///
    /// 왜 있는가: 에셋의 기본 5종 조합식은 합성 5종 조합식이 슬롯째 복사된 잔재다.
    /// git 이력상 ef30864(2026-03-07)부터 fbe8eb7(2026-05-14)까지 기본5 == 합성5 였고,
    /// b5c721a("조합식 수정")가 합성 5종만 갱신하면서 기본 5종의 복사본이 낡은 채 남았다.
    /// 현재 조합 규칙은 "★2 재료 2개"이고 합성 5종은 그 형태지만, 기본 5종에 박힌 것은
    /// ★1이 섞인 이전 규칙이다.
    ///
    /// 무엇이 문제인가: 조합 기능 자체는 멀쩡하다 — 조합 UI 가 GetMythicTypes() 로 후보를
    /// 받으므로 기본 다이스는 아예 오르지 않는다. 문제는 <b>성장 상세 패널</b>이다.
    /// UIDiceGrowthPage 가 Enum.GetValues(typeof(DiceType)) 전체를 돌아 기본 다이스도
    /// 목록에 띄우고, UIDiceGrowthDetailPanel.RefreshRecipeSection 이 recipe.Count > 0 이면
    /// 조합 섹션을 켠다. 그래서 소환으로만 얻는 기본 다이스에 존재하지 않는 조합법이,
    /// 그것도 폐기된 수치로 표시된다.
    ///
    /// 왜 메뉴인가: 에셋을 파일시스템으로 편집하면 에디터가 켜져 있을 때 메모리의 사본이
    /// 덮어써 작업이 유실된다(AGENTS.md 절대 규칙). AssetDatabase 를 거친다.
    ///
    /// 자동 실행하지 않는다. 씬·에셋을 건드리는 에디터 훅은 1.1 에서 전부 걷어냈다.
    /// </summary>
    public static class DiceRecipeCleaner
    {
        private const string AssetPath = "Assets/ScriptableObject/DiceMetaDataDatabase.asset";

        // 소환으로만 얻는 타입. 조합식을 가질 이유가 없다.
        private static readonly DiceType[] SummonOnlyTypes =
        {
            DiceType.Normal, DiceType.Fire, DiceType.Ice, DiceType.Thunder, DiceType.Poison,
        };

        [MenuItem("Tools/OJ/Dice/Clear Stale Recipes On Basic Dice")]
        public static void Clear()
        {
            var database = AssetDatabase.LoadAssetAtPath<DiceMetaDataDatabase>(AssetPath);
            if (database == null)
            {
                Debug.LogError($"{AssetPath} 를 찾지 못했다.");
                return;
            }

            var serialized = new SerializedObject(database);
            SerializedProperty metas = serialized.FindProperty("metas");
            if (metas == null || !metas.isArray)
            {
                Debug.LogError("metas 배열을 찾지 못했다. 필드명이 바뀌었는지 확인할 것.");
                return;
            }

            var cleared = new List<string>();
            var alreadyEmpty = new List<string>();

            for (int i = 0; i < metas.arraySize; i++)
            {
                SerializedProperty meta = metas.GetArrayElementAtIndex(i);
                SerializedProperty diceType = meta.FindPropertyRelative("diceType");
                SerializedProperty recipe = meta.FindPropertyRelative("recipeMaterials");
                if (diceType == null || recipe == null)
                    continue;

                if (!IsSummonOnly((DiceType)diceType.intValue))
                    continue;

                string name = ((DiceType)diceType.intValue).ToString();
                if (recipe.arraySize == 0)
                {
                    alreadyEmpty.Add(name);
                    continue;
                }

                cleared.Add($"{name}({recipe.arraySize}개)");
                recipe.ClearArray();
            }

            if (cleared.Count == 0)
            {
                Debug.Log("지울 조합식이 없다. 이미 정리돼 있다: " + string.Join(", ", alreadyEmpty));
                return;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Verify(cleared);
        }

        /// <summary>
        /// 저장 직후 에셋을 다시 읽어 실제로 비었는지 본다. 여기서 확인하지 않으면
        /// "메뉴는 눌렀는데 저장이 안 된" 상태를 다음 F7 때까지 모른다.
        /// </summary>
        private static void Verify(List<string> cleared)
        {
            var reloaded = AssetDatabase.LoadAssetAtPath<DiceMetaDataDatabase>(AssetPath);
            var stillFilled = new List<string>();

            for (int i = 0; i < SummonOnlyTypes.Length; i++)
            {
                DiceMetaDataDatabase.DiceMeta meta = reloaded != null ? reloaded.Get(SummonOnlyTypes[i]) : null;
                if (meta != null && meta.recipeMaterials != null && meta.recipeMaterials.Count > 0)
                    stillFilled.Add($"{SummonOnlyTypes[i]}({meta.recipeMaterials.Count}개)");
            }

            Debug.Log($"기본 다이스 조합식 정리 완료: {string.Join(", ", cleared)}\n" +
                      $"  저장 후 재확인 — 남아 있는 조합식: " +
                      (stillFilled.Count == 0 ? "없음" : string.Join(", ", stillFilled)));

            if (stillFilled.Count > 0)
            {
                Debug.LogError("저장이 반영되지 않았다. 에셋이 다른 곳에서 열려 있는지 확인할 것: " +
                               string.Join(", ", stillFilled));
            }

            Selection.activeObject = reloaded;
        }

        private static bool IsSummonOnly(DiceType diceType)
        {
            for (int i = 0; i < SummonOnlyTypes.Length; i++)
            {
                if (SummonOnlyTypes[i] == diceType)
                    return true;
            }

            return false;
        }
    }
}
#endif
