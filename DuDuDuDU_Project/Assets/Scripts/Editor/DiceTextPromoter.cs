#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using OJ.Dice;

namespace OJ.EditorTools
{
    /// <summary>
    /// 코드 기본값의 <b>표시 문구</b>(description / milestones)를 에셋으로 옮긴다. (4.1)
    ///
    /// 왜 필요한가: 4.3 에서 MergeMeta 를 걷어냈다. 그동안 그 필드들은 에셋에 값이 있어도
    /// 코드 쪽이 이기고 있었고, 덮어쓰기를 없애자 에셋의 <b>낡은 판</b>이 드러났다.
    /// 문구가 후퇴하는 항목이 8줄 있었다 — "전이"가 "공격 대상"으로 바뀌어 연쇄 번개라는
    /// 메커니즘 설명이 사라지고, King 계열의 "소환 중인 동안"이라는 조건이 빠지고,
    /// KingNormal 은 3연타 설명이 통째로 없어진다.
    ///
    /// 그래서 코드 문구를 에셋에 <b>한 번</b> 써넣어 에셋을 진짜 정본으로 만든다.
    /// 이것이 베이스라인 4.1 이 말한 "코드 fallback 값을 asset 에 1회 덤프"인데,
    /// <b>문구에만</b> 적용한다. 수치는 옮기면 안 된다 — 코드의 킹 다이스 강화 비용이 0 이라
    /// 그대로 덤프하면 강화가 공짜가 된다.
    ///
    /// 옮기고 나면 골든 기준선과 값이 같아지므로 재덤프해도 diff 가 0 이다.
    ///
    /// 자동 실행하지 않는다. 에셋 편집은 AssetDatabase 를 거친다(절대 규칙).
    /// </summary>
    public static class DiceTextPromoter
    {
        private const string AssetPath = "Assets/ScriptableObject/DiceMetaDataDatabase.asset";

        [MenuItem("Tools/OJ/Dice/Promote Code Text Into Asset")]
        public static void Promote()
        {
            var database = AssetDatabase.LoadAssetAtPath<DiceMetaDataDatabase>(AssetPath);
            if (database == null)
            {
                Debug.LogError($"{AssetPath} 를 찾지 못했다.");
                return;
            }

            IReadOnlyDictionary<DiceType, DiceMetaDataDatabase.DiceMeta> codeDefaults =
                DiceMetaDataProvider.EditorOnlyCodeDefaults;

            var serialized = new SerializedObject(database);
            SerializedProperty metas = serialized.FindProperty("metas");
            if (metas == null || !metas.isArray)
            {
                Debug.LogError("metas 배열을 찾지 못했다. 필드명이 바뀌었는지 확인할 것.");
                return;
            }

            var changed = new List<string>();
            var untouched = new List<string>();
            var missingInCode = new List<string>();

            for (int i = 0; i < metas.arraySize; i++)
            {
                SerializedProperty entry = metas.GetArrayElementAtIndex(i);
                SerializedProperty diceTypeProperty = entry.FindPropertyRelative("diceType");
                if (diceTypeProperty == null)
                    continue;

                var diceType = (DiceType)diceTypeProperty.intValue;

                DiceMetaDataDatabase.DiceMeta source;
                if (!codeDefaults.TryGetValue(diceType, out source) || source == null)
                {
                    missingInCode.Add(diceType.ToString());
                    continue;
                }

                bool entryChanged = false;
                entryChanged |= PromoteDescription(entry, source, diceType, changed);
                entryChanged |= PromoteMilestones(entry, source, diceType, changed);

                if (!entryChanged)
                    untouched.Add(diceType.ToString());
            }

            if (changed.Count == 0)
            {
                Debug.Log("옮길 문구가 없다. 에셋이 이미 코드와 같다.");
                return;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Verify(codeDefaults, changed, untouched, missingInCode);
        }

        private static bool PromoteDescription(
            SerializedProperty entry,
            DiceMetaDataDatabase.DiceMeta source,
            DiceType diceType,
            List<string> changed)
        {
            SerializedProperty description = entry.FindPropertyRelative("description");
            if (description == null || description.stringValue == source.description)
                return false;

            changed.Add($"{diceType}.description");
            description.stringValue = source.description;
            return true;
        }

        private static bool PromoteMilestones(
            SerializedProperty entry,
            DiceMetaDataDatabase.DiceMeta source,
            DiceType diceType,
            List<string> changed)
        {
            SerializedProperty milestones = entry.FindPropertyRelative("milestones");
            if (milestones == null || !milestones.isArray || source.milestones == null)
                return false;

            if (!MilestonesDiffer(milestones, source.milestones))
                return false;

            changed.Add($"{diceType}.milestones");

            milestones.ClearArray();
            for (int i = 0; i < source.milestones.Count; i++)
            {
                milestones.InsertArrayElementAtIndex(i);
                SerializedProperty item = milestones.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("level").intValue = source.milestones[i].level;
                item.FindPropertyRelative("description").stringValue = source.milestones[i].description;
            }

            return true;
        }

        private static bool MilestonesDiffer(
            SerializedProperty milestones,
            List<DiceMetaDataDatabase.DiceLevelMilestone> source)
        {
            if (milestones.arraySize != source.Count)
                return true;

            for (int i = 0; i < source.Count; i++)
            {
                SerializedProperty item = milestones.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative("level").intValue != source[i].level)
                    return true;
                if (item.FindPropertyRelative("description").stringValue != source[i].description)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 저장 직후 에셋을 다시 읽어 코드 문구와 실제로 같아졌는지 본다.
        /// 여기서 확인하지 않으면 "메뉴는 눌렀는데 저장이 안 된" 상태를 다음 F7 때까지 모른다.
        /// </summary>
        private static void Verify(
            IReadOnlyDictionary<DiceType, DiceMetaDataDatabase.DiceMeta> codeDefaults,
            List<string> changed,
            List<string> untouched,
            List<string> missingInCode)
        {
            var reloaded = AssetDatabase.LoadAssetAtPath<DiceMetaDataDatabase>(AssetPath);
            var stillDifferent = new List<string>();

            foreach (var pair in codeDefaults)
            {
                DiceMetaDataDatabase.DiceMeta assetMeta = reloaded != null ? reloaded.Get(pair.Key) : null;
                if (assetMeta == null || pair.Value == null)
                    continue;

                if (assetMeta.description != pair.Value.description)
                    stillDifferent.Add($"{pair.Key}.description");

                if (!SameMilestones(assetMeta.milestones, pair.Value.milestones))
                    stillDifferent.Add($"{pair.Key}.milestones");
            }

            Debug.Log($"코드 문구를 에셋으로 옮겼다 — {changed.Count}건\n" +
                      $"  변경: {string.Join(", ", changed)}\n" +
                      $"  이미 동일: {untouched.Count}종\n" +
                      $"  코드에 없는 항목(건드리지 않음): " +
                      (missingInCode.Count == 0 ? "없음" : string.Join(", ", missingInCode)) + "\n" +
                      $"  저장 후 재확인 — 아직 다른 항목: " +
                      (stillDifferent.Count == 0 ? "없음" : string.Join(", ", stillDifferent)));

            if (stillDifferent.Count > 0)
            {
                Debug.LogError("저장이 반영되지 않았다: " + string.Join(", ", stillDifferent));
            }

            Selection.activeObject = reloaded;
        }

        private static bool SameMilestones(
            List<DiceMetaDataDatabase.DiceLevelMilestone> a,
            List<DiceMetaDataDatabase.DiceLevelMilestone> b)
        {
            int countA = a != null ? a.Count : 0;
            int countB = b != null ? b.Count : 0;
            if (countA != countB)
                return false;

            for (int i = 0; i < countA; i++)
            {
                if (a[i].level != b[i].level || a[i].description != b[i].description)
                    return false;
            }

            return true;
        }
    }
}
#endif
