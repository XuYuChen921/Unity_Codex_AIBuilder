using TMPro;
using UnityEngine;

namespace AIBuilder
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class AssetCatalogLabel : MonoBehaviour
    {
        public Transform target;
        public Camera forcedCamera;
        public float verticalPadding = 0.25f;
        public float screenUpPaddingScale = 0.32f;
        public float distanceScale = 0.012f;
        public float orthographicScale = 0.06f;
        public float minWorldScale = 0.08f;
        public float maxWorldScale = 0.36f;
        public Vector2 labelWidthRange = new Vector2(1.8f, 6f);
        public int maxLineCharacters = 22;

        private TextMeshPro text;
        private TextMesh legacyText;

        private void OnEnable()
        {
            text = GetComponent<TextMeshPro>();
            legacyText = GetComponent<TextMesh>();
            Refresh();
        }

        private void OnValidate()
        {
            verticalPadding = Mathf.Max(0f, verticalPadding);
            screenUpPaddingScale = Mathf.Max(0f, screenUpPaddingScale);
            distanceScale = Mathf.Max(0.001f, distanceScale);
            orthographicScale = Mathf.Max(0.001f, orthographicScale);
            minWorldScale = Mathf.Max(0.01f, minWorldScale);
            maxWorldScale = Mathf.Max(minWorldScale, maxWorldScale);
            labelWidthRange.x = Mathf.Max(0.5f, labelWidthRange.x);
            labelWidthRange.y = Mathf.Max(labelWidthRange.x, labelWidthRange.y);
            maxLineCharacters = Mathf.Max(8, maxLineCharacters);
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        public void Refresh()
        {
            Refresh(forcedCamera != null ? forcedCamera : ResolveViewCamera());
        }

        public void Refresh(Camera viewCamera)
        {
            if (target == null || !TryCalculateWorldBounds(target, out var bounds))
            {
                return;
            }

            if (text == null)
            {
                text = GetComponent<TextMeshPro>();
            }

            if (legacyText == null)
            {
                legacyText = GetComponent<TextMesh>();
            }

            var maxDimension = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            maxDimension = Mathf.Max(0.1f, maxDimension);

            if (viewCamera != null)
            {
                var viewUp = viewCamera.transform.up;
                var viewForward = viewCamera.transform.forward;
                var projectedHalfHeight = CalculateProjectedHalfExtent(bounds, viewUp);
                var projectedHalfDepth = CalculateProjectedHalfExtent(bounds, viewForward);
                var screenPadding = verticalPadding + maxDimension * screenUpPaddingScale;
                transform.position = bounds.center +
                                     viewUp * (projectedHalfHeight + screenPadding) -
                                     viewForward * (projectedHalfDepth + 0.02f);
            }
            else
            {
                var basePosition = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                transform.position = basePosition + Vector3.up * (verticalPadding + maxDimension * 0.2f);
            }

            if (viewCamera != null)
            {
                var forward = transform.position - viewCamera.transform.position;
                if (forward.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(forward.normalized, viewCamera.transform.up);
                }

                var labelScale = viewCamera.orthographic
                    ? viewCamera.orthographicSize * orthographicScale
                    : Vector3.Distance(viewCamera.transform.position, transform.position) * distanceScale;
                transform.localScale = Vector3.one * Mathf.Clamp(labelScale, minWorldScale, maxWorldScale);
            }
            else
            {
                transform.localScale = Vector3.one * Mathf.Clamp(maxDimension * 0.08f, minWorldScale, maxWorldScale);
            }

            var displayName = FormatDisplayName(target.name, maxLineCharacters);
            if (text != null)
            {
                text.text = displayName;
                text.rectTransform.sizeDelta = new Vector2(
                    Mathf.Clamp(maxDimension * 1.35f, labelWidthRange.x, labelWidthRange.y),
                    1.2f);
            }
            else if (legacyText != null)
            {
                legacyText.text = displayName;
                legacyText.anchor = TextAnchor.MiddleCenter;
                legacyText.alignment = TextAlignment.Center;
                legacyText.fontSize = 56;
                legacyText.characterSize = 0.1f;
                legacyText.color = Color.white;
            }
        }

        public static bool TryCalculateWorldBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
            {
                return false;
            }

            var hasBounds = false;
            var renderers = root.GetComponentsInChildren<Renderer>(false);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (hasBounds)
            {
                return true;
            }

            var colliders = root.GetComponentsInChildren<Collider>(false);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (hasBounds)
            {
                return true;
            }

            var filters = root.GetComponentsInChildren<MeshFilter>(false);
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters[i];
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                {
                    continue;
                }

                var worldBounds = TransformBounds(mesh.bounds, filter.transform);
                if (!hasBounds)
                {
                    bounds = worldBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(worldBounds);
                }
            }

            if (!hasBounds)
            {
                bounds = new Bounds(root.position, Vector3.one * 0.1f);
            }

            return true;
        }

        private static Camera ResolveViewCamera()
        {
            if (Camera.current != null)
            {
                return Camera.current;
            }

            return Camera.main;
        }

        private static float CalculateProjectedHalfExtent(Bounds bounds, Vector3 axis)
        {
            axis.Normalize();
            var extents = bounds.extents;
            return Mathf.Abs(axis.x) * extents.x +
                   Mathf.Abs(axis.y) * extents.y +
                   Mathf.Abs(axis.z) * extents.z;
        }

        private static string FormatDisplayName(string value, int maxLineLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLineLength)
            {
                return value;
            }

            var midpoint = value.Length / 2;
            var bestIndex = -1;
            var bestDistance = int.MaxValue;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (character != '_' && character != '-' && character != ' ')
                {
                    continue;
                }

                var distance = Mathf.Abs(i - midpoint);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex > 0 && bestIndex < value.Length - 1)
            {
                return value.Substring(0, bestIndex + 1) + "\n" + value.Substring(bestIndex + 1);
            }

            return value.Substring(0, midpoint) + "\n" + value.Substring(midpoint);
        }

        private static Bounds TransformBounds(Bounds localBounds, Transform transform)
        {
            var center = transform.TransformPoint(localBounds.center);
            var extents = localBounds.extents;
            var axisX = transform.TransformVector(extents.x, 0f, 0f);
            var axisY = transform.TransformVector(0f, extents.y, 0f);
            var axisZ = transform.TransformVector(0f, 0f, extents.z);
            extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
            extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
            extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);
            return new Bounds(center, extents * 2f);
        }
    }
}
