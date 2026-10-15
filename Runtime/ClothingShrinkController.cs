using UnityEngine;

namespace AIBuilder
{
    public enum ClothingShrinkDirection
    {
        SurfaceNormal = 0,
        LocalAxis = 1
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ClothingShrinkController : MonoBehaviour
    {
        public const string ShaderName = "AIBuilder/URP/Vertex Color Clothing Shrink";

        private static readonly int AmountId = Shader.PropertyToID("_ClothingShrinkAmount");
        private static readonly int ModeId = Shader.PropertyToID("_ClothingShrinkMode");
        private static readonly int AxisId = Shader.PropertyToID("_ClothingShrinkAxisOS");
        private static readonly int CenterId = Shader.PropertyToID("_ClothingShrinkCenterOS");
        private static readonly int DebugMaskId = Shader.PropertyToID("_ClothingShrinkDebugMask");

        [SerializeField, Range(0f, 0.2f), InspectorName("收缩强度（模型单位）")]
        private float amount = 0.01f;

        [SerializeField, InspectorName("收缩方向")]
        private ClothingShrinkDirection direction = ClothingShrinkDirection.SurfaceNormal;

        [SerializeField, InspectorName("局部轴线方向")]
        private Vector3 localAxis = Vector3.up;

        [SerializeField, InspectorName("局部轴线中心")]
        private Vector3 localCenter = Vector3.zero;

        [SerializeField, Range(0f, 1f), InspectorName("显示蒙版")]
        private float debugMask;

        private Renderer targetRenderer;
        private MaterialPropertyBlock propertyBlock;

        public float Amount
        {
            get => amount;
            set
            {
                amount = Mathf.Clamp(value, 0f, 0.2f);
                Apply();
            }
        }

        public ClothingShrinkDirection Direction
        {
            get => direction;
            set
            {
                direction = value;
                Apply();
            }
        }

        public Vector3 LocalAxis
        {
            get => localAxis;
            set
            {
                localAxis = value;
                Apply();
            }
        }

        public Vector3 LocalCenter
        {
            get => localCenter;
            set
            {
                localCenter = value;
                Apply();
            }
        }

        public float DebugMask
        {
            get => debugMask;
            set
            {
                debugMask = Mathf.Clamp01(value);
                Apply();
            }
        }

        public void Apply()
        {
            targetRenderer = targetRenderer != null ? targetRenderer : GetComponent<Renderer>();
            if (targetRenderer == null)
            {
                return;
            }

            propertyBlock = propertyBlock ?? new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(AmountId, amount);
            propertyBlock.SetFloat(ModeId, (float)direction);
            propertyBlock.SetVector(AxisId, NormalizeAxis(localAxis));
            propertyBlock.SetVector(CenterId, localCenter);
            propertyBlock.SetFloat(DebugMaskId, debugMask);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        public static Vector3 NormalizeAxis(Vector3 axis)
        {
            return axis.sqrMagnitude > 0.000001f ? axis.normalized : Vector3.up;
        }

        private void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnValidate()
        {
            amount = Mathf.Clamp(amount, 0f, 0.2f);
            debugMask = Mathf.Clamp01(debugMask);
            Apply();
        }

        private void OnDidApplyAnimationProperties()
        {
            Apply();
        }

        private void OnDisable()
        {
            targetRenderer = targetRenderer != null ? targetRenderer : GetComponent<Renderer>();
            if (targetRenderer == null)
            {
                return;
            }

            propertyBlock = propertyBlock ?? new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(AmountId, 0f);
            propertyBlock.SetFloat(DebugMaskId, 0f);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
