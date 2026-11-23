using UnityEngine;
using UnityEngine.UI;
using Pinball;

namespace OJ.Pinball
{
    /// <summary>
    /// 발사대 연출. 쏠 때마다 스프링이 눌렸다 튕겨 오르며 흔들리고, 숨어 있던 공이 스프링 윗면에 나타나는
    /// 순간(<see cref="PinballBoardView.BallRevealed"/>) 번쩍임과 불꽃이 인다.
    ///
    /// 순수한 연출이다 — 칸·지급·궤적과 무관하다. 연속 발사(꾹 누르기)로 다시 불리면 지금 눌린 정도에서 이어서 눌린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PinballSpringLauncher : MonoBehaviour
    {
        [Tooltip("공이 나타나는 순간을 받는다. 비우면 불꽃 없이 스프링만 움직인다.")]
        [SerializeField] private PinballBoardView boardView;

        [Tooltip("스프링 그림. 피벗이 보이는 아래 끝 가운데에 있어야 한다 — 위가 늘고 줄며 아래는 제자리다.")]
        [SerializeField] private RectTransform spring;

        [Tooltip("스프링 보이는 부분의 높이(px). 피벗에서 윗면까지다.")]
        [SerializeField] private float springHeight = 188f;

        [Tooltip("스프링 윗면에서 위로 뻗는 번쩍임. 피벗은 아래 가운데.")]
        [SerializeField] private RectTransform flash;

        [Tooltip("불꽃 조각 풀. 평소엔 꺼져 있다.")]
        [SerializeField] private RectTransform[] sparks;

        [Header("스프링")]
        [Tooltip("눌렸을 때의 세로 배율.")]
        [SerializeField, Range(0.2f, 1f)] private float compressedScale = 0.5f;

        [Tooltip("눌렸을 때 가로가 퍼지는 정도(줄어든 만큼의 비율).")]
        [SerializeField, Range(0f, 1f)] private float bulge = 0.35f;

        [Tooltip("눌리는 데 걸리는 시간(초).")]
        [SerializeField] private float compressTime = 0.05f;

        [Tooltip("튕긴 뒤 초당 흔들리는 횟수.")]
        [SerializeField] private float recoilHz = 4.5f;

        [Tooltip("클수록 빨리 가라앉는다.")]
        [SerializeField] private float recoilDamping = 8f;

        [Tooltip("튕긴 뒤 가라앉기까지의 시간(초).")]
        [SerializeField] private float recoilTime = 0.55f;

        [Header("번쩍임·불꽃")]
        [SerializeField] private float flashTime = 0.22f;
        [SerializeField, Range(0f, 1f)] private float flashAlpha = 0.6f;
        [SerializeField] private float sparkUpMin = 380f;
        [SerializeField] private float sparkUpMax = 900f;
        [SerializeField] private float sparkSpread = 170f;
        [SerializeField] private float sparkGravity = 1500f;
        [SerializeField] private Vector2 sparkLife = new Vector2(0.3f, 0.55f);

        private Vector2 springBase;

        // 눌림 진행. 음수면 쉬는 중.
        private float time = -1f;
        private float fromScale = 1f;

        private Image flashImage;
        private float flashAge = -1f;

        private Image[] sparkImages;
        private Vector2[] sparkPos;
        private Vector2[] sparkVel;
        private float[] sparkAge;
        private float[] sparkLifeSec;

        private void Awake()
        {
            if (spring != null) springBase = spring.anchoredPosition;

            if (flash != null) flashImage = flash.GetComponent<Image>();

            int count = sparks != null ? sparks.Length : 0;
            sparkImages = new Image[count];
            sparkPos = new Vector2[count];
            sparkVel = new Vector2[count];
            sparkAge = new float[count];
            sparkLifeSec = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (sparks[i] != null) sparkImages[i] = sparks[i].GetComponent<Image>();
            }
        }

        private void OnEnable()
        {
            if (boardView != null) boardView.BallRevealed += Burst;
        }

        private void OnDisable()
        {
            if (boardView != null) boardView.BallRevealed -= Burst;
            Rest();
        }

        /// <summary>한 발 쏠 때 부른다. 스프링이 눌렸다 튕긴다.</summary>
        public void Fire()
        {
            if (spring == null) return;

            fromScale = spring.localScale.y;
            time = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (time >= 0f)
            {
                time += dt;
                if (time >= compressTime + recoilTime)
                {
                    time = -1f;
                    ApplySpring(1f);
                }
                else
                {
                    ApplySpring(SpringScale(time));
                }
            }

            TickFlash(dt);
            TickSparks(dt);
        }

        private float SpringScale(float t)
        {
            if (t < compressTime)
            {
                float u = t / Mathf.Max(0.0001f, compressTime);
                return Mathf.Lerp(fromScale, compressedScale, 1f - (1f - u) * (1f - u));
            }

            // 풀리면서 위로 넘쳤다가 감쇠하며 제자리로 돌아온다.
            float tau = t - compressTime;
            float swing = Mathf.Exp(-recoilDamping * tau) * Mathf.Cos(2f * Mathf.PI * recoilHz * tau);
            return 1f - (1f - compressedScale) * swing;
        }

        private void ApplySpring(float scaleY)
        {
            if (spring == null) return;

            spring.localScale = new Vector3(1f + (1f - scaleY) * bulge, scaleY, 1f);
        }

        // 지금 눌린 정도를 따라간 스프링 윗면.
        private Vector2 TopPoint()
        {
            return springBase + new Vector2(0f, springHeight * (spring != null ? spring.localScale.y : 1f));
        }

        private void Burst()
        {
            Vector2 top = TopPoint();

            if (flash != null)
            {
                flash.anchoredPosition = top;
                flash.localScale = Vector3.one;
                SetAlpha(flashImage, flashAlpha);
                flash.gameObject.SetActive(true);
                flashAge = 0f;
            }

            if (sparks == null) return;

            for (int i = 0; i < sparks.Length; i++)
            {
                if (sparks[i] == null) continue;

                sparkPos[i] = top + new Vector2(Random.Range(-10f, 10f), 0f);
                sparkVel[i] = new Vector2(Random.Range(-sparkSpread, sparkSpread),
                                          Random.Range(sparkUpMin, sparkUpMax));
                sparkAge[i] = 0f;
                sparkLifeSec[i] = Random.Range(sparkLife.x, sparkLife.y);
                sparks[i].anchoredPosition = sparkPos[i];
                sparks[i].localScale = Vector3.one;
                sparks[i].gameObject.SetActive(true);
            }
        }

        private void TickFlash(float dt)
        {
            if (flashAge < 0f || flash == null) return;

            flashAge += dt;
            float p = flashAge / Mathf.Max(0.0001f, flashTime);
            if (p >= 1f)
            {
                flashAge = -1f;
                flash.gameObject.SetActive(false);
                return;
            }

            // 가늘어지며 길게 뻗다가 사라진다.
            flash.localScale = new Vector3(1f - p, 1f + p * 0.6f, 1f);
            SetAlpha(flashImage, (1f - p) * flashAlpha);
        }

        private void TickSparks(float dt)
        {
            if (sparks == null) return;

            for (int i = 0; i < sparks.Length; i++)
            {
                if (sparks[i] == null || sparkLifeSec[i] <= 0f) continue;

                sparkAge[i] += dt;
                float p = sparkAge[i] / sparkLifeSec[i];
                if (p >= 1f)
                {
                    sparkLifeSec[i] = 0f;
                    sparks[i].gameObject.SetActive(false);
                    continue;
                }

                sparkVel[i].y -= sparkGravity * dt;
                sparkPos[i] += sparkVel[i] * dt;
                sparks[i].anchoredPosition = sparkPos[i];
                sparks[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.35f, p);
                SetAlpha(sparkImages[i], 1f - p * p);
            }
        }

        private static void SetAlpha(Image image, float alpha)
        {
            if (image == null) return;

            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }

        /// <summary>창이 꺼지거나 다시 켜질 때 진행 중이던 것을 전부 걷어 낸다.</summary>
        private void Rest()
        {
            time = -1f;
            ApplySpring(1f);

            flashAge = -1f;
            if (flash != null) flash.gameObject.SetActive(false);

            if (sparks == null) return;
            for (int i = 0; i < sparks.Length; i++)
            {
                if (sparkLifeSec != null && i < sparkLifeSec.Length) sparkLifeSec[i] = 0f;
                if (sparks[i] != null) sparks[i].gameObject.SetActive(false);
            }
        }
    }
}
