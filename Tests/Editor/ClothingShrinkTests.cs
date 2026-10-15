using AIBuilder;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools.Tests
{
    public sealed class ClothingShrinkTests
    {
        [Test]
        public void WithMask_WritesGrayscaleAndPreservesAlpha()
        {
            Color result = ClothingShrinkPainterUtility.WithMask(new Color(0.2f, 0.4f, 0.8f, 0.35f), 0.7f);

            Assert.That(result.r, Is.EqualTo(0.7f).Within(0.0001f));
            Assert.That(result.g, Is.EqualTo(0.7f).Within(0.0001f));
            Assert.That(result.b, Is.EqualTo(0.7f).Within(0.0001f));
            Assert.That(result.a, Is.EqualTo(0.35f).Within(0.0001f));
        }

        [Test]
        public void BlendMask_LeavesVerticesOutsideBrushUnchanged()
        {
            float result = ClothingShrinkPainterUtility.BlendMask(0.25f, 1f, 1.01f, 1f, 0.5f);

            Assert.That(result, Is.EqualTo(0.25f));
        }

        [Test]
        public void BlendMask_PaintsBrushCenterToTargetAtFullOpacity()
        {
            float result = ClothingShrinkPainterUtility.BlendMask(0.1f, 0.8f, 0f, 1f, 0.5f);

            Assert.That(result, Is.EqualTo(0.8f).Within(0.0001f));
        }

        [Test]
        public void RefreshMeshAfterUndo_RebindsRendererAndKeepsRedo()
        {
            var gameObject = new GameObject("ClothingShrinkUndoRefreshTest");
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero },
                colors = new[] { Color.black }
            };
            try
            {
                var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                Undo.RegisterCompleteObjectUndo(mesh, "测试服装收缩刷新");
                mesh.colors = new[] { Color.white };

                Undo.PerformUndo();
                ClothingShrinkPainterUtility.RefreshMeshAfterUndo(mesh, renderer);
                Assert.That(ClothingShrinkPainterUtility.ReadMask(mesh.colors[0]), Is.EqualTo(0f));
                Assert.That(renderer.sharedMesh, Is.SameAs(mesh));

                Undo.PerformRedo();
                ClothingShrinkPainterUtility.RefreshMeshAfterUndo(mesh, renderer);
                Assert.That(ClothingShrinkPainterUtility.ReadMask(mesh.colors[0]), Is.EqualTo(1f));
                Assert.That(renderer.sharedMesh, Is.SameAs(mesh));
            }
            finally
            {
                Undo.ClearUndo(mesh);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void UndoState_RestoresExactMaskAndRedoValue()
        {
            var mesh = new Mesh
            {
                vertices = new[] { Vector3.zero },
                colors = new[] { ClothingShrinkPainterUtility.WithMask(Color.white, 0.2f) }
            };
            var state = ScriptableObject.CreateInstance<ClothingShrinkUndoState>();
            try
            {
                state.Capture(mesh, mesh.colors);
                Undo.RegisterCompleteObjectUndo(state, "测试服装收缩精确撤销");
                mesh.colors = new[] { ClothingShrinkPainterUtility.WithMask(Color.white, 0.85f) };
                state.Capture(mesh, mesh.colors);

                Undo.PerformUndo();
                state.Apply();
                Assert.That(ClothingShrinkPainterUtility.ReadMask(mesh.colors[0]), Is.EqualTo(0.2f).Within(0.0001f));

                Undo.PerformRedo();
                state.Apply();
                Assert.That(ClothingShrinkPainterUtility.ReadMask(mesh.colors[0]), Is.EqualTo(0.85f).Within(0.0001f));
            }
            finally
            {
                Undo.ClearUndo(state);
                Object.DestroyImmediate(state);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Controller_UsesUpForZeroLengthAxis()
        {
            Assert.That(ClothingShrinkController.NormalizeAxis(Vector3.zero), Is.EqualTo(Vector3.up));
        }

        [Test]
        public void Controller_WritesPerRendererShaderParameters()
        {
            var gameObject = new GameObject("ClothingShrinkControllerTest");
            try
            {
                var renderer = gameObject.AddComponent<MeshRenderer>();
                var controller = gameObject.AddComponent<ClothingShrinkController>();
                controller.Amount = 0.025f;
                controller.Direction = ClothingShrinkDirection.LocalAxis;
                controller.LocalAxis = Vector3.right;
                controller.LocalCenter = new Vector3(1f, 2f, 3f);
                controller.DebugMask = 0.6f;

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);

                Assert.That(block.GetFloat("_ClothingShrinkAmount"), Is.EqualTo(0.025f).Within(0.0001f));
                Assert.That(block.GetFloat("_ClothingShrinkMode"), Is.EqualTo(1f));
                Assert.That(block.GetVector("_ClothingShrinkAxisOS"), Is.EqualTo((Vector4)Vector3.right));
                Assert.That(block.GetVector("_ClothingShrinkCenterOS"), Is.EqualTo((Vector4)new Vector3(1f, 2f, 3f)));
                Assert.That(block.GetFloat("_ClothingShrinkDebugMask"), Is.EqualTo(0.6f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ShrinkShader_IsAvailableWithExpectedProperties()
        {
            Shader shader = Shader.Find(ClothingShrinkController.ShaderName);

            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                Assert.That(material.HasProperty("_ClothingShrinkAmount"), Is.True);
                Assert.That(material.HasProperty("_ClothingShrinkMode"), Is.True);
                Assert.That(material.HasProperty("_ClothingShrinkAxisOS"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }
    }
}
