using System.Collections.Generic;
using UnityEngine;

namespace OJ.Utils
{
    [DisallowMultipleComponent]
    public class SimpleSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private List<Sprite> sprites = new List<Sprite>();
        [SerializeField] private float frameInterval = 0.5f;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool loop = true;

        [Header("Scale Wobble")]
        [SerializeField] private bool useScaleWobble = true;
        [SerializeField] private Vector3 scaleMultiplierA = Vector3.one;
        [SerializeField] private Vector3 scaleMultiplierB = new Vector3(1.06f, 0.96f, 1.0f);
        [SerializeField] private float scaleWobbleSeconds = 0.8f;
        [SerializeField] private bool restoreScaleOnStop = true;

        private int frameIndex;
        private float nextFrameTime;
        private float playStartTime;
        private bool isPlaying;
        private bool hasBaseScale;
        private Vector3 baseScale;

        private void Awake()
        {
            ResolveRenderer();
            CaptureBaseScale();
        }

        private void OnEnable()
        {
            CaptureBaseScale();

            if (playOnEnable)
                PlayFromStart();
        }

        private void OnDisable()
        {
            if (restoreScaleOnStop)
                RestoreScale();
        }

        private void Update()
        {
            if (!isPlaying)
                return;

            UpdateScaleWobble();
            UpdateFrame();
        }

        private void UpdateFrame()
        {
            if (targetRenderer == null || sprites == null || sprites.Count == 0)
                return;

            if (Time.time < nextFrameTime)
                return;

            if (frameIndex >= sprites.Count)
                frameIndex = 0;

            targetRenderer.sprite = sprites[frameIndex];
            frameIndex++;

            if (frameIndex >= sprites.Count)
            {
                if (!loop)
                {
                    isPlaying = false;
                    frameIndex = sprites.Count - 1;
                    if (restoreScaleOnStop)
                        RestoreScale();
                    return;
                }

                frameIndex = 0;
            }

            nextFrameTime = Time.time + Mathf.Max(0.01f, frameInterval);
        }

        public void Play()
        {
            ResolveRenderer();
            CaptureBaseScale();
            isPlaying = true;
            nextFrameTime = 0.0f;
            playStartTime = Time.time;
        }

        public void PlayFromStart()
        {
            frameIndex = 0;
            Play();
        }

        public void Stop()
        {
            isPlaying = false;

            if (restoreScaleOnStop)
                RestoreScale();
        }

        public void SetFrame(int index)
        {
            if (targetRenderer == null || sprites == null || sprites.Count == 0)
                return;

            frameIndex = Mathf.Clamp(index, 0, sprites.Count - 1);
            targetRenderer.sprite = sprites[frameIndex];
        }

        private void OnValidate()
        {
            frameInterval = Mathf.Max(0.01f, frameInterval);
            scaleWobbleSeconds = Mathf.Max(0.01f, scaleWobbleSeconds);
            ResolveRenderer();
        }

        private void ResolveRenderer()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<SpriteRenderer>();
        }

        private void UpdateScaleWobble()
        {
            if (!useScaleWobble)
                return;

            CaptureBaseScale();

            float pingPong = Mathf.PingPong((Time.time - playStartTime) / scaleWobbleSeconds, 1.0f);
            float smoothT = Mathf.SmoothStep(0.0f, 1.0f, pingPong);
            Vector3 multiplier = Vector3.Lerp(scaleMultiplierA, scaleMultiplierB, smoothT);
            transform.localScale = Vector3.Scale(baseScale, multiplier);
        }

        private void CaptureBaseScale()
        {
            if (hasBaseScale)
                return;

            baseScale = transform.localScale;
            hasBaseScale = true;
        }

        public void RestoreScale()
        {
            if (!hasBaseScale)
                return;

            transform.localScale = baseScale;
        }
    }
}
