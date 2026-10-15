using System;
using System.Collections.Generic;
using System.IO;
using AIBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

namespace AIBuilder.EditorTools
{
    public sealed class AIProBuilderModelBuildOptions
    {
        public bool ReplaceExisting;
        public bool SavePrefab;
        public bool PersistMaterials;
        public bool OverwriteExistingPrefab;
        public bool SelectGeneratedRoot = true;
        public string OutputFolder = "Assets/AIBuilder/GeneratedModels";
    }

    public static class AIProBuilderModelGenerator
    {
        private const string DataFolder = "Assets/AIBuilder/Data";
        private const string PackageDefaultSpecPath = "Packages/com.xuyuchen.ai-builder/Editor/DefaultSpecs/ProBuilderModel_StylizedCrate.json";

        public static string LastSavedPrefabPath { get; private set; }

        public static void OpenWindow()
        {
            AIProBuilderModelWindow.Open();
        }

        public static void GenerateDefaultModelFromMenu()
        {
            var specAsset = LoadDefaultModelSpec();
            if (specAsset == null)
            {
                EditorUtility.DisplayDialog("AI ProBuilder Model", $"Default model spec not found:\n{PackageDefaultSpecPath}", "OK");
                return;
            }

            GenerateFromTextAsset(specAsset, new AIProBuilderModelBuildOptions());
        }

        public static void CreateTemplateFromSelectedImages()
        {
            var selectedTextures = new List<Texture2D>();
            foreach (var selected in Selection.objects)
            {
                if (selected is Texture2D texture)
                {
                    selectedTextures.Add(texture);
                }
            }

            if (selectedTextures.Count == 0)
            {
                EditorUtility.DisplayDialog("AI ProBuilder Model", "Select one or more reference Texture2D assets in the Project window first.", "OK");
                return;
            }

            EnsureFolder("Assets", "AIBuilder");
            EnsureFolder("Assets/AIBuilder", "Data");

            var spec = CreateTemplateSpec(selectedTextures);
            var outputPath = AssetDatabase.GenerateUniqueAssetPath($"{DataFolder}/{SanitizeFileName(spec.modelName)}.json");
            File.WriteAllText(outputPath, JsonUtility.ToJson(spec, true));
            AssetDatabase.ImportAsset(outputPath);
            AssetDatabase.Refresh();

            var created = AssetDatabase.LoadAssetAtPath<TextAsset>(outputPath);
            Selection.activeObject = created;
            Debug.Log($"AI ProBuilder Model created template spec: {outputPath}");
        }

        public static TextAsset LoadDefaultModelSpec()
        {
            return AssetDatabase.LoadAssetAtPath<TextAsset>(PackageDefaultSpecPath);
        }

        public static GameObject GenerateFromTextAsset(TextAsset specAsset, AIProBuilderModelBuildOptions options)
        {
            if (specAsset == null)
            {
                EditorUtility.DisplayDialog("AI ProBuilder Model", "ModelSpec JSON is missing.", "OK");
                return null;
            }

            ProBuilderModelSpec spec;
            try
            {
                spec = JsonUtility.FromJson<ProBuilderModelSpec>(specAsset.text);
            }
            catch (Exception exception)
            {
                DisplayError($"ModelSpec JSON 解析失败：\n{exception.Message}");
                return null;
            }

            if (spec == null)
            {
                DisplayError($"无法解析 ModelSpec JSON：\n{AssetDatabase.GetAssetPath(specAsset)}");
                return null;
            }

            return Generate(spec, options);
        }

        public static GameObject Generate(ProBuilderModelSpec spec, AIProBuilderModelBuildOptions options)
        {
            LastSavedPrefabPath = null;
            if (spec == null)
            {
                return null;
            }

            if (options == null)
            {
                options = new AIProBuilderModelBuildOptions();
            }

            var outputFolder = string.IsNullOrWhiteSpace(options.OutputFolder) ? spec.outputFolder : options.OutputFolder;
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                outputFolder = "Assets/AIBuilder/GeneratedModels";
            }

            var validation = AIBuilderSpecValidator.ValidateModelSpec(spec, outputFolder);
            if (!validation.IsValid)
            {
                DisplayError(validation.ToDisplayString());
                return null;
            }

            AIBuilderAssetPathUtility.TryNormalizeAssetsFolder(outputFolder, out var normalizedOutputFolder, out _);
            outputFolder = normalizedOutputFolder;

            if (validation.Warnings.Count > 0)
            {
                Debug.LogWarning("AI ProBuilder Model 配置警告：\n" + validation.ToDisplayString());
            }

            var shouldSavePrefab = options.SavePrefab && spec.savePrefab;
            var shouldPersistMaterials = shouldSavePrefab || options.PersistMaterials;
            string prefabPath = null;
            string materialFolder = null;
            var materialPrefix = SanitizeFileName(spec.modelName);
            if (shouldSavePrefab)
            {
                AIBuilderAssetPathUtility.EnsureAssetFolder(outputFolder);
                prefabPath = $"{outputFolder}/{SanitizeFileName(spec.modelName)}.prefab";
                if (!options.OverwriteExistingPrefab)
                {
                    prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);
                }
                materialPrefix = SanitizeFileName(Path.GetFileNameWithoutExtension(prefabPath));
            }

            if (shouldPersistMaterials)
            {
                AIBuilderAssetPathUtility.EnsureAssetFolder(outputFolder);
                materialFolder = outputFolder + "/Materials";
                AIBuilderAssetPathUtility.EnsureAssetFolder(materialFolder);
            }

            var rootName = string.IsNullOrWhiteSpace(spec.generatedRootName)
                ? $"AI_ProBuilder_Model_{SanitizeObjectName(spec.modelName)}"
                : spec.generatedRootName;

            if (options.ReplaceExisting)
            {
                var existing = GameObject.Find(rootName);
                if (existing != null)
                {
                    Undo.DestroyObjectImmediate(existing);
                }
            }
            else if (GameObject.Find(rootName) != null)
            {
                rootName = GameObjectUtility.GetUniqueNameForSibling(null, rootName);
            }

            var root = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(root, "生成 ProBuilder 模型");
            root.transform.position = spec.origin;

            var createdParts = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            var analysisElements = BuildAnalysisElementMap(spec.sceneAnalysis);
            foreach (var part in spec.parts)
            {
                analysisElements.TryGetValue(part.analysisElementId ?? string.Empty, out var analysisElement);
                var partObject = CreatePart(
                    part,
                    analysisElement,
                    spec.camera,
                    spec.referenceFrame,
                    materialPrefix,
                    materialFolder,
                    shouldPersistMaterials,
                    options.OverwriteExistingPrefab);
                if (partObject == null)
                {
                    continue;
                }

                partObject.transform.SetParent(root.transform, false);
                createdParts[GetPartKey(part)] = partObject;
            }

            foreach (var part in spec.parts)
            {
                var partKey = GetPartKey(part);
                if (string.IsNullOrWhiteSpace(part.parentName) ||
                    !createdParts.TryGetValue(partKey, out var child) ||
                    !createdParts.TryGetValue(part.parentName, out var parent))
                {
                    continue;
                }

                child.transform.SetParent(parent.transform, false);
            }

            CreateReferenceCamera(spec.camera, spec.referenceFrame, root.transform);

            if (options.SelectGeneratedRoot)
            {
                Selection.activeGameObject = root;
                SceneView.lastActiveSceneView?.FrameSelected();
            }

            if (shouldSavePrefab)
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
                AssetDatabase.SaveAssets();
                LastSavedPrefabPath = prefabPath;
                Debug.Log($"AI ProBuilder Model 已保存 prefab：{prefabPath}");
            }
            else
            {
                Debug.Log("AI ProBuilder Model 预览完成：未写入 prefab 或材质资产。");
            }

            Debug.Log($"AI ProBuilder Model generated {createdParts.Count} editable ProBuilder parts for '{spec.modelName}'.");
            return root;
        }

        private static void CreateReferenceCamera(
            ProBuilderModelCameraSpec cameraSpec,
            ProBuilderReferenceFrameSpec referenceFrame,
            Transform root)
        {
            if (cameraSpec == null || !cameraSpec.enabled)
            {
                return;
            }

            var direction = cameraSpec.lookAt - cameraSpec.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.forward;
            }

            var cameraObject = new GameObject("AIBuilder_ReferenceCamera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "生成白盒参考相机");
            cameraObject.transform.SetParent(root, false);
            cameraObject.transform.localPosition = cameraSpec.position;
            cameraObject.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = Mathf.Clamp(cameraSpec.fieldOfView, 10f, 120f);
            if (referenceFrame != null && referenceFrame.enabled && referenceFrame.width > 0 && referenceFrame.height > 0)
            {
                camera.aspect = referenceFrame.width / (float)referenceFrame.height;
            }
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1000f;
        }

        private static GameObject CreatePart(
            ProBuilderModelPartSpec part,
            ProBuilderSceneElementSpec analysisElement,
            ProBuilderModelCameraSpec cameraSpec,
            ProBuilderReferenceFrameSpec referenceFrame,
            string materialPrefix,
            string materialFolder,
            bool persistMaterial,
            bool overwriteMaterial)
        {
            if (part == null)
            {
                return null;
            }

            var partName = string.IsNullOrWhiteSpace(part.name) ? "Part" : SanitizeObjectName(part.name);
            var shape = NormalizeShape(part.shape);

            if (shape == "empty" || shape == "group")
            {
                var empty = new GameObject(partName);
                ApplyTransform(empty.transform, part);
                return empty;
            }

            var mesh = shape == "screen_polygon"
                ? CreateScreenPolygon(analysisElement, cameraSpec, referenceFrame)
                : CreatePrimitive(shape, part);
            if (mesh == null)
            {
                Debug.LogWarning($"AI ProBuilder Model skipped unsupported shape '{part.shape}' on part '{partName}'.");
                return null;
            }

            var gameObject = mesh.gameObject;
            gameObject.name = partName;
            if (shape != "screen_polygon")
            {
                ApplyTransform(gameObject.transform, part);
            }
            ApplyMaterial(gameObject, part, materialPrefix, materialFolder, persistMaterial, overwriteMaterial);

            if (part.addCollider && gameObject.GetComponent<Collider>() == null)
            {
                gameObject.AddComponent<MeshCollider>();
            }

            mesh.ToMesh();
            mesh.Refresh();
            EditorUtility.SetDirty(mesh);
            return gameObject;
        }

        private static Dictionary<string, ProBuilderSceneElementSpec> BuildAnalysisElementMap(
            ProBuilderSceneAnalysisSpec analysis)
        {
            var result = new Dictionary<string, ProBuilderSceneElementSpec>(StringComparer.OrdinalIgnoreCase);
            if (analysis?.enabled != true || analysis.elements == null)
            {
                return result;
            }

            foreach (var element in analysis.elements)
            {
                if (element != null && !string.IsNullOrWhiteSpace(element.id))
                {
                    result[element.id.Trim()] = element;
                }
            }

            return result;
        }

        private static ProBuilderMesh CreateScreenPolygon(
            ProBuilderSceneElementSpec element,
            ProBuilderModelCameraSpec cameraSpec,
            ProBuilderReferenceFrameSpec referenceFrame)
        {
            if (element?.generateFromScreen != true || element.screenPolygon == null || element.screenPolygon.Count < 3)
            {
                return null;
            }

            var cameraPosition = cameraSpec?.position ?? new Vector3(0f, 3f, -10f);
            var lookAt = cameraSpec?.lookAt ?? Vector3.zero;
            var cameraForward = lookAt - cameraPosition;
            if (cameraForward.sqrMagnitude < 0.0001f)
            {
                cameraForward = Vector3.forward;
            }
            cameraForward.Normalize();

            var cameraRotation = Quaternion.LookRotation(cameraForward, Vector3.up);
            var cameraRight = cameraRotation * Vector3.right;
            var cameraUp = cameraRotation * Vector3.up;
            var polygonRotation = Quaternion.LookRotation(cameraUp, cameraForward);
            var aspect = referenceFrame != null && referenceFrame.enabled && referenceFrame.width > 0 && referenceFrame.height > 0
                ? referenceFrame.width / (float)referenceFrame.height
                : 16f / 9f;
            var fieldOfView = Mathf.Clamp(cameraSpec?.fieldOfView ?? 50f, 10f, 120f);
            var depth = Mathf.Max(0.1f, element.depthMeters);
            var verticalSpan = 2f * depth * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            var horizontalSpan = verticalSpan * aspect;

            var minX = 1f;
            var maxX = 0f;
            var minY = 1f;
            var maxY = 0f;
            foreach (var point in element.screenPolygon)
            {
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            var centerX = (minX + maxX) * 0.5f;
            var centerY = (minY + maxY) * 0.5f;
            var center = cameraPosition +
                         cameraForward * depth +
                         cameraRight * ((centerX - 0.5f) * horizontalSpan) +
                         cameraUp * ((0.5f - centerY) * verticalSpan);
            var points = new List<Vector3>(element.screenPolygon.Count);
            foreach (var point in element.screenPolygon)
            {
                points.Add(new Vector3(
                    (point.x - centerX) * horizontalSpan,
                    0f,
                    (centerY - point.y) * verticalSpan));
            }

            var mesh = ProBuilderMesh.Create();
            mesh.transform.localPosition = center;
            mesh.transform.localRotation = polygonRotation;
            var result = mesh.CreateShapeFromPolygon(points, Mathf.Max(0.02f, element.thicknessMeters), true);
            if (result.status == ActionResult.Status.Failure || result.status == ActionResult.Status.NoChange)
            {
                UnityEngine.Object.DestroyImmediate(mesh.gameObject);
                return null;
            }

            return mesh;
        }

        private static ProBuilderMesh CreatePrimitive(string shape, ProBuilderModelPartSpec part)
        {
            switch (shape)
            {
                case "box":
                case "cube":
                    return ShapeGenerator.GenerateCube(PivotLocation.Center, Vector3.one);
                case "cylinder":
                    return ShapeGenerator.GenerateCylinder(PivotLocation.Center, ClampEven(part.segments, 4, 64), 0.5f, 1f, Mathf.Max(0, part.heightCuts), part.smooth ? 1 : -1);
                case "cone":
                    return ShapeGenerator.GenerateCone(PivotLocation.Center, 0.5f, 1f, Mathf.Clamp(part.segments, 4, 64));
                case "sphere":
                case "icosphere":
                    return ShapeGenerator.GenerateIcosahedron(PivotLocation.Center, 0.5f, Mathf.Clamp(part.subdivisions, 0, 3), true, false);
                case "plane":
                case "quad":
                    return ShapeGenerator.GeneratePlane(PivotLocation.Center, 1f, 1f, 0, 0, Axis.Up);
                case "prism":
                case "wedge":
                case "roof":
                    return ShapeGenerator.GeneratePrism(PivotLocation.Center, Vector3.one);
                case "pipe":
                case "ring":
                    return ShapeGenerator.GeneratePipe(PivotLocation.Center, 0.5f, 1f, Mathf.Clamp(part.thickness, 0.02f, 0.45f), ClampEven(part.segments, 6, 64), Mathf.Max(0, part.heightCuts + 1));
                case "torus":
                    return ShapeGenerator.GenerateTorus(PivotLocation.Center, Mathf.Clamp(part.segments, 6, 64), Mathf.Clamp(part.segments, 6, 64), 0.35f, 0.15f, part.smooth, 360f, 360f, false);
                case "arch":
                    return ShapeGenerator.GenerateArch(PivotLocation.Center, Mathf.Clamp(part.archAngle, 10f, 360f), 0.5f, 0.25f, 0.25f, Mathf.Clamp(part.segments, 4, 32), true, true, true, true, true);
                case "stair":
                case "stairs":
                    return ShapeGenerator.GenerateStair(PivotLocation.Center, Vector3.one, Mathf.Clamp(part.segments, 2, 24), true);
                default:
                    return null;
            }
        }

        private static void ApplyTransform(Transform transform, ProBuilderModelPartSpec part)
        {
            transform.localPosition = part.position;
            transform.localEulerAngles = part.rotation;
            transform.localScale = SafeScale(part.size);
        }

        private static void ApplyMaterial(
            GameObject gameObject,
            ProBuilderModelPartSpec part,
            string materialPrefix,
            string materialFolder,
            bool persistMaterial,
            bool overwriteMaterial)
        {
            var renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                return;
            }

            var materialName = string.IsNullOrWhiteSpace(part.materialName)
                ? $"AI_PB_{SanitizeFileName(part.name)}"
                : SanitizeFileName(part.materialName);

            renderer.sharedMaterial = CreateMaterial(
                materialPrefix + "_" + materialName,
                materialFolder,
                part.color,
                part.emissionColor,
                part.emissionIntensity,
                string.Equals(NormalizeShape(part.shape), "screen_polygon", StringComparison.Ordinal),
                persistMaterial,
                overwriteMaterial);
        }

        private static Material CreateMaterial(
            string name,
            string materialFolder,
            Color baseColor,
            Color emissionColor,
            float emissionIntensity,
            bool unlit,
            bool persist,
            bool overwrite)
        {
            Material material = null;
            string path = null;
            if (persist)
            {
                path = $"{materialFolder}/{name}.mat";
                if (overwrite)
                {
                    material = AssetDatabase.LoadAssetAtPath<Material>(path);
                }
                else
                {
                    path = AssetDatabase.GenerateUniqueAssetPath(path);
                }
            }

            if (material == null)
            {
                var shader = unlit
                    ? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard")
                    : Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                {
                    throw new InvalidOperationException("找不到可用的 Lit 或 Standard Shader。");
                }

                material = new Material(shader) { name = name };
                if (persist)
                {
                    AssetDatabase.CreateAsset(material, path);
                }
                else
                {
                    material.hideFlags = HideFlags.HideAndDontSave;
                }
            }

            SetMaterialColor(material, "_BaseColor", baseColor);
            SetMaterialColor(material, "_Color", baseColor);
            if (unlit && material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.2f);
            }

            if (emissionIntensity > 0f)
            {
                material.EnableKeyword("_EMISSION");
                SetMaterialColor(material, "_EmissionColor", emissionColor * emissionIntensity);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }

            if (persist)
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static ProBuilderModelSpec CreateTemplateSpec(IReadOnlyList<Texture2D> textures)
        {
            var spec = new ProBuilderModelSpec
            {
                blockoutType = AIBlockoutSpecRepository.ObjectBlockout,
                displayName = "参考图物体白盒",
                modelName = "ReferenceModel_FromImages",
                generatedRootName = "AI_ProBuilder_Model_Reference",
                generatorVersion = "model_v2",
                sourceMode = "visual_analysis",
                visualNotes = new[]
                {
                    "A planner or artist fills this after reading the supplied reference images.",
                    "Describe silhouette, proportions, style language, surface detail, and visible material regions."
                },
                modelingNotes = new[]
                {
                    "Use ProBuilder primitives first. Split the model into editable named parts.",
                    "If a detail is not visible from one view, infer conservatively and mark it in notes."
                }
            };

            for (var i = 0; i < textures.Count; i++)
            {
                var texture = textures[i];
                spec.referenceImages.Add(new ProBuilderModelReferenceImageSpec
                {
                    assetPath = AssetDatabase.GetAssetPath(texture),
                    view = i == 0 ? "primary" : "additional",
                    width = texture.width,
                    height = texture.height,
                    notes = "Reference image for visual analysis."
                });
            }

            spec.parts.Add(new ProBuilderModelPartSpec
            {
                name = "placeholder_body",
                shape = "box",
                position = new Vector3(0f, 0.5f, 0f),
                size = new Vector3(1.5f, 1f, 1f),
                color = new Color(0.6f, 0.6f, 0.55f, 1f),
                notes = "Replace this placeholder after the reference is analyzed."
            });

            return spec;
        }

        private static string NormalizeShape(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "box";
            }

            return value.Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
        }

        private static string GetPartKey(ProBuilderModelPartSpec part)
        {
            if (part == null || string.IsNullOrWhiteSpace(part.name))
            {
                return "Part";
            }

            return part.name.Trim();
        }

        private static Vector3 SafeScale(Vector3 size)
        {
            return new Vector3(
                Mathf.Max(0.001f, Mathf.Abs(size.x)),
                Mathf.Max(0.001f, Mathf.Abs(size.y)),
                Mathf.Max(0.001f, Mathf.Abs(size.z)));
        }

        private static int ClampEven(int value, int min, int max)
        {
            var clamped = Mathf.Clamp(value, min, max);
            if (clamped % 2 != 0)
            {
                clamped++;
            }

            return Mathf.Clamp(clamped, min, max);
        }

        private static void SetMaterialColor(Material material, string property, Color color)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, color);
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static string SanitizeObjectName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "GeneratedModel";
            }

            return value.Trim().Replace('/', '_').Replace('\\', '_');
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "GeneratedModel";
            }

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Trim().Replace(' ', '_');
        }

        private static void DisplayError(string message)
        {
            Debug.LogError("AI ProBuilder Model：\n" + message);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("AI ProBuilder Model", message, "确定");
            }
        }
    }
}
