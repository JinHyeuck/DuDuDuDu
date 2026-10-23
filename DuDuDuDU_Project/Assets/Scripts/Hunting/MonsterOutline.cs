using UnityEngine;

namespace OJ.Hunting
{
    /// <summary>
    /// 보스·현상금 몬스터에 두르는 <b>빨간 테두리</b>. 일반 몬스터와 한눈에 갈리게 한다.
    ///
    /// <b>방식.</b> 몸 스프라이트를 같은 모양 그대로 여덟 방향으로 조금씩 비껴 붉은 한 가지
    /// 색으로 칠해 몸 뒤에 깐다. 몸이 가린 나머지가 지워지고 가장자리만 남아 테두리가 된다.
    /// 스프라이트 피벗이 어디든, 애니메이션이 프레임을 갈아 끼우든 따라간다 — 매 프레임
    /// 몸의 스프라이트를 그대로 베끼기 때문이다.
    ///
    /// <b>켜고 끄는 기준은 몬스터의 역할(<c>IsBounty</c>·<c>IsStageBoss</c>)이다.</b> 몬스터는
    /// 풀에서 재사용되므로 지난번에 보스였던 몸이 이번엔 일반으로 나올 수 있다.
    /// 그래서 한 번 만든 뒤에는 매 프레임 역할을 읽어 보였다 감췄다 한다 — 역할이 켜지고
    /// 꺼지는 자리(<c>OnSpawn</c>·<c>OnDisable</c>)마다 따로 적으면 하나를 빠뜨렸을 때
    /// 테두리가 일반 몬스터에 남는다.
    /// </summary>
    internal sealed class MonsterOutline : MonoBehaviour
    {
        private const string ShaderResourcePath = "Shaders/OJSpriteFlat";

        /// <summary>
        /// 테두리 두께. 스프라이트 긴 변의 비율이라 몬스터 크기(보스는 더 큼)가 달라도
        /// 같은 비율로 보인다.
        /// </summary>
        private const float ThicknessRatio = 0.045f;

        private static readonly Color OutlineColor = new Color(1f, 0.1f, 0.08f, 1f);

        private static readonly Vector2[] Directions =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
            new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
        };

        private static Material sharedMaterial;
        private static bool materialMissingLogged;

        private Monster owner;
        private SpriteRenderer body;
        private SpriteRenderer[] copies;
        private bool shown;

        /// <summary>
        /// 몬스터에 테두리를 붙인다. 이미 있으면 그대로 둔다.
        /// 역할 표시(<c>ConfigureAs*</c>)가 켜지는 순간에 부르면 되고, 일반 몬스터에는 만들어지지 않는다.
        /// </summary>
        public static void Attach(Monster monster, SpriteRenderer bodyRenderer)
        {
            if (monster == null || bodyRenderer == null)
                return;

            if (monster.TryGetComponent(out MonsterOutline existing))
            {
                existing.Refresh();
                return;
            }

            Material material = GetMaterial();
            if (material == null)
                return;

            var outline = monster.gameObject.AddComponent<MonsterOutline>();
            outline.Build(monster, bodyRenderer, material);
        }

        private static Material GetMaterial()
        {
            if (sharedMaterial != null)
                return sharedMaterial;

            Shader shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null)
            {
                // 조용히 넘어가지 않는다. 테두리가 안 보이는 증상만으로는 셰이더가 빠졌다는 것을
                // 알 수 없다 — 빌드에서 Resources 가 잘렸거나 경로가 바뀐 경우다.
                if (!materialMissingLogged)
                {
                    materialMissingLogged = true;
                    Debug.LogError("[테두리] 셰이더 Resources/" + ShaderResourcePath + " 를 못 찾았다. " +
                                   "보스·현상금에 빨간 테두리가 그려지지 않는다.");
                }

                return null;
            }

            sharedMaterial = new Material(shader) { name = "MonsterOutline (runtime)" };
            return sharedMaterial;
        }

        private void Build(Monster monster, SpriteRenderer bodyRenderer, Material material)
        {
            owner = monster;
            body = bodyRenderer;
            copies = new SpriteRenderer[Directions.Length];

            for (int i = 0; i < copies.Length; i++)
            {
                var go = new GameObject("BossOutline" + i);
                go.layer = body.gameObject.layer;
                go.transform.SetParent(body.transform, false);

                SpriteRenderer copy = go.AddComponent<SpriteRenderer>();
                copy.sharedMaterial = material;
                copy.color = OutlineColor;
                copy.sortingLayerID = body.sortingLayerID;
                copy.sortingOrder = body.sortingOrder - 1;
                copy.enabled = false;
                copies[i] = copy;
            }

            shown = false;
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (owner == null || body == null || copies == null)
                return;

            Sprite sprite = body.sprite;
            bool wanted = (owner.IsBounty || owner.IsStageBoss) && sprite != null && body.enabled;

            if (!wanted)
            {
                if (shown)
                    SetShown(false);
                return;
            }

            shown = true;

            Vector3 size = sprite.bounds.size;
            float thickness = Mathf.Max(size.x, size.y) * ThicknessRatio;

            for (int i = 0; i < copies.Length; i++)
            {
                SpriteRenderer copy = copies[i];
                copy.sprite = sprite;
                copy.flipX = body.flipX;
                copy.flipY = body.flipY;
                copy.sortingOrder = body.sortingOrder - 1;
                copy.transform.localPosition = new Vector3(
                    Directions[i].x * thickness, Directions[i].y * thickness, 0f);
                copy.enabled = true;
            }
        }

        private void SetShown(bool value)
        {
            shown = value;
            for (int i = 0; i < copies.Length; i++)
                copies[i].enabled = value;
        }
    }
}
