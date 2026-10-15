using System;
using UnityEngine;

namespace AIBuilder.CreatureStage
{
    public enum StageGroundNameMatch { Prefix, Contains, Exact }
    public enum StageGroundScanMode { Name, StaticMeshes }

    [DisallowMultipleComponent, AddComponentMenu("AIBuilder/角色剧情/地面绑定设置")]
    public sealed class StageGroundSettings : MonoBehaviour
    {
        [Tooltip("Search under this root, or the current scene when empty. / 限定查找范围；未指定时查找当前场景。")]
        public Transform groundRoot;
        public string groundName = "Ground";
        public StageGroundNameMatch match = StageGroundNameMatch.Prefix;
        public StageGroundScanMode scanMode;
        public Transform[] excludedRoots = Array.Empty<Transform>();
        public Transform[] includedRoots = Array.Empty<Transform>();
        public string excludedKeywords = "grass,plant,vegetation,foliage,bush,tree,草,灌木,植被,树";
        [Min(0)] public float maxStepUp = .5f;
        [Min(.1f)] public float maxDrop = 100;
        [Range(0, 90)] public float maxSlope = 60;
        [HideInInspector] public Collider[] boundSurfaces = Array.Empty<Collider>();

        // 停留点按已绑定表面投射，未命中时保留原位置。
        public bool TryProject(Vector3 point, out Vector3 result)
        {
            result = point;
            var best = float.PositiveInfinity;
            var up = scanMode == StageGroundScanMode.StaticMeshes ? Mathf.Max(.001f, maxStepUp) : 100f;
            var down = scanMode == StageGroundScanMode.StaticMeshes ? Mathf.Max(.1f, maxDrop) : 100f;
            var ray = new Ray(point + Vector3.up * up, Vector3.down);
            foreach (var surface in boundSurfaces ?? Array.Empty<Collider>())
            {
                if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy) continue;
                var bounds = surface.bounds;
                if (point.x < bounds.min.x || point.x > bounds.max.x || point.z < bounds.min.z || point.z > bounds.max.z) continue;
                if (!surface.Raycast(ray, out var hit, up + down)) continue;
                if (scanMode == StageGroundScanMode.StaticMeshes && Vector3.Angle(hit.normal, Vector3.up) > maxSlope) continue;
                var score = Mathf.Abs(hit.point.y - point.y);
                if (score >= best) continue;
                best = score; result = hit.point;
            }
            return !float.IsPositiveInfinity(best);
        }

        public bool Includes(MeshFilter mesh, out string reason)
        {
            if (mesh == null || mesh.sharedMesh == null) { reason = "No valid mesh / 没有有效 Mesh"; return false; }
            if (mesh.gameObject.scene != gameObject.scene) { reason = "Different scene / 不属于当前场景"; return false; }
            if (Under(mesh.transform, excludedRoots)) { reason = "Explicitly excluded / 明确排除（含子物体）"; return false; }
            if (!mesh.gameObject.activeInHierarchy) { reason = "Inactive object / 物体未启用"; return false; }
            if (Under(mesh.transform, includedRoots)) { reason = "Explicitly included / 强制包含（含子物体）"; return true; }
            if (scanMode == StageGroundScanMode.Name) { var yes = Matches(mesh.name); reason = yes ? "Name matches / 名称匹配" : "Name does not match / 名称不匹配"; return yes; }
            if (!mesh.gameObject.isStatic) { reason = "Not static / 不是静态物体"; return false; }
            var keywords = (excludedKeywords ?? "").Split(new[] { ',', '，', ';', '；', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var node = mesh.transform; node != null; node = node.parent)
            {
                foreach (var keyword in keywords)
                    if (keyword.Trim().Length > 0 && node.name.IndexOf(keyword.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                    { reason = "Excluded keyword:  / 名称排除：" + keyword.Trim(); return false; }
                if (node == groundRoot) break;
            }
            reason = "Static mesh / 静态 Mesh"; return true;
        }

        private static bool Under(Transform value, Transform[] roots)
        {
            if (roots != null) foreach (var root in roots)
                if (root != null && (root == value || value.IsChildOf(root))) return true;
            return false;
        }

        public bool Matches(string objectName)
        {
            var pattern = groundName == null ? "" : groundName.Trim();
            if (pattern.Length == 0 || string.IsNullOrEmpty(objectName)) return false;
            switch (match)
            {
                case StageGroundNameMatch.Prefix: return objectName.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
                case StageGroundNameMatch.Contains: return objectName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
                case StageGroundNameMatch.Exact: return string.Equals(objectName, pattern, StringComparison.OrdinalIgnoreCase);
                default: return false;
            }
        }
    }
}
