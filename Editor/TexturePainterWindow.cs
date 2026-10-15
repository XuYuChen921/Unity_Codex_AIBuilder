using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIBuilder.EditorTools
{
    public enum TexturePaintToolMode
    {
        Paint,
        Pick
    }

    public static class TexturePainterUtility
    {
        public static Color32 BlendColor(
            Color32 current,
            Color target,
            float normalizedDistance,
            float opacity,
            float hardness)
        {
            if (normalizedDistance >= 1f)
            {
                return current;
            }

            float exponent = Mathf.Lerp(2.5f, 0.25f, Mathf.Clamp01(hardness));
            float falloff = Mathf.Pow(1f - Mathf.Clamp01(normalizedDistance), exponent);
            Color blended = Color.Lerp(current, target, Mathf.Clamp01(opacity) * falloff);
            blended.a = current.a / 255f;
            return blended;
        }

        public static bool ApplyBrush(
            Color32[] pixels,
            int width,
            int height,
            Vector2 uv,
            float radiusPixels,
            Color target,
            float opacity,
            float hardness,
            out RectInt dirtyRect)
        {
            dirtyRect = new RectInt();
            if (pixels == null || pixels.Length != width * height || width <= 0 || height <= 0)
            {
                return false;
            }

            float radius = Mathf.Max(0.5f, radiusPixels);
            float centerX = Mathf.Clamp01(uv.x) * (width - 1);
            float centerY = Mathf.Clamp01(uv.y) * (height - 1);
            int xMin = Mathf.Clamp(Mathf.FloorToInt(centerX - radius), 0, width - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(centerX + radius), 0, width - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(centerY - radius), 0, height - 1);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(centerY + radius), 0, height - 1);
            bool changed = false;

            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float normalizedDistance = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                    if (normalizedDistance >= 1f)
                    {
                        continue;
                    }

                    int index = y * width + x;
                    Color32 original = pixels[index];
                    Color32 painted = BlendColor(original, target, normalizedDistance, opacity, hardness);
                    if (painted.r == original.r
                        && painted.g == original.g
                        && painted.b == original.b
                        && painted.a == original.a)
                    {
                        continue;
                    }

                    pixels[index] = painted;
                    changed = true;
                }
            }

            if (changed)
            {
                dirtyRect = new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
            }
            return changed;
        }

        public static Color SampleBilinear(Color32[] pixels, int width, int height, Vector2 uv)
        {
            if (pixels == null || pixels.Length != width * height || width <= 0 || height <= 0)
            {
                return Color.clear;
            }

            float x = Mathf.Clamp01(uv.x) * (width - 1);
            float y = Mathf.Clamp01(uv.y) * (height - 1);
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int y1 = Mathf.Min(y0 + 1, height - 1);
            Color bottom = Color.Lerp(pixels[y0 * width + x0], pixels[y0 * width + x1], x - x0);
            Color top = Color.Lerp(pixels[y1 * width + x0], pixels[y1 * width + x1], x - x0);
            return Color.Lerp(bottom, top, y - y0);
        }

        public static Vector2 NormalizeUV(Vector2 uv, TextureWrapMode wrapMode)
        {
            if (wrapMode == TextureWrapMode.Clamp)
            {
                return new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
            }
            if (wrapMode == TextureWrapMode.Mirror)
            {
                return new Vector2(Mirror(uv.x), Mirror(uv.y));
            }
            if (wrapMode == TextureWrapMode.MirrorOnce)
            {
                return new Vector2(Mathf.Clamp01(Mathf.Abs(uv.x)), Mathf.Clamp01(Mathf.Abs(uv.y)));
            }
            return new Vector2(Mathf.Repeat(uv.x, 1f), Mathf.Repeat(uv.y, 1f));
        }

        private static float Mirror(float value)
        {
            float repeated = Mathf.Repeat(value, 2f);
            return repeated <= 1f ? repeated : 2f - repeated;
        }

        public static bool IsTriangleInSubMesh(Mesh mesh, int triangleIndex, int subMeshIndex)
        {
            if (mesh == null
                || triangleIndex < 0
                || subMeshIndex < 0
                || subMeshIndex >= mesh.subMeshCount)
            {
                return false;
            }

            long firstTriangle = 0;
            for (int index = 0; index < mesh.subMeshCount; index++)
            {
                if (mesh.GetTopology(index) != MeshTopology.Triangles)
                {
                    if (index == subMeshIndex)
                    {
                        return false;
                    }
                    continue;
                }

                long triangleCount = (long)mesh.GetIndexCount(index) / 3;
                if (index == subMeshIndex)
                {
                    return triangleIndex >= firstTriangle && triangleIndex < firstTriangle + triangleCount;
                }
                firstTriangle += triangleCount;
            }
            return false;
        }

        public static RectInt Union(RectInt left, RectInt right)
        {
            if (left.width <= 0 || left.height <= 0)
            {
                return right;
            }
            if (right.width <= 0 || right.height <= 0)
            {
                return left;
            }

            int xMin = Mathf.Min(left.xMin, right.xMin);
            int yMin = Mathf.Min(left.yMin, right.yMin);
            int xMax = Mathf.Max(left.xMax, right.xMax);
            int yMax = Mathf.Max(left.yMax, right.yMax);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }

    public sealed class TexturePaintUndoState : ScriptableObject
    {
        [SerializeField] private Texture2D targetTexture;
        [SerializeField] private Color32[] pixels = Array.Empty<Color32>();

        public Texture2D TargetTexture => targetTexture;

        public void Capture(Texture2D texture, Color32[] sourcePixels)
        {
            targetTexture = texture;
            pixels = sourcePixels != null ? (Color32[])sourcePixels.Clone() : Array.Empty<Color32>();
        }

        public bool Apply()
        {
            if (targetTexture == null || pixels.Length != targetTexture.width * targetTexture.height)
            {
                return false;
            }

            targetTexture.SetPixels32(pixels);
            targetTexture.Apply(false, false);
            EditorUtility.SetDirty(targetTexture);
            return true;
        }
    }

    public sealed class TexturePainterWindow : EditorWindow
    {
        private const string GeneratedFolder = "Assets/AIBuilderGenerated/TexturePaint";

        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private int materialSlot;
        [SerializeField] private TexturePaintToolMode toolMode;
        [SerializeField] private Color brushColor = Color.white;
        [SerializeField] private float brushDiameterPixels = 64f;
        [SerializeField] private float brushOpacity = 1f;
        [SerializeField] private float brushHardness = 0.65f;
        [SerializeField] private bool paintEnabled;

        private Texture2D editableTexture;
        private Material editableMaterial;
        private string textureProperty;
        private Color32[] workingPixels = Array.Empty<Color32>();
        private TexturePaintUndoState undoState;
        private bool strokeActive;
        private int strokeUndoGroup = -1;
        private bool hasLastPaintUV;
        private Vector2 lastPaintUV;
        private GameObject hitObject;
        private MeshCollider hitCollider;
        private Mesh hitMesh;
        private Mesh bakedHitMesh;
        private string lastPickInfo = string.Empty;
        private bool autoActivatePickOnNextGUI;

        public static void OpenWindow()
        {
            GetWindow<TexturePainterWindow>("贴图绘制");
        }

        private void OnEnable()
        {
            EnsureUndoState();
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            Selection.selectionChanged += OnSelectionChanged;
            TryUseCurrentSelection();
            SyncEditableTexture();
            autoActivatePickOnNextGUI = toolMode == TexturePaintToolMode.Pick && !paintEnabled;
        }

        private void OnDisable()
        {
            FinishStroke();
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.delayCall -= RefreshAfterUndoRedo;
            DestroyHitCollider();
            if (undoState != null)
            {
                Undo.ClearUndo(undoState);
                DestroyImmediate(undoState);
                undoState = null;
            }
        }

        private void OnSelectionChanged()
        {
            if (!paintEnabled)
            {
                TryUseCurrentSelection();
                Repaint();
            }
        }

        private void OnUndoRedoPerformed()
        {
            EditorApplication.delayCall -= RefreshAfterUndoRedo;
            EditorApplication.delayCall += RefreshAfterUndoRedo;
        }

        private void RefreshAfterUndoRedo()
        {
            EditorApplication.delayCall -= RefreshAfterUndoRedo;
            if (undoState == null || !undoState.Apply())
            {
                return;
            }

            editableTexture = undoState.TargetTexture;
            workingPixels = editableTexture.GetPixels32();
            SaveTextureAsset(false);
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Scene 贴图吸色与涂抹", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "先创建独立贴图和材质副本，再在 Scene 视图吸取贴图原色或涂抹。不会覆盖 FBX、原贴图和共享材质。",
                MessageType.Info);

            Renderer selected = (Renderer)EditorGUILayout.ObjectField(
                "目标 Renderer", targetRenderer, typeof(Renderer), true);
            if (selected != targetRenderer)
            {
                SetTargetRenderer(selected);
            }

            DrawMaterialSelector();
            if (!TryGetTarget(out Material material, out string propertyName, out Texture2D texture, out string error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
                return;
            }

            EditorGUILayout.ObjectField("当前材质", material, typeof(Material), false);
            EditorGUILayout.ObjectField("颜色贴图", texture, typeof(Texture2D), false);
            EditorGUILayout.LabelField("贴图属性", propertyName);

            bool editable = IsEditableTexture(texture);
            if (!editable)
            {
                EditorGUILayout.HelpBox("当前是原始贴图。必须先创建副本才能绘制。", MessageType.Warning);
                if (GUILayout.Button("创建独立贴图和材质副本", GUILayout.Height(30f)))
                {
                    CreateEditableCopies(material, propertyName, texture);
                }
                return;
            }

            if (editableTexture != texture)
            {
                SetEditableTexture(material, propertyName, texture);
            }

            DrawPaintControls();
        }

        private void DrawMaterialSelector()
        {
            if (targetRenderer == null)
            {
                return;
            }

            Material[] materials = targetRenderer.sharedMaterials;
            if (materials.Length == 0)
            {
                return;
            }

            string[] names = new string[materials.Length];
            for (int index = 0; index < materials.Length; index++)
            {
                names[index] = index + ": " + (materials[index] != null ? materials[index].name : "<空材质>");
            }

            int nextSlot = EditorGUILayout.Popup("材质槽", Mathf.Clamp(materialSlot, 0, materials.Length - 1), names);
            if (nextSlot != materialSlot)
            {
                FinishStroke();
                paintEnabled = false;
                materialSlot = nextSlot;
                SetEditableTexture(null, null, null);
                RefreshHitCollider();
            }
        }

        private void DrawPaintControls()
        {
            EditorGUILayout.Space(6f);
            if (autoActivatePickOnNextGUI)
            {
                autoActivatePickOnNextGUI = false;
                SetSceneInteraction(true);
            }

            TexturePaintToolMode nextToolMode = (TexturePaintToolMode)GUILayout.Toolbar(
                (int)toolMode, new[] { "涂抹", "贴图吸色" });
            if (nextToolMode != toolMode)
            {
                toolMode = nextToolMode;
                if (toolMode == TexturePaintToolMode.Pick && !paintEnabled)
                {
                    SetSceneInteraction(true);
                }
            }

            if (toolMode == TexturePaintToolMode.Pick)
            {
                Rect colorRect = EditorGUILayout.GetControlRect();
                colorRect = EditorGUI.PrefixLabel(colorRect, new GUIContent("吸取结果（只读）"));
                EditorGUI.DrawRect(colorRect, brushColor);
            }
            else
            {
                brushColor = EditorGUILayout.ColorField(
                    new GUIContent("画笔颜色", "系统屏幕吸管已关闭；需要从模型贴图取色时，请切换到上方“贴图吸色”。"),
                    brushColor,
                    false,
                    false,
                    false);
            }
            brushDiameterPixels = EditorGUILayout.Slider("画笔直径（像素）", brushDiameterPixels, 1f, 512f);
            brushOpacity = EditorGUILayout.Slider("单次强度", brushOpacity, 0.01f, 1f);
            brushHardness = EditorGUILayout.Slider("硬度", brushHardness, 0f, 1f);

            if (!string.IsNullOrEmpty(lastPickInfo))
            {
                EditorGUILayout.HelpBox(lastPickInfo, MessageType.None);
            }

            bool nextPaintEnabled = GUILayout.Toggle(
                paintEnabled,
                GetSceneInteractionButtonLabel(),
                "Button",
                GUILayout.Height(30f));
            if (nextPaintEnabled != paintEnabled)
            {
                SetSceneInteraction(nextPaintEnabled);
            }

            EditorGUILayout.HelpBox(
                "左键涂抹；切到“贴图吸色”后左键读取 UV 对应的贴图像素，不读取材质颜色、灯光、阴影或后处理。涂抹模式下也可按住 Ctrl 临时吸色。",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新命中网格"))
                {
                    RefreshHitCollider();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("立即保存贴图"))
                {
                    SaveTextureAsset(true);
                }
            }
        }

        private string GetSceneInteractionButtonLabel()
        {
            if (toolMode == TexturePaintToolMode.Pick)
            {
                return paintEnabled ? "结束 Scene 贴图吸色" : "开始 Scene 贴图吸色";
            }
            return paintEnabled ? "结束 Scene 绘制" : "开始 Scene 绘制";
        }

        private void SetSceneInteraction(bool enabled)
        {
            paintEnabled = enabled;
            if (paintEnabled)
            {
                RefreshHitCollider();
                ShowNotification(new GUIContent(
                    toolMode == TexturePaintToolMode.Pick
                        ? "请在 Scene 模型表面左键吸取贴图像素"
                        : "请在 Scene 模型表面左键涂抹"));
            }
            else
            {
                FinishStroke();
                DestroyHitCollider();
            }
            SceneView.RepaintAll();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!paintEnabled || editableTexture == null || targetRenderer == null || EditorApplication.isPlaying)
            {
                return;
            }

            Event current = Event.current;
            bool undoShortcut = current.type == EventType.KeyDown
                && current.control
                && !current.shift
                && current.keyCode == KeyCode.Z;
            bool redoShortcut = current.type == EventType.KeyDown
                && current.control
                && (current.keyCode == KeyCode.Y || current.shift && current.keyCode == KeyCode.Z);
            if (undoShortcut || redoShortcut)
            {
                FinishStroke();
                current.Use();
                if (redoShortcut)
                {
                    Undo.PerformRedo();
                }
                else
                {
                    Undo.PerformUndo();
                }
                EditorApplication.delayCall -= RefreshAfterUndoRedo;
                RefreshAfterUndoRedo();
                return;
            }

            if (current.type == EventType.Layout && !current.alt)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
            {
                RefreshHitCollider();
            }

            if (strokeActive && current.rawType == EventType.MouseUp && current.button == 0)
            {
                FinishStroke();
                current.Use();
                return;
            }

            if (hitCollider == null || hitMesh == null)
            {
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            bool hasHit = hitCollider.Raycast(ray, out RaycastHit hit, float.MaxValue)
                && TexturePainterUtility.IsTriangleInSubMesh(hitMesh, hit.triangleIndex, materialSlot);
            if (hasHit && current.type == EventType.Repaint)
            {
                Handles.color = toolMode == TexturePaintToolMode.Pick || current.control
                    ? new Color(0.2f, 0.75f, 1f, 0.95f)
                    : new Color(brushColor.r, brushColor.g, brushColor.b, 0.95f);
                Handles.DrawWireDisc(hit.point, hit.normal, EstimateWorldBrushRadius(hit));
            }

            bool leftMouseEvent = (current.type == EventType.MouseDown || current.type == EventType.MouseDrag)
                && current.button == 0
                && !current.alt
                && hasHit;
            if (leftMouseEvent)
            {
                Vector2 uv = GetPaintUV(hit.textureCoord);
                if (toolMode == TexturePaintToolMode.Pick || current.control)
                {
                    if (current.type == EventType.MouseDown)
                    {
                        PickColor(uv);
                    }
                }
                else
                {
                    if (!strokeActive)
                    {
                        BeginStroke();
                    }
                    PaintTo(uv);
                }
                current.Use();
            }

            if (current.type == EventType.MouseMove || current.type == EventType.MouseDrag)
            {
                sceneView.Repaint();
            }
        }

        private void BeginStroke()
        {
            if (editableTexture == null || workingPixels.Length == 0)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            strokeUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("绘制模型颜色贴图");
            EnsureUndoState();
            undoState.Capture(editableTexture, workingPixels);
            Undo.RegisterCompleteObjectUndo(undoState, "绘制模型颜色贴图");
            strokeActive = true;
            hasLastPaintUV = false;
        }

        private void FinishStroke()
        {
            if (!strokeActive)
            {
                return;
            }

            strokeActive = false;
            hasLastPaintUV = false;
            if (strokeUndoGroup >= 0)
            {
                Undo.CollapseUndoOperations(strokeUndoGroup);
                strokeUndoGroup = -1;
            }
            SaveTextureAsset(false);
        }

        private void PaintTo(Vector2 uv)
        {
            if (editableTexture == null || workingPixels.Length != editableTexture.width * editableTexture.height)
            {
                return;
            }

            Vector2 start = hasLastPaintUV ? lastPaintUV : uv;
            Vector2 end = uv;
            if (editableTexture.wrapMode != TextureWrapMode.Clamp)
            {
                if (Mathf.Abs(end.x - start.x) > 0.5f)
                {
                    end.x += end.x < start.x ? 1f : -1f;
                }
                if (Mathf.Abs(end.y - start.y) > 0.5f)
                {
                    end.y += end.y < start.y ? 1f : -1f;
                }
            }

            float distancePixels = new Vector2(
                (end.x - start.x) * editableTexture.width,
                (end.y - start.y) * editableTexture.height).magnitude;
            float radius = brushDiameterPixels * 0.5f;
            int steps = Mathf.Clamp(Mathf.CeilToInt(distancePixels / Mathf.Max(1f, radius * 0.35f)), 1, 128);
            RectInt dirty = new RectInt();
            bool changed = false;
            for (int step = 1; step <= steps; step++)
            {
                Vector2 stampUV = TexturePainterUtility.NormalizeUV(
                    Vector2.Lerp(start, end, step / (float)steps), editableTexture.wrapMode);
                if (TexturePainterUtility.ApplyBrush(
                        workingPixels,
                        editableTexture.width,
                        editableTexture.height,
                        stampUV,
                        radius,
                        brushColor,
                        brushOpacity,
                        brushHardness,
                        out RectInt stampRect))
                {
                    dirty = TexturePainterUtility.Union(dirty, stampRect);
                    changed = true;
                }
            }

            lastPaintUV = uv;
            hasLastPaintUV = true;
            if (!changed)
            {
                return;
            }

            UploadDirtyRect(dirty);
            undoState.Capture(editableTexture, workingPixels);
            Repaint();
            SceneView.RepaintAll();
        }

        private void UploadDirtyRect(RectInt dirty)
        {
            var region = new Color32[dirty.width * dirty.height];
            int destination = 0;
            for (int y = dirty.yMin; y < dirty.yMax; y++)
            {
                int source = y * editableTexture.width + dirty.xMin;
                Array.Copy(workingPixels, source, region, destination, dirty.width);
                destination += dirty.width;
            }
            editableTexture.SetPixels32(dirty.x, dirty.y, dirty.width, dirty.height, region);
            editableTexture.Apply(false, false);
            EditorUtility.SetDirty(editableTexture);
        }

        private void PickColor(Vector2 uv)
        {
            brushColor = TexturePainterUtility.SampleBilinear(
                workingPixels, editableTexture.width, editableTexture.height, uv);
            Color32 sampled = brushColor;
            int pixelX = Mathf.Clamp(Mathf.RoundToInt(uv.x * (editableTexture.width - 1)), 0, editableTexture.width - 1);
            int pixelY = Mathf.Clamp(Mathf.RoundToInt(uv.y * (editableTexture.height - 1)), 0, editableTexture.height - 1);
            lastPickInfo =
                $"来源：CPU 贴图像素（非屏幕）\n{editableTexture.name}，UV ({uv.x:F4}, {uv.y:F4})，" +
                $"像素 ({pixelX}, {pixelY})，RGB ({sampled.r}, {sampled.g}, {sampled.b})";
            ShowNotification(new GUIContent("已从贴图像素吸色"));
            Repaint();
        }

        private Vector2 GetPaintUV(Vector2 meshUV)
        {
            if (editableMaterial == null || string.IsNullOrEmpty(textureProperty))
            {
                return TexturePainterUtility.NormalizeUV(meshUV, editableTexture.wrapMode);
            }

            Vector2 scale = editableMaterial.GetTextureScale(textureProperty);
            Vector2 offset = editableMaterial.GetTextureOffset(textureProperty);
            Vector2 transformed = Vector2.Scale(meshUV, scale) + offset;
            return TexturePainterUtility.NormalizeUV(transformed, editableTexture.wrapMode);
        }

        private float EstimateWorldBrushRadius(RaycastHit hit)
        {
            if (hitMesh == null || hit.triangleIndex < 0)
            {
                return HandleUtility.GetHandleSize(hit.point) * 0.05f;
            }

            int[] triangles = hitMesh.triangles;
            Vector3[] vertices = hitMesh.vertices;
            Vector2[] uvs = hitMesh.uv;
            int first = hit.triangleIndex * 3;
            if (first + 2 >= triangles.Length || uvs.Length != vertices.Length)
            {
                return HandleUtility.GetHandleSize(hit.point) * 0.05f;
            }

            int i0 = triangles[first];
            int i1 = triangles[first + 1];
            int i2 = triangles[first + 2];
            Vector3 p0 = hitObject.transform.TransformPoint(vertices[i0]);
            Vector3 p1 = hitObject.transform.TransformPoint(vertices[i1]);
            Vector3 p2 = hitObject.transform.TransformPoint(vertices[i2]);
            float worldArea = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
            Vector2 uv0 = Vector2.Scale(uvs[i0], new Vector2(editableTexture.width, editableTexture.height));
            Vector2 uv1 = Vector2.Scale(uvs[i1], new Vector2(editableTexture.width, editableTexture.height));
            Vector2 uv2 = Vector2.Scale(uvs[i2], new Vector2(editableTexture.width, editableTexture.height));
            float pixelArea = Mathf.Abs((uv1.x - uv0.x) * (uv2.y - uv0.y)
                - (uv1.y - uv0.y) * (uv2.x - uv0.x)) * 0.5f;
            if (worldArea <= 0f || pixelArea <= 0.0001f)
            {
                return HandleUtility.GetHandleSize(hit.point) * 0.05f;
            }
            return Mathf.Sqrt(worldArea / pixelArea) * brushDiameterPixels * 0.5f;
        }

        private void CreateEditableCopies(Material sourceMaterial, string propertyName, Texture2D sourceTexture)
        {
            try
            {
                EnsureAssetFolder(GeneratedFolder);
                string safeTextureName = MakeSafeFileName(sourceTexture.name + "_Paint");
                string texturePath = AssetDatabase.GenerateUniqueAssetPath(
                    GeneratedFolder + "/" + safeTextureName + ".png");
                WriteTextureCopy(sourceTexture, texturePath);
                ConfigureEditableTextureImporter(texturePath, sourceTexture);

                Texture2D textureCopy = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (textureCopy == null)
                {
                    throw new InvalidOperationException("独立贴图导入失败：" + texturePath);
                }

                string materialPath = AssetDatabase.GenerateUniqueAssetPath(
                    GeneratedFolder + "/" + MakeSafeFileName(sourceMaterial.name + "_Paint") + ".mat");
                Material materialCopy = new Material(sourceMaterial)
                {
                    name = sourceMaterial.name + "_Paint"
                };
                materialCopy.SetTexture(propertyName, textureCopy);
                AssetDatabase.CreateAsset(materialCopy, materialPath);

                Material[] materials = targetRenderer.sharedMaterials;
                Undo.RecordObject(targetRenderer, "使用独立贴图绘制材质");
                materials[materialSlot] = materialCopy;
                targetRenderer.sharedMaterials = materials;
                EditorUtility.SetDirty(targetRenderer);
                AssetDatabase.SaveAssets();

                SetEditableTexture(materialCopy, propertyName, textureCopy);
                RefreshHitCollider();
                EditorGUIUtility.PingObject(textureCopy);
                ShowNotification(new GUIContent("已创建独立贴图和材质副本"));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("创建贴图副本失败", exception.Message, "关闭");
            }
        }

        private void SaveTextureAsset(bool notify)
        {
            if (editableTexture == null || workingPixels.Length != editableTexture.width * editableTexture.height)
            {
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(editableTexture);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            editableTexture.SetPixels32(workingPixels);
            editableTexture.Apply(false, false);
            File.WriteAllBytes(ToAbsolutePath(assetPath), editableTexture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            Texture2D reloaded = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (reloaded != null)
            {
                editableTexture = reloaded;
                workingPixels = editableTexture.GetPixels32();
                if (editableMaterial != null && !string.IsNullOrEmpty(textureProperty))
                {
                    editableMaterial.SetTexture(textureProperty, editableTexture);
                    EditorUtility.SetDirty(editableMaterial);
                }
            }
            AssetDatabase.SaveAssets();
            if (notify)
            {
                ShowNotification(new GUIContent("贴图已保存"));
            }
        }

        private void RefreshHitCollider()
        {
            DestroyHitCollider();
            Mesh source = GetTargetMesh();
            if (targetRenderer == null || source == null || !source.isReadable || source.uv.Length != source.vertexCount)
            {
                return;
            }

            hitObject = new GameObject("AIBuilder Texture Paint Hit")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            hitObject.transform.SetParent(targetRenderer.transform, false);
            hitCollider = hitObject.AddComponent<MeshCollider>();

            if (targetRenderer is SkinnedMeshRenderer skinnedRenderer)
            {
                bakedHitMesh = new Mesh
                {
                    name = "AIBuilder Texture Paint Baked Hit",
                    hideFlags = HideFlags.HideAndDontSave
                };
                skinnedRenderer.BakeMesh(bakedHitMesh);
                hitMesh = bakedHitMesh;
            }
            else
            {
                hitMesh = source;
            }
            hitCollider.sharedMesh = hitMesh;
        }

        private void DestroyHitCollider()
        {
            hitCollider = null;
            hitMesh = null;
            if (hitObject != null)
            {
                DestroyImmediate(hitObject);
                hitObject = null;
            }
            if (bakedHitMesh != null)
            {
                DestroyImmediate(bakedHitMesh);
                bakedHitMesh = null;
            }
        }

        private void SetTargetRenderer(Renderer renderer)
        {
            if (targetRenderer == renderer)
            {
                return;
            }

            FinishStroke();
            paintEnabled = false;
            DestroyHitCollider();
            targetRenderer = renderer;
            materialSlot = 0;
            SetEditableTexture(null, null, null);
            SyncEditableTexture();
        }

        private void TryUseCurrentSelection()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            Renderer renderer = selected.GetComponent<SkinnedMeshRenderer>();
            renderer = renderer != null ? renderer : selected.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                SetTargetRenderer(renderer);
            }
        }

        private void SyncEditableTexture()
        {
            if (TryGetTarget(out Material material, out string propertyName, out Texture2D texture, out _)
                && IsEditableTexture(texture))
            {
                SetEditableTexture(material, propertyName, texture);
            }
        }

        private void SetEditableTexture(Material material, string propertyName, Texture2D texture)
        {
            if (editableTexture == texture && editableMaterial == material && textureProperty == propertyName)
            {
                return;
            }

            FinishStroke();
            editableMaterial = material;
            textureProperty = propertyName;
            editableTexture = texture;
            lastPickInfo = string.Empty;
            workingPixels = editableTexture != null ? editableTexture.GetPixels32() : Array.Empty<Color32>();
            EnsureUndoState();
            Undo.ClearUndo(undoState);
            undoState.Capture(editableTexture, workingPixels);
        }

        private bool TryGetTarget(
            out Material material,
            out string propertyName,
            out Texture2D texture,
            out string error)
        {
            material = null;
            propertyName = null;
            texture = null;
            if (targetRenderer == null)
            {
                error = "请选择场景中的 SkinnedMeshRenderer 或 MeshRenderer。";
                return false;
            }
            if (!(targetRenderer is SkinnedMeshRenderer) && !(targetRenderer is MeshRenderer))
            {
                error = "当前只支持 SkinnedMeshRenderer 和 MeshRenderer。";
                return false;
            }

            Mesh mesh = GetTargetMesh();
            if (mesh == null)
            {
                error = "Renderer 没有可用 Mesh。";
                return false;
            }
            if (!mesh.isReadable)
            {
                error = "模型未开启 Read/Write，无法建立 Scene 绘制命中网格。请在 FBX Import Settings > Model 中开启 Read/Write。";
                return false;
            }
            if (mesh.uv == null || mesh.uv.Length != mesh.vertexCount)
            {
                error = "模型没有完整的 UV0，无法映射到颜色贴图。";
                return false;
            }

            Material[] materials = targetRenderer.sharedMaterials;
            if (materials.Length == 0 || materialSlot < 0 || materialSlot >= materials.Length)
            {
                error = "Renderer 没有可用材质槽。";
                return false;
            }
            if (materialSlot >= mesh.subMeshCount)
            {
                error = "所选材质槽没有对应的三角形子网格。";
                return false;
            }

            material = materials[materialSlot];
            if (material == null)
            {
                error = "所选材质槽为空。";
                return false;
            }

            if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") is Texture2D baseMap)
            {
                propertyName = "_BaseMap";
                texture = baseMap;
            }
            else if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") is Texture2D mainTexture)
            {
                propertyName = "_MainTex";
                texture = mainTexture;
            }
            else
            {
                error = "所选材质没有可编辑的 _BaseMap / _MainTex Texture2D。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private Mesh GetTargetMesh()
        {
            if (targetRenderer is SkinnedMeshRenderer skinnedRenderer)
            {
                return skinnedRenderer.sharedMesh;
            }
            return targetRenderer != null ? targetRenderer.GetComponent<MeshFilter>()?.sharedMesh : null;
        }

        private void EnsureUndoState()
        {
            if (undoState != null)
            {
                return;
            }
            undoState = CreateInstance<TexturePaintUndoState>();
            undoState.hideFlags = HideFlags.HideAndDontSave;
        }

        private static bool IsEditableTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return false;
            }
            string path = AssetDatabase.GetAssetPath(texture).Replace('\\', '/');
            if (!path.StartsWith(GeneratedFolder + "/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return AssetImporter.GetAtPath(path) is TextureImporter importer && importer.isReadable;
        }

        private static void WriteTextureCopy(Texture2D source, string assetPath)
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.Equals(Path.GetExtension(sourcePath), ".png", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(ToAbsolutePath(sourcePath), ToAbsolutePath(assetPath), true);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                return;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(
                source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readable.Apply(false, false);
                File.WriteAllBytes(ToAbsolutePath(assetPath), readable.EncodeToPNG());
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                Object.DestroyImmediate(readable);
            }
        }

        private static void ConfigureEditableTextureImporter(string assetPath, Texture2D source)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer))
            {
                throw new InvalidOperationException("找不到贴图导入设置：" + assetPath);
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            TextureImporter sourceImporter = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = sourceImporter != null && sourceImporter.alphaIsTransparency;
            importer.mipmapEnabled = sourceImporter == null || sourceImporter.mipmapEnabled;
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = source.wrapMode;
            importer.filterMode = source.filterMode;
            importer.anisoLevel = source.anisoLevel;
            importer.maxTextureSize = Mathf.Clamp(
                Mathf.NextPowerOfTwo(Mathf.Max(source.width, source.height)), 32, 16384);
            importer.SaveAndReimport();
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }
                current = next;
            }
        }

        private static string MakeSafeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }
            return string.IsNullOrWhiteSpace(value) ? "TexturePaint" : value.Trim();
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException("无法确定 Unity 项目根目录。");
            }
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
