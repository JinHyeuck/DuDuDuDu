using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OJ.EditorTools
{
    /// <summary>
    /// 인게임 글자를 PSD(<c>Art/0PSD/인게임관련.psd</c>)의 글자 레이어에 맞춘다 — 크기·색·외곽선.
    ///
    /// <b>폰트는 BM HANNA 로 고정한다.</b> PSD 글자 레이어는 NotoSansKR-Black 이지만 이 게임의
    /// 폰트는 BM HANNA 다(사용자 결정, 2026-10-01). 한 번 Noto 로 바꿨다가 되돌린 이력이 있어서
    /// 표의 폰트 칸에는 BM HANNA 하나만 있다.
    ///
    /// <b>왜 표로 들고 있나.</b> 대상이 씬(HUD)과 손으로 만든 프리팹이라 코드로 통째로 굽지 못한다.
    /// 인스펙터에서 한 칸씩 고치면 무엇을 PSD 에 맞췄는지 남지 않고, 다음 PSD 갱신 때 다시
    /// 처음부터 대조해야 한다. 여기 표가 곧 "PSD 레이어 ↔ 오브젝트" 대응표다.
    ///
    /// <b>두 번 돌려도 안전하다.</b> 값이 이미 같으면 건드리지 않고 "그대로" 로 찍는다.
    /// 오브젝트를 못 찾으면 멈추지 않고 "없음" 으로 찍는다 — 한 칸 이름이 바뀌었다고
    /// 나머지를 못 고치면 안 된다. 끝에 요약을 내 일부만 성공한 것을 말한다.
    ///
    /// 표에 없는 글자(다이스·피해 숫자·유물 등)는 PSD 에 대응 레이어가 없어 건드리지 않는다.
    /// 기록: <c>DuDuDuDU_Project/Docs/IngamePsdText.md</c>
    /// </summary>
    public static class IngamePsdTextApplier
    {
        private const string HannaPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF.asset";
        private const string HannaPlainPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF Material_NoneOutLine.mat";

        private const string BattleScene = "Assets/Scenes/BattleScene.unity";
        private const string ResultDialog = "Assets/Prefab/Refactory/BattleScene/UIStageResultDialog.prefab";
        private const string RewardElement = "Assets/Prefab/Hunting/UIRewardElement.prefab";
        private const string CombatPower = "Assets/Prefab/Lobby/UICombatPowerDisplay.prefab";

        private enum Face { Hanna }

        /// <summary>한 글자의 목표. size·color 가 null 이면 그 값은 그대로 둔다.</summary>
        private sealed class Spec
        {
            public string Asset;
            public string Path;
            public Face Face;
            public float? Size;
            public string Color;
            public bool Outline;
            public string PsdLayer;

            public Spec(string asset, string path, Face face, float? size, string color, bool outline, string psdLayer)
            {
                Asset = asset; Path = path; Face = face; Size = size; Color = color; Outline = outline; PsdLayer = psdLayer;
            }
        }

        // PSD 레이어 값(크기는 레이어 변형까지 곱한 실제 pt). 외곽선은 레이어 효과가 켜진 것만 true.
        private static readonly Spec[] Specs =
        {
            // ── 전투 HUD (그룹 ingame) ─────────────────────────────────
            new Spec(BattleScene, "UI/Canvas/WaveCount", Face.Hanna, 40f, "ffffff", true, "WAVE 1"),
            new Spec(BattleScene, "UI/Canvas/MonsterKill/MonsterRemainCount", Face.Hanna, 30f, "ffffff", true, "50/60"),
            new Spec(BattleScene, "UI/Canvas/Speed/Text (TMP)", Face.Hanna, 40f, "ffffff", true, "x2"),
            new Spec(BattleScene, "UI/Canvas/PlayBtn/Text (TMP)", Face.Hanna, 60f, "ffd200", true, "시작웨이브/Play Wave"),
            new Spec(BattleScene, "UI/Canvas/Start/SummoText", Face.Hanna, 35f, "ffffff", true, "시작"),
            new Spec(BattleScene, "UI/Canvas/SummonBtn/Gold", Face.Hanna, 35f, "ffffff", true, "10 / 1,000"),
            new Spec(BattleScene, "UI/Canvas/SummonBtn/SummoText", Face.Hanna, 35f, "ffffff", true, "뽑기"),
            new Spec(BattleScene, "UI/Canvas/Upgrade/SummoText", Face.Hanna, 35f, "ffffff", true, "강화 복사"),
            // PSD 에 레이어가 없는 HUD 숫자 둘. 폰트만 BM HANNA 로 못박는다.
            new Spec(BattleScene, "UI/Canvas/Well/Hp/Text (TMP)", Face.Hanna, null, null, true, "(없음 — 폰트만)"),
            new Spec(BattleScene, "UI/Canvas/Upgrade/Text (TMP)", Face.Hanna, null, null, true, "(없음 — 폰트만)"),

            // ── 결과 창 (그룹 승리·패배) ───────────────────────────────
            new Spec(ResultDialog, "View/WinIcon/Text (TMP)", Face.Hanna, 100f, "fffc00", true, "승리/승리"),
            new Spec(ResultDialog, "View/WinIcon/StageLabel", Face.Hanna, 50f, "df4444", false, "승리/Stage 1"),
            new Spec(ResultDialog, "View/LoseIcon/Text (TMP)", Face.Hanna, 100f, "b8a3d7", true, "패배/패배"),
            new Spec(ResultDialog, "View/LoseIcon/StageLabel", Face.Hanna, 48f, "b8a3d7", false, "패배/Stage 1"),
            new Spec(ResultDialog, "View/MainValueText", Face.Hanna, 130f, "ffffff", true, "승리/13 (주석 130pt)"),
            new Spec(ResultDialog, "View/BestStageLabel", Face.Hanna, 48f, "ffc600", true, "Best Stage"),
            new Spec(ResultDialog, "View/BestStageValue", Face.Hanna, 48f, "ffc600", true, "15"),
            new Spec(ResultDialog, "View/BackButton/Text (TMP)", Face.Hanna, 40f, "ffffff", true, "(주석 40pt / 버튼)"),

            // ── 보상 칸 (그룹 보상) ────────────────────────────────────
            new Spec(RewardElement, "AmountText", Face.Hanna, 35f, "ffffff", true, "보상/3,000"),

            // ── 전투력 알림 (그룹 강해졌을경우) — BM HANNA, 외곽선 없음 ──
            new Spec(CombatPower, "ToastRoot/PowerText", Face.Hanna, 60f, "ffd200", false, "130.00k"),
            new Spec(CombatPower, "ToastRoot/Delta/DeltaText", Face.Hanna, 60f, "1bff00", false, "50"),
        };

        [MenuItem("OJ/개발/인게임 UI/PSD 글자 맞추기")]
        private static void Apply()
        {
            if (LoadFonts() == null)
                return;

            var log = new StringBuilder("[PSD 글자] 적용").AppendLine();
            int changed = 0, same = 0, missing = 0;

            var byAsset = new Dictionary<string, List<Spec>>();
            foreach (Spec spec in Specs)
            {
                if (!byAsset.TryGetValue(spec.Asset, out List<Spec> list))
                    byAsset[spec.Asset] = list = new List<Spec>();
                list.Add(spec);
            }

            foreach (var pair in byAsset)
            {
                bool isScene = pair.Key.EndsWith(".unity");
                log.AppendLine("  " + pair.Key);

                if (isScene)
                {
                    Scene scene = EditorSceneManager.OpenScene(pair.Key, OpenSceneMode.Single);

                    // 씬을 열면 쓰지 않는 에셋이 내려가 앞서 읽어 둔 폰트가 파괴된 참조가 된다.
                    // 그래서 연 <b>뒤에</b> 읽는다.
                    var fonts = LoadFonts();
                    bool dirty = false;
                    foreach (Spec spec in pair.Value)
                        dirty |= ApplyOne(FindInScene(scene, spec.Path), spec, fonts, log, ref changed, ref same, ref missing);

                    if (dirty)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
                else
                {
                    GameObject root = PrefabUtility.LoadPrefabContents(pair.Key);
                    try
                    {
                        var fonts = LoadFonts();
                        bool dirty = false;
                        foreach (Spec spec in pair.Value)
                            dirty |= ApplyOne(root.transform.Find(spec.Path), spec, fonts, log, ref changed, ref same, ref missing);

                        if (dirty)
                            PrefabUtility.SaveAsPrefabAsset(root, pair.Key);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            log.AppendLine("  바꿈 " + changed + " / 그대로 " + same + " / 없음 " + missing);
            if (missing > 0)
                Debug.LogWarning(log.ToString());
            else
                Debug.Log(log.ToString());
        }

        private static Dictionary<Face, (TMP_FontAsset font, Material plain)> LoadFonts()
        {
            var fonts = new Dictionary<Face, (TMP_FontAsset font, Material plain)>
            {
                [Face.Hanna] = (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HannaPath), AssetDatabase.LoadAssetAtPath<Material>(HannaPlainPath)),
            };

            foreach (var pair in fonts)
            {
                if (pair.Value.font == null || pair.Value.plain == null)
                {
                    Debug.LogError("[PSD 글자] 폰트나 외곽선 없는 머티리얼이 없다: " + pair.Key + " — 아무것도 바꾸지 않는다.");
                    return null;
                }
            }

            return fonts;
        }

        private static Transform FindInScene(Scene scene, string path)
        {
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != rootName)
                    continue;
                return slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
            }
            return null;
        }

        private static bool ApplyOne(
            Transform target, Spec spec, Dictionary<Face, (TMP_FontAsset font, Material plain)> fonts,
            StringBuilder log, ref int changed, ref int same, ref int missing)
        {
            TMP_Text text = target != null ? target.GetComponent<TMP_Text>() : null;
            if (text == null)
            {
                missing++;
                log.AppendLine("    ! 없음: " + spec.Path);
                return false;
            }

            (TMP_FontAsset font, Material plain) face = fonts[spec.Face];
            Material material = spec.Outline ? face.font.material : face.plain;

            var diffs = new List<string>();
            Undo.RecordObject(text, "PSD 글자 맞추기");

            if (text.font != face.font)
            {
                diffs.Add("폰트 " + (text.font != null ? text.font.name : "null") + "→" + face.font.name);
                text.font = face.font;
            }

            if (text.fontSharedMaterial != material)
            {
                diffs.Add("머티리얼 → " + material.name);
                text.fontSharedMaterial = material;
            }

            if (spec.Size.HasValue && !Mathf.Approximately(text.fontSize, spec.Size.Value))
            {
                diffs.Add("크기 " + text.fontSize + "→" + spec.Size.Value);
                text.fontSize = spec.Size.Value;

                // 자동 크기가 켜져 있으면 fontSize 가 무시된다. 상한을 목표값으로 맞춰
                // 칸이 넉넉하면 PSD 크기, 좁으면 줄어들게 한다.
                if (text.enableAutoSizing)
                    text.fontSizeMax = spec.Size.Value;
            }

            if (spec.Color != null && ColorUtility.TryParseHtmlString("#" + spec.Color, out Color color))
            {
                color.a = text.color.a;
                if (text.color != color)
                {
                    diffs.Add("색 " + ColorUtility.ToHtmlStringRGB(text.color) + "→" + spec.Color.ToUpperInvariant());
                    text.color = color;
                }
            }

            if (diffs.Count == 0)
            {
                same++;
                log.AppendLine("    = 그대로: " + spec.Path);
                return false;
            }

            EditorUtility.SetDirty(text);
            changed++;
            log.AppendLine("    * " + spec.Path + " (" + spec.PsdLayer + "): " + string.Join(", ", diffs));
            return true;
        }
    }
}
