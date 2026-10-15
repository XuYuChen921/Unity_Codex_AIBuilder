using UnityEngine;

namespace AIBuilder.CreatureStage
{
    // 标识工具创建的路径分组，避免把用户自建层级当作可整理节点。
    [AddComponentMenu("")]
    public sealed class StageStoryPointGroup : MonoBehaviour
    {
        public StagePerformanceTrack track;
    }
}
