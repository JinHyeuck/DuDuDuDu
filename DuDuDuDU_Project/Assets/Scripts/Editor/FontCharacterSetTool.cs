#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace OJ.EditorTools
{
    // BM HANNA Pro OTF 를 Static 아틀라스로 굽기 위한 문자 세트 도구.
    //
    // Static 폰트는 아틀라스에 없는 글자를 런타임에 만들지 못한다 — 그냥 두부(네모)가 된다.
    // 그래서 "프로젝트가 실제로 쓰는 글자"를 파일로 뽑아 Font Asset Creator 에 먹이고,
    // 구운 뒤에 다시 검사해서 빠진 글자가 없는지 확인하는 두 개의 메뉴로 나눴다.
    //
    // 두 메뉴 다 여러 번 눌러도 안전하다. 문자 세트는 줄어들지 않는다(기존 세트와 합집합).
    //
    // Font Asset Creator 설정 (Window > TextMeshPro > Font Asset Creator):
    //   Source Font File   BMHANNAProOTF
    //   Sampling Point Size  Custom Size 49   ← Noto 와 같은 값. 이게 외곽선 두께를 좌우한다.
    //   Padding            5                 ← _GradientScale 6 유지. 바꾸면 머티리얼도 다시 맞춰야 한다.
    //   Packing Method     Optimum
    //   Atlas Resolution   2048 x 1024       ← 1109자가 여유 있게 들어간다. 모자라면 2048 x 2048
    //   Character Set      Characters from File → BMHANNAProOTF_CharacterSet
    //   Render Mode        SDFAA
    //   Get Kerning Pairs  끔
    // 구운 뒤 Save (Save as 아님) 로 기존 에셋에 덮어쓴다. 그래야 머티리얼·아틀라스 서브에셋이
    // 유지되고 프리팹/씬의 참조 988개가 안 끊긴다. 그다음 인스펙터에서
    // Atlas Population Mode 를 Static 으로 바꾸고, 마지막에 커버리지 검사를 돌린다.
    public static class FontCharacterSetTool
    {
        private const string FontAssetPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF.asset";
        private const string CharacterSetPath = "Assets/BMHANNAProOTF/BMHANNAProOTF_CharacterSet.txt";

        // 런타임에 화면으로 나가지 않는 문자열은 아틀라스에 넣을 이유가 없다.
        private static readonly string[] SkipFolders = { "Plugins", "AI Toolkit", "Tests", "TextMesh Pro", "Editor" };

        [MenuItem("OJ/개발/폰트/문자 세트 다시 뽑기")]
        public static void RegenerateCharacterSet()
        {
            HashSet<char> used = ScanProject(out _);

            var final = new HashSet<char>(used);
            for (char c = ' '; c <= '~'; c++) final.Add(c);           // ASCII 는 항상
            final.UnionWith(ReadCharacterSetFile());                  // 기존 세트 유지 (줄어들지 않게)
            final.UnionWith(ReadFontCharacters(LoadFontAsset(false))); // 폰트가 이미 가진 기호도 유지

            List<char> sorted = final.OrderBy(c => (int)c).ToList();
            var sb = new StringBuilder();
            for (int i = 0; i < sorted.Count; i++)
            {
                sb.Append(sorted[i]);
                if ((i + 1) % 64 == 0) sb.Append('\n');
            }
            if (sb.Length == 0 || sb[sb.Length - 1] != '\n') sb.Append('\n');

            File.WriteAllText(CharacterSetPath, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(CharacterSetPath);

            Debug.Log($"[폰트] 문자 세트 갱신: {CharacterSetPath}\n" +
                      $"  총 {sorted.Count}자 (프로젝트 사용 {used.Count}자)\n" +
                      $"  {Summarize(sorted)}\n" +
                      $"  Font Asset Creator → Character Set: Characters from File → 이 파일 지정");
        }

        [MenuItem("OJ/개발/폰트/커버리지 검사")]
        public static void VerifyCoverage()
        {
            TMP_FontAsset font = LoadFontAsset(true);
            if (font == null) return;

            HashSet<char> inFont = ReadFontCharacters(font);
            HashSet<char> used = ScanProject(out Dictionary<char, string> firstSource);
            for (char c = ' '; c <= '~'; c++) used.Add(c);

            List<char> missing = used.Where(c => !inFont.Contains(c)).OrderBy(c => (int)c).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"[폰트] 커버리지 검사 — {Path.GetFileName(FontAssetPath)}");
            sb.AppendLine($"  글자 수 {inFont.Count} / 아틀라스 {font.atlasWidth}x{font.atlasHeight} " +
                          $"{font.atlasTextures?.Length ?? 0}장 / padding {font.atlasPadding} / 모드 {font.atlasPopulationMode}");
            sb.AppendLine($"  프로젝트 사용 {used.Count}자 → 빠진 글자 {missing.Count}자");

            // 사후 검사: 머티리얼 값이 아틀라스와 어긋나면 외곽선 두께가 조용히 달라진다.
            // 프로퍼티 ID 대신 이름으로 읽는다 — ShaderUtilities 의 static ID 는 TMP 가
            // 한 번이라도 텍스트를 그리기 전에는 0 이라 엉뚱한 값이 나온다.
            foreach (Material mat in CollectFontMaterials(font))
            {
                if (!mat.HasProperty("_GradientScale")) continue;

                float gradient = mat.GetFloat("_GradientScale");
                float texW = mat.GetFloat("_TextureWidth");
                float texH = mat.GetFloat("_TextureHeight");
                bool ok = Mathf.Approximately(gradient, font.atlasPadding + 1)
                          && Mathf.Approximately(texW, font.atlasWidth)
                          && Mathf.Approximately(texH, font.atlasHeight);
                sb.AppendLine($"  {(ok ? "OK" : "불일치")} {mat.name}: GradientScale {gradient} (기대 {font.atlasPadding + 1}), " +
                              $"Texture {texW}x{texH} (기대 {font.atlasWidth}x{font.atlasHeight}), " +
                              $"OutlineWidth {mat.GetFloat("_OutlineWidth")}, " +
                              $"외곽선 {(mat.IsKeywordEnabled("OUTLINE_ON") ? "ON" : "OFF")}");
            }

            if (missing.Count > 0)
            {
                sb.AppendLine("  빠진 글자:");
                foreach (char c in missing.Take(120))
                {
                    firstSource.TryGetValue(c, out string src);
                    sb.AppendLine($"    '{c}' U+{((int)c):X4}  {src}");
                }
                if (missing.Count > 120) sb.AppendLine($"    ... 외 {missing.Count - 120}자");
                Debug.LogWarning(sb.ToString());
                return;
            }

            Debug.Log(sb.ToString());
        }

        private static TMP_FontAsset LoadFontAsset(bool logIfMissing)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null && logIfMissing) Debug.LogError($"[폰트] 폰트 에셋을 못 찾았다: {FontAssetPath}");
            return font;
        }

        private static HashSet<char> ReadFontCharacters(TMP_FontAsset font)
        {
            var set = new HashSet<char>();
            if (font == null || font.characterTable == null) return set;
            foreach (TMP_Character ch in font.characterTable)
                if (ch != null && ch.unicode <= char.MaxValue) set.Add((char)ch.unicode);
            return set;
        }

        private static HashSet<char> ReadCharacterSetFile()
        {
            var set = new HashSet<char>();
            if (!File.Exists(CharacterSetPath)) return set;
            foreach (char c in File.ReadAllText(CharacterSetPath))
                if (c >= ' ') set.Add(c);
            return set;
        }

        private static IEnumerable<Material> CollectFontMaterials(TMP_FontAsset font)
        {
            if (font.material != null) yield return font.material;

            string folder = Path.GetDirectoryName(FontAssetPath);
            if (string.IsNullOrEmpty(folder)) yield break;
            folder = folder.Replace('\\', '/');

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat != null && mat != font.material) yield return mat;
            }
        }

        // 화면에 나갈 수 있는 문자열만 긁는다: 프리팹/씬의 m_text, SO 에셋, 런타임 .cs 의 문자열 리터럴.
        private static HashSet<char> ScanProject(out Dictionary<char, string> firstSource)
        {
            var used = new HashSet<char>();
            var sources = new Dictionary<char, string>();

            void Add(string text, string path)
            {
                if (string.IsNullOrEmpty(text)) return;
                foreach (char c in text)
                {
                    if (c < ' ') continue;
                    used.Add(c);
                    if (!sources.ContainsKey(c)) sources[c] = path;
                }
            }

            foreach (string path in Directory.EnumerateFiles("Assets", "*.*", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                if (SkipFolders.Any(f => normalized.Contains("/" + f + "/"))) continue;

                string ext = Path.GetExtension(normalized);
                if (ext == ".prefab" || ext == ".unity")
                {
                    foreach (Match m in Regex.Matches(File.ReadAllText(normalized), @"^\s*m_text:(.*)$", RegexOptions.Multiline))
                        Add(m.Groups[1].Value, normalized);
                }
                else if (ext == ".asset")
                {
                    Add(File.ReadAllText(normalized), normalized);
                }
                else if (ext == ".cs")
                {
                    string src = File.ReadAllText(normalized);
                    src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
                    src = Regex.Replace(src, @"//.*$", string.Empty, RegexOptions.Multiline);
                    foreach (Match m in Regex.Matches(src, @"@""((?:[^""]|"""")*)""|""((?:[^""\\\n]|\\.)*)"""))
                        Add(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value, normalized);
                }
            }

            firstSource = sources;
            return used;
        }

        private static string Summarize(IEnumerable<char> chars)
        {
            int ascii = 0, hangul = 0, hanja = 0, etc = 0;
            foreach (char c in chars)
            {
                if (c < 128) ascii++;
                else if (c >= 0xAC00 && c <= 0xD7A3) hangul++;
                else if (c >= 0x4E00 && c <= 0x9FFF) hanja++;
                else etc++;
            }
            return $"ASCII {ascii} / 한글 {hangul} / 한자 {hanja} / 기호 {etc}";
        }
    }
}
#endif
