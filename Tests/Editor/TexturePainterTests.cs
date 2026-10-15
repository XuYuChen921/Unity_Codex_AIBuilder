using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools.Tests
{
    public sealed class TexturePainterTests
    {
        [Test]
        public void BlendColor_PaintsRgbAndPreservesAlpha()
        {
            var original = new Color32(10, 20, 30, 77);

            Color32 result = TexturePainterUtility.BlendColor(
                original, Color.red, 0f, 1f, 1f);

            Assert.That(result.r, Is.EqualTo(255));
            Assert.That(result.g, Is.EqualTo(0));
            Assert.That(result.b, Is.EqualTo(0));
            Assert.That(result.a, Is.EqualTo(77));
        }

        [Test]
        public void ApplyBrush_OnlyChangesPixelsInsideRadius()
        {
            var pixels = new Color32[25];
            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(0, 0, 0, 255);
            }

            bool changed = TexturePainterUtility.ApplyBrush(
                pixels, 5, 5, new Vector2(0.5f, 0.5f), 1.1f, Color.white, 1f, 1f, out RectInt dirty);

            Assert.That(changed, Is.True);
            Assert.That(pixels[12].r, Is.EqualTo(255));
            Assert.That(pixels[0].r, Is.EqualTo(0));
            Assert.That(dirty.Contains(new Vector2Int(2, 2)), Is.True);
        }

        [Test]
        public void NormalizeUV_UsesClampOrRepeat()
        {
            Vector2 clamped = TexturePainterUtility.NormalizeUV(new Vector2(1.25f, -0.25f), TextureWrapMode.Clamp);
            Vector2 repeated = TexturePainterUtility.NormalizeUV(new Vector2(1.25f, -0.25f), TextureWrapMode.Repeat);

            Assert.That(clamped, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(repeated.x, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(repeated.y, Is.EqualTo(0.75f).Within(0.0001f));
        }

        [Test]
        public void SampleBilinear_ReadsTexturePixelsWithoutSceneDisplayColor()
        {
            var pixels = new[]
            {
                new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255),
                new Color32(0, 0, 255, 255), new Color32(255, 255, 255, 255)
            };

            Color corner = TexturePainterUtility.SampleBilinear(pixels, 2, 2, Vector2.zero);
            Color center = TexturePainterUtility.SampleBilinear(pixels, 2, 2, new Vector2(0.5f, 0.5f));

            Assert.That(corner, Is.EqualTo(Color.red));
            Assert.That(center.r, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(center.g, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(center.b, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(center.a, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void IsTriangleInSubMesh_SeparatesMaterialSlots()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero, Vector3.right, Vector3.up,
                    Vector3.one, Vector3.right * 2f, Vector3.up * 2f
                },
                subMeshCount = 2
            };
            try
            {
                mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                mesh.SetTriangles(new[] { 3, 4, 5 }, 1);

                Assert.That(TexturePainterUtility.IsTriangleInSubMesh(mesh, 0, 0), Is.True);
                Assert.That(TexturePainterUtility.IsTriangleInSubMesh(mesh, 0, 1), Is.False);
                Assert.That(TexturePainterUtility.IsTriangleInSubMesh(mesh, 1, 1), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void UndoState_RestoresExactTexturePixels()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var state = ScriptableObject.CreateInstance<TexturePaintUndoState>();
            var black = new[]
            {
                new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 255),
                new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 255)
            };
            var white = new[]
            {
                new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255),
                new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255)
            };
            try
            {
                texture.SetPixels32(black);
                texture.Apply();
                state.Capture(texture, black);
                Undo.RegisterCompleteObjectUndo(state, "测试贴图绘制撤销");
                state.Capture(texture, white);

                Undo.PerformUndo();
                Assert.That(state.Apply(), Is.True);
                Assert.That(texture.GetPixels32()[0].r, Is.EqualTo(0));

                Undo.PerformRedo();
                Assert.That(state.Apply(), Is.True);
                Assert.That(texture.GetPixels32()[0].r, Is.EqualTo(255));
            }
            finally
            {
                Undo.ClearUndo(state);
                Object.DestroyImmediate(state);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
