using UnityEngine;
using OJ.Core;

namespace OJ.Hunting
{
    /// <summary>
    /// 세로로 긴 화면(22:9 등)에서 전투 화면이 깨지지 않게 카메라와 배경을 맞춘다.
    ///
    /// 전투 UI 는 캔버스가 가로 기준(1080)으로 스케일돼 아래에서 위로 쌓이는데, 카메라가
    /// <c>orthoSize</c> 를 고정하면 월드가 가로로 더 좁게 잘려 확대돼 보이고 배경 그림은 위쪽이 끝난다.
    /// 그래서 월드 한 칸의 픽셀 밀도를 가로 기준으로 고정하고, 아래 끝은 그대로 둔 채
    /// 늘어난 높이만큼 위쪽을 더 보여 준다(<see cref="TallScreenView"/>). 배경은 그 화면을 덮을 만큼 키운다.
    ///
    /// <b>월드를 그리는 카메라와 UI(데미지 숫자)를 그리는 카메라를 같이 맞춘다.</b> 데미지 숫자는
    /// 월드 좌표에 놓이지만 UI 카메라로 그려지므로, 둘의 시야가 다르면 숫자가 몬스터에서 어긋난다.
    ///
    /// 9:16 이거나 가로로 더 넓은 화면에서는 카메라를 건드리지 않는다.
    ///
    /// 로비처럼 월드가 화면 가운데에 놓이고 UI 도 가운데 기준으로 붙어 있는 씬은 <see cref="keepCentered"/> 로
    /// 카메라 중심을 그대로 둔 채 시야만 넓힌다(아래 끝을 고정하면 가운데에 놓인 UI 와 월드가 어긋난다).
    /// </summary>
    public sealed class BattleViewFit : MonoBehaviour
    {
        [Tooltip("월드를 그리는 카메라가 맨 앞이다. 그 뒤에 같은 시야를 써야 하는 카메라(UI)를 둔다.")]
        [SerializeField] private Camera[] cameras;

        [SerializeField] private SpriteRenderer background;

        [Tooltip("켜면 아래 끝 대신 카메라 중심을 고정한다.")]
        [SerializeField] private bool keepCentered;

        private float[] baseOrthoSize;
        private float[] baseCenterY;
        private Vector3 baseBackgroundScale;
        private Vector3 baseBackgroundPosition;
        private int appliedWidth = -1;
        private int appliedHeight = -1;
        private Sprite appliedSprite;

        private void Awake()
        {
            if (cameras == null || cameras.Length == 0)
            {
                enabled = false;
                return;
            }

            baseOrthoSize = new float[cameras.Length];
            baseCenterY = new float[cameras.Length];
            for (int i = 0; i < cameras.Length; i++)
            {
                baseOrthoSize[i] = cameras[i].orthographicSize;
                baseCenterY[i] = cameras[i].transform.position.y;
            }

            if (background != null)
            {
                baseBackgroundScale = background.transform.localScale;
                baseBackgroundPosition = background.transform.position;
            }

            Apply();
        }

        // 화면 크기가 바뀌거나(회전·분할 화면) 테마가 배경 그림을 바꾸면 다시 맞춘다.
        private void LateUpdate()
        {
            bool sizeChanged = Screen.width != appliedWidth || Screen.height != appliedHeight;
            bool spriteChanged = background != null && background.sprite != appliedSprite;
            if (sizeChanged || spriteChanged)
                Apply();
        }

        private void Apply()
        {
            appliedWidth = Screen.width;
            appliedHeight = Screen.height;

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null)
                    continue;

                float ortho = TallScreenView.OrthoSize(appliedWidth, appliedHeight, baseOrthoSize[i]);
                cameras[i].orthographicSize = ortho;

                Vector3 position = cameras[i].transform.position;
                position.y = keepCentered
                    ? baseCenterY[i]
                    : TallScreenView.CenterY(baseCenterY[i], baseOrthoSize[i], ortho);
                cameras[i].transform.position = position;
            }

            FitBackground();
        }

        private void FitBackground()
        {
            appliedSprite = background != null ? background.sprite : null;
            if (appliedSprite == null || cameras[0] == null)
                return;

            Camera world = cameras[0];
            float viewHeight = world.orthographicSize * 2f;
            float viewWidth = viewHeight * appliedWidth / Mathf.Max(1, appliedHeight);

            // 늘리기 전 크기(씬에 놓인 배율)를 기준으로 덮을 배율을 구한다. 이미 늘린 값 위에 또 곱하지 않는다.
            Vector2 spriteSize = appliedSprite.bounds.size;
            float width = spriteSize.x * Mathf.Abs(baseBackgroundScale.x);
            float height = spriteSize.y * Mathf.Abs(baseBackgroundScale.y);
            float cover = TallScreenView.CoverScale(width, height, viewWidth, viewHeight);

            Transform t = background.transform;
            t.localScale = baseBackgroundScale * cover;

            // 카메라가 위로 올라간 만큼 배경도 따라 올려 시야 한가운데에 둔다.
            Vector3 position = baseBackgroundPosition;
            position.y += world.transform.position.y - baseCenterY[0];
            t.position = position;
        }
    }
}
