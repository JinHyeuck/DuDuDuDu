using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OJ.Utils;

namespace OJ.Lobby
{
    [ExecuteAlways]
    public class SpritePointLightController : MonoBehaviour
    {
        private static readonly int LightDirId = Shader.PropertyToID("_LightDir");
        private static readonly int LightIntensityId = Shader.PropertyToID("_LightIntensity");
        private static readonly int LightColorId = Shader.PropertyToID("_LightColor");

        [SerializeField] private Transform lightSource;
        [SerializeField] private List<Renderer> targetRenderers = new List<Renderer>();
        [SerializeField] private List<Graphic> targetGraphics = new List<Graphic>();

        [Header("Light Shape")]
        [SerializeField] private float lightHeight = 0.85f;
        [SerializeField] private float radius = 4.0f;
        [SerializeField] private float baseIntensity = 1.15f;
        [SerializeField] private Color lightColor = new Color(1.0f, 0.72f, 0.42f, 1.0f);

        [Header("Fire Flicker")]
        [SerializeField] private bool useFlicker = true;
        [SerializeField] private float flickerSpeed = 5.0f;
        [SerializeField, Range(0.0f, 1.0f)] private float flickerAmount = 0.18f;

        [Header("Direction Wobble")]
        [SerializeField] private bool useDirectionWobble = true;
        [SerializeField] private bool useLocalWobblePoints = true;
        [SerializeField] private Vector3 wobblePointA = new Vector3(-0.08f, 0.04f, 0.0f);
        [SerializeField] private Vector3 wobblePointB = new Vector3(0.08f, -0.03f, 0.0f);
        [SerializeField] private float wobbleSeconds = 1.2f;

        [Header("Height Wobble")]
        [SerializeField] private bool useHeightWobble = true;
        [SerializeField] private float heightWobbleAmount = 0.12f;
        [SerializeField] private float heightWobbleSeconds = 0.9f;

        [Header("Target Search")]
        [SerializeField] private bool collectTargetsInChildren = true;
        [SerializeField] private bool collectSceneTargetsInRadius;
        [SerializeField] private LayerMask targetLayerMask = ~0;
        [SerializeField] private bool includeInactiveTargets;

        private MaterialPropertyBlock propertyBlock;
        private Dictionary<Graphic, Material> graphicMaterialInstances;
        private Dictionary<Graphic, Material> originalGraphicMaterials;

        private void Reset()
        {
            EnsureRuntimeState();
            lightSource = transform;
            RefreshTargets();
        }

        private void OnEnable()
        {
            EnsureRuntimeState();

            if (lightSource == null)
                lightSource = transform;

            RefreshTargetsIfEmpty();
            ApplyLight();
        }

        private void LateUpdate()
        {
            ApplyLight();
        }

        private void OnDisable()
        {
            EnsureRuntimeState();
            RestoreGraphicMaterials();
        }

        private void OnValidate()
        {
            EnsureRuntimeState();

            radius = Mathf.Max(0.01f, radius);
            lightHeight = Mathf.Max(0.01f, lightHeight);
            baseIntensity = Mathf.Max(0.0f, baseIntensity);
            wobbleSeconds = Mathf.Max(0.01f, wobbleSeconds);
            heightWobbleAmount = Mathf.Max(0.0f, heightWobbleAmount);
            heightWobbleSeconds = Mathf.Max(0.01f, heightWobbleSeconds);

            if (!isActiveAndEnabled)
                return;

            RefreshTargetsIfEmpty();
            ApplyLight();
        }

        private void OnDrawGizmosSelected()
        {
            Transform source = lightSource != null ? lightSource : transform;
            Gizmos.color = new Color(lightColor.r, lightColor.g, lightColor.b, 0.35f);
            Gizmos.DrawWireSphere(source.position, radius);
            Gizmos.DrawLine(source.position, source.position + Vector3.forward * lightHeight);

            if (useHeightWobble && heightWobbleAmount > 0.0f)
            {
                Gizmos.color = new Color(1.0f, 0.72f, 0.25f, 0.8f);
                Gizmos.DrawLine(
                    source.position + Vector3.forward * Mathf.Max(0.01f, lightHeight - heightWobbleAmount),
                    source.position + Vector3.forward * (lightHeight + heightWobbleAmount));
            }

            if (useDirectionWobble)
            {
                Vector3 pointA = GetWobblePoint(source, wobblePointA);
                Vector3 pointB = GetWobblePoint(source, wobblePointB);
                Gizmos.color = new Color(1.0f, 0.45f, 0.12f, 0.9f);
                Gizmos.DrawLine(pointA, pointB);
                Gizmos.DrawWireSphere(pointA, 0.04f);
                Gizmos.DrawWireSphere(pointB, 0.04f);
            }
        }

        [ContextMenu("Refresh Targets")]
        public void RefreshTargets()
        {
            EnsureRuntimeState();

            targetRenderers.RemoveAll(renderer => renderer == null);
            targetGraphics.RemoveAll(graphic => graphic == null);

            if (!collectTargetsInChildren)
            {
                if (collectSceneTargetsInRadius)
                    CollectSceneTargetsInRadius();
                return;
            }

            Renderer[] childRenderers = GetComponentsInChildren<Renderer>(includeInactiveTargets);
            for (int i = 0; i < childRenderers.Length; i++)
                AddTarget(childRenderers[i]);

            Graphic[] childGraphics = GetComponentsInChildren<Graphic>(includeInactiveTargets);
            for (int i = 0; i < childGraphics.Length; i++)
                AddTarget(childGraphics[i]);

            if (collectSceneTargetsInRadius)
                CollectSceneTargetsInRadius();
        }

        [ContextMenu("Collect Scene Targets In Radius")]
        public void CollectSceneTargetsInRadius()
        {
            EnsureRuntimeState();

            Renderer[] sceneRenderers = FindObjectsOfType<Renderer>(includeInactiveTargets);
            Vector3 lightPosition = lightSource != null ? lightSource.position : transform.position;

            for (int i = 0; i < sceneRenderers.Length; i++)
            {
                Renderer target = sceneRenderers[i];
                if (target == null)
                    continue;

                if (((1 << target.gameObject.layer) & targetLayerMask.value) == 0)
                    continue;

                float distance = Vector2.Distance(lightPosition, target.bounds.center);
                if (distance > radius)
                    continue;

                AddTarget(target);
            }

            Graphic[] sceneGraphics = FindObjectsOfType<Graphic>(includeInactiveTargets);
            for (int i = 0; i < sceneGraphics.Length; i++)
            {
                Graphic target = sceneGraphics[i];
                if (target == null)
                    continue;

                if (((1 << target.gameObject.layer) & targetLayerMask.value) == 0)
                    continue;

                float distance = Vector2.Distance(lightPosition, target.transform.position);
                if (distance > radius)
                    continue;

                AddTarget(target);
            }
        }

        public void AddTarget(Renderer target)
        {
            EnsureRuntimeState();

            if (target == null || target.transform == lightSource || targetRenderers.Contains(target))
                return;

            targetRenderers.Add(target);
        }

        public void RemoveTarget(Renderer target)
        {
            EnsureRuntimeState();

            if (target == null)
                return;

            targetRenderers.Remove(target);
        }

        public void AddTarget(Graphic target)
        {
            EnsureRuntimeState();

            if (target == null || target.transform == lightSource || targetGraphics.Contains(target))
                return;

            targetGraphics.Add(target);
        }

        public void RemoveTarget(Graphic target)
        {
            EnsureRuntimeState();

            if (target == null)
                return;

            targetGraphics.Remove(target);
            RestoreGraphicMaterial(target);
        }

        private void RefreshTargetsIfEmpty()
        {
            EnsureRuntimeState();

            if (targetRenderers.Count > 0 || targetGraphics.Count > 0)
                return;

            RefreshTargets();
        }

        private void ApplyLight()
        {
            EnsureRuntimeState();

            if (lightSource == null)
                return;

            float flicker = GetFlicker();
            Vector3 lightPosition = GetAnimatedLightPosition();
            float animatedLightHeight = GetAnimatedLightHeight();

            ApplyRendererTargets(lightPosition, flicker, animatedLightHeight);
            ApplyGraphicTargets(lightPosition, flicker, animatedLightHeight);
        }

        private void ApplyRendererTargets(Vector3 lightPosition, float flicker, float animatedLightHeight)
        {
            for (int i = targetRenderers.Count - 1; i >= 0; i--)
            {
                Renderer target = targetRenderers[i];
                if (target == null)
                {
                    targetRenderers.RemoveAt(i);
                    continue;
                }

                if (!includeInactiveTargets && !target.gameObject.activeInHierarchy)
                    continue;

                Vector3 localDirection = CalculateLightDirection(
                    target.transform,
                    target.bounds.center,
                    lightPosition,
                    animatedLightHeight,
                    flicker,
                    out float intensity);

                SpriteRenderer spriteRenderer = target as SpriteRenderer;
                if (spriteRenderer != null)
                {
                    if (spriteRenderer.flipX)
                        localDirection.x = -localDirection.x;
                    if (spriteRenderer.flipY)
                        localDirection.y = -localDirection.y;
                }

                target.GetPropertyBlock(propertyBlock);
                propertyBlock.SetVector(LightDirId, new Vector4(localDirection.x, localDirection.y, localDirection.z, 0.0f));
                propertyBlock.SetFloat(LightIntensityId, intensity);
                propertyBlock.SetColor(LightColorId, lightColor);
                target.SetPropertyBlock(propertyBlock);
            }
        }

        private void ApplyGraphicTargets(Vector3 lightPosition, float flicker, float animatedLightHeight)
        {
            for (int i = targetGraphics.Count - 1; i >= 0; i--)
            {
                Graphic target = targetGraphics[i];
                if (target == null)
                {
                    targetGraphics.RemoveAt(i);
                    continue;
                }

                if (!includeInactiveTargets && !target.gameObject.activeInHierarchy)
                    continue;

                Material material = GetGraphicMaterial(target);
                if (material == null)
                    continue;

                Vector3 localDirection = CalculateLightDirection(
                    target.transform,
                    target.transform.position,
                    lightPosition,
                    animatedLightHeight,
                    flicker,
                    out float intensity);

                material.SetVector(LightDirId, new Vector4(localDirection.x, localDirection.y, localDirection.z, 0.0f));
                material.SetFloat(LightIntensityId, intensity);
                material.SetColor(LightColorId, lightColor);
                target.SetMaterialDirty();
            }
        }

        private Vector3 CalculateLightDirection(
            Transform targetTransform,
            Vector3 targetPosition,
            Vector3 lightPosition,
            float animatedLightHeight,
            float flicker,
            out float intensity)
        {
            Vector3 worldOffset = lightPosition - targetPosition;
            float planarDistance = new Vector2(worldOffset.x, worldOffset.y).magnitude;
            float distance01 = Mathf.Clamp01(planarDistance / radius);
            float falloff = 1.0f - (distance01 * distance01 * (3.0f - 2.0f * distance01));
            intensity = baseIntensity * falloff * flicker;

            Vector3 worldDirection = new Vector3(worldOffset.x, worldOffset.y, animatedLightHeight).normalized;
            return targetTransform.InverseTransformDirection(worldDirection).normalized;
        }

        private float GetFlicker()
        {
            if (!useFlicker || flickerAmount <= 0.0f)
                return 1.0f;

            float time = GetTime();
            float noiseA = Mathf.PerlinNoise(time * flickerSpeed, 0.13f);
            float noiseB = Mathf.PerlinNoise(time * flickerSpeed * 1.7f, 1.91f);
            float noise = (noiseA * 0.7f) + (noiseB * 0.3f);
            return Mathf.Lerp(1.0f - flickerAmount, 1.0f + flickerAmount, noise);
        }

        private Vector3 GetAnimatedLightPosition()
        {
            if (!useDirectionWobble)
                return lightSource.position;

            float pingPong = Mathf.PingPong(GetTime() / wobbleSeconds, 1.0f);
            float smoothT = Mathf.SmoothStep(0.0f, 1.0f, pingPong);
            Vector3 pointA = GetWobblePoint(lightSource, wobblePointA);
            Vector3 pointB = GetWobblePoint(lightSource, wobblePointB);
            return Vector3.Lerp(pointA, pointB, smoothT);
        }

        private Vector3 GetWobblePoint(Transform source, Vector3 point)
        {
            return useLocalWobblePoints ? source.TransformPoint(point) : point;
        }

        private float GetAnimatedLightHeight()
        {
            if (!useHeightWobble || heightWobbleAmount <= 0.0f)
                return lightHeight;

            float pingPong = Mathf.PingPong(GetTime() / heightWobbleSeconds, 1.0f);
            float smoothT = Mathf.SmoothStep(0.0f, 1.0f, pingPong);
            float minHeight = Mathf.Max(0.01f, lightHeight - heightWobbleAmount);
            float maxHeight = lightHeight + heightWobbleAmount;
            return Mathf.Lerp(minHeight, maxHeight, smoothT);
        }

        private Material GetGraphicMaterial(Graphic graphic)
        {
            EnsureRuntimeState();

            if (graphicMaterialInstances.TryGetValue(graphic, out Material material) && material != null)
                return material;

            Material source = graphic.material;
            if (source == null || !source.HasProperty(LightDirId))
                return null;

            Material instance = new Material(source)
            {
                name = $"{source.name} ({nameof(SpritePointLightController)})",
                hideFlags = HideFlags.DontSave
            };

            originalGraphicMaterials[graphic] = source;
            graphicMaterialInstances[graphic] = instance;
            graphic.material = instance;
            return instance;
        }

        private void RestoreGraphicMaterials()
        {
            EnsureRuntimeState();

            foreach (KeyValuePair<Graphic, Material> pair in originalGraphicMaterials)
            {
                if (pair.Key != null)
                    pair.Key.material = pair.Value;
            }

            foreach (KeyValuePair<Graphic, Material> pair in graphicMaterialInstances)
            {
                if (pair.Value == null)
                    continue;

                if (Application.isPlaying)
                    Destroy(pair.Value);
                else
                    DestroyImmediate(pair.Value);
            }

            originalGraphicMaterials.Clear();
            graphicMaterialInstances.Clear();
        }

        private void RestoreGraphicMaterial(Graphic graphic)
        {
            EnsureRuntimeState();

            if (graphic == null)
                return;

            if (originalGraphicMaterials.TryGetValue(graphic, out Material originalMaterial))
                graphic.material = originalMaterial;

            if (graphicMaterialInstances.TryGetValue(graphic, out Material material) && material != null)
            {
                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
            }

            originalGraphicMaterials.Remove(graphic);
            graphicMaterialInstances.Remove(graphic);
        }

        private void EnsureRuntimeState()
        {
            if (targetRenderers == null)
                targetRenderers = new List<Renderer>();
            if (targetGraphics == null)
                targetGraphics = new List<Graphic>();
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
            if (graphicMaterialInstances == null)
                graphicMaterialInstances = new Dictionary<Graphic, Material>();
            if (originalGraphicMaterials == null)
                originalGraphicMaterials = new Dictionary<Graphic, Material>();
        }

        private float GetTime()
        {
            if (Application.isPlaying)
                return Time.time;

#if UNITY_EDITOR
            return (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
            return 0.0f;
#endif
        }
    }
}
