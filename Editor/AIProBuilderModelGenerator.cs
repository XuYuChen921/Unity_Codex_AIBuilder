using System;
using System.Collections.Generic;
using System.IO;
using AIBuilder;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using Object = UnityEngine.Object;

namespace AIBuilder.EditorTools
{
    public sealed class AIProBuilderModelBuildOptions
    {
        public bool ReplaceExisting = true;
        public bool SavePrefab = true;
        public bool SelectGeneratedRoot = true;
        public string OutputFolder = "Assets/AIBuilder/GeneratedModels";
    }

    public static class AIProBuilderModelGenerator
    {
        private const string DataFolder = "Assets/AIBuilder/Data";
        private const string PackageDefaultSpecPath = "Packages/com.xuyuchen.ai-builder/Editor/DefaultSpecs/ProBuilderModel_StylizedCrate.json";

        [MenuItem("AIBuilder/ProBuilder Model From Reference")]
        public static void OpenWindow()
        {
            AIProBuilderModelWindow.Open();
        }

        [MenuItem("AIBuilder/Generate Default ProBuilder Model")]
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

        [MenuItem("AIBuilder/Create ProBuilder ModelSpec Template From Selected Images")]
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

            var spec = JsonUtility.FromJson<ProBuilderModelSpec>(specAsset.text);
            if (spec == null)
            {
                EditorUtility.DisplayDialog("AI ProBuilder Model", $"Could not parse ModelSpec JSON:\n{AssetDatabase.GetAssetPath(specAsset)}", "OK");
                return null;
            }

            return Generate(spec, options);
        }

        public static GameObject Generate(ProBuilderModelSpec spec, AIProBuilderModelBuildOptions options)
        {
            if (spec == null)
            {
                return null;
            }

            if (options == null)
            {
                options = new AIProBuilderModelBuildOptions();
            }

            if (spec.parts == null || spec.parts.Count == 0)
            {
                EditorUtility.DisplayDialog("AI ProBuilder Model", "ModelSpec has no parts. Add at least one ProBuilderModelPartSpec.", "OK");
                return null;
            }

            EnsureFolder("Assets", "AIBuilder");
            EnsureFolder("Assets/AIBuilder", "Materials");

            var rootName = string.IsNullOrWhiteSpace(spec.generatedRootName)
                ? $"AI_ProBuilder_Model_{SanitizeObjectName(spec.modelName)}"
                : spec.generatedRootName;

            if (options.ReplaceExisting)
            {
                var existing = GameObject.Find(rootName);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing);
                }
            }

            var root = new GameObject(rootName);
            root.transform.position = spec.origin;

            var createdParts = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in spec.parts)
            {
                var partObject = CreatePart(part);
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

            if (options.SelectGeneratedRoot)
            {
                Selection.activeGameObject = root;
                SceneView.lastActiveSceneView?.FrameSelected();
            }

            var shouldSavePrefab = options.SavePrefab;
            if (shouldSavePrefab)
            {
                var folder = string.IsNullOrWhiteSpace(options.OutputFolder) ? spec.outputFolder : options.OutputFolder;
                if (string.IsNullOrWhiteSpace(folder))
                {
                    folder = "Assets/AIBuilder/GeneratedModels";
                }

                EnsureFolderPath(folder);
                var path = $"{folder}/{SanitizeFileName(spec.modelName)}.prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
                Debug.Log($"AI ProBuilder Model saved prefab: {path}");
            }

            Debug.Log($"AI ProBuilder Model generated {createdParts.Count} editable ProBuilder parts for '{spec.modelName}'.");
            return root;
        }

        private static GameObject CreatePart(ProBuilderModelPartSpec part)
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

            var mesh = CreatePrimitive(shape, part);
            if (mesh == null)
            {
                Debug.LogWarning($"AI ProBuilder Model skipped unsupported shape '{part.shape}' on part '{partName}'.");
                return null;
            }

            var gameObject = mesh.gameObject;
            gameObject.name = partName;
            ApplyTransform(gameObject.transform, part);
            ApplyMaterial(gameObject, part);

            if (part.addCollider && gameObject.GetComponent<Collider>() == null)
            {
                gameObject.AddComponent<MeshCollider>();
            }

            mesh.ToMesh();
            mesh.Refresh();
            EditorUtility.SetDirty(mesh);
            return gameObject;
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

        private static void ApplyMaterial(GameObject gameObject, ProBuilderModelPartSpec part)
        {
            var renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                return;
            }

            var materialName = string.IsNullOrWhiteSpace(part.materialName)
                ? $"AI_PB_{SanitizeFileName(part.name)}"
                : SanitizeFileName(part.materialName);

            renderer.sharedMaterial = CreateMaterial(materialName, part.color, part.emissionColor, part.emissionIntensity);
        }

        private static Material CreateMaterial(string name, Color baseColor, Color emissionColor, float emissionIntensity)
        {
            EnsureFolder("Assets", "AIBuilder");
            EnsureFolder("Assets/AIBuilder", "Materials");

            var path = $"Assets/AIBuilder/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            SetMaterialColor(material, "_BaseColor", baseColor);
            SetMaterialColor(material, "_Color", baseColor);

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

            EditorUtility.SetDirty(material);
            return material;
        }

        private static ProBuilderModelSpec CreateTemplateSpec(IReadOnlyList<Texture2D> textures)
        {
            var spec = new ProBuilderModelSpec
            {
                modelName = "ReferenceModel_FromImages",
                generatedRootName = "AI_ProBuilder_Model_Reference",
                generatorVersion = "model_v1",
                sourceMode = "codex_visual_analysis",
                visualNotes = new[]
                {
                    "Codex fills this after reading the supplied reference images.",
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
                    notes = "Reference image for Codex visual analysis."
                });
            }

            spec.parts.Add(new ProBuilderModelPartSpec
            {
                name = "placeholder_body",
                shape = "box",
                position = new Vector3(0f, 0.5f, 0f),
                size = new Vector3(1.5f, 1f, 1f),
                color = new Color(0.6f, 0.6f, 0.55f, 1f),
                notes = "Replace this placeholder after Codex analyzes the reference."
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

        private static void EnsureFolderPath(string folderPath)
        {
            folderPath = folderPath.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrWhiteSpace(folderPath) ||
                (folderPath != "Assets" && !folderPath.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                throw new ArgumentException("Output folder must be under Assets.", nameof(folderPath));
            }

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                EnsureFolder(current, parts[i]);
                current = $"{current}/{parts[i]}";
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
    }
}
