using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using OJ.SceneFlow;
using OJ.UI;

namespace OJ.Dice
{
    /// <summary>
    /// 보드 위 다이스의 연출 셋 — 소환 팝 · 머지 먼지 · 진화 번개.
    ///
    /// <b>왜 static 인가.</b> 연출은 상태가 없다. 대상과 색만 받아 오브젝트를 찍고
    /// 스스로 지운다. MonoBehaviour 로 세우면 씬에 오브젝트를 놓고 <c>BattleScope</c> 의
    /// 등재 목록까지 늘려야 하는데, 얻는 것이 없다(AGENTS "가져오지 않는 것" 과 같은 판단).
    ///
    /// <b>왜 ParticleSystem 이 아닌가.</b> UI 는 uGUI 이고 보드는 캔버스 안에 있다.
    /// <c>ParticleSystem</c> 은 캔버스 정렬에 끼지 못해 보드 뒤나 앞에 통째로 깔리고,
    /// 그걸 맞추려면 전용 카메라·렌더 텍스처·정렬 레이어가 따라온다. 입자 수십 개짜리
    /// 연출에 렌더 경로를 하나 더 들이지 않는다 — <c>Image</c> 로 찍고 UniTask 로 민다.
    ///
    /// <b>시간은 전부 <c>unscaledDeltaTime</c> 이다.</b> 배속·일시정지를 타지 않는다
    /// (<c>UIAppear</c>·<c>TutorialDirector</c> 와 같은 규약). 연출 길이가 시간 설정에
    /// 묶이면 2배속에서 먼지가 절반만 보인다.
    /// </summary>
    public static class DiceFx
    {
        // ──────────────────────────────────────────────── 소환: 커졌다 작아진다

        private const float PopGrowSeconds = 0.22f;
        private const float PopSettleSeconds = 0.14f;
        private const float PopPeak = 1.3f;

        /// <summary>소환 팝이 끝나기까지. 연출이 끝나길 기다려야 하는 쪽(튜토리얼)이 쓴다.</summary>
        public const float SummonSeconds = PopGrowSeconds + PopSettleSeconds;

        /// <summary>
        /// 놓인 다이스가 0 → 1.3 → 1 로 자리를 잡는다.
        ///
        /// <b>스케일만 건드린다.</b> 다이스는 <c>GridLayoutGroup</c> 아래 슬롯의 자식이고,
        /// 레이아웃 그룹은 자식의 좌표를 자기가 소유한다 — 연출이 좌표를 쓰면 그 값이
        /// 눌러앉아 배치가 무너진다(<c>UIAppear</c> 가 같은 이유로 좌표를 안 쓴다).
        /// </summary>
        public static void PlaySummon(Transform dice)
        {
            if (dice == null)
                return;

            PopAsync(dice).Forget();
        }

        private static async UniTaskVoid PopAsync(Transform target)
        {
            // 첫 프레임부터 0 으로 둔다. 한 프레임이라도 제 크기로 보이면 깜빡인 것처럼 보인다.
            target.localScale = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < SummonSeconds)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (target == null)
                    return;

                elapsed += Time.unscaledDeltaTime;
                float scale = elapsed < PopGrowSeconds
                    ? Mathf.LerpUnclamped(0f, PopPeak, UIEase.OutQuad(elapsed / PopGrowSeconds))
                    : Mathf.Lerp(PopPeak, 1f, UIEase.OutQuad(Mathf.Clamp01((elapsed - PopGrowSeconds) / PopSettleSeconds)));

                target.localScale = Vector3.one * scale;
            }

            target.localScale = Vector3.one;
        }

        // ──────────────────────────────────────────────── 머지: 속성 색 먼지

        private const int DustCount = 14;
        private const float DustSeconds = 0.42f;
        private const float DustMaxDelay = 0.07f;

        /// <summary>
        /// 머지 결과 자리에서 먼지가 터진다. <paramref name="color"/> 는 <b>결과물</b>의
        /// 속성 색이다 — 재료가 아니라 방금 무엇이 됐는지를 보여 주는 연출이다.
        /// </summary>
        public static void PlayMerge(RectTransform dice, Color color)
        {
            if (dice == null)
                return;

            MergeAsync(dice, color).Forget();
        }

        private static async UniTaskVoid MergeAsync(RectTransform dice, Color color)
        {
            if (!TryCreateBurst(dice, "MergeDust", out RectTransform burst, out float diceSize))
                return;

            // 속성 색 그대로는 어두운 것들(얼음 0,0.4,1 · 독 0,0.76,0.03)이 배경에 묻는다.
            // 흰색을 조금 섞어 "먼지" 로 읽히게 한다. 색은 알아볼 만큼 남는다.
            Color dust = Color.Lerp(color, Color.white, 0.3f);

            var puffs = new RectTransform[DustCount];
            var images = new Image[DustCount];
            var directions = new Vector2[DustCount];
            var distances = new float[DustCount];
            var delays = new float[DustCount];

            for (int i = 0; i < DustCount; i++)
            {
                // 균등 분할에 지터를 섞는다. 완전 랜덤이면 한쪽에 뭉쳐 "터졌다" 로 안 보인다.
                float angle = (360f / DustCount) * i + Random.Range(-12f, 12f);
                float rad = angle * Mathf.Deg2Rad;

                directions[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                distances[i] = diceSize * Random.Range(0.42f, 0.78f);
                delays[i] = Random.Range(0f, DustMaxDelay);

                // 0.26~0.42 는 <b>보이는 알갱이</b>가 아니라 그림 전체의 크기다 —
                // Fx_Smoke_0 은 32px 안에 지름 12px 짜리 흰 원이라 실제로는 37%만 보인다.
                images[i] = CreateImage(burst, "Dust", DustSprite, dust,
                    Vector2.one * (diceSize * Random.Range(0.26f, 0.42f)));
                puffs[i] = images[i].rectTransform;
                SetAlpha(images[i], 0f);
            }

            float elapsed = 0f;
            while (elapsed < DustSeconds + DustMaxDelay)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (burst == null)
                    return;

                elapsed += Time.unscaledDeltaTime;

                for (int i = 0; i < DustCount; i++)
                {
                    float t = (elapsed - delays[i]) / DustSeconds;
                    if (t <= 0f)
                        continue;

                    t = Mathf.Clamp01(t);
                    float spread = UIEase.OutCubic(t);

                    Vector2 pos = directions[i] * (distances[i] * spread);
                    pos.y += diceSize * 0.22f * spread; // 살짝 떠오른다 — 가라앉는 먼지는 무겁다
                    puffs[i].anchoredPosition = pos;
                    puffs[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.35f, t);

                    // 또렷하게 머물다 끝에서 급히 사라진다. 선형으로 빼면 처음부터 흐리다.
                    SetAlpha(images[i], Mathf.Clamp01(1f - t * t));
                }
            }

            Object.Destroy(burst.gameObject);
        }

        // ──────────────────────────────────────────────── 진화: 속성 색 번개

        private const int BoltSegments = 9;
        private const int ForkSegments = 3;
        private const int ForkFromSegment = 3;
        private const int SparkCount = 9;

        private const float EvolveSeconds = 0.5f;

        /// <summary>번개가 위에서 다이스까지 뻗어 내려오는 시간. 짧을수록 "내리친다".</summary>
        private const float StrikeSeconds = 0.06f;

        /// <summary>지터를 한 번 다시 뽑는 시점. 번개가 한 번 꿈틀한 것처럼 보인다.</summary>
        private const float ReJitterAt = 0.14f;

        /// <summary>
        /// 다이스 위에서 번개가 내리치고, 맞은 자리에서 섬광과 불티가 튄다.
        /// <paramref name="color"/> 는 진화 <b>결과</b>의 속성 색이다.
        /// </summary>
        public static void PlayEvolve(RectTransform dice, Color color)
        {
            if (dice == null)
                return;

            EvolveAsync(dice, color).Forget();
        }

        private static async UniTaskVoid EvolveAsync(RectTransform dice, Color color)
        {
            if (!TryCreateBurst(dice, "EvolveBolt", out RectTransform burst, out float diceSize))
                return;

            // 속을 거의 흰색으로 두고 겉을 속성 색으로 감싼다. 번개가 "밝다" 로 읽히는 것은
            // 이 두 겹 때문이다 — 한 겹이면 색만 있고 빛나지 않는다.
            Color glowColor = new Color(color.r, color.g, color.b, 0.55f);
            Color coreColor = Color.Lerp(color, Color.white, 0.45f);

            // 맞은 자리의 섬광. 번개 줄기보다 먼저(아래에) 찍어야 줄기가 그 위로 지나간다.
            // 프레임이 스스로 퍼지는 그림이라 스케일을 건드리지 않는다.
            Sprite[] impact = ImpactFrames;
            Image flash = impact.Length > 0
                ? CreateImage(burst, "Flash", impact[0],
                    Color.Lerp(color, Color.white, 0.55f), Vector2.one * (diceSize * 1.15f))
                : null;

            if (flash != null)
                SetAlpha(flash, 0f);

            var bolt = new GameObject("Bolt", typeof(RectTransform)).GetComponent<RectTransform>();
            bolt.SetParent(burst, false);
            bolt.anchorMin = bolt.anchorMax = bolt.pivot = new Vector2(0.5f, 0.5f);
            bolt.sizeDelta = Vector2.zero;
            CanvasGroup boltGroup = bolt.gameObject.AddComponent<CanvasGroup>();

            int segments = BoltSegments + ForkSegments;
            var glow = new RectTransform[segments];
            var core = new RectTransform[segments];

            // 겉(glow)을 전부 먼저 찍어야 속(core)이 그 위에 온다 — uGUI 는 형제 순서가 곧 층이다.
            // 꺼진 채로 태어난다. 켜 두면 아래 루프가 첫 프레임을 돌기 전에 <b>줄기 전체가
            // 한 프레임 번쩍</b>해서 "내리친다" 가 아니라 "이미 와 있다" 로 보인다.
            for (int i = 0; i < segments; i++)
            {
                glow[i] = CreateImage(bolt, "Glow", null, glowColor, Vector2.zero).rectTransform;
                glow[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < segments; i++)
            {
                core[i] = CreateImage(bolt, "Core", null, coreColor, Vector2.zero).rectTransform;
                core[i].gameObject.SetActive(false);
            }

            LayoutBolt(glow, core, diceSize);

            // 불티. 섬광 위로 튄다.
            var sparks = new RectTransform[SparkCount];
            var sparkImages = new Image[SparkCount];
            var sparkDirs = new Vector2[SparkCount];
            var sparkDistances = new float[SparkCount];

            for (int i = 0; i < SparkCount; i++)
            {
                // 위쪽 반구로만 튄다. 아래로도 뿌리면 맞은 자리가 아니라 터진 자리처럼 보인다.
                float rad = Random.Range(25f, 155f) * Mathf.Deg2Rad;
                sparkDirs[i] = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                sparkDistances[i] = diceSize * Random.Range(0.5f, 1.1f);

                sparkImages[i] = CreateImage(burst, "Spark", DustSprite, coreColor,
                    Vector2.one * (diceSize * Random.Range(0.05f, 0.1f)));
                sparks[i] = sparkImages[i].rectTransform;
                SetAlpha(sparkImages[i], 0f);
            }

            bool reJittered = false;
            float elapsed = 0f;

            while (elapsed < EvolveSeconds)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (burst == null)
                    return;

                elapsed += Time.unscaledDeltaTime;

                // 1) 위에서 아래로 뻗는다. 마디를 순서대로 켜는 것만으로 방향이 읽힌다.
                int visible = Mathf.Clamp(
                    Mathf.CeilToInt(elapsed / StrikeSeconds * BoltSegments), 0, BoltSegments);

                for (int i = 0; i < segments; i++)
                {
                    // 갈래는 본줄기가 그 마디를 지난 뒤에 돋는다.
                    bool on = i < BoltSegments ? i < visible : visible > ForkFromSegment;
                    if (glow[i].gameObject.activeSelf != on)
                    {
                        glow[i].gameObject.SetActive(on);
                        core[i].gameObject.SetActive(on);
                    }
                }

                if (!reJittered && elapsed >= ReJitterAt)
                {
                    reJittered = true;
                    LayoutBolt(glow, core, diceSize);
                }

                // 2) 깜빡이다 사라진다.
                boltGroup.alpha = BoltAlpha(elapsed);

                // 3) 번개가 닿은 뒤의 섬광과 불티.
                float impactT = Mathf.Clamp01((elapsed - StrikeSeconds) / 0.24f);
                if (flash != null && impactT > 0f)
                {
                    flash.sprite = impact[Mathf.Min(impact.Length - 1, (int)(impactT * impact.Length))];

                    // 마지막 프레임만 빼서 끈다. 처음부터 흐리게 빼면 "맞았다" 가 약해진다.
                    SetAlpha(flash, impactT < 0.75f ? 1f : 1f - (impactT - 0.75f) / 0.25f);
                }

                float sparkT = Mathf.Clamp01((elapsed - StrikeSeconds) / 0.4f);
                if (sparkT > 0f)
                {
                    for (int i = 0; i < SparkCount; i++)
                    {
                        Vector2 pos = sparkDirs[i] * (sparkDistances[i] * UIEase.OutCubic(sparkT));
                        pos.y -= diceSize * 1.1f * sparkT * sparkT; // 중력
                        sparks[i].anchoredPosition = pos;
                        SetAlpha(sparkImages[i], Mathf.Clamp01(1f - sparkT * sparkT));
                    }
                }
            }

            Object.Destroy(burst.gameObject);
        }

        /// <summary>
        /// 번개 줄기의 알파. <b>완전히 꺼지는 구간을 두지 않는다</b> — 0 이 되면 번개가
        /// 끊겨 보이고, 그건 깜빡임이 아니라 사라진 것으로 읽힌다.
        /// </summary>
        private static float BoltAlpha(float t)
        {
            const float Hold = 0.2f;
            const float Fade = 0.16f;

            if (t < Hold)
            {
                bool blink = (t > 0.075f && t < 0.1f) || (t > 0.135f && t < 0.155f);
                return blink ? 0.45f : 1f;
            }

            return Mathf.Clamp01(1f - (t - Hold) / Fade);
        }

        /// <summary>
        /// 번개 줄기를 다시 뽑아 마디를 놓는다. 같은 <c>Image</c> 들을 재배치하므로
        /// 연출 도중에 다시 불러 "꿈틀" 을 만들 수 있다.
        ///
        /// 본줄기는 위쪽의 아무 데서 시작해 <b>다이스 중심에 꽂힌다</b> — 아래로 갈수록
        /// 지터를 줄여서, 맞은 자리가 흔들리지 않게 한다.
        /// </summary>
        private static void LayoutBolt(RectTransform[] glow, RectTransform[] core, float diceSize)
        {
            float top = diceSize * 2.6f;
            var points = new Vector2[BoltSegments + 1];

            points[0] = new Vector2(Random.Range(-0.2f, 0.2f) * diceSize, top);
            points[BoltSegments] = Vector2.zero;

            // <b>좌우를 번갈아 꺾는다.</b> 마디마다 독립 난수를 쓰면 같은 쪽으로 두 번
            // 꺾이는 일이 잦아 지그재그가 아니라 비스듬한 금처럼 보인다 — 실제로 그랬다.
            float side = Random.value < 0.5f ? -1f : 1f;

            for (int i = 1; i < BoltSegments; i++)
            {
                float k = (float)i / BoltSegments;

                // 아래로 갈수록 폭을 줄여 다이스 중심에 꽂는다.
                float sway = diceSize * Random.Range(0.12f, 0.3f) * (1f - k);
                points[i] = new Vector2(
                    Mathf.Lerp(points[0].x, 0f, k) + side * sway,
                    Mathf.Lerp(top, 0f, k));

                side = -side;
            }

            float coreWidth = diceSize * 0.07f;
            float glowWidth = diceSize * 0.22f;

            for (int i = 0; i < BoltSegments; i++)
            {
                PlaceSegment(glow[i], points[i], points[i + 1], glowWidth);
                PlaceSegment(core[i], points[i], points[i + 1], coreWidth);
            }

            // 갈래 하나. 본줄기 중간에서 옆으로 빠져 허공에서 끝난다.
            Vector2 branch = points[ForkFromSegment];
            float forkSide = Random.value < 0.5f ? -1f : 1f;
            Vector2 previous = branch;

            for (int i = 0; i < ForkSegments; i++)
            {
                float k = (float)(i + 1) / ForkSegments;
                Vector2 next = branch + new Vector2(
                    forkSide * diceSize * 0.5f * k + Random.Range(-0.06f, 0.06f) * diceSize,
                    -diceSize * 0.7f * k + Random.Range(-0.06f, 0.06f) * diceSize);

                PlaceSegment(glow[BoltSegments + i], previous, next, glowWidth * 0.6f);
                PlaceSegment(core[BoltSegments + i], previous, next, coreWidth * 0.6f);
                previous = next;
            }
        }

        /// <summary>
        /// 두 점을 잇는 막대 하나. 세로 사각형을 만들어 <c>a→b</c> 방향으로 돌린다.
        /// 길이에 <paramref name="width"/> 를 더하는 것은 <b>이음새</b> 때문이다 —
        /// 딱 맞게 자르면 꺾이는 자리마다 틈이 보인다.
        /// </summary>
        private static void PlaceSegment(RectTransform segment, Vector2 a, Vector2 b, float width)
        {
            Vector2 delta = b - a;

            segment.anchoredPosition = (a + b) * 0.5f;
            segment.sizeDelta = new Vector2(width, delta.magnitude + width);
            segment.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, delta));
        }

        // ──────────────────────────────────────────────── 연출 캔버스

        /// <summary>
        /// 연출이 올라갈 자리. <b>씬이 내려가면 같이 죽고</b>, 그때 이 참조는 Unity 의
        /// null 로 떨어지므로 다음 연출이 다시 만든다.
        /// </summary>
        private static RectTransform fxRoot;

        private const string DustSpritePath = "Art/Fx/Fx_Smoke_0";

        private static Sprite dustSprite;
        private static bool dustSpriteLoaded;

        /// <summary>
        /// 먼지·불티·섬광에 쓰는 흰 원. 흰색이라 <c>Image.color</c> 로 속성 색을 그대로 입는다.
        ///
        /// 못 찾으면 <c>null</c> 로 돈다 — <c>Image</c> 는 스프라이트가 없으면 네모를 그리므로
        /// 연출이 죽지는 않는다. 다만 <b>조용히</b> 네모가 되는 것은 막는다(AGENTS "조용한 폴백").
        /// </summary>
        private static Sprite DustSprite
        {
            get
            {
                if (!dustSpriteLoaded)
                {
                    dustSpriteLoaded = true;
                    dustSprite = Resources.Load<Sprite>(DustSpritePath);

                    if (dustSprite == null)
                        Debug.LogWarning($"[연출] 먼지 스프라이트가 없다: Resources/{DustSpritePath}. 네모 입자로 돈다.");
                }

                return dustSprite;
            }
        }

        private const string ImpactFramePathPrefix = "Art/Fx/Fx_Normal_";

        private static Sprite[] impactFrames;

        /// <summary>
        /// 번개가 맞은 자리의 섬광. <b>몬스터 피격에 쓰는 그 그림이다</b>(<c>Fx_Normal_*</c>) —
        /// 흰 테가 퍼지는 5컷이라 속성 색을 입히면 그대로 속성 섬광이 된다.
        ///
        /// 새 그림을 그리지 않는 이유는 <b>톤</b> 때문이다. 이 프로젝트의 이펙트는 전부
        /// 안티에일리어싱 없는 픽셀 그림이고(<c>filterMode: Point</c>), 코드로 만든
        /// 매끈한 원을 섞으면 그 한 장만 다른 게임에서 온 것처럼 보인다.
        /// </summary>
        private static Sprite[] ImpactFrames
        {
            get
            {
                if (impactFrames != null)
                    return impactFrames;

                var frames = new List<Sprite>(5);
                for (int i = 0; ; i++)
                {
                    Sprite frame = Resources.Load<Sprite>(ImpactFramePathPrefix + i);
                    if (frame == null)
                        break;

                    frames.Add(frame);
                }

                if (frames.Count == 0)
                    Debug.LogWarning($"[연출] 피격 섬광이 없다: Resources/{ImpactFramePathPrefix}0. 번개만 친다.");

                impactFrames = frames.ToArray();
                return impactFrames;
            }
        }

        /// <summary>
        /// 다이스 자리에 연출 컨테이너를 만든다. <paramref name="diceSize"/> 는 다이스의
        /// <b>연출 캔버스 단위</b> 크기 — 모든 거리·굵기를 여기에 비례시켜서 해상도나
        /// 보드 칸 크기가 바뀌어도 따라온다.
        /// </summary>
        private static bool TryCreateBurst(RectTransform dice, string name, out RectTransform burst, out float diceSize)
        {
            burst = null;
            diceSize = 0f;

            RectTransform root = EnsureFxRoot();
            if (root == null)
                return false;

            Canvas diceCanvas = dice.GetComponentInParent<Canvas>();
            Camera diceCamera = EventCameraOf(diceCanvas);
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                diceCamera, dice.TransformPoint(dice.rect.center));

            // 연출 캔버스는 Overlay 라 카메라가 없다.
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    root, screenPoint, null, out Vector2 local))
            {
                return false;
            }

            // 다이스의 화면 픽셀 크기를 연출 캔버스 단위로 되돌린다.
            //
            // <b>크기를 <c>rect * lossyScale</c> 로 구하면 안 된다.</b> 보드 캔버스는
            // ScreenSpaceCamera 라 그 <c>lossyScale</c> 이 <b>월드 단위</b>(1920px ≈ 10유닛,
            // 즉 0.005배)이고, 연출 캔버스는 Overlay 라 <b>픽셀 배율</b>(1.0)이다. 둘을 나누면
            // 170px 짜리 다이스가 <b>0.9px</b> 로 나와 연출이 통째로 안 보인다 — 실제로 그랬다.
            // 그래서 위치와 같은 길(화면 좌표)로 재서 단위를 맞춘다.
            Vector3[] corners = new Vector3[4];
            dice.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(diceCamera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(diceCamera, corners[2]);

            float onScreen = Mathf.Min(
                Mathf.Abs(topRight.x - bottomLeft.x),
                Mathf.Abs(topRight.y - bottomLeft.y));

            // Overlay 캔버스의 lossyScale 은 곧 scaleFactor 다 — 캔버스 단위 → 화면 픽셀.
            float rootScale = root.lossyScale.x;
            diceSize = rootScale > 0.0001f ? onScreen / rootScale : onScreen;

            // 1px 짜리 연출은 보이지 않는다. 오브젝트를 찍기 전에 돌아간다.
            if (diceSize <= 1f)
                return false;

            var go = new GameObject(name, typeof(RectTransform));
            burst = (RectTransform)go.transform;
            burst.SetParent(root, false);
            burst.anchorMin = burst.anchorMax = burst.pivot = new Vector2(0.5f, 0.5f);
            burst.sizeDelta = Vector2.zero;
            burst.anchoredPosition = local;

            return true;
        }

        /// <summary>
        /// 연출 전용 Overlay 캔버스.
        ///
        /// <b>왜 보드 캔버스에 얹지 않나.</b> 진화는 상세창이 <b>열린 채로</b> 일어난다
        /// (<c>UIBattleDiceDetailPanel.OnClickEvolve</c> 는 창을 안 닫는다). 그 창은
        /// <c>UIService</c> 가 만든 Overlay 캔버스에 있고, Overlay 는 ScreenSpaceCamera 인
        /// 보드 캔버스보다 <b>정렬 순서와 무관하게 항상 위에</b> 그려진다 — 보드 쪽에 얹으면
        /// 번개가 카드 뒤로 숨는다. 그래서 같은 Overlay 로 올라가 팝업 위에 선다.
        ///
        /// 페이드 띠(<c>FadeView.SortingOrder</c> 이상)보다는 아래다. 씬 전환과 개발용
        /// 치트는 연출을 덮어야 한다.
        ///
        /// <b><c>GraphicRaycaster</c> 를 일부러 안 붙인다.</b> 레이캐스터가 없으면 이 캔버스는
        /// 입력에 통째로 투명해서, 연출이 보드 클릭을 먹는 사고가 구조로 막힌다.
        /// (입자마다 <c>raycastTarget = false</c> 인 것은 두 번째 자물쇠다.)
        /// </summary>
        private static RectTransform EnsureFxRoot()
        {
            if (fxRoot != null)
                return fxRoot;

            var go = new GameObject("DiceFxRoot", typeof(RectTransform));

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = FadeView.SortingOrder - 1;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            fxRoot = (RectTransform)go.transform;
            return fxRoot;
        }

        private static Camera EventCameraOf(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.worldCamera;
        }

        private static Image CreateImage(RectTransform parent, string name, Sprite sprite, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;

            return image;
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
