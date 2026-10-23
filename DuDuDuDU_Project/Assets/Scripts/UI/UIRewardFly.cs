using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.SceneFlow;

namespace OJ.UI
{
    /// <summary>
    /// 보상 아이콘이 한 점에서 터져 나와 목표 칸으로 날아가는 연출.
    ///
    /// <b>화면 좌표로만 말한다.</b> 출발점은 월드(죽은 자리)일 수도 있고 UI 일 수도 있고, 목표는
    /// ScreenSpaceCamera 캔버스(보드 HUD)에 있거나 Overlay 에 있다. 그래서 둘 다 화면 픽셀로 바꿔 건네고,
    /// 연출은 자체 Overlay 캔버스 위에서 그 좌표 그대로 그린다(Overlay 의 위치 = 화면 픽셀).
    ///
    /// <b>시간은 실제 시간이다.</b> 배속·일시정지를 타지 않는다 — 결과창이 게임을 세워도 날던 아이콘은 도착한다.
    /// <b>입력을 먹지 않는다.</b> 레이캐스터를 달지 않고 그림마다 raycastTarget 도 끈다.
    /// 목표가 도중에 사라지면(씬 정리) 그 자리에서 조용히 끝난다. 값은 이미 지급된 뒤라 이 연출은 보기만 한다.
    /// </summary>
    public static class UIRewardFly
    {
        private static UIRewardFlyLayer layer;

        /// <summary>월드 좌표에서 출발한다. 메인 카메라가 없으면 화면 가운데에서 출발한다.</summary>
        public static void FlyFromWorld(Vector3 world, Sprite icon, RectTransform target, int amount,
            string label = null, TMP_Text fontSource = null, Color? labelColor = null, Action<int> onArrive = null,
            float iconScale = 1f)
        {
            Camera camera = Camera.main;
            Vector2 screen = camera != null
                ? (Vector2)camera.WorldToScreenPoint(world)
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Fly(screen, icon, target, amount, label, fontSource, labelColor, onArrive, iconScale);
        }

        /// <summary>
        /// 화면 좌표에서 출발한다. <paramref name="amount"/> 는 아이콘 개수를 정하는 데만 쓴다(많을수록 더 흩뿌린다).
        /// <paramref name="label"/> 은 마지막 아이콘이 닿을 때 목표 위에 떠올랐다 사라진다.
        /// <paramref name="iconScale"/> 은 그림 안쪽 여백이 큰 아이콘(마석)을 키워 보이게 하는 배율이다.
        /// <paramref name="onArrive"/> 는 아이콘이 닿을 때마다 불리고, 인자는 그 아이콘이 맡은 몫이다
        /// (몫을 다 더하면 <paramref name="amount"/> 와 정확히 같다). 화면 숫자를 도착에 맞춰 올리는 데 쓴다.
        /// </summary>
        public static void Fly(Vector2 fromScreen, Sprite icon, RectTransform target, int amount,
            string label = null, TMP_Text fontSource = null, Color? labelColor = null, Action<int> onArrive = null,
            float iconScale = 1f)
        {
            if (target == null || amount <= 0)
            {
                // 날릴 곳이 없어도 몫은 돌려준다 — 숫자를 쥐고 있던 쪽이 영영 못 푸는 일이 없게.
                onArrive?.Invoke(Mathf.Max(0, amount));
                return;
            }

            EnsureLayer().Launch(fromScreen, icon, target, amount, CountFor(amount), label, fontSource,
                labelColor ?? Color.white, onArrive, iconScale);
        }

        /// <summary>한 번 튕긴다(크기가 잠깐 커졌다 돌아온다). 아이콘이 닿을 때마다 목표에 건다.</summary>
        public static void Punch(RectTransform target)
        {
            if (target == null)
                return;

            EnsureLayer().Punch(target);
        }

        /// <summary>아이콘 없이 그 칸 위에 글자만 띄운다(강화 성공의 "+10%" 같은 것).</summary>
        public static void FloatText(RectTransform at, string label, TMP_Text fontSource, Color color)
        {
            if (at == null || string.IsNullOrEmpty(label))
                return;

            EnsureLayer().SpawnFloat(at, label,
                fontSource != null ? fontSource.font : null,
                fontSource != null ? fontSource.fontSharedMaterial : null,
                color);
        }

        private static int CountFor(int amount)
        {
            return Mathf.Clamp(Mathf.CeilToInt(amount / 10f), 3, 8);
        }

        private static UIRewardFlyLayer EnsureLayer()
        {
            if (layer != null)
                return layer;

            var go = new GameObject("RewardFlyLayer", typeof(RectTransform));

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // 페이드 띠 바로 아래다. 팝업과 보드 연출(DiceFx) 위에 서되 씬 전환은 덮어야 한다.
            canvas.sortingOrder = FadeView.SortingOrder - 2;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            layer = go.AddComponent<UIRewardFlyLayer>();
            layer.Init(canvas);
            return layer;
        }

        /// <summary>그 칸이 그려지는 카메라를 거쳐 화면 픽셀로 바꾼다. 중심(pivot 이 아니라 사각형 가운데)을 쓴다.</summary>
        internal static Vector2 ScreenPointOf(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? (canvas.worldCamera != null ? canvas.worldCamera : Camera.main)
                : null;

            return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        }
    }

    internal sealed class UIRewardFlyLayer : MonoBehaviour
    {
        private const float IconSize = 72f;
        private const float BurstTime = 0.2f;
        private const float StaggerStep = 0.045f;
        private const float PunchTime = 0.22f;
        private const float PunchScale = 0.22f;
        private const float FloatTime = 0.95f;
        private const float FloatRise = 90f;

        private sealed class Batch
        {
            public int Remaining;
            public int AmountLeft;
            public int Share;
            public Action<int> OnArrive;
            public RectTransform Target;
            public string Label;
            public TMP_FontAsset Font;
            public Material FontMaterial;
            public Color LabelColor;
        }

        private sealed class Particle
        {
            public Image Image;
            public Batch Batch;
            public Vector2 Start;
            public Vector2 Burst;
            public float Delay;
            public float Age;
            public float FlyTime;
            public float Arc;
        }

        private sealed class FloatingText
        {
            public TMP_Text Text;
            public Vector2 From;
            public float Age;
        }

        private sealed class PunchState
        {
            public Vector3 BaseScale;
            public float Age;
        }

        private readonly List<Particle> particles = new List<Particle>();
        private readonly Stack<Image> idle = new Stack<Image>();
        private readonly List<FloatingText> floats = new List<FloatingText>();
        private readonly Dictionary<RectTransform, PunchState> punches = new Dictionary<RectTransform, PunchState>();
        private readonly List<RectTransform> finishedPunches = new List<RectTransform>();

        private Canvas canvas;

        public void Init(Canvas owner)
        {
            canvas = owner;
        }

        public void Launch(Vector2 from, Sprite icon, RectTransform target, int amount, int count, string label,
            TMP_Text fontSource, Color labelColor, Action<int> onArrive, float iconScale)
        {
            var batch = new Batch
            {
                Remaining = count,
                AmountLeft = amount,
                Share = amount / count,
                OnArrive = onArrive,
                Target = target,
                Label = label,
                Font = fontSource != null ? fontSource.font : null,
                FontMaterial = fontSource != null ? fontSource.fontSharedMaterial : null,
                LabelColor = labelColor,
            };

            float scale = canvas.scaleFactor;
            for (int i = 0; i < count; i++)
            {
                Image image = idle.Count > 0 ? idle.Pop() : CreateImage();
                image.sprite = icon;
                image.rectTransform.sizeDelta = new Vector2(IconSize, IconSize) * iconScale;
                image.gameObject.SetActive(true);

                float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                float radius = UnityEngine.Random.Range(60f, 150f) * scale;

                particles.Add(new Particle
                {
                    Image = image,
                    Batch = batch,
                    Start = from,
                    Burst = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    Delay = i * StaggerStep,
                    FlyTime = UnityEngine.Random.Range(0.45f, 0.65f),
                    Arc = UnityEngine.Random.Range(-90f, 90f) * scale,
                });

                image.rectTransform.position = from;
                image.rectTransform.localScale = Vector3.zero;
            }
        }

        public void Punch(RectTransform target)
        {
            if (punches.TryGetValue(target, out PunchState state))
            {
                // 튕기는 중이면 처음부터 다시 튕기되 기준 크기는 건드리지 않는다(작아졌다 커지는 누적을 막는다).
                state.Age = 0f;
                return;
            }

            punches[target] = new PunchState { BaseScale = target.localScale, Age = 0f };
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateParticles(dt);
            UpdateFloats(dt);
            UpdatePunches(dt);
        }

        private void UpdateParticles(float dt)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                RectTransform target = p.Batch.Target;

                // 목표가 사라졌다 — 씬이 정리되는 중이다. 날던 것을 조용히 거둔다.
                // 몫은 돌려준다. 쥐고 있던 쪽이 영영 못 푸는 일이 없게 한다(받는 쪽도 같이 사라졌을 수 있어 비어 있어도 된다).
                if (target == null)
                {
                    Settle(p.Batch);
                    Recycle(p.Image);
                    particles.RemoveAt(i);
                    continue;
                }

                p.Age += dt;
                float age = p.Age - p.Delay;
                if (age < 0f)
                    continue;

                RectTransform rect = p.Image.rectTransform;
                if (age < BurstTime)
                {
                    // 터져 나온다: 빠르게 퍼지다 멎는다.
                    float k = age / BurstTime;
                    float eased = 1f - (1f - k) * (1f - k);
                    rect.position = Vector2.LerpUnclamped(p.Start, p.Burst, eased);
                    rect.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, eased);
                    continue;
                }

                float t = Mathf.Clamp01((age - BurstTime) / p.FlyTime);
                if (t >= 1f)
                {
                    Arrive(p.Batch, target);
                    Recycle(p.Image);
                    particles.RemoveAt(i);
                    continue;
                }

                // 가속하며 날아간다. 옆으로 휜 호를 더하되 양 끝에서 0 이라 정확히 목표에 닿는다.
                Vector2 to = UIRewardFly.ScreenPointOf(target);
                Vector2 line = Vector2.LerpUnclamped(p.Burst, to, t * t);
                Vector2 dir = to - p.Burst;
                Vector2 side = dir.sqrMagnitude > 0.0001f ? new Vector2(-dir.y, dir.x).normalized : Vector2.right;

                rect.position = line + side * (Mathf.Sin(t * Mathf.PI) * p.Arc);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, t);
            }
        }

        private void Arrive(Batch batch, RectTransform target)
        {
            Punch(target);
            Settle(batch);

            if (batch.Remaining == 0 && !string.IsNullOrEmpty(batch.Label))
                SpawnFloat(target, batch.Label, batch.Font, batch.FontMaterial, batch.LabelColor);
        }

        // 아이콘 하나가 맡은 몫을 건넨다. 마지막 아이콘이 나머지를 전부 가져가 합이 정확히 맞는다.
        private static void Settle(Batch batch)
        {
            batch.Remaining--;
            int share = batch.Remaining == 0 ? batch.AmountLeft : batch.Share;
            batch.AmountLeft -= share;
            batch.OnArrive?.Invoke(share);
        }

        public void SpawnFloat(RectTransform target, string label, TMP_FontAsset font, Material fontMaterial, Color color)
        {
            var go = new GameObject("Float", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            if (fontMaterial != null)
                text.fontSharedMaterial = fontMaterial;

            text.text = label;
            text.fontSize = 56f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(420f, 90f);

            Vector2 from = UIRewardFly.ScreenPointOf(target);
            text.rectTransform.position = from;
            floats.Add(new FloatingText { Text = text, From = from });
        }

        private void UpdateFloats(float dt)
        {
            float scale = canvas.scaleFactor;
            for (int i = floats.Count - 1; i >= 0; i--)
            {
                FloatingText f = floats[i];
                f.Age += dt;
                float k = Mathf.Clamp01(f.Age / FloatTime);

                if (k >= 1f)
                {
                    Destroy(f.Text.gameObject);
                    floats.RemoveAt(i);
                    continue;
                }

                f.Text.rectTransform.position = f.From + new Vector2(0f, FloatRise * scale * (1f - (1f - k) * (1f - k)));

                float pop = k < 0.2f ? Mathf.Lerp(0.6f, 1.15f, k / 0.2f) : Mathf.Lerp(1.15f, 1f, (k - 0.2f) / 0.8f);
                f.Text.rectTransform.localScale = Vector3.one * pop;

                Color c = f.Text.color;
                c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                f.Text.color = c;
            }
        }

        private void UpdatePunches(float dt)
        {
            if (punches.Count == 0)
                return;

            finishedPunches.Clear();
            foreach (KeyValuePair<RectTransform, PunchState> pair in punches)
            {
                RectTransform rect = pair.Key;
                PunchState state = pair.Value;

                if (rect == null)
                {
                    finishedPunches.Add(rect);
                    continue;
                }

                state.Age += dt;
                float k = Mathf.Clamp01(state.Age / PunchTime);
                rect.localScale = state.BaseScale * (1f + PunchScale * Mathf.Sin(k * Mathf.PI));

                if (k >= 1f)
                {
                    rect.localScale = state.BaseScale;
                    finishedPunches.Add(rect);
                }
            }

            for (int i = 0; i < finishedPunches.Count; i++)
                punches.Remove(finishedPunches[i]);
        }

        private Image CreateImage()
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.rectTransform.sizeDelta = new Vector2(IconSize, IconSize);
            return image;
        }

        private void Recycle(Image image)
        {
            image.gameObject.SetActive(false);
            idle.Push(image);
        }
    }
}
