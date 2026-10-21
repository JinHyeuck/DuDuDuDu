using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.Bounty;
using OJ.DI;
using OJ.UI;

namespace OJ.Economy
{
    /// <summary>
    /// 영혼 보너스 저금통. <b>원래 저금통 자리</b>(하단 줄, 현상금과 소환 버튼 사이)에 떠서, 적이 죽을 때마다
    /// <b>영혼이 구불구불 날아와 쌓이는 것</b>과 <b>n/n(도착한 영혼 / 목표)</b>을 보여 준다.
    /// 목표를 채우면 "받기!" 로 바뀌고 <b>유저가 눌러</b> 팝업(<see cref="UISoulBonusDialog"/>)을 연다. 게임은 멈추지 않는다.
    ///
    /// <b>영혼 비행.</b> 죽은 자리(월드)를 화면 좌표로 바꿔 이 창 안의 좌표로 옮기고, 저금통 아이콘까지 날린다.
    /// 시작·끝을 잇는 직선 위에 수직 방향 사인(S자) 흔들림을 얹어 <b>직선이 아닌 구불구불한 길</b>을 그린다
    /// (흔들림은 시작과 끝에서 0 이라 정확히 아이콘에 닿는다). 영혼 모양은 스프라이트가 없어 런타임에 방사형 광채 텍스처를 만든다.
    /// 동시에 많이 나오므로 Image 를 풀로 돌려 쓰고, 한도를 넘으면 그림 없이 곧바로 도착으로 센다.
    ///
    /// <b>시간은 실제 시간이다.</b> 배속·일시정지를 타지 않는다 — 팝업이 게임을 세워도 날던 영혼은 끝까지 날아 도착한다.
    /// <b>여기에 판단을 두지 않는다.</b> 세고 팝업을 여는 일은 <see cref="SoulBonusManager"/> 가 한다.
    /// </summary>
    public sealed class UISoulBonusButton : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [SerializeField] private Button button;
        [SerializeField] private RectTransform bobTarget;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Image gaugeFill;

        private static readonly Color EmptyColor = new Color(0.45f, 0.9f, 1f, 1f);
        private static readonly Color FullColor = new Color(1f, 0.9f, 0.35f, 1f);

        private const float PopTime = 0.22f;
        private const float BobBase = 40f;
        private const int MaxSouls = 48;
        private const float FlightMin = 0.8f;
        private const float FlightMax = 1.05f;
        private const float WobbleAmplitude = 120f;
        private const float WobbleCycles = 1.5f;
        private const float SoulSize = 92f;

        private sealed class Soul
        {
            public Image Image;
            public Vector3 From;
            public float Age;
            public float Duration;
            public float Sign;
            public float Amplitude;
        }

        private readonly List<Soul> flying = new List<Soul>();
        private readonly Stack<Image> idle = new Stack<Image>();

        private SoulBonusManager manager;
        private string lastCount;
        private string lastName;
        private bool readyBob;
        private float shownFill;
        private float popElapsed = PopTime;
        private float time;

        protected override void OnLoad()
        {
            button.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            manager?.TryOpen();
        }

        protected override void OnEnter()
        {
            lastCount = null;
            lastName = null;
            popElapsed = PopTime;

            Unlisten();
            manager = battle != null && battle.Game != null ? battle.Game.SoulBonus : null;
            if (manager != null)
            {
                manager.FlightRequest = Launch;
                manager.OnSoulArrived += OnArrived;
                shownFill = manager.Goal > 0 ? manager.DisplayKills / (float)manager.Goal : 0f;
            }

            Refresh();
        }

        protected override void OnExit()
        {
            Unlisten();
        }

        protected override void OnUnload()
        {
            Unlisten();
        }

        private void Unlisten()
        {
            if (manager == null)
                return;

            manager.OnSoulArrived -= OnArrived;
            if (manager.FlightRequest == (System.Action<Vector3>)Launch)
                manager.FlightRequest = null;

            // 날던 영혼은 사라진다. 도착한 것으로 쳐서 화면 수가 데이터를 따라가게 한다.
            ClearSouls();
            manager.FlushDisplay();
            manager = null;
        }

        private void Update()
        {
            if (!_isEnter)
                return;

            time += Time.unscaledDeltaTime;
            Refresh();
            Animate();
            UpdateSouls(Time.unscaledDeltaTime);
        }

        private void Refresh()
        {
            if (manager == null)
                return;

            int goal = manager.Goal;
            int shown = manager.DisplayKills;

            string count = shown + "/" + goal;
            if (count != lastCount)
            {
                countText.SetText(count);
                lastCount = count;
            }

            // 막대는 숫자를 부드럽게 따라간다. 가득 차면 색이 노랗게 깜빡인다.
            float target = goal > 0 ? Mathf.Clamp01(shown / (float)goal) : 0f;
            shownFill = Mathf.MoveTowards(shownFill, target, Time.unscaledDeltaTime * 3f);
            gaugeFill.fillAmount = shownFill;

            Color color = Color.Lerp(EmptyColor, FullColor, shownFill);
            if (target >= 1f)
                color = Color.Lerp(FullColor, Color.white, 0.5f + 0.5f * Mathf.Sin(time * 14f));

            gaugeFill.color = color;

            // 받을 수 있으면 이름 자리가 "받기!" 로 바뀌고 돼지가 들썩인다.
            bool ready = manager.IsReady;
            string label = ready ? "받기!" : "영혼 보너스";
            if (label != lastName)
            {
                nameText.SetText(label);
                nameText.color = ready ? FullColor : Color.white;
                lastName = label;
            }

            readyBob = ready;
        }

        private void OnArrived()
        {
            popElapsed = 0f;
        }

        private void Animate()
        {
            if (bobTarget == null)
                return;

            float scale = 1f;
            if (popElapsed < PopTime)
            {
                popElapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(popElapsed / PopTime);
                scale = 1f + 0.18f * Mathf.Sin(k * Mathf.PI);
            }

            bobTarget.localScale = Vector3.one * scale;
            bobTarget.anchoredPosition = new Vector2(0f, BobBase + (readyBob ? Mathf.Sin(time * 6f) * 8f : 0f));
        }

        // ── 영혼 비행 ─────────────────────────────────────────────

        private void Launch(Vector3 deathWorld)
        {
            if (flying.Count >= MaxSouls)
            {
                // 한도를 넘으면 그림 없이 곧바로 도착으로 센다 — 센 수가 틀리면 팝업이 안 뜬다.
                manager?.NotifySoulArrived();
                return;
            }

            Image image = idle.Count > 0 ? idle.Pop() : CreateSoulImage();
            image.gameObject.SetActive(true);

            Vector3 from = WorldToLocalWorld(deathWorld);
            image.rectTransform.position = from;
            image.rectTransform.localScale = Vector3.one;

            flying.Add(new Soul
            {
                Image = image,
                From = from,
                Age = 0f,
                Duration = Random.Range(FlightMin, FlightMax),
                Sign = Random.value < 0.5f ? -1f : 1f,
                Amplitude = WobbleAmplitude * Random.Range(0.7f, 1.2f),
            });
        }

        private void UpdateSouls(float dt)
        {
            if (flying.Count == 0)
                return;

            Vector3 to = icon.transform.position;
            float pixel = transform.lossyScale.x;

            for (int i = flying.Count - 1; i >= 0; i--)
            {
                Soul soul = flying[i];
                soul.Age += dt;
                float t = Mathf.Clamp01(soul.Age / soul.Duration);

                if (t >= 1f)
                {
                    Recycle(soul.Image);
                    flying.RemoveAt(i);
                    manager?.NotifySoulArrived();
                    continue;
                }

                // 직선 위를 가속하며 가고, 그 위에 수직 사인 흔들림을 얹는다(양 끝에서 0 — 정확히 닿는다).
                float eased = t * t * (3f - 2f * t);
                Vector3 line = Vector3.Lerp(soul.From, to, eased);

                Vector3 dir = to - soul.From;
                Vector3 side = dir.sqrMagnitude > 0.0001f
                    ? new Vector3(-dir.y, dir.x, 0f).normalized
                    : Vector3.right;

                float wobble = Mathf.Sin(t * WobbleCycles * 2f * Mathf.PI) * Mathf.Pow(1f - t, 0.6f);
                soul.Image.rectTransform.position = line + side * (soul.Sign * soul.Amplitude * pixel * wobble);

                // 가까워질수록 작아지고 마지막에 옅어진다.
                soul.Image.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, t);
                Color c = soul.Image.color;
                c.a = 1f - Mathf.Clamp01((t - 0.85f) / 0.15f);
                soul.Image.color = c;
            }
        }

        private Image CreateSoulImage()
        {
            var go = new GameObject("Soul", typeof(RectTransform));
            go.layer = icon.gameObject.layer;
            go.transform.SetParent(dialogView.transform, false);

            var image = go.AddComponent<Image>();
            image.sprite = SoulSprite.Get();
            image.raycastTarget = false;
            image.color = new Color(0.62f, 0.96f, 1f, 1f);
            image.rectTransform.sizeDelta = new Vector2(SoulSize, SoulSize);
            return image;
        }

        private void Recycle(Image image)
        {
            Color c = image.color;
            c.a = 1f;
            image.color = c;
            image.gameObject.SetActive(false);
            idle.Push(image);
        }

        private void ClearSouls()
        {
            for (int i = 0; i < flying.Count; i++)
                Recycle(flying[i].Image);

            flying.Clear();
        }

        /// <summary>
        /// 게임 월드의 위치를 이 창 캔버스의 월드 좌표로 바꾼다. <b>월드 좌표를 그대로 쓰면 안 된다</b> —
        /// 이 창은 오버레이 캔버스(좌표가 곧 화면 픽셀)라 같은 숫자가 전혀 다른 자리를 가리킨다. 화면 좌표를 거쳐 건넌다.
        /// </summary>
        private Vector3 WorldToLocalWorld(Vector3 world)
        {
            Camera gameCamera = Camera.main;
            Vector3 screen = gameCamera != null
                ? gameCamera.WorldToScreenPoint(world)
                : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

            var space = dialogView.transform as RectTransform;
            Canvas myCanvas = space != null ? space.GetComponentInParent<Canvas>() : null;
            Camera myCamera = myCanvas != null && myCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? myCanvas.worldCamera
                : null;

            return space != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(space, screen, myCamera, out Vector3 result)
                ? result
                : icon.transform.position;
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        // 원래 저금통 자리: 하단 줄에서 현상금과 소환 버튼 사이(왼쪽 아래 기준).
        private static readonly Vector2 Size = new Vector2(130f, 190f);
        private static readonly Vector2 Position = new Vector2(285f, 100f);

        /// <summary>에디터 굽기 전용.</summary>
        public static UISoulBonusButton Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UISoulBonusButton", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(0f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Size;
            rootRect.anchoredPosition = Position;

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UISoulBonusButton>();
            dialog.dialogView = view;

            // 백키로 닫지 않는다 — 전투 중 백키는 다른 뜻이고, 이 창은 상태 표시일 뿐이다.
            dialog.UseBackBtn = false;
            dialog.BakeOpenStyle(DialogOpenStyle.None);

            // 눌림 면적은 버튼 전체. 투명 판이 받는다.
            Image hit = UIBountyUIFactory.CreateImage("Hit", view.transform, new Color(1f, 1f, 1f, 0f));
            UIBountyUIFactory.Stretch(hit.rectTransform);
            dialog.button = hit.gameObject.AddComponent<Button>();
            dialog.button.targetGraphic = hit;
            dialog.button.transition = Selectable.Transition.None;

            // 돼지 — 튀는 덩어리.
            GameObject bob = UIBountyUIFactory.CreateRect("Bob", view.transform);
            UIBountyUIFactory.SetRect(bob.GetComponent<RectTransform>(), new Vector2(110f, 110f), new Vector2(0f, BobBase));
            dialog.bobTarget = bob.GetComponent<RectTransform>();

            dialog.icon = UIBountyUIFactory.CreateImage("Icon", bob.transform, Color.white);
            UIBountyUIFactory.SetRect(dialog.icon.rectTransform, new Vector2(104f, 104f), Vector2.zero);
            dialog.icon.sprite = UIBountyUIFactory.LoadSprite("Main/Icon_Reward_Pig");
            dialog.icon.preserveAspect = true;
            dialog.icon.raycastTarget = false;

            // 우측 상단 n/n 배지.
            Image badge = UIBountyUIFactory.CreateSprite("Badge", view.transform,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_SmallBox"), 3f, new Vector2(92f + 26f, 38f + 26f), new Vector2(26f, 88f), true);
            badge.color = new Color(0.12f, 0.1f, 0.2f, 0.95f);
            badge.raycastTarget = false;

            dialog.countText = UIBountyUIFactory.CreateText("Count", view.transform, "0/10", 26f,
                TextAlignmentOptions.Center, new Color(1f, 0.92f, 0.4f, 1f), font);
            UIBountyUIFactory.SetRect(dialog.countText.rectTransform, new Vector2(92f, 38f), new Vector2(26f, 88f));

            // 채움 게이지.
            Image gaugeBack = UIBountyUIFactory.CreateImage("GaugeBack", view.transform, new Color(0.05f, 0.05f, 0.1f, 0.9f));
            UIBountyUIFactory.SetRect(gaugeBack.rectTransform, new Vector2(114f, 24f), new Vector2(0f, -30f));
            gaugeBack.raycastTarget = false;

            dialog.gaugeFill = UIBountyUIFactory.CreateImage("GaugeFill", gaugeBack.transform, new Color(0.45f, 0.9f, 1f, 1f));
            UIBountyUIFactory.SetRect(dialog.gaugeFill.rectTransform, new Vector2(106f, 16f), Vector2.zero);
            dialog.gaugeFill.type = Image.Type.Filled;
            dialog.gaugeFill.fillMethod = Image.FillMethod.Horizontal;
            dialog.gaugeFill.fillOrigin = 0;
            dialog.gaugeFill.fillAmount = 0f;
            dialog.gaugeFill.raycastTarget = false;

            dialog.nameText = UIBountyUIFactory.CreateText("Name", view.transform, "영혼 보너스", 24f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.nameText.rectTransform, new Vector2(Size.x + 30f, 34f), new Vector2(0f, -62f));

            return dialog;
        }
    }

    /// <summary>
    /// 영혼 스프라이트. 킷에 동그란 광채가 없어 방사형 그라데이션 텍스처를 한 번 만들어 모든 영혼이 나눠 쓴다.
    /// 가운데는 하얗고 가장자리로 갈수록 투명해진다 — 색은 <c>Image.color</c> 로 입힌다.
    /// </summary>
    internal static class SoulSprite
    {
        private const int Resolution = 64;

        private static Sprite cached;

        public static Sprite Get()
        {
            if (cached != null)
                return cached;

            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = "SoulOrb",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            float half = (Resolution - 1) * 0.5f;
            var pixels = new Color32[Resolution * Resolution];
            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float glow = Mathf.Clamp01(1f - d);
                    float alpha = Mathf.Pow(glow, 1.05f);
                    float core = Mathf.Clamp01(1f - d / 0.5f);

                    // 가운데는 하얀 알맹이, 바깥은 색이 입혀질 광채.
                    byte shade = (byte)(Mathf.Lerp(0.75f, 1f, core) * 255f);
                    pixels[y * Resolution + x] = new Color32(shade, shade, shade, (byte)(Mathf.Clamp01(alpha + core * 0.6f) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            cached = Sprite.Create(texture, new Rect(0f, 0f, Resolution, Resolution), new Vector2(0.5f, 0.5f), 100f);
            cached.name = "SoulOrb";
            return cached;
        }
    }
}
