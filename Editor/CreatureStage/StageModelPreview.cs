using System;
using System.Linq;
using AIBuilder.CreatureStage;
using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools.CreatureStage
{
    // 独立预览场景中的模型副本，源模型和业务场景不参与求值。
    public sealed class StageModelPreview : IDisposable
    {
        private PreviewRenderUtility preview;
        private GameObject instance;
        private StagePoseSession session;
        private Bounds bounds;
        public double Duration { get; private set; }

        public StageModelPreview(GameObject model, AnimationClip clip)
        {
            if (model == null || clip == null) throw new ArgumentException("需要模型和动画。");
            if (!EditorUtility.IsPersistent(model)) throw new ArgumentException("请选择 Project 中的源模型。");
            if (model.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("基础预览只接受不带业务脚本的模型，避免实例化触发副作用。");
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(model)) as ModelImporter;
            if (importer == null || importer.animationType != ModelImporterAnimationType.Generic || importer.optimizeGameObjects)
                throw new InvalidOperationException("当前预览需要未优化骨架的 Generic 模型 FBX。");
            try
            {
                preview = new PreviewRenderUtility();
                instance = UnityEngine.Object.Instantiate(model);
                instance.hideFlags = HideFlags.HideAndDontSave;
                preview.AddSingleGO(instance);
                instance.SetActive(true);
                var animator = instance.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = instance.AddComponent<Animator>();
                    animator.avatar = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(model)).OfType<Avatar>().FirstOrDefault();
                }
                animator.enabled = true;
                session = new StagePoseSession(animator, clip);
                Duration = clip.length;
                session.Evaluate(0);
                var renderers = instance.GetComponentsInChildren<Renderer>();
                bounds = renderers.Length == 0 ? new Bounds(Vector3.zero, Vector3.one) : renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                preview.camera.fieldOfView = 35;
                preview.camera.nearClipPlane = 0.001f;
                preview.camera.farClipPlane = Mathf.Max(100, bounds.size.magnitude * 20);
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35, 30, 0);
                preview.lights[1].intensity = 0.7f;
                preview.lights[1].transform.rotation = Quaternion.Euler(330, 220, 0);
                preview.ambientColor = new Color(0.35f, 0.35f, 0.35f);
            }
            catch { Dispose(); throw; }
        }

        public void Draw(Rect rect, double sourceTime, float yaw)
        {
            if (preview == null || session == null) return;
            session.Evaluate(sourceTime);
            if (Event.current.type != EventType.Repaint) return;
            PositionCamera(yaw);
            preview.BeginPreview(rect, GUIStyle.none);
            preview.Render(true);
            var texture = preview.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        public Texture2D RenderStill(double sourceTime, float yaw, int width, int height)
        {
            if (preview == null || session == null) throw new ObjectDisposedException(nameof(StageModelPreview));
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            session.Evaluate(sourceTime);
            PositionCamera(yaw);
            preview.BeginStaticPreview(new Rect(0, 0, width, height));
            preview.Render(true);
            return preview.EndStaticPreview();
        }

        private void PositionCamera(float yaw)
        {
            var radius = Mathf.Max(0.1f, bounds.extents.magnitude);
            var rotation = Quaternion.Euler(10, yaw, 0);
            preview.camera.transform.SetPositionAndRotation(bounds.center - rotation * Vector3.forward * radius * 3.8f, rotation);
        }

        public void Dispose()
        {
            try { session?.Dispose(); }
            finally
            {
                session = null;
                preview?.Cleanup();
                preview = null;
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                instance = null;
            }
        }
    }
}
