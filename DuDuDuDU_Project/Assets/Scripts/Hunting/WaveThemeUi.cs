using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Battle;
using OJ.Core;
using OJ.UI;

namespace OJ.Hunting
{
    /// <summary>
    /// 웨이브 테마 화면(상단 웨이브 현황 · 시작 문구 · 보스/현상금 체력바 · 선택 창)을 <b>코드로 만든다</b>.
    /// 씬·프리팹 YAML 을 손대지 않는 규칙(AGENTS 절대 규칙 3)이라 런타임에 캔버스 아래 붙인다(<c>LobbyUpgradeBadges</c> 와 같은 방식).
    /// 스프라이트 에셋을 새로 들이지 않고 원·둥근 사각형을 한 번 그려 쓴다.
    /// </summary>
    public static class WaveThemeUi
    {
        private static Sprite circle;
        private static Sprite rounded;

        public static readonly Color Navy = new Color(0.10f, 0.13f, 0.24f, 1f);
        public static readonly Color Red = new Color(0.86f, 0.18f, 0.18f, 1f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.18f, 1f);
        public static readonly Color Panel = new Color(0.11f, 0.13f, 0.22f, 0.98f);

        public static Sprite Circle()
        {
            if (circle != null)
                return circle;

            const int n = 96;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = n * 0.5f - 1f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x - n * 0.5f + 0.5f;
                    float dy = y - n * 0.5f + 0.5f;
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return circle;
        }

        /// <summary>모서리가 둥근 사각형(9분할). Image 를 Sliced 로 두고 쓴다.</summary>
        public static Sprite Rounded()
        {
            if (rounded != null)
                return rounded;

            const int n = 64;
            const float radius = 18f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, n - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, n - radius);
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            rounded = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return rounded;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            // 부모와 같은 레이어. 중첩 캔버스는 자기 오브젝트의 레이어로 카메라에 걸러져서, 기본 레이어면 UI 카메라에 안 보인다.
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static Image Img(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
        {
            RectTransform rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            if (sprite == Rounded())
                image.type = Image.Type.Sliced;
            return image;
        }

        public static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool outline = true)
        {
            RectTransform rect = Rect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            // 폰트의 기본 머티리얼은 글자를 두껍게(FaceDilate 1) 부풀리고 두꺼운 외곽선을 품고 있어 작은 글자가 번진다.
            // 보통 굵기 머티리얼에 얇은 외곽선만 얹는다.
            if (font != null)
                tmp.fontSharedMaterial = PlainMaterial(font);

            if (outline)
            {
                tmp.outlineColor = new Color32(10, 12, 28, 255);
                tmp.outlineWidth = 0.12f;
                tmp.fontMaterial.EnableKeyword("OUTLINE_ON");
            }

            return tmp;
        }

        private static readonly Dictionary<TMP_FontAsset, Material> plainMaterials = new Dictionary<TMP_FontAsset, Material>();

        /// <summary>외곽선·부풀림·그림자가 없는 보통 굵기 머티리얼(프로젝트의 "NoneOutLine" 머티리얼과 같은 값).</summary>
        private static Material PlainMaterial(TMP_FontAsset font)
        {
            if (plainMaterials.TryGetValue(font, out Material plain) && plain != null)
                return plain;

            plain = new Material(font.material) { name = font.name + " (plain)" };
            plain.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
            plain.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            plain.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
            plain.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
            plain.SetFloat(ShaderUtilities.ID_ScaleRatio_A, 0.8333333f);
            plain.SetFloat(ShaderUtilities.ID_ScaleRatio_C, 0.6770833f);
            plain.DisableKeyword("OUTLINE_ON");
            plain.DisableKeyword("UNDERLAY_ON");
            plainMaterials[font] = plain;
            return plain;
        }

        private static void BuildTextBadge(Transform parent, TMP_FontAsset font, string name, string label, Color color, float size)
        {
            Image back = Img(name, parent, Circle(), color);
            Anchor(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            Text("Label", back.transform, font, label, size * (label.Length > 2 ? 0.28f : 0.34f), Color.white);
        }

        /// <summary>웨이브 보상 아이콘. 칸 하나(약 48px)에 들어간다. 아이콘 에셋 대신 색과 글자로 그린다 — 한눈에 구분되는 것이 목적이다.</summary>
        public static void BuildRewardBadge(Transform parent, TMP_FontAsset font, WaveInterlude kind, float size)
        {
            switch (kind)
            {
                case WaveInterlude.SoulBonus:
                {
                    Sprite stone = BattlePointUtility.GetIcon(BattlePointType.EnhanceStone);
                    Image back = Img("Soul", parent, Circle(), new Color(0.35f, 0.25f, 0.62f, 1f));
                    Anchor(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
                    if (stone != null)
                    {
                        Image icon = Img("Icon", back.transform, stone, Color.white);
                        Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.8f, size * 0.8f));
                    }
                    else
                    {
                        Text("Label", back.transform, font, "영혼", size * 0.34f, Color.white);
                    }

                    break;
                }
                case WaveInterlude.BountyChallenge:
                {
                    Image back = Img("Bounty", parent, Circle(), new Color(0.62f, 0.2f, 0.62f, 1f));
                    Anchor(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
                    Text("Label", back.transform, font, "현상", size * 0.34f, Color.white);
                    break;
                }
                case WaveInterlude.SlotsOrSoul:
                {
                    Image back = Img("Slots", parent, Rounded(), new Color(0.9f, 0.66f, 0.14f, 1f));
                    Anchor(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
                    Text("Label", back.transform, font, "+4", size * 0.5f, new Color(0.25f, 0.14f, 0f, 1f), outline: false);
                    break;
                }
                case WaveInterlude.BossReward:
                {
                    Image back = Img("Boss", parent, Rounded(), new Color(0.86f, 0.22f, 0.22f, 1f));
                    Anchor(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.3f, size));
                    Text("Label", back.transform, font, "+4·영혼", size * 0.34f, Color.white);
                    break;
                }
                case WaveInterlude.LuckyDice:
                    BuildTextBadge(parent, font, "LuckyDice", "행운", new Color(0.85f, 0.55f, 0.1f, 1f), size);
                    break;
                case WaveInterlude.BossChallenge:
                    BuildTextBadge(parent, font, "BossChallenge", "보스전", new Color(0.55f, 0.1f, 0.14f, 1f), size);
                    break;
                case WaveInterlude.WallHeal:
                    BuildTextBadge(parent, font, "WallHeal", "회복", new Color(0.18f, 0.55f, 0.32f, 1f), size);
                    break;
                case WaveInterlude.DiceGiftBasic:
                    BuildTextBadge(parent, font, "DiceGiftBasic", "다이스", new Color(0.2f, 0.4f, 0.8f, 1f), size);
                    break;
                case WaveInterlude.DiceGiftRare:
                    BuildTextBadge(parent, font, "DiceGiftRare", "특수", new Color(0.4f, 0.25f, 0.8f, 1f), size);
                    break;
                case WaveInterlude.SpGamble:
                case WaveInterlude.StoneGamble:
                    BuildTextBadge(parent, font, kind.ToString(), "도박", new Color(0.7f, 0.35f, 0.1f, 1f), size);
                    break;
                case WaveInterlude.FreeSummons:
                    BuildTextBadge(parent, font, "FreeSummons", "소환", new Color(0.2f, 0.55f, 0.7f, 1f), size);
                    break;
                case WaveInterlude.ProbUp:
                    BuildTextBadge(parent, font, "ProbUp", "확률", new Color(0.25f, 0.5f, 0.35f, 1f), size);
                    break;
                case WaveInterlude.WallUpgrade:
                    BuildTextBadge(parent, font, "WallUpgrade", "벽업", new Color(0.45f, 0.45f, 0.5f, 1f), size);
                    break;
                case WaveInterlude.Exchange:
                    BuildTextBadge(parent, font, "Exchange", "환전", new Color(0.55f, 0.5f, 0.15f, 1f), size);
                    break;
                case WaveInterlude.AttackSpeedBuff:
                    BuildTextBadge(parent, font, "AttackSpeedBuff", "공속", new Color(0.85f, 0.4f, 0.15f, 1f), size);
                    break;
                case WaveInterlude.SlowBuff:
                    BuildTextBadge(parent, font, "SlowBuff", "슬로", new Color(0.3f, 0.55f, 0.8f, 1f), size);
                    break;
                case WaveInterlude.WeakenBuff:
                    BuildTextBadge(parent, font, "WeakenBuff", "약화", new Color(0.5f, 0.3f, 0.6f, 1f), size);
                    break;
                case WaveInterlude.RiskyDeal:
                    BuildTextBadge(parent, font, "RiskyDeal", "위험", new Color(0.6f, 0.15f, 0.2f, 1f), size);
                    break;
                case WaveInterlude.Merchant:
                    BuildTextBadge(parent, font, "Merchant", "상인", new Color(0.65f, 0.5f, 0.2f, 1f), size);
                    break;
            }
        }
    }

    /// <summary>
    /// 상단 웨이브 현황. 동그라미 안에 웨이브 번호, 아래에 그 웨이브가 끝날 때 받는 보상 아이콘. 10의 배수(보스)는 빨간 동그라미,
    /// 지금 웨이브는 노란 고리로 강조한다. 5웨이브씩 한 쪽으로 보여 주고 뒤에 "…" 와 마지막 웨이브를 붙인다.
    /// </summary>
    public sealed class UIWaveStrip : MonoBehaviour
    {
        private const float NodeSize = 80f;
        private const float Spacing = 96f;
        private const int PageSize = 5;

        private TMP_FontAsset font;
        private int total;
        private int shownPage = -1;
        private int shownWave = -1;
        private RectTransform content;

        private Func<int, WaveInterlude> after = WaveTheme.After;

        /// <param name="after">웨이브마다 끝난 뒤의 보상(아래 보상 아이콘). 판마다 무작위 이벤트 계획을 넘긴다.</param>
        public static UIWaveStrip Create(Transform canvas, TMP_FontAsset font, int totalWaves, Func<int, WaveInterlude> after = null)
        {
            RectTransform root = WaveThemeUi.Rect("WaveStrip", canvas);
            WaveThemeUi.Anchor(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(760f, 190f));
            var strip = root.gameObject.AddComponent<UIWaveStrip>();
            strip.font = font;
            strip.total = Mathf.Max(1, totalWaves);
            strip.content = root;
            if (after != null)
                strip.after = after;
            return strip;
        }

        public void SetWave(int wave)
        {
            wave = Mathf.Clamp(wave, 1, total);
            int page = (wave - 1) / PageSize;
            if (page == shownPage && wave == shownWave)
                return;

            shownPage = page;
            shownWave = wave;
            Rebuild(wave, page);
        }

        private void Rebuild(int current, int page)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);

            int first = page * PageSize + 1;
            int last = Mathf.Min(first + PageSize - 1, total);
            var waves = new List<int>();
            for (int w = first; w <= last; w++)
                waves.Add(w);

            bool tail = last < total;
            int count = waves.Count + (tail ? 2 : 0);
            float width = (count - 1) * Spacing;
            float x = -width * 0.5f;
            float topY = -NodeSize * 0.5f - 6f;

            // 연결선: 지나온 구간은 노랗게.
            Image line = WaveThemeUi.Img("Line", content, WaveThemeUi.Rounded(), new Color(0.05f, 0.07f, 0.14f, 0.85f));
            WaveThemeUi.Anchor(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, topY), new Vector2(width + NodeSize, 20f));

            for (int i = 0; i < waves.Count; i++)
            {
                BuildNode(waves[i], current, new Vector2(x + i * Spacing, topY));
            }

            if (tail)
            {
                TMP_Text dots = WaveThemeUi.Text("Dots", content, font, "...", 46f, Color.white);
                WaveThemeUi.Anchor(dots.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                    new Vector2(x + waves.Count * Spacing, topY - 6f), new Vector2(Spacing, 60f));
                BuildNode(total, current, new Vector2(x + (waves.Count + 1) * Spacing, topY));
            }
        }

        private void BuildNode(int wave, int current, Vector2 position)
        {
            bool boss = WaveTheme.IsBossWave(wave);
            bool isCurrent = wave == current;
            bool done = wave < current;

            RectTransform node = WaveThemeUi.Rect("Wave" + wave, content);
            WaveThemeUi.Anchor(node, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), position, new Vector2(NodeSize, NodeSize));
            if (isCurrent)
                node.localScale = Vector3.one * 1.18f;

            if (isCurrent)
            {
                Image ring = WaveThemeUi.Img("Highlight", node, WaveThemeUi.Circle(), WaveThemeUi.Gold);
                WaveThemeUi.Anchor(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(NodeSize + 16f, NodeSize + 16f));
            }

            Color body = boss ? WaveThemeUi.Red : WaveThemeUi.Navy;
            if (done)
                body = Color.Lerp(body, new Color(0.25f, 0.25f, 0.3f, 1f), 0.55f);

            Image circle = WaveThemeUi.Img("Body", node, WaveThemeUi.Circle(), body);
            WaveThemeUi.Anchor(circle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(NodeSize, NodeSize));

            TMP_Text number = WaveThemeUi.Text("Number", node, font, wave.ToString(), boss ? 44f : 42f,
                done ? new Color(1f, 1f, 1f, 0.55f) : Color.white);
            WaveThemeUi.Anchor(number.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(NodeSize, NodeSize));

            RectTransform badge = WaveThemeUi.Rect("Reward", node);
            WaveThemeUi.Anchor(badge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -NodeSize * 0.5f - 34f), new Vector2(52f, 52f));
            WaveThemeUi.BuildRewardBadge(badge, font, after(wave), 48f);
            if (done)
            {
                foreach (Graphic g in badge.GetComponentsInChildren<Graphic>())
                {
                    Color c = g.color;
                    c.a *= 0.45f;
                    g.color = c;
                }
            }
        }
    }

    /// <summary>웨이브가 열릴 때 화면 가운데에 "N웨이브 Start" 를 크게 띄운다. 보스 웨이브는 빨갛게 "BOSS".</summary>
    public sealed class UIWaveStartBanner : MonoBehaviour
    {
        private TMP_Text label;
        private CanvasGroup group;
        private Coroutine routine;

        public static UIWaveStartBanner Create(Transform canvas, TMP_FontAsset font)
        {
            RectTransform root = WaveThemeUi.Rect("WaveStartBanner", canvas);
            WaveThemeUi.Anchor(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(1000f, 220f));
            var banner = root.gameObject.AddComponent<UIWaveStartBanner>();
            // 기여도 패널 같은 다이얼로그 캔버스에 가려지지 않게 자기 정렬 순서를 쥔다(선택 창 5000 보다는 아래).
            var sorting = root.gameObject.AddComponent<Canvas>();
            sorting.overrideSorting = true;
            sorting.sortingOrder = 4000;
            banner.group = root.gameObject.AddComponent<CanvasGroup>();
            banner.group.alpha = 0f;
            banner.group.blocksRaycasts = false;
            banner.group.interactable = false;
            banner.label = WaveThemeUi.Text("Label", root, font, string.Empty, 128f, WaveThemeUi.Gold);
            // 큰 글자라 외곽선도 굵게. 어두운 배경에서도 보이게 검정으로.
            banner.label.outlineColor = new Color32(0, 0, 0, 255);
            banner.label.outlineWidth = 0.16f;
            banner.label.fontMaterial.EnableKeyword("OUTLINE_ON");
            WaveThemeUi.Anchor(banner.label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 220f));
            return banner;
        }

        public void Play(int wave, bool boss)
        {
            label.text = boss ? wave + "웨이브 BOSS!" : wave + "웨이브 Start";
            label.color = boss ? new Color(1f, 0.32f, 0.28f, 1f) : WaveThemeUi.Gold;

            if (routine != null)
                StopCoroutine(routine);

            routine = StartCoroutine(Animate(0.9f, 0.45f));
        }

        /// <summary>임의의 문구를 같은 연출로 띄운다(스테이지 클리어 등). 전체 길이는 0.25 + hold + fade 초.</summary>
        public void PlayText(string text, Color color, float hold, float fade)
        {
            label.text = text;
            label.color = color;

            if (routine != null)
                StopCoroutine(routine);

            routine = StartCoroutine(Animate(hold, fade));
        }

        /// <summary>실제 시간으로 돈다 — 배속·일시정지와 상관없이 같은 길이로 보인다.</summary>
        private IEnumerator Animate(float hold, float fade)
        {
            const float popIn = 0.25f;

            float t = 0f;
            while (t < popIn)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / popIn);
                float scale = k < 0.7f ? Mathf.Lerp(0.4f, 1.18f, k / 0.7f) : Mathf.Lerp(1.18f, 1f, (k - 0.7f) / 0.3f);
                label.rectTransform.localScale = Vector3.one * scale;
                group.alpha = Mathf.Clamp01(k * 1.6f);
                yield return null;
            }

            label.rectTransform.localScale = Vector3.one;
            group.alpha = 1f;

            float wait = 0f;
            while (wait < hold)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }

            t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(t / fade);
                yield return null;
            }

            group.alpha = 0f;
            routine = null;
        }

        public void HideNow()
        {
            if (routine != null)
                StopCoroutine(routine);

            routine = null;
            group.alpha = 0f;
        }
    }

    /// <summary>
    /// 보스·현상금 몬스터 체력바. 상단 웨이브 현황 바로 아래에 뜬다. 보스면 그 아래에 <b>남은 시간</b>을 같이 보인다(60초 안에 잡아야 한다).
    /// 매 프레임 살아 있는 보스(없으면 현상금)를 찾아 그린다 — 몬스터는 풀에서 재사용돼 이벤트로 잇기보다 읽는 편이 안전하다.
    /// </summary>
    public sealed class UIEnemyBar : MonoBehaviour
    {
        private MonsterManager monsters;
        private Func<Monster, (string name, float left, float limit)> info;
        private CanvasGroup group;
        private TMP_Text nameText;
        private TMP_Text percentText;
        private TMP_Text timerText;
        private RectTransform fill;
        private Image fillImage;
        private GameObject timerRoot;
        private RectTransform timerFill;
        private Image timerFillImage;

        /// <param name="info">대상 몬스터의 (이름, 남은 시간, 제한 시간). 제한 시간이 없으면 남은 시간을 음수로 돌려준다.</param>
        public static UIEnemyBar Create(Transform canvas, TMP_FontAsset font, MonsterManager monsters, Func<Monster, (string name, float left, float limit)> info)
        {
            RectTransform root = WaveThemeUi.Rect("EnemyBar", canvas);
            WaveThemeUi.Anchor(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(40f, -262f), new Vector2(620f, 120f));
            var bar = root.gameObject.AddComponent<UIEnemyBar>();
            bar.monsters = monsters;
            bar.info = info;
            bar.group = root.gameObject.AddComponent<CanvasGroup>();
            bar.group.alpha = 0f;
            bar.group.blocksRaycasts = false;

            Image back = WaveThemeUi.Img("Back", root, WaveThemeUi.Rounded(), new Color(0.05f, 0.06f, 0.12f, 0.9f));
            WaveThemeUi.Anchor(back.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(620f, 44f));

            RectTransform fillArea = WaveThemeUi.Rect("FillArea", root);
            WaveThemeUi.Anchor(fillArea, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(604f, 32f));
            Image track = WaveThemeUi.Img("Track", fillArea, WaveThemeUi.Rounded(), new Color(0.25f, 0.08f, 0.1f, 1f));
            WaveThemeUi.Anchor(track.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            track.rectTransform.anchorMax = Vector2.one;
            track.rectTransform.offsetMin = track.rectTransform.offsetMax = Vector2.zero;

            bar.fillImage = WaveThemeUi.Img("Fill", fillArea, WaveThemeUi.Rounded(), WaveThemeUi.Red);
            bar.fill = bar.fillImage.rectTransform;
            bar.fill.anchorMin = Vector2.zero;
            bar.fill.anchorMax = Vector2.one;
            bar.fill.offsetMin = bar.fill.offsetMax = Vector2.zero;

            bar.nameText = WaveThemeUi.Text("Name", root, font, "BOSS", 30f, Color.white, TextAlignmentOptions.Left);
            WaveThemeUi.Anchor(bar.nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-150f, -10f), new Vector2(300f, 32f));
            bar.percentText = WaveThemeUi.Text("Percent", root, font, "100%", 28f, Color.white, TextAlignmentOptions.Right);
            WaveThemeUi.Anchor(bar.percentText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(150f, -10f), new Vector2(300f, 32f));

            // 남은 시간 게이지. 체력바 바로 아래에서 줄어들고, 그 위에 남은 초를 글자로도 보인다.
            RectTransform timer = WaveThemeUi.Rect("Timer", root);
            WaveThemeUi.Anchor(timer, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(620f, 36f));
            bar.timerRoot = timer.gameObject;
            Image timerBack = WaveThemeUi.Img("Back", timer, WaveThemeUi.Rounded(), new Color(0.05f, 0.06f, 0.12f, 0.9f));
            timerBack.rectTransform.anchorMin = Vector2.zero;
            timerBack.rectTransform.anchorMax = Vector2.one;
            timerBack.rectTransform.offsetMin = timerBack.rectTransform.offsetMax = Vector2.zero;
            RectTransform timerArea = WaveThemeUi.Rect("FillArea", timer);
            timerArea.anchorMin = Vector2.zero;
            timerArea.anchorMax = Vector2.one;
            timerArea.offsetMin = new Vector2(8f, 6f);
            timerArea.offsetMax = new Vector2(-8f, -6f);
            bar.timerFillImage = WaveThemeUi.Img("Fill", timerArea, WaveThemeUi.Rounded(), WaveThemeUi.Gold);
            bar.timerFill = bar.timerFillImage.rectTransform;
            bar.timerFill.anchorMin = Vector2.zero;
            bar.timerFill.anchorMax = Vector2.one;
            bar.timerFill.offsetMin = bar.timerFill.offsetMax = Vector2.zero;
            bar.timerText = WaveThemeUi.Text("Label", timer, font, string.Empty, 26f, Color.white);
            bar.timerText.rectTransform.anchorMin = Vector2.zero;
            bar.timerText.rectTransform.anchorMax = Vector2.one;
            bar.timerText.rectTransform.offsetMin = bar.timerText.rectTransform.offsetMax = Vector2.zero;
            timer.gameObject.SetActive(false);
            return bar;
        }

        private void Update()
        {
            Monster target = null;
            bool boss = false;
            if (monsters != null)
            {
                List<Monster> list = monsters.activeMonsters;
                for (int i = 0; i < list.Count; i++)
                {
                    Monster m = list[i];
                    if (m == null || m.HpPercent <= 0)
                        continue;

                    if (m.IsStageBoss)
                    {
                        target = m;
                        boss = true;
                        break;
                    }

                    if (m.IsBounty && target == null)
                        target = m;
                }
            }

            if (target == null)
            {
                group.alpha = 0f;
                return;
            }

            group.alpha = 1f;
            float ratio = Mathf.Clamp01(target.HpPercent / 100f);
            fill.anchorMax = new Vector2(ratio, 1f);
            fillImage.color = boss ? WaveThemeUi.Red : new Color(0.72f, 0.3f, 0.78f, 1f);
            (string name, float left, float limit) shown = info != null ? info(target) : (boss ? "BOSS" : "현상금", -1f, 0f);
            nameText.text = shown.name;
            percentText.text = target.HpPercent + "%";

            float left = shown.left;
            if (left >= 0f)
            {
                int seconds = Mathf.CeilToInt(left);
                bool urgent = left <= 10f;
                timerRoot.SetActive(true);
                timerText.text = "남은 시간 " + seconds + "초";
                timerText.color = urgent ? new Color(1f, 0.3f, 0.25f, 1f) : Color.white;
                timerFill.anchorMax = new Vector2(shown.limit > 0f ? Mathf.Clamp01(left / shown.limit) : 0f, 1f);
                timerFillImage.color = urgent ? WaveThemeUi.Red : WaveThemeUi.Gold;
            }
            else
            {
                timerRoot.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 웨이브 사이의 선택 창. 화면을 딤으로 덮고 가운데에 제목·설명·버튼 몇 개를 띄운다. 고르면 사라지고 선택 번호를 돌려준다.
    /// 바깥을 눌러도 닫히지 않는다 — 다음 웨이브로 가려면 반드시 고른다(영혼 보너스처럼 닫아도 되는 창은 기존 창을 쓴다).
    /// </summary>
    public sealed class UIChoicePopup : MonoBehaviour
    {
        public struct Option
        {
            public string Label;
            public string Sub;
            public Color Color;
            public bool Enabled;

            public Option(string label, string sub, Color color, bool enabled = true)
            {
                Label = label;
                Sub = sub;
                Color = color;
                Enabled = enabled;
            }
        }

        private Action<int> onChosen;
        private RectTransform[] optionRects;

        /// <summary>마지막으로 고른 선택지의 화면 좌표. 이벤트 보상이 거기서 날아오른다(없으면 화면 가운데).</summary>
        public static Vector2 LastChosenScreen { get; private set; } = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        /// <summary>선택지 수. 밸런스 봇이 고를 때 쓴다.</summary>
        public int OptionCount { get; private set; }

        /// <summary>선택지 제목. 밸런스 봇이 어떤 창인지 알아보는 데 쓴다.</summary>
        public string Title { get; private set; }

        public static UIChoicePopup Open(Transform canvas, TMP_FontAsset font, string title, string message, IList<Option> options, Action<int> onChosen)
        {
            RectTransform root = WaveThemeUi.Rect("ChoicePopup", canvas);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            // 다른 UI 위에 오도록 따로 정렬한다.
            var overrideCanvas = root.gameObject.AddComponent<Canvas>();
            overrideCanvas.overrideSorting = true;
            overrideCanvas.sortingOrder = 5000;
            root.gameObject.AddComponent<GraphicRaycaster>();

            var popup = root.gameObject.AddComponent<UIChoicePopup>();
            popup.onChosen = onChosen;
            popup.OptionCount = options.Count;
            popup.Title = title;
            popup.optionRects = new RectTransform[options.Count];

            Image dim = WaveThemeUi.Img("Dim", root, null, new Color(0f, 0f, 0f, 0.7f), raycast: true);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;

            float buttonHeight = 150f;
            float gap = 22f;
            float panelHeight = 330f + options.Count * (buttonHeight + gap);
            Image panel = WaveThemeUi.Img("Panel", root, WaveThemeUi.Rounded(), WaveThemeUi.Panel);
            WaveThemeUi.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(900f, panelHeight));

            TMP_Text titleText = WaveThemeUi.Text("Title", panel.transform, font, title, 66f, WaveThemeUi.Gold);
            WaveThemeUi.Anchor(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(840f, 90f));
            TMP_Text messageText = WaveThemeUi.Text("Message", panel.transform, font, message, 38f, Color.white, outline: false);
            WaveThemeUi.Anchor(messageText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(840f, 90f));
            messageText.textWrappingMode = TextWrappingModes.Normal;

            for (int i = 0; i < options.Count; i++)
            {
                Option option = options[i];
                float y = -280f - i * (buttonHeight + gap) - buttonHeight * 0.5f;
                Image button = WaveThemeUi.Img("Option" + i, panel.transform, WaveThemeUi.Rounded(),
                    option.Enabled ? option.Color : new Color(0.3f, 0.3f, 0.34f, 1f), raycast: true);
                WaveThemeUi.Anchor(button.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(800f, buttonHeight));

                popup.optionRects[i] = button.rectTransform;
                var ui = button.gameObject.AddComponent<Button>();
                ui.targetGraphic = button;
                ui.interactable = option.Enabled;
                int index = i;
                ui.onClick.AddListener(() => popup.Choose(index));

                bool hasSub = !string.IsNullOrEmpty(option.Sub);
                TMP_Text label = WaveThemeUi.Text("Label", button.transform, font, option.Label, 54f, Color.white);
                WaveThemeUi.Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, hasSub ? 22f : 0f), new Vector2(760f, 70f));
                if (hasSub)
                {
                    TMP_Text sub = WaveThemeUi.Text("Sub", button.transform, font, option.Sub, 32f, new Color(1f, 1f, 1f, 0.85f), outline: false);
                    WaveThemeUi.Anchor(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(760f, 44f));
                }
            }

            return popup;
        }

        public void Choose(int index)
        {
            // 창이 사라지기 전에 고른 버튼의 화면 좌표를 적어 둔다 — 보상이 거기서 날아오른다.
            LastChosenScreen = optionRects != null && index >= 0 && index < optionRects.Length && optionRects[index] != null
                ? UIRewardFly.ScreenPointOf(optionRects[index])
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Action<int> callback = onChosen;
            onChosen = null;
            Destroy(gameObject);
            callback?.Invoke(index);
        }

        public void Close()
        {
            onChosen = null;
            Destroy(gameObject);
        }
    }
}
