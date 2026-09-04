#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using OJ.Equipment;

namespace OJ.EditorTools
{
    /// <summary>
    /// 보석 효과의 <c>targetDiceType</c>이 <b>Tornado(100)</b>로 잘못 박힌 것을
    /// <b>Max(205)</b>로 되돌린다. — enum 리맵 사고 복구
    ///
    /// 무슨 일이 있었나: <c>ef30864</c>("특수 다이스 추가") 이전에는 <c>DiceType.Max == 11</c>
    /// 이었고, 에셋의 <c>targetDiceType: 11</c>은 <b>"모든 다이스에 적용"</b>을 뜻했다.
    /// 그 커밋이 합성 다이스를 넣으며 <c>Max</c>를 11 → 205로 밀었는데, 에셋은
    /// 11 → <b>100</b>으로 리맵됐다. 100은 <c>Max</c>가 아니라 <c>Tornado</c>다.
    ///
    /// 왜 조용했나: <see cref="EquipmentManager"/>의 <c>IsTargetMatched</c>는
    /// <c>targetDiceType</c>을 <c>GetBaseElementType(diceType)</c>과 비교하는데,
    /// 그 함수는 합성·킹을 전부 기본 5종으로 접으므로 <b>{0,1,2,3,4}만 반환한다.</b>
    /// 100은 절대 나올 수 없어 매칭이 항상 실패한다. 컴파일도 콘솔도 조용하고,
    /// 보석을 껴도 데미지가 그대로일 뿐이다.
    ///
    /// 살아 있던 19개(<c>WellHpOnKill</c> 10 + <c>GoldOnKill</c> 9)는 그 게터들이
    /// <c>EnumerateActiveEffects(DiceType.Max)</c>로 물어서 <c>IsTargetMatched</c> 첫 줄의
    /// 조기 반환에 걸리기 때문이다 — 우연히 산 것이다.
    ///
    /// <b>이 도구는 한 번만 쓰는 것이다.</b> 고치고 나면 대상이 0건이 되고,
    /// 그때부터는 눌러도 "고칠 것이 없다"만 나온다. 지우지 않고 남기는 것은 같은 사고가
    /// 재발했을 때(에셋을 손으로 편집하다 100을 다시 넣는 등) 바로 확인할 수 있게 하려는 것이다.
    ///
    /// 자동 실행하지 않는다. 에셋 편집은 <c>AssetDatabase</c>를 거친다(절대 규칙).
    /// </summary>
    public static class GemTargetTypeRepair
    {
        private const string AssetPath = "Assets/ScriptableObject/GemDefinitionDatabase.asset";

        /// <summary>잘못 박힌 값. Tornado 는 targetDiceType 으로 의미가 없다 —
        /// 회오리 다이스만 노리는 보석은 기획에 없고, 있더라도 매칭이 불가능하다.</summary>
        private const DiceType BrokenTarget = DiceType.Tornado;

        /// <summary>원래 의도. "모든 다이스에 적용".</summary>
        private const DiceType IntendedTarget = DiceType.Max;

        [MenuItem("Tools/OJ/Equipment/Repair Gem Target Dice Type")]
        public static void Repair()
        {
            var database = AssetDatabase.LoadAssetAtPath<GemDefinitionDatabase>(AssetPath);
            if (database == null)
            {
                Debug.LogError($"{AssetPath} 를 찾지 못했다.");
                return;
            }

            var serialized = new SerializedObject(database);
            SerializedProperty definitions = serialized.FindProperty("gemDefinitions");
            if (definitions == null || !definitions.isArray)
            {
                Debug.LogError("gemDefinitions 배열을 찾지 못했다. 필드명이 바뀌었는지 확인할 것.");
                return;
            }

            var repaired = new List<string>();
            int scannedEffects = 0;

            for (int i = 0; i < definitions.arraySize; i++)
            {
                SerializedProperty definition = definitions.GetArrayElementAtIndex(i);
                SerializedProperty gemId = definition.FindPropertyRelative("gemId");
                SerializedProperty effects = definition.FindPropertyRelative("effects");
                if (effects == null || !effects.isArray)
                    continue;

                for (int e = 0; e < effects.arraySize; e++)
                {
                    SerializedProperty target = effects.GetArrayElementAtIndex(e)
                        .FindPropertyRelative("targetDiceType");
                    if (target == null)
                        continue;

                    scannedEffects++;
                    if (target.intValue != (int)BrokenTarget)
                        continue;

                    target.intValue = (int)IntendedTarget;
                    repaired.Add($"{(gemId != null ? gemId.stringValue : "?")}#{e}");
                }
            }

            if (repaired.Count == 0)
            {
                Debug.Log($"고칠 것이 없다. 효과 {scannedEffects}개를 훑었고 " +
                          $"targetDiceType == {BrokenTarget}({(int)BrokenTarget}) 인 것이 0개다.");
                return;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Verify(scannedEffects, repaired);
        }

        /// <summary>
        /// 저장 직후 다시 읽어 실제로 반영됐는지 본다. 여기서 확인하지 않으면
        /// "메뉴는 눌렀는데 저장이 안 된" 상태를 다음 F7 때까지 모른다.
        /// </summary>
        private static void Verify(int scannedEffects, List<string> repaired)
        {
            var reloaded = AssetDatabase.LoadAssetAtPath<GemDefinitionDatabase>(AssetPath);
            int stillBroken = 0;
            int nowIntended = 0;

            if (reloaded != null)
            {
                IReadOnlyList<GemDefinition> all = reloaded.GemDefinitions;
                for (int i = 0; i < all.Count; i++)
                {
                    List<GemEffect> effects = all[i] != null ? all[i].effects : null;
                    if (effects == null)
                        continue;

                    for (int e = 0; e < effects.Count; e++)
                    {
                        if (effects[e] == null)
                            continue;

                        if (effects[e].targetDiceType == BrokenTarget)
                            stillBroken++;
                        else if (effects[e].targetDiceType == IntendedTarget)
                            nowIntended++;
                    }
                }
            }

            Debug.Log($"보석 targetDiceType 복구: {repaired.Count}건\n" +
                      $"  훑은 효과: {scannedEffects}개\n" +
                      $"  저장 후 재확인 — {BrokenTarget} 남은 것: {stillBroken}개 / " +
                      $"{IntendedTarget} 인 것: {nowIntended}개\n" +
                      $"  고친 목록: {string.Join(", ", repaired)}");

            if (stillBroken > 0)
            {
                Debug.LogError($"저장이 반영되지 않았다. {BrokenTarget} 이 {stillBroken}개 남아 있다.");
            }

            Selection.activeObject = reloaded;
        }
    }
}
#endif
