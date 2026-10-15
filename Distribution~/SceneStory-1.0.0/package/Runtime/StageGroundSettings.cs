using System;
using UnityEngine;

namespace AIBuilder.CreatureStage
{
    public enum StageGroundNameMatch { Prefix, Contains, Exact }

    [DisallowMultipleComponent, AddComponentMenu("AIBuilder/角色剧情/地面绑定设置")]
    public sealed class StageGroundSettings : MonoBehaviour
    {
        [Tooltip("限定查找范围；未指定时查找当前场景。")]
        public Transform groundRoot;
        public string groundName = "Ground";
        public StageGroundNameMatch match = StageGroundNameMatch.Prefix;

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
