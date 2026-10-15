using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AIBuilder;
using UnityEditor;
using UnityEngine;

namespace AIBuilder.EditorTools
{
    public static class ClothingShrinkPainterUtility
    {
        public static float ReadMask(Color color)
        {
            return Mathf.Clamp01(color.r * 0.299f + color.g * 0.587f + color.b * 0.114f);
        }

        public static Color WithMask(Color original, float mask)
        {
            float value = Mathf.Clamp01(mask);
            return new Color(value, value, value, original.a);
        }

        public static float BlendMask(
            float current,
            float target,
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
            return Mathf.Lerp(current, Mathf.Clamp01(target), Mathf.Clamp01(opacity) * falloff);
        }

        public static void RefreshMeshAfterUndo(Mesh mesh, Renderer renderer)
        {
            if (mesh == null || !mesh.isReadable)
            {
                return;
            }

            Color[] restoredColors = mesh.colors;
            if (restoredColors.Length == mesh.vertexCount)
            {
                mesh.colors = restoredColors;
            }
            mesh.UploadMeshData(false);

            if (renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                skinnedRenderer.sharedMesh = mesh;
            }
            else if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null)
                {
                    filter.sharedMesh = mesh;
                }
            }
        }
    }

    public sealed class ClothingShrinkUndoState : ScriptableObject
    {
        [SerializeField] private Mesh targetMesh;
        [SerializeField] private Color[] colors = Array.Empty<Color>();

        public Mesh TargetMesh => targetMesh;

        public void Capture(Mesh mesh, Color[] sourceColors)
        {
            targetMesh = mesh;
            colors = sourceColors != null ? (Color[])sourceColors.Clone() : Array.Empty<Color>();
        }

        public void Apply()
        {
            if (targetMesh == null || !targetMesh.isReadable || colors.Length != targetMesh.vertexCount)
            {
                return;
            }

            targetMesh.colors = (Color[])colors.Clone();
            targetMesh.UploadMeshData(false);
            EditorUtility.SetDirty(targetMesh);
        }
    }

    public sealed class ClothingShrinkPainterWindow : EditorWindow
    {
        private const string GeneratedFolder = "Assets/AIBuilderGenerated/ClothingShrink";

        private Renderer targetRenderer;
        private float brushRadius = 0.08f;
        private float brushOpacity = 0.35f;
        private float brushHardness = 0.5f;
        private float brushValue = 1f;
        private bool paintEnabled;
        private bool strokeActive;
        private int strokeUndoGroup = -1;
        private Vector2 scroll;
        private ClothingShrinkUndoState undoState;

        private GameObject hitObject;
        private MeshCollider hitCollider;
        private Mesh bakedHitMesh;
        private Vector3[] hitVertices = Array.Empty<Vector3>();

        public static void Open()
        {
            GetWindow<ClothingShrinkPainterWindow>("服装收缩");
        }

        private void OnEnable()
        {
            EnsureUndoState();
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            TryUseCurrentSelection();
        }

        private void OnDisable()
        {
            FinishStroke();
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.delayCall -= RefreshAfterUndoRedo;
            DestroyHitCollider();
        }

        private void OnDestroy()
        {
            if (undoState == null)
            {
                return;
            }

            Undo.ClearUndo(undoState);
            DestroyImmediate(undoState);
            undoState = null;
        }

        private void OnUndoRedoPerformed()
        {
            EditorApplication.delayCall -= RefreshAfterUndoRedo;
            EditorApplication.delayCall += RefreshAfterUndoRedo;
        }

        private void RefreshAfterUndoRedo()
        {
            undoState?.Apply();
            Mesh mesh = undoState != null && undoState.TargetMesh != null
                ? undoState.TargetMesh
                : GetTargetMesh();
            Renderer renderer = GetTargetMesh() == mesh ? targetRenderer : null;
            ClothingShrinkPainterUtility.RefreshMeshAfterUndo(mesh, renderer);
            if (paintEnabled)
            {
                RefreshHitCollider();
            }
            EditorApplication.QueuePlayerLoopUpdate();
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnSelectionChange()
        {
            if (!paintEnabled)
            {
                TryUseCurrentSelection();
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("服装顶点色收缩", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "白色顶点色 = 最大收缩，黑色 = 不移动。工具先复制独立 Mesh，再在 Scene 视图绘制，不会覆盖原 FBX。",
                MessageType.Info);

            Renderer selected = (Renderer)EditorGUILayout.ObjectField(
                "服装 Renderer", targetRenderer, typeof(Renderer), true);
            if (selected != targetRenderer)
            {
                SetTargetRenderer(selected);
            }

            Mesh mesh = GetTargetMesh();
            DrawMeshStatus(mesh);
            DrawMaterialStatus();
            DrawController();
            DrawPainter(mesh);

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "建议先从 0.005–0.02 模型单位开始。Shift + 左键擦除，Ctrl+Z 撤销整次笔画。Alt/右键仍用于旋转 Scene 视图。",
                MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void DrawMeshStatus(Mesh mesh)
        {
            if (!TryValidateRenderer(out string rendererError))
            {
                EditorGUILayout.HelpBox(rendererError, MessageType.Warning);
                return;
            }

            string path = mesh != null ? AssetDatabase.GetAssetPath(mesh) : string.Empty;
            int colorCount = mesh != null && mesh.isReadable ? mesh.colors.Length : 0;
            EditorGUILayout.LabelField("网格", mesh != null ? mesh.name : "未找到");
            EditorGUILayout.LabelField("顶点 / 顶点色", mesh != null ? $"{mesh.vertexCount} / {colorCount}" : "-");
            EditorGUILayout.LabelField("资源", string.IsNullOrEmpty(path) ? "场景内 Mesh" : path);

            bool isEditable = IsEditableMesh(mesh, out string editError);
            using (new EditorGUI.DisabledScope(mesh == null || EditorApplication.isPlaying))
            {
                if (GUILayout.Button("创建可编辑 Mesh 副本（推荐）", GUILayout.Height(28f)))
                {
                    CreateEditableMeshCopy(mesh);
                }
            }

            if (!isEditable)
            {
                EditorGUILayout.HelpBox(editError, MessageType.Warning);
            }
        }

        private void DrawMaterialStatus()
        {
            if (targetRenderer == null)
            {
                return;
            }

            Material[] materials = targetRenderer.sharedMaterials;
            bool usesShrinkShader = materials.Length > 0 && materials
                .Where(material => material != null)
                .All(material => material.shader != null
                    && material.shader.name == ClothingShrinkController.ShaderName);
            if (usesShrinkShader)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "当前材质未全部使用 AIBuilder 服装收缩 Shader。请复制原材质后切换 Shader，并重新填写贴图参数。工具不会自动替换材质。",
                MessageType.Warning);
            if (GUILayout.Button("定位服装收缩 Shader"))
            {
                Shader shader = Shader.Find(ClothingShrinkController.ShaderName);
                if (shader != null)
                {
                    Selection.activeObject = shader;
                    EditorGUIUtility.PingObject(shader);
                }
            }
        }

        private void DrawController()
        {
            if (targetRenderer == null)
            {
                return;
            }

            ClothingShrinkController controller = targetRenderer.GetComponent<ClothingShrinkController>();
            if (controller == null)
            {
                if (GUILayout.Button("添加收缩参数组件"))
                {
                    controller = Undo.AddComponent<ClothingShrinkController>(targetRenderer.gameObject);
                    controller.Apply();
                }
                return;
            }

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("收缩预览", EditorStyles.boldLabel);
            float amount = EditorGUILayout.Slider("强度（模型单位）", controller.Amount, 0f, 0.2f);
            ClothingShrinkDirection direction = (ClothingShrinkDirection)EditorGUILayout.EnumPopup(
                "方向", controller.Direction);
            Vector3 axis = controller.LocalAxis;
            Vector3 center = controller.LocalCenter;
            if (direction == ClothingShrinkDirection.LocalAxis)
            {
                axis = EditorGUILayout.Vector3Field("局部轴线", axis);
                center = EditorGUILayout.Vector3Field("轴线中心", center);
            }
            float debugMask = EditorGUILayout.Slider("显示蒙版", controller.DebugMask, 0f, 1f);

            if (!Mathf.Approximately(amount, controller.Amount)
                || direction != controller.Direction
                || axis != controller.LocalAxis
                || center != controller.LocalCenter
                || !Mathf.Approximately(debugMask, controller.DebugMask))
            {
                Undo.RecordObject(controller, "调整服装收缩参数");
                controller.Amount = amount;
                controller.Direction = direction;
                controller.LocalAxis = axis;
                controller.LocalCenter = center;
                controller.DebugMask = debugMask;
                EditorUtility.SetDirty(controller);
                SceneView.RepaintAll();
            }
        }

        private void DrawPainter(Mesh mesh)
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField("顶点色画笔", EditorStyles.boldLabel);
            brushRadius = Mathf.Max(0.0001f, EditorGUILayout.FloatField("半径（世界单位）", brushRadius));
            brushOpacity = EditorGUILayout.Slider("单次强度", brushOpacity, 0.01f, 1f);
            brushHardness = EditorGUILayout.Slider("硬度", brushHardness, 0f, 1f);
            brushValue = EditorGUILayout.Slider("目标灰度", brushValue, 0f, 1f);

            bool canPaint = IsEditableMesh(mesh, out _) && !EditorApplication.isPlaying;
            using (new EditorGUI.DisabledScope(!canPaint))
            {
                EditorGUILayout.BeginHorizontal();
                bool nextPaintEnabled = GUILayout.Toggle(
                    paintEnabled, paintEnabled ? "结束绘制" : "在 Scene 视图绘制", "Button", GUILayout.Height(30f));
                if (nextPaintEnabled != paintEnabled)
                {
                    paintEnabled = nextPaintEnabled;
                    if (paintEnabled)
                    {
                        RefreshHitCollider();
                    }
                    else
                    {
                        FinishStroke();
                    }
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("刷新命中网格", GUILayout.Height(30f)))
                {
                    RefreshHitCollider();
                    SceneView.RepaintAll();
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("蒙版全部设为黑色（不收缩）"))
                {
                    ResetMask(mesh);
                }
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!paintEnabled || targetRenderer == null || EditorApplication.isPlaying)
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

            if (hitCollider == null)
            {
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            bool hasHit = hitCollider.Raycast(ray, out RaycastHit hit, float.MaxValue);
            if (hasHit && current.type == EventType.Repaint)
            {
                Handles.color = current.shift
                    ? new Color(0.2f, 0.6f, 1f, 0.9f)
                    : Color.Lerp(new Color(0.2f, 0.6f, 1f, 0.9f), new Color(1f, 0.2f, 0.1f, 0.9f), brushValue);
                Handles.DrawWireDisc(hit.point, hit.normal, brushRadius);
            }

            bool paintEvent = (current.type == EventType.MouseDown || current.type == EventType.MouseDrag)
                && current.button == 0
                && !current.alt
                && hasHit;
            if (paintEvent)
            {
                if (!strokeActive)
                {
                    BeginStroke();
                }
                PaintAt(hit.point, current.shift ? 0f : brushValue);
                current.Use();
            }

            if (strokeActive && current.rawType == EventType.MouseUp && current.button == 0)
            {
                FinishStroke();
                current.Use();
            }

            if (current.type == EventType.MouseMove || current.type == EventType.MouseDrag)
            {
                sceneView.Repaint();
            }
        }

        private void BeginStroke()
        {
            Mesh mesh = GetTargetMesh();
            if (mesh == null)
            {
                return;
            }

            Undo.IncrementCurrentGroup();
            strokeUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("绘制服装收缩蒙版");
            EnsureUndoState();
            undoState.Capture(mesh, mesh.colors);
            Undo.RegisterCompleteObjectUndo(undoState, "绘制服装收缩蒙版");
            strokeActive = true;
        }

        private void FinishStroke()
        {
            if (!strokeActive)
            {
                return;
            }

            strokeActive = false;
            if (strokeUndoGroup >= 0)
            {
                Undo.CollapseUndoOperations(strokeUndoGroup);
                strokeUndoGroup = -1;
            }
            AssetDatabase.SaveAssets();
        }

        private void PaintAt(Vector3 hitPoint, float targetValue)
        {
            Mesh mesh = GetTargetMesh();
            if (mesh == null || hitVertices.Length != mesh.vertexCount)
            {
                return;
            }

            Color[] colors = mesh.colors;
            if (colors.Length != mesh.vertexCount)
            {
                return;
            }

            bool changed = false;
            for (int index = 0; index < hitVertices.Length; index++)
            {
                Vector3 worldPosition = targetRenderer.transform.TransformPoint(hitVertices[index]);
                float normalizedDistance = Vector3.Distance(worldPosition, hitPoint) / brushRadius;
                if (normalizedDistance >= 1f)
                {
                    continue;
                }

                float current = ClothingShrinkPainterUtility.ReadMask(colors[index]);
                float painted = ClothingShrinkPainterUtility.BlendMask(
                    current, targetValue, normalizedDistance, brushOpacity, brushHardness);
                if (Mathf.Approximately(current, painted))
                {
                    continue;
                }

                colors[index] = ClothingShrinkPainterUtility.WithMask(colors[index], painted);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            mesh.colors = colors;
            undoState.Capture(mesh, colors);
            EditorUtility.SetDirty(mesh);
            Repaint();
            SceneView.RepaintAll();
        }

        private void CreateEditableMeshCopy(Mesh source)
        {
            if (source == null || targetRenderer == null)
            {
                return;
            }

            if (!source.isReadable)
            {
                EditorUtility.DisplayDialog(
                    "无法复制网格",
                    "该模型未开启 Read/Write。请在 FBX Import Settings > Model 中开启 Read/Write 后重试。原 FBX 不会被自动改动。",
                    "知道了");
                return;
            }

            try
            {
                EnsureAssetFolder(GeneratedFolder);
                Mesh copy = Instantiate(source);
                copy.name = source.name + "_ClothingShrinkEditable";
                EnsureVertexColors(copy);

                string fileName = MakeSafeFileName(copy.name) + ".asset";
                string path = AssetDatabase.GenerateUniqueAssetPath(GeneratedFolder + "/" + fileName);
                AssetDatabase.CreateAsset(copy, path);

                if (targetRenderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    Undo.RecordObject(skinnedRenderer, "使用服装收缩 Mesh 副本");
                    skinnedRenderer.sharedMesh = copy;
                    EditorUtility.SetDirty(skinnedRenderer);
                }
                else
                {
                    MeshFilter filter = targetRenderer.GetComponent<MeshFilter>();
                    Undo.RecordObject(filter, "使用服装收缩 Mesh 副本");
                    filter.sharedMesh = copy;
                    EditorUtility.SetDirty(filter);
                }

                AssetDatabase.SaveAssets();
                ResetUndoState(copy);
                RefreshHitCollider();
                EditorGUIUtility.PingObject(copy);
                ShowNotification(new GUIContent("已创建独立 Mesh 副本"));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("复制失败", exception.Message, "关闭");
            }
        }

        private void ResetMask(Mesh mesh)
        {
            if (mesh == null || !EditorUtility.DisplayDialog(
                    "重置收缩蒙版",
                    "将当前可编辑 Mesh 的全部顶点色 RGB 设为黑色。Alpha 会保留，可用 Undo 撤销。",
                    "重置",
                    "取消"))
            {
                return;
            }

            EnsureUndoState();
            undoState.Capture(mesh, mesh.colors);
            Undo.RegisterCompleteObjectUndo(undoState, "重置服装收缩蒙版");
            Color[] colors = mesh.colors;
            if (colors.Length != mesh.vertexCount)
            {
                colors = Enumerable.Repeat(new Color(0f, 0f, 0f, 1f), mesh.vertexCount).ToArray();
            }
            else
            {
                for (int index = 0; index < colors.Length; index++)
                {
                    colors[index] = ClothingShrinkPainterUtility.WithMask(colors[index], 0f);
                }
            }
            mesh.colors = colors;
            undoState.Capture(mesh, colors);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
        }

        private void RefreshHitCollider()
        {
            DestroyHitCollider();
            Mesh source = GetTargetMesh();
            if (targetRenderer == null || source == null || !source.isReadable)
            {
                return;
            }

            hitObject = new GameObject("AIBuilder Clothing Shrink Paint Hit")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            hitObject.transform.SetParent(targetRenderer.transform, false);
            hitCollider = hitObject.AddComponent<MeshCollider>();

            if (targetRenderer is SkinnedMeshRenderer skinnedRenderer)
            {
                bakedHitMesh = new Mesh
                {
                    name = "AIBuilder Clothing Shrink Baked Hit",
                    hideFlags = HideFlags.HideAndDontSave
                };
                skinnedRenderer.BakeMesh(bakedHitMesh);
                hitCollider.sharedMesh = bakedHitMesh;
                hitVertices = bakedHitMesh.vertices;
            }
            else
            {
                hitCollider.sharedMesh = source;
                hitVertices = source.vertices;
            }
        }

        private void DestroyHitCollider()
        {
            hitVertices = Array.Empty<Vector3>();
            hitCollider = null;
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

        private void SetTargetRenderer(Renderer renderer)
        {
            if (targetRenderer == renderer)
            {
                return;
            }

            FinishStroke();
            paintEnabled = false;
            targetRenderer = renderer;
            ResetUndoState(GetTargetMesh());
            RefreshHitCollider();
            Repaint();
            SceneView.RepaintAll();
        }

        private bool TryValidateRenderer(out string error)
        {
            if (targetRenderer == null)
            {
                error = "请选择衣服或裤子对象上的 SkinnedMeshRenderer / MeshRenderer。";
                return false;
            }
            if (targetRenderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            {
                error = string.Empty;
                return true;
            }
            if (targetRenderer is MeshRenderer && targetRenderer.GetComponent<MeshFilter>()?.sharedMesh != null)
            {
                error = string.Empty;
                return true;
            }

            error = "所选 Renderer 没有可用 Mesh。";
            return false;
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

            undoState = CreateInstance<ClothingShrinkUndoState>();
            undoState.hideFlags = HideFlags.HideAndDontSave;
        }

        private void ResetUndoState(Mesh mesh)
        {
            EnsureUndoState();
            Undo.ClearUndo(undoState);
            undoState.Capture(mesh, mesh != null && mesh.isReadable ? mesh.colors : Array.Empty<Color>());
        }

        private static bool IsEditableMesh(Mesh mesh, out string error)
        {
            if (mesh == null)
            {
                error = "没有可编辑 Mesh。";
                return false;
            }
            if (!mesh.isReadable)
            {
                error = "Mesh 未开启 Read/Write，无法读取和写入顶点色。";
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(mesh);
            if (!string.IsNullOrEmpty(assetPath)
                && !string.Equals(Path.GetExtension(assetPath), ".asset", StringComparison.OrdinalIgnoreCase))
            {
                error = "当前 Mesh 来自 FBX/OBJ。请先创建独立 .asset 副本，避免修改导入模型。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static void EnsureVertexColors(Mesh mesh)
        {
            Color[] colors = mesh.colors;
            if (colors.Length == mesh.vertexCount)
            {
                return;
            }
            mesh.colors = Enumerable.Repeat(new Color(0f, 0f, 0f, 1f), mesh.vertexCount).ToArray();
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
            return value;
        }
    }
}
