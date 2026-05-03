using System;
using System.Collections.Generic;
using System.IO;
using AIBuilder;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AIBuilder.EditorTools
{
    public sealed class AIReferenceSceneBuildOptions
    {
        public bool UseProjectAssets = true;
        public bool SaveScene = true;
        public bool CreateAssetReport = true;
        public int SeedOffset;
    }

    public static class AIReferenceSceneGenerator
    {
        public const string DefaultSpecPath = "Assets/AIBuilder/Data/SceneSpec.json";

        private const string MaterialFolder = "Assets/AIBuilder/Materials";
        private const string GeneratedFolder = "Assets/AIBuilder/Generated";
        private const string SceneFolder = "Assets/AIBuilder/Scenes";
        private const string AssetReportPath = "Assets/AIBuilder/Generated/AssetMatchReport.md";

        public static TextAsset LoadDefaultSceneSpec()
        {
            return AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultSpecPath);
        }

        public static void GenerateFromTextAsset(TextAsset textAsset, AIReferenceSceneBuildOptions options)
        {
            if (textAsset == null)
            {
                EditorUtility.DisplayDialog("AI Scene Builder", "Select a SceneSpec JSON first.", "OK");
                return;
            }

            var spec = JsonUtility.FromJson<SceneSpec>(textAsset.text);
            if (spec == null)
            {
                EditorUtility.DisplayDialog("AI Scene Builder", "Could not parse SceneSpec JSON.", "OK");
                return;
            }

            Generate(spec, options);
        }

        public static void Generate(SceneSpec spec, AIReferenceSceneBuildOptions options)
        {
            options ??= new AIReferenceSceneBuildOptions();
            spec.seed += options.SeedOffset;

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureProjectFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = spec.sceneName;

            var random = new System.Random(spec.seed);
            var materials = new MaterialPalette(spec);
            var assetLibrary = new ProjectAssetLibrary(options.UseProjectAssets);

            var root = new GameObject(spec.generatedRootName);
            root.transform.position = Vector3.zero;

            BuildLighting(spec, root.transform);
            BuildCamera(spec, root.transform);
            BuildGround(spec, root.transform, materials, random);
            var features = spec.features ?? new SceneFeatureSpec();
            if (features.centralFeature)
            {
                BuildCentralFeature(spec, root.transform, materials, assetLibrary, random);
            }

            if (features.magicPools)
            {
                BuildMagicPools(root.transform, materials, spec, random);
            }

            BuildScatterGroups(spec, root.transform, materials, assetLibrary, random);
            if (features.heroForegroundTrees)
            {
                BuildHeroForegroundTrees(root.transform, materials, assetLibrary, random);
            }

            if (features.clouds)
            {
                BuildClouds(root.transform, materials, random);
            }

            if (features.postProcessing)
            {
                BuildPostProcessing(spec, root.transform);
            }

            Selection.activeGameObject = root;
            EditorUtility.SetDirty(root);

            if (options.SaveScene)
            {
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), GetGeneratedScenePath(spec));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (options.CreateAssetReport)
            {
                assetLibrary.WriteReport(AssetReportPath);
            }

            Debug.Log($"AI Scene Builder generated '{spec.sceneName}' at {GetGeneratedScenePath(spec)}");
        }

        private static void EnsureProjectFolders()
        {
            EnsureFolder("Assets", "AIBuilder");
            EnsureFolder("Assets/AIBuilder", "Runtime");
            EnsureFolder("Assets/AIBuilder", "Editor");
            EnsureFolder("Assets/AIBuilder", "Data");
            EnsureFolder("Assets/AIBuilder", "Materials");
            EnsureFolder("Assets/AIBuilder", "Generated");
            EnsureFolder("Assets/AIBuilder", "Scenes");
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void BuildLighting(SceneSpec spec, Transform root)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = spec.lighting.ambientSkyColor;
            RenderSettings.ambientEquatorColor = spec.lighting.ambientEquatorColor;
            RenderSettings.ambientGroundColor = spec.lighting.ambientGroundColor;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = spec.lighting.fogColor;
            RenderSettings.fogDensity = spec.lighting.fogDensity;

            var lightObject = new GameObject("Warm_Left_Sun");
            lightObject.transform.SetParent(root);
            lightObject.transform.rotation = Quaternion.Euler(spec.lighting.sunEuler);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = spec.lighting.sunColor;
            light.intensity = spec.lighting.sunIntensity;
            light.shadows = LightShadows.Soft;
        }

        private static void BuildCamera(SceneSpec spec, Transform root)
        {
            var cameraObject = new GameObject("Reference_Composition_Camera");
            cameraObject.transform.SetParent(root);
            cameraObject.transform.position = spec.camera.position;
            cameraObject.transform.LookAt(spec.camera.lookAt);

            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = spec.camera.fieldOfView;
            camera.nearClipPlane = spec.camera.nearClipPlane;
            camera.farClipPlane = spec.camera.farClipPlane;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            camera.tag = "MainCamera";

            var urpCamera = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            urpCamera.renderPostProcessing = true;
        }

        private static void BuildGround(SceneSpec spec, Transform root, MaterialPalette materials, System.Random random)
        {
            var groundMesh = MeshFactory.CreatePlane(spec.terrain.size.x, spec.terrain.size.y);
            var ground = CreateMeshObject(
                "LowPoly_Grass_Clearing",
                groundMesh,
                materials.GrassBase,
                root,
                new Vector3(0f, spec.terrain.groundYOffset, 0f),
                Quaternion.identity,
                Vector3.one);
            ground.AddComponent<MeshCollider>();

            var patchRoot = new GameObject("Grass_Color_Patches");
            patchRoot.transform.SetParent(root);

            for (var i = 0; i < spec.terrain.grassPatchCount; i++)
            {
                var position = SampleOpenClearingPosition(spec, random);
                var radiusX = RandomRange(random, 0.7f, 2.5f);
                var radiusZ = RandomRange(random, 0.45f, 1.65f);
                var mesh = MeshFactory.CreateIrregularDisc(random, radiusX, radiusZ, RandomInt(random, 5, 8));
                var material = random.NextDouble() > 0.5d ? materials.GrassPatchA : materials.GrassPatchB;
                CreateMeshObject(
                    $"Grass_Patch_{i:000}",
                    mesh,
                    material,
                    patchRoot.transform,
                    new Vector3(position.x, 0.018f, position.z),
                    Quaternion.Euler(0f, RandomRange(random, 0f, 360f), 0f),
                    Vector3.one);
            }
        }

        private static void BuildCentralFeature(SceneSpec spec, Transform root, MaterialPalette materials, ProjectAssetLibrary assetLibrary, System.Random random)
        {
            var featureRoot = new GameObject("Central_Stone_Altar_And_Heart");
            featureRoot.transform.SetParent(root);
            featureRoot.transform.position = spec.centralFeature.position;

            var altarPrefab = assetLibrary.FindPrefab(new[] { "altar", "stone_slab", "stone slab" }, "altar", random);
            if (altarPrefab != null)
            {
                PlacePrefabInstance(
                    altarPrefab,
                    featureRoot.transform,
                    $"Central_Altar_{altarPrefab.name}",
                    Vector3.zero,
                    Quaternion.identity,
                    Mathf.Max(0.75f, spec.centralFeature.altarHeight * 1.8f),
                    1f,
                    assetLibrary,
                    "altar");
            }
            else
            {
                var baseCylinder = CreateMeshObject(
                    "Round_Stone_Platform",
                    MeshFactory.CreateCylinder(24, spec.centralFeature.altarRadius, spec.centralFeature.altarHeight),
                    materials.StoneWarm,
                    featureRoot.transform,
                    new Vector3(0f, spec.centralFeature.altarHeight * 0.5f, 0f),
                    Quaternion.identity,
                    Vector3.one);
                baseCylinder.AddComponent<MeshCollider>();

                CreateMeshObject(
                    "Mossy_Top_Disc",
                    MeshFactory.CreateCylinder(24, spec.centralFeature.altarRadius * 0.82f, 0.08f),
                    materials.Moss,
                    featureRoot.transform,
                    new Vector3(0f, spec.centralFeature.altarHeight + 0.055f, 0f),
                    Quaternion.identity,
                    Vector3.one);
            }

            const int ringStoneCount = 18;
            for (var i = 0; i < ringStoneCount; i++)
            {
                var angle = Mathf.PI * 2f * i / ringStoneCount + RandomRange(random, -0.08f, 0.08f);
                var radius = spec.centralFeature.altarRadius + RandomRange(random, -0.12f, 0.28f);
                var localPosition = new Vector3(Mathf.Cos(angle) * radius, 0.28f, Mathf.Sin(angle) * radius);
                var scale = new Vector3(RandomRange(random, 0.55f, 1.1f), RandomRange(random, 0.35f, 0.75f), RandomRange(random, 0.5f, 0.95f));
                var ringStonePrefab = assetLibrary.FindPrefab(new[] { "rock", "stone" }, "rock", random);
                if (ringStonePrefab != null)
                {
                    PlacePrefabInstance(
                        ringStonePrefab,
                        featureRoot.transform,
                        $"Ring_Stone_{i:00}_{ringStonePrefab.name}",
                        localPosition,
                        Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f),
                        RandomRange(random, 0.35f, 0.75f),
                        1f,
                        assetLibrary,
                        "rock");
                }
                else
                {
                    CreateMeshObject(
                        $"Ring_Stone_{i:00}",
                        MeshFactory.CreateRock(random),
                        random.NextDouble() > 0.5d ? materials.StoneWarm : materials.StoneCool,
                        featureRoot.transform,
                        localPosition,
                        Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f),
                        scale);
                }
            }

            var heart = CreateMeshObject(
                "Floating_Glowing_Heart",
                MeshFactory.CreateHeart(32, 0.22f),
                materials.MagicHeart,
                featureRoot.transform,
                new Vector3(0f, spec.centralFeature.heartHeight, 0f),
                Quaternion.Euler(0f, 180f, 0f),
                new Vector3(0.16f, 0.16f, 0.16f));
            heart.AddComponent<RotateAroundSelf>().Speed = 18f;

            var heartLightObject = new GameObject("Heart_Green_Point_Light");
            heartLightObject.transform.SetParent(featureRoot.transform);
            heartLightObject.transform.localPosition = new Vector3(0f, spec.centralFeature.heartHeight, 0f);
            var heartLight = heartLightObject.AddComponent<Light>();
            heartLight.type = LightType.Point;
            heartLight.color = spec.centralFeature.glowColor;
            heartLight.intensity = spec.centralFeature.glowIntensity;
            heartLight.range = 8.5f;
            heartLight.shadows = LightShadows.None;

            CreateSparkleSystem(
                "Heart_Sparkles",
                featureRoot.transform,
                new Vector3(0f, spec.centralFeature.heartHeight, 0f),
                spec.centralFeature.glowColor,
                0.95f,
                22f);
        }

        private static void BuildMagicPools(Transform root, MaterialPalette materials, SceneSpec spec, System.Random random)
        {
            var poolRoot = new GameObject("Foreground_Magic_Pools");
            poolRoot.transform.SetParent(root);

            var positions = new[]
            {
                new Vector3(-15.5f, 0.035f, -10.8f),
                new Vector3(15.5f, 0.035f, -10.4f)
            };

            for (var i = 0; i < positions.Length; i++)
            {
                var pool = CreateMeshObject(
                    $"Magic_Glow_Pool_{i + 1}",
                    MeshFactory.CreateIrregularDisc(random, 1.4f, 0.7f, 14),
                    materials.MagicPool,
                    poolRoot.transform,
                    positions[i],
                    Quaternion.Euler(0f, RandomRange(random, 0f, 360f), 0f),
                    Vector3.one);

                var lightObject = new GameObject($"Magic_Pool_Light_{i + 1}");
                lightObject.transform.SetParent(pool.transform);
                lightObject.transform.localPosition = Vector3.up * 0.2f;
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = spec.centralFeature.glowColor;
                light.intensity = 1.9f;
                light.range = 4.2f;

                CreateSparkleSystem(
                    $"Magic_Pool_Sparkles_{i + 1}",
                    poolRoot.transform,
                    positions[i] + Vector3.up * 0.25f,
                    spec.centralFeature.glowColor,
                    0.65f,
                    14f);
            }
        }

        private static void BuildScatterGroups(
            SceneSpec spec,
            Transform root,
            MaterialPalette materials,
            ProjectAssetLibrary assetLibrary,
            System.Random random)
        {
            foreach (var group in spec.scatterGroups)
            {
                var groupRoot = new GameObject(group.id);
                groupRoot.transform.SetParent(root);
                for (var i = 0; i < group.count; i++)
                {
                    var position = SampleScatterPosition(spec, group, random);
                    var rotation = Quaternion.Euler(0f, RandomRange(random, 0f, 360f), 0f);
                    var scale = RandomRange(random, group.scaleRange.x, group.scaleRange.y);
                    var prefab = assetLibrary.FindPrefab(group.assetKeywords, group.type, random);

                    if (prefab != null)
                    {
                        PlacePrefabInstance(
                            prefab,
                            groupRoot.transform,
                            $"{group.type}_{i:000}_{prefab.name}",
                            position,
                            rotation,
                            GetTargetPrefabHeight(group.type, scale),
                            1f,
                            assetLibrary,
                            group.type);
                        continue;
                    }

                    CreateFallbackObject(group.type, groupRoot.transform, position, rotation, scale, materials, random);
                }
            }
        }

        private static void BuildHeroForegroundTrees(Transform root, MaterialPalette materials, ProjectAssetLibrary assetLibrary, System.Random random)
        {
            var heroRoot = new GameObject("Foreground_Frame_Trees");
            heroRoot.transform.SetParent(root);
            var heroTreePrefab = assetLibrary.FindPrefab(new[] { "big tree", "tree" }, "hero_tree", random);

            if (heroTreePrefab != null)
            {
                PlacePrefabInstance(
                    heroTreePrefab,
                    heroRoot.transform,
                    $"Left_Hero_{heroTreePrefab.name}",
                    new Vector3(-18.6f, 0f, -8.8f),
                    Quaternion.Euler(0f, 18f, 0f),
                    7.0f,
                    1f,
                    assetLibrary,
                    "hero_tree");

                heroTreePrefab = assetLibrary.FindPrefab(new[] { "big tree", "tree" }, "hero_tree", random);
                PlacePrefabInstance(
                    heroTreePrefab,
                    heroRoot.transform,
                    $"Right_Hero_{heroTreePrefab.name}",
                    new Vector3(18.2f, 0f, -7.8f),
                    Quaternion.Euler(0f, -22f, 0f),
                    6.4f,
                    1f,
                    assetLibrary,
                    "hero_tree");
                return;
            }

            CreateBroadleafTree(
                heroRoot.transform,
                new Vector3(-18.6f, 0f, -8.8f),
                Quaternion.Euler(0f, 18f, 0f),
                2.25f,
                materials,
                random,
                "Left_Hero_Broadleaf");

            CreateBroadleafTree(
                heroRoot.transform,
                new Vector3(18.2f, 0f, -7.8f),
                Quaternion.Euler(0f, -22f, 0f),
                2.05f,
                materials,
                random,
                "Right_Hero_Broadleaf");
        }

        private static void BuildClouds(Transform root, MaterialPalette materials, System.Random random)
        {
            var cloudRoot = new GameObject("Soft_Background_Clouds");
            cloudRoot.transform.SetParent(root);

            var positions = new[]
            {
                new Vector3(-12f, 10.8f, 24f),
                new Vector3(-4f, 12.2f, 30f),
                new Vector3(8f, 11.4f, 25f),
                new Vector3(17f, 12.8f, 31f)
            };

            for (var i = 0; i < positions.Length; i++)
            {
                var cloud = new GameObject($"Cloud_{i + 1}");
                cloud.transform.SetParent(cloudRoot.transform);
                cloud.transform.position = positions[i];

                var puffCount = RandomInt(random, 3, 6);
                for (var j = 0; j < puffCount; j++)
                {
                    CreateMeshObject(
                        $"Puff_{j}",
                        MeshFactory.CreateLowPolyBlob(random, 7, 3),
                        materials.Cloud,
                        cloud.transform,
                        new Vector3(RandomRange(random, -1.7f, 1.7f), RandomRange(random, -0.25f, 0.25f), RandomRange(random, -0.2f, 0.2f)),
                        Quaternion.Euler(0f, RandomRange(random, 0f, 360f), 0f),
                        new Vector3(RandomRange(random, 1.5f, 3.2f), RandomRange(random, 0.28f, 0.55f), RandomRange(random, 0.55f, 1.05f)));
                }
            }
        }

        private static void BuildPostProcessing(SceneSpec spec, Transform root)
        {
            var volumeProfilePath = GetVolumeProfilePath(spec);
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(volumeProfilePath) != null)
            {
                AssetDatabase.DeleteAsset(volumeProfilePath);
            }

            var volumeObject = new GameObject("URP_Global_Volume");
            volumeObject.transform.SetParent(root);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = $"{SanitizeFileName(spec.sceneName)}_VolumeProfile";

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(spec.lighting.bloomIntensity);
            bloom.threshold.Override(0.78f);
            bloom.scatter.Override(0.7f);

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(18f);
            color.contrast.Override(8f);
            color.postExposure.Override(0.05f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.14f);
            vignette.smoothness.Override(0.55f);

            AssetDatabase.CreateAsset(profile, volumeProfilePath);
            volume.sharedProfile = profile;
        }

        private static string GetGeneratedScenePath(SceneSpec spec)
        {
            return $"{SceneFolder}/{SanitizeFileName(spec.sceneName)}_Generated.unity";
        }

        private static string GetVolumeProfilePath(SceneSpec spec)
        {
            return $"{GeneratedFolder}/{SanitizeFileName(spec.sceneName)}_VolumeProfile.asset";
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "GeneratedScene";
            }

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Trim().Replace(' ', '_');
        }

        private static Vector3 SampleOpenClearingPosition(SceneSpec spec, System.Random random)
        {
            var halfWidth = spec.terrain.size.x * 0.37f;
            var halfDepth = spec.terrain.size.y * 0.30f;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var x = RandomRange(random, -halfWidth, halfWidth);
                var z = RandomRange(random, -halfDepth, halfDepth);
                var altarDelta = new Vector2(x - spec.centralFeature.position.x, z - spec.centralFeature.position.z);
                if (altarDelta.magnitude > spec.centralFeature.altarRadius + 1.4f)
                {
                    return new Vector3(x, 0f, z);
                }
            }

            return new Vector3(RandomRange(random, -halfWidth, halfWidth), 0f, RandomRange(random, -halfDepth, halfDepth));
        }

        private static Vector3 SampleScatterPosition(SceneSpec spec, ScatterGroupSpec group, System.Random random)
        {
            if (group.type == "mountain")
            {
                return new Vector3(
                    RandomRange(random, group.areaMin.x, group.areaMax.x),
                    0f,
                    RandomRange(random, group.areaMin.z, group.areaMax.z));
            }

            for (var attempt = 0; attempt < 30; attempt++)
            {
                var position = group.edgeBias > 0f && random.NextDouble() < group.edgeBias
                    ? SampleBorderPosition(group, random)
                    : new Vector3(
                        RandomRange(random, group.areaMin.x, group.areaMax.x),
                        0f,
                        RandomRange(random, group.areaMin.z, group.areaMax.z));

                if (IsOpenClearing(position, spec))
                {
                    continue;
                }

                if (Vector3.Distance(position, spec.centralFeature.position) < spec.centralFeature.altarRadius + 2.3f)
                {
                    continue;
                }

                return position;
            }

            return SampleBorderPosition(group, random);
        }

        private static bool IsOpenClearing(Vector3 position, SceneSpec spec)
        {
            var x = position.x / (spec.terrain.size.x * 0.34f);
            var z = (position.z + 1.3f) / (spec.terrain.size.y * 0.27f);
            return x * x + z * z < 1f;
        }

        private static Vector3 SampleBorderPosition(ScatterGroupSpec group, System.Random random)
        {
            var side = RandomInt(random, 0, 4);
            var marginX = Mathf.Max(1.6f, (group.areaMax.x - group.areaMin.x) * 0.13f);
            var marginZ = Mathf.Max(1.6f, (group.areaMax.z - group.areaMin.z) * 0.15f);

            return side switch
            {
                0 => new Vector3(RandomRange(random, group.areaMin.x, group.areaMin.x + marginX), 0f, RandomRange(random, group.areaMin.z, group.areaMax.z)),
                1 => new Vector3(RandomRange(random, group.areaMax.x - marginX, group.areaMax.x), 0f, RandomRange(random, group.areaMin.z, group.areaMax.z)),
                2 => new Vector3(RandomRange(random, group.areaMin.x, group.areaMax.x), 0f, RandomRange(random, group.areaMin.z, group.areaMin.z + marginZ)),
                _ => new Vector3(RandomRange(random, group.areaMin.x, group.areaMax.x), 0f, RandomRange(random, group.areaMax.z - marginZ, group.areaMax.z))
            };
        }

        private static void CreateFallbackObject(
            string type,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            float scale,
            MaterialPalette materials,
            System.Random random)
        {
            switch (type)
            {
                case "pine_tree":
                    CreatePineTree(parent, position, rotation, scale, materials, random);
                    break;
                case "broadleaf_tree":
                    CreateBroadleafTree(parent, position, rotation, scale, materials, random, "Broadleaf_Tree");
                    break;
                case "rock":
                    CreateRock(parent, position, rotation, scale, materials, random);
                    break;
                case "bush":
                    CreateBush(parent, position, rotation, scale, materials, random);
                    break;
                case "flower":
                    CreateFlower(parent, position, rotation, scale, materials, random);
                    break;
                case "mountain":
                    CreateMountain(parent, position, rotation, scale, materials, random);
                    break;
                default:
                    CreateRock(parent, position, rotation, scale, materials, random);
                    break;
            }
        }

        private static GameObject CreatePineTree(Transform parent, Vector3 position, Quaternion rotation, float scale, MaterialPalette materials, System.Random random)
        {
            var tree = new GameObject("Pine_Tree");
            tree.transform.SetParent(parent);
            tree.transform.SetPositionAndRotation(position, rotation);
            tree.transform.localScale = Vector3.one * scale;

            CreateMeshObject("Trunk", MeshFactory.CreateCylinder(7, 0.15f, 1.7f), materials.Bark, tree.transform, new Vector3(0f, 0.85f, 0f), Quaternion.identity, Vector3.one);

            var foliage = random.NextDouble() > 0.5d ? materials.PineA : materials.PineB;
            CreateMeshObject("Lower_Needles", MeshFactory.CreateCone(9, 0.9f, 1.25f), foliage, tree.transform, new Vector3(0f, 0.95f, 0f), Quaternion.identity, Vector3.one);
            CreateMeshObject("Middle_Needles", MeshFactory.CreateCone(9, 0.7f, 1.15f), foliage, tree.transform, new Vector3(0f, 1.55f, 0f), Quaternion.identity, Vector3.one);
            CreateMeshObject("Top_Needles", MeshFactory.CreateCone(8, 0.48f, 0.95f), materials.LeafBright, tree.transform, new Vector3(0f, 2.12f, 0f), Quaternion.identity, Vector3.one);

            return tree;
        }

        private static GameObject CreateBroadleafTree(
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            float scale,
            MaterialPalette materials,
            System.Random random,
            string name)
        {
            var tree = new GameObject(name);
            tree.transform.SetParent(parent);
            tree.transform.SetPositionAndRotation(position, rotation);
            tree.transform.localScale = Vector3.one * scale;

            CreateMeshObject("Trunk", MeshFactory.CreateCylinder(8, 0.22f, 2.0f), materials.Bark, tree.transform, new Vector3(0f, 1.0f, 0f), Quaternion.identity, Vector3.one);
            CreateMeshObject("Left_Branch", MeshFactory.CreateCylinder(6, 0.09f, 1.15f), materials.Bark, tree.transform, new Vector3(-0.28f, 1.55f, 0f), Quaternion.Euler(0f, 0f, 42f), Vector3.one);
            CreateMeshObject("Right_Branch", MeshFactory.CreateCylinder(6, 0.08f, 1.05f), materials.Bark, tree.transform, new Vector3(0.3f, 1.55f, 0.05f), Quaternion.Euler(0f, 0f, -38f), Vector3.one);

            CreateMeshObject("Main_Canopy", MeshFactory.CreateLowPolyBlob(random, 9, 4), materials.LeafMid, tree.transform, new Vector3(0f, 2.45f, 0f), Quaternion.identity, new Vector3(1.15f, 0.78f, 1.05f));
            CreateMeshObject("Left_Canopy", MeshFactory.CreateLowPolyBlob(random, 8, 3), materials.LeafDark, tree.transform, new Vector3(-0.75f, 2.2f, 0f), Quaternion.Euler(0f, 25f, 0f), new Vector3(0.85f, 0.62f, 0.8f));
            CreateMeshObject("Right_Canopy", MeshFactory.CreateLowPolyBlob(random, 8, 3), materials.LeafBright, tree.transform, new Vector3(0.72f, 2.25f, 0.08f), Quaternion.Euler(0f, -18f, 0f), new Vector3(0.85f, 0.62f, 0.8f));

            return tree;
        }

        private static void CreateRock(Transform parent, Vector3 position, Quaternion rotation, float scale, MaterialPalette materials, System.Random random)
        {
            CreateMeshObject(
                "LowPoly_Rock",
                MeshFactory.CreateRock(random),
                random.NextDouble() > 0.45d ? materials.StoneWarm : materials.StoneCool,
                parent,
                position + Vector3.up * 0.28f * scale,
                rotation,
                new Vector3(scale * RandomRange(random, 0.8f, 1.6f), scale * RandomRange(random, 0.45f, 0.95f), scale * RandomRange(random, 0.75f, 1.35f)));
        }

        private static void CreateBush(Transform parent, Vector3 position, Quaternion rotation, float scale, MaterialPalette materials, System.Random random)
        {
            var bush = new GameObject("Bush_Cluster");
            bush.transform.SetParent(parent);
            bush.transform.SetPositionAndRotation(position, rotation);
            bush.transform.localScale = Vector3.one * scale;

            var count = RandomInt(random, 2, 5);
            for (var i = 0; i < count; i++)
            {
                CreateMeshObject(
                    $"Bush_Lobe_{i}",
                    MeshFactory.CreateLowPolyBlob(random, 7, 3),
                    i % 2 == 0 ? materials.LeafDark : materials.LeafMid,
                    bush.transform,
                    new Vector3(RandomRange(random, -0.38f, 0.38f), 0.36f, RandomRange(random, -0.25f, 0.25f)),
                    Quaternion.Euler(0f, RandomRange(random, 0f, 360f), 0f),
                    new Vector3(RandomRange(random, 0.45f, 0.85f), RandomRange(random, 0.28f, 0.55f), RandomRange(random, 0.42f, 0.78f)));
            }
        }

        private static void CreateFlower(Transform parent, Vector3 position, Quaternion rotation, float scale, MaterialPalette materials, System.Random random)
        {
            var flower = new GameObject("Small_Flower");
            flower.transform.SetParent(parent);
            flower.transform.SetPositionAndRotation(position, rotation);
            flower.transform.localScale = Vector3.one * scale;

            CreateMeshObject("Stem", MeshFactory.CreateCylinder(5, 0.018f, 0.32f), materials.Stem, flower.transform, new Vector3(0f, 0.16f, 0f), Quaternion.identity, Vector3.one);

            var petalMaterial = random.Next(0, 4) switch
            {
                0 => materials.FlowerWhite,
                1 => materials.FlowerYellow,
                2 => materials.FlowerPink,
                _ => materials.FlowerPurple
            };

            for (var i = 0; i < 5; i++)
            {
                var angle = i * 72f;
                CreateMeshObject(
                    $"Petal_{i}",
                    MeshFactory.CreateIrregularDisc(random, 0.055f, 0.105f, 7),
                    petalMaterial,
                    flower.transform,
                    Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0.35f, 0.075f),
                    Quaternion.Euler(90f, angle, 0f),
                    Vector3.one);
            }

            CreateMeshObject("Center", MeshFactory.CreateIrregularDisc(random, 0.045f, 0.045f, 7), materials.FlowerYellow, flower.transform, new Vector3(0f, 0.35f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
        }

        private static void CreateMountain(Transform parent, Vector3 position, Quaternion rotation, float scale, MaterialPalette materials, System.Random random)
        {
            CreateMeshObject(
                "Background_LowPoly_Mountain",
                MeshFactory.CreateMountain(random),
                random.NextDouble() > 0.5d ? materials.MountainNear : materials.MountainFar,
                parent,
                position,
                rotation,
                new Vector3(scale * RandomRange(random, 3.2f, 5.5f), scale * RandomRange(random, 2.8f, 5.2f), scale * RandomRange(random, 1.1f, 1.9f)));
        }

        private static void CreateSparkleSystem(string name, Transform parent, Vector3 position, Color color, float radius, float rate)
        {
            var sparkleObject = new GameObject(name);
            sparkleObject.transform.SetParent(parent);
            sparkleObject.transform.localPosition = position;

            var particles = sparkleObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.12f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = particles.emission;
            emission.rateOverTime = rate;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            var renderer = sparkleObject.GetComponent<ParticleSystemRenderer>();
            renderer.material = MaterialPalette.CreateMaterial("AI_Magic_Sparkle", color, color, 2.8f);
        }

        private static GameObject CreateMeshObject(
            string name,
            Mesh mesh,
            Material material,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localRotation = localRotation;
            gameObject.transform.localScale = localScale;

            var meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;

            return gameObject;
        }

        private static GameObject PlacePrefabInstance(
            GameObject prefab,
            Transform parent,
            string name,
            Vector3 position,
            Quaternion rotation,
            float targetHeight,
            float uniformScale,
            ProjectAssetLibrary assetLibrary,
            string semanticType)
        {
            if (prefab == null)
            {
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                return null;
            }

            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = rotation;
            instance.transform.localScale = Vector3.one * Mathf.Max(0.01f, uniformScale);

            if (targetHeight > 0f)
            {
                var bounds = CalculateRendererBounds(instance);
                if (bounds.HasValue && bounds.Value.size.y > 0.0001f)
                {
                    var factor = targetHeight / bounds.Value.size.y;
                    instance.transform.localScale *= factor;
                }
            }

            SnapToGround(instance, parent.TransformPoint(position).y);
            assetLibrary.RecordUse(semanticType, prefab);
            return instance;
        }

        private static float GetTargetPrefabHeight(string semanticType, float scale)
        {
            return semanticType switch
            {
                "pine_tree" => 3.4f * scale,
                "broadleaf_tree" => 3.2f * scale,
                "hero_tree" => 6.6f * scale,
                "rock" => 0.8f * scale,
                "bush" => 0.72f * scale,
                "flower" => 0.34f * scale,
                "mountain" => 5.0f * scale,
                "altar" => 1.0f * scale,
                _ => scale
            };
        }

        private static void SnapToGround(GameObject instance, float groundY)
        {
            var bounds = CalculateRendererBounds(instance);
            if (!bounds.HasValue)
            {
                return;
            }

            instance.transform.position += Vector3.up * (groundY - bounds.Value.min.y);
        }

        private static Bounds? CalculateRendererBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return null;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static float RandomRange(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private static int RandomInt(System.Random random, int minInclusive, int maxExclusive)
        {
            return random.Next(minInclusive, maxExclusive);
        }

        private sealed class ProjectAssetLibrary
        {
            private readonly bool enabled;
            private readonly List<PrefabCandidate> allPrefabs = new List<PrefabCandidate>();
            private readonly Dictionary<string, List<PrefabCandidate>> candidateCache = new Dictionary<string, List<PrefabCandidate>>();
            private readonly Dictionary<string, int> usageCounts = new Dictionary<string, int>();
            private readonly Dictionary<string, string> usagePaths = new Dictionary<string, string>();

            public ProjectAssetLibrary(bool enabled)
            {
                this.enabled = enabled;
                if (enabled)
                {
                    ScanPrefabs();
                }
            }

            public GameObject FindPrefab(string[] keywords, string semanticType, System.Random random)
            {
                if (!enabled)
                {
                    return null;
                }

                var candidates = GetCandidates(keywords, semanticType);
                if (candidates.Count == 0)
                {
                    return null;
                }

                var totalWeight = 0;
                for (var i = 0; i < candidates.Count; i++)
                {
                    totalWeight += Mathf.Max(1, candidates[i].Score);
                }

                var pick = random.Next(0, totalWeight);
                for (var i = 0; i < candidates.Count; i++)
                {
                    pick -= Mathf.Max(1, candidates[i].Score);
                    if (pick < 0)
                    {
                        return candidates[i].Prefab;
                    }
                }

                return candidates[candidates.Count - 1].Prefab;
            }

            public void RecordUse(string semanticType, GameObject prefab)
            {
                if (prefab == null)
                {
                    return;
                }

                var path = AssetDatabase.GetAssetPath(prefab);
                var key = $"{semanticType}: {prefab.name}";
                usageCounts.TryGetValue(key, out var count);
                usageCounts[key] = count + 1;
                usagePaths[key] = path;
            }

            public void WriteReport(string path)
            {
                EnsureFolder("Assets/AIBuilder", "Generated");

                var lines = new List<string>
                {
                    "# AIBuilder Asset Match Report",
                    string.Empty,
                    $"Project asset matching: {(enabled ? "enabled" : "disabled")}",
                    $"Scanned prefabs: {allPrefabs.Count}",
                    string.Empty,
                    "## Used Prefabs",
                    string.Empty
                };

                if (usageCounts.Count == 0)
                {
                    lines.Add("No project prefabs were used. The generator fell back to procedural primitive meshes.");
                }
                else
                {
                    var keys = new List<string>(usageCounts.Keys);
                    keys.Sort(StringComparer.OrdinalIgnoreCase);
                    foreach (var key in keys)
                    {
                        lines.Add($"- {key} x{usageCounts[key]}");
                        lines.Add($"  Path: `{usagePaths[key]}`");
                    }
                }

                lines.Add(string.Empty);
                lines.Add("## Top Candidates");
                lines.Add(string.Empty);

                var candidates = new List<PrefabCandidate>(allPrefabs);
                candidates.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
                var max = Mathf.Min(80, candidates.Count);
                for (var i = 0; i < max; i++)
                {
                    lines.Add($"- `{candidates[i].Path}`");
                }

                File.WriteAllLines(path, lines);
                AssetDatabase.ImportAsset(path);
            }

            private void ScanPrefabs()
            {
                allPrefabs.Clear();
                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.StartsWith("Assets/AIBuilder/", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null)
                    {
                        continue;
                    }

                    allPrefabs.Add(new PrefabCandidate
                    {
                        Prefab = prefab,
                        Path = path,
                        NormalizedName = Normalize(Path.GetFileNameWithoutExtension(path)),
                        NormalizedPath = Normalize(path)
                    });
                }
            }

            private List<PrefabCandidate> GetCandidates(string[] keywords, string semanticType)
            {
                var cacheKey = $"{semanticType}|{string.Join("|", keywords ?? Array.Empty<string>())}".ToLowerInvariant();
                if (candidateCache.TryGetValue(cacheKey, out var cached))
                {
                    return cached;
                }

                var results = new List<PrefabCandidate>();
                for (var i = 0; i < allPrefabs.Count; i++)
                {
                    var candidate = allPrefabs[i];
                    var score = ScoreCandidate(candidate, keywords, semanticType);
                    if (score <= 0)
                    {
                        continue;
                    }

                    results.Add(new PrefabCandidate
                    {
                        Prefab = candidate.Prefab,
                        Path = candidate.Path,
                        NormalizedName = candidate.NormalizedName,
                        NormalizedPath = candidate.NormalizedPath,
                        Score = score
                    });
                }

                results.Sort((a, b) => b.Score.CompareTo(a.Score));
                if (results.Count > 16)
                {
                    results.RemoveRange(16, results.Count - 16);
                }

                candidateCache[cacheKey] = results;
                return results;
            }

            private static int ScoreCandidate(PrefabCandidate candidate, string[] keywords, string semanticType)
            {
                var score = 0;
                var name = candidate.NormalizedName;
                var path = candidate.NormalizedPath;

                if (keywords != null)
                {
                    foreach (var keyword in keywords)
                    {
                        var normalizedKeyword = Normalize(keyword);
                        if (string.IsNullOrWhiteSpace(normalizedKeyword))
                        {
                            continue;
                        }

                        if (MatchesKeyword(name, normalizedKeyword))
                        {
                            score += 18;
                        }
                        else if (MatchesKeyword(path, normalizedKeyword))
                        {
                            score += 8;
                        }
                    }
                }

                score += semanticType switch
                {
                    "pine_tree" => ScoreTree(name, path),
                    "broadleaf_tree" => ScoreTree(name, path),
                    "hero_tree" => ScoreTree(name, path) + (name.Contains("big tree") || name.Contains("big_tree") ? 18 : 0),
                    "rock" => ScoreRock(name, path),
                    "bush" => ScoreBush(name, path),
                    "flower" => ScoreFlower(name, path),
                    "altar" => ScoreAltar(name, path),
                    "mountain" => ScoreMountain(name, path),
                    _ => 0
                };

                if (IsClearlyWrongType(name, semanticType))
                {
                    score -= 100;
                }

                return score >= MinimumScoreForSemanticType(semanticType) ? score : 0;
            }

            private static int MinimumScoreForSemanticType(string semanticType)
            {
                return semanticType switch
                {
                    "pine_tree" => 24,
                    "broadleaf_tree" => 24,
                    "hero_tree" => 24,
                    "rock" => 20,
                    "bush" => 20,
                    "flower" => 20,
                    "altar" => 30,
                    "mountain" => 25,
                    _ => 1
                };
            }

            private static int ScoreTree(string name, string path)
            {
                var score = 0;
                if (ContainsToken(name, "tree") || name.Contains("big_tree"))
                {
                    score += 32;
                }

                if (path.Contains("prefabs") && path.Contains("nature"))
                {
                    score += 8;
                }

                return score;
            }

            private static int ScoreRock(string name, string path)
            {
                var score = 0;
                if (ContainsToken(name, "rock"))
                {
                    score += 36;
                }

                if (ContainsToken(name, "stone"))
                {
                    score += 18;
                }

                if (path.Contains("prefabs") && path.Contains("nature"))
                {
                    score += 8;
                }

                return score;
            }

            private static int ScoreBush(string name, string path)
            {
                var score = 0;
                if (ContainsToken(name, "shrub") || ContainsToken(name, "shrubs"))
                {
                    score += 36;
                }

                if (ContainsToken(name, "bush"))
                {
                    score += 36;
                }

                if (path.Contains("prefabs") && path.Contains("nature"))
                {
                    score += 8;
                }

                return score;
            }

            private static int ScoreFlower(string name, string path)
            {
                var score = 0;
                if (ContainsToken(name, "flower"))
                {
                    score += 40;
                }

                if (ContainsToken(name, "gras") || ContainsToken(name, "grass"))
                {
                    score += 7;
                }

                if (path.Contains("prefabs") && path.Contains("nature"))
                {
                    score += 8;
                }

                return score;
            }

            private static int ScoreAltar(string name, string path)
            {
                var score = 0;
                if (ContainsToken(name, "altar"))
                {
                    score += 44;
                }

                if (name.Contains("stone slab") || name.Contains("stone_slab"))
                {
                    score += 18;
                }

                if (path.Contains("prefabs") && path.Contains("nature"))
                {
                    score += 8;
                }

                return score;
            }

            private static int ScoreMountain(string name, string path)
            {
                var score = 0;
                if (ContainsToken(name, "mountain") || ContainsToken(name, "hill") || ContainsToken(name, "cliff"))
                {
                    score += 36;
                }

                if (ContainsToken(name, "rock"))
                {
                    score += 5;
                }

                return score;
            }

            private static bool IsClearlyWrongType(string name, string semanticType)
            {
                if (semanticType.Contains("tree") || semanticType == "hero_tree")
                {
                    return ContainsToken(name, "rock")
                           || ContainsToken(name, "flower")
                           || ContainsToken(name, "shrub")
                           || ContainsToken(name, "shrubs")
                           || ContainsToken(name, "grass")
                           || ContainsToken(name, "gras")
                           || ContainsToken(name, "lamp")
                           || ContainsToken(name, "sign")
                           || ContainsToken(name, "fence")
                           || ContainsToken(name, "box")
                           || ContainsToken(name, "wagon")
                           || ContainsToken(name, "altar")
                           || ContainsToken(name, "arch");
                }

                if (semanticType == "rock")
                {
                    return ContainsToken(name, "tree")
                           || ContainsToken(name, "flower")
                           || ContainsToken(name, "shrub")
                           || ContainsToken(name, "shrubs")
                           || ContainsToken(name, "grass")
                           || ContainsToken(name, "gras");
                }

                if (semanticType == "bush")
                {
                    return ContainsToken(name, "tree") || ContainsToken(name, "rock") || ContainsToken(name, "flower");
                }

                if (semanticType == "flower")
                {
                    return ContainsToken(name, "tree") || ContainsToken(name, "rock") || ContainsToken(name, "shrub") || ContainsToken(name, "shrubs");
                }

                if (semanticType == "altar")
                {
                    return ContainsToken(name, "tree") || ContainsToken(name, "flower") || ContainsToken(name, "shrub") || ContainsToken(name, "shrubs");
                }

                return false;
            }

            private static string Normalize(string value)
            {
                return string.IsNullOrWhiteSpace(value)
                    ? string.Empty
                    : value.ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
            }

            private static bool MatchesKeyword(string value, string keyword)
            {
                if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(keyword))
                {
                    return false;
                }

                return keyword.Contains("_") ? value.Contains(keyword) : ContainsToken(value, keyword);
            }

            private static bool ContainsToken(string value, string token)
            {
                if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(token))
                {
                    return false;
                }

                return value == token
                       || value.StartsWith($"{token}_", StringComparison.Ordinal)
                       || value.EndsWith($"_{token}", StringComparison.Ordinal)
                       || value.Contains($"_{token}_");
            }

            private sealed class PrefabCandidate
            {
                public GameObject Prefab;
                public string Path;
                public string NormalizedName;
                public string NormalizedPath;
                public int Score;
            }
        }

        private sealed class MaterialPalette
        {
            public readonly Material GrassBase;
            public readonly Material GrassPatchA;
            public readonly Material GrassPatchB;
            public readonly Material Bark;
            public readonly Material PineA;
            public readonly Material PineB;
            public readonly Material LeafDark;
            public readonly Material LeafMid;
            public readonly Material LeafBright;
            public readonly Material StoneWarm;
            public readonly Material StoneCool;
            public readonly Material Moss;
            public readonly Material MagicHeart;
            public readonly Material MagicPool;
            public readonly Material Stem;
            public readonly Material FlowerWhite;
            public readonly Material FlowerYellow;
            public readonly Material FlowerPink;
            public readonly Material FlowerPurple;
            public readonly Material MountainNear;
            public readonly Material MountainFar;
            public readonly Material Cloud;

            public MaterialPalette(SceneSpec spec)
            {
                GrassBase = CreateMaterial("AI_Grass_Base", spec.terrain.baseGrassColor);
                GrassPatchA = CreateMaterial("AI_Grass_Patch_Warm", spec.terrain.patchGrassA);
                GrassPatchB = CreateMaterial("AI_Grass_Patch_Cool", spec.terrain.patchGrassB);
                Bark = CreateMaterial("AI_Tree_Bark", new Color(0.33f, 0.21f, 0.12f, 1f));
                PineA = CreateMaterial("AI_Pine_Dark", new Color(0.16f, 0.42f, 0.13f, 1f));
                PineB = CreateMaterial("AI_Pine_Lit", new Color(0.37f, 0.64f, 0.12f, 1f));
                LeafDark = CreateMaterial("AI_Leaf_Dark", new Color(0.12f, 0.38f, 0.09f, 1f));
                LeafMid = CreateMaterial("AI_Leaf_Mid", new Color(0.32f, 0.61f, 0.12f, 1f));
                LeafBright = CreateMaterial("AI_Leaf_Bright", new Color(0.62f, 0.82f, 0.14f, 1f));
                StoneWarm = CreateMaterial("AI_Stone_Warm", new Color(0.56f, 0.56f, 0.47f, 1f));
                StoneCool = CreateMaterial("AI_Stone_Cool", new Color(0.39f, 0.48f, 0.48f, 1f));
                Moss = CreateMaterial("AI_Moss", new Color(0.4f, 0.68f, 0.11f, 1f));
                MagicHeart = CreateMaterial("AI_Magic_Heart", spec.centralFeature.glowColor, spec.centralFeature.glowColor, 3.6f);
                MagicPool = CreateMaterial("AI_Magic_Pool", new Color(0.66f, 1f, 0.08f, 0.82f), spec.centralFeature.glowColor, 2.2f);
                Stem = CreateMaterial("AI_Flower_Stem", new Color(0.22f, 0.52f, 0.1f, 1f));
                FlowerWhite = CreateMaterial("AI_Flower_White", new Color(0.96f, 0.95f, 0.82f, 1f));
                FlowerYellow = CreateMaterial("AI_Flower_Yellow", new Color(1f, 0.83f, 0.12f, 1f));
                FlowerPink = CreateMaterial("AI_Flower_Pink", new Color(1f, 0.45f, 0.61f, 1f));
                FlowerPurple = CreateMaterial("AI_Flower_Purple", new Color(0.66f, 0.34f, 0.78f, 1f));
                MountainNear = CreateMaterial("AI_Mountain_Near", new Color(0.42f, 0.62f, 0.39f, 1f));
                MountainFar = CreateMaterial("AI_Mountain_Far", new Color(0.52f, 0.72f, 0.68f, 1f));
                Cloud = CreateMaterial("AI_Cloud", new Color(0.92f, 0.96f, 0.86f, 1f));
            }

            public static Material CreateMaterial(string name, Color baseColor)
            {
                return CreateMaterial(name, baseColor, Color.black, 0f);
            }

            public static Material CreateMaterial(string name, Color baseColor, Color emissionColor, float emissionIntensity)
            {
                EnsureFolder("Assets/AIBuilder", "Materials");
                var path = $"{MaterialFolder}/{name}.mat";
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
                material.SetFloat("_Smoothness", 0.18f);

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

            private static void SetMaterialColor(Material material, string property, Color color)
            {
                if (material.HasProperty(property))
                {
                    material.SetColor(property, color);
                }
            }
        }

        private static class MeshFactory
        {
            public static Mesh CreatePlane(float width, float depth)
            {
                var vertices = new[]
                {
                    new Vector3(-width * 0.5f, 0f, -depth * 0.5f),
                    new Vector3(width * 0.5f, 0f, -depth * 0.5f),
                    new Vector3(-width * 0.5f, 0f, depth * 0.5f),
                    new Vector3(width * 0.5f, 0f, depth * 0.5f)
                };
                var triangles = new[] { 0, 2, 1, 1, 2, 3 };
                return FinalizeMesh("AI_Plane", vertices, triangles);
            }

            public static Mesh CreateIrregularDisc(System.Random random, float radiusX, float radiusZ, int segments)
            {
                var vertices = new List<Vector3> { Vector3.zero };
                for (var i = 0; i < segments; i++)
                {
                    var angle = Mathf.PI * 2f * i / segments;
                    var radiusJitter = RandomRange(random, 0.72f, 1.15f);
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radiusX * radiusJitter, 0f, Mathf.Sin(angle) * radiusZ * radiusJitter));
                }

                var triangles = new List<int>();
                for (var i = 1; i <= segments; i++)
                {
                    var next = i == segments ? 1 : i + 1;
                    triangles.Add(0);
                    triangles.Add(next);
                    triangles.Add(i);
                }

                return FinalizeMesh("AI_IrregularDisc", vertices.ToArray(), triangles.ToArray());
            }

            public static Mesh CreateCylinder(int sides, float radius, float height)
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                var bottomCenter = vertices.Count;
                vertices.Add(new Vector3(0f, -height * 0.5f, 0f));
                var topCenter = vertices.Count;
                vertices.Add(new Vector3(0f, height * 0.5f, 0f));

                for (var i = 0; i < sides; i++)
                {
                    var angle = Mathf.PI * 2f * i / sides;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, -height * 0.5f, Mathf.Sin(angle) * radius));
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, height * 0.5f, Mathf.Sin(angle) * radius));
                }

                for (var i = 0; i < sides; i++)
                {
                    var next = (i + 1) % sides;
                    var bottom = 2 + i * 2;
                    var top = bottom + 1;
                    var nextBottom = 2 + next * 2;
                    var nextTop = nextBottom + 1;

                    triangles.Add(bottom);
                    triangles.Add(top);
                    triangles.Add(nextTop);
                    triangles.Add(bottom);
                    triangles.Add(nextTop);
                    triangles.Add(nextBottom);

                    triangles.Add(bottomCenter);
                    triangles.Add(nextBottom);
                    triangles.Add(bottom);

                    triangles.Add(topCenter);
                    triangles.Add(top);
                    triangles.Add(nextTop);
                }

                return FinalizeClosedFlatMesh("AI_Cylinder", vertices, triangles);
            }

            public static Mesh CreateCone(int sides, float radius, float height)
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                var baseCenter = vertices.Count;
                vertices.Add(Vector3.zero);
                var apex = vertices.Count;
                vertices.Add(new Vector3(0f, height, 0f));

                for (var i = 0; i < sides; i++)
                {
                    var angle = Mathf.PI * 2f * i / sides;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
                }

                for (var i = 0; i < sides; i++)
                {
                    var current = 2 + i;
                    var next = 2 + ((i + 1) % sides);

                    triangles.Add(apex);
                    triangles.Add(next);
                    triangles.Add(current);

                    triangles.Add(baseCenter);
                    triangles.Add(current);
                    triangles.Add(next);
                }

                return FinalizeClosedFlatMesh("AI_Cone", vertices, triangles);
            }

            public static Mesh CreateLowPolyBlob(System.Random random, int segments, int rings)
            {
                var vertices = new List<Vector3> { Vector3.up };
                for (var ring = 1; ring < rings; ring++)
                {
                    var phi = Mathf.PI * ring / rings;
                    var y = Mathf.Cos(phi);
                    var ringRadius = Mathf.Sin(phi);
                    for (var segment = 0; segment < segments; segment++)
                    {
                        var angle = Mathf.PI * 2f * segment / segments;
                        var jitter = RandomRange(random, 0.78f, 1.18f);
                        vertices.Add(new Vector3(Mathf.Cos(angle) * ringRadius * jitter, y * RandomRange(random, 0.9f, 1.1f), Mathf.Sin(angle) * ringRadius * jitter));
                    }
                }

                var bottomIndex = vertices.Count;
                vertices.Add(Vector3.down);

                var triangles = new List<int>();
                for (var segment = 0; segment < segments; segment++)
                {
                    var current = 1 + segment;
                    var next = 1 + ((segment + 1) % segments);
                    triangles.Add(0);
                    triangles.Add(current);
                    triangles.Add(next);
                }

                for (var ring = 0; ring < rings - 2; ring++)
                {
                    var ringStart = 1 + ring * segments;
                    var nextRingStart = ringStart + segments;
                    for (var segment = 0; segment < segments; segment++)
                    {
                        var current = ringStart + segment;
                        var next = ringStart + ((segment + 1) % segments);
                        var lower = nextRingStart + segment;
                        var lowerNext = nextRingStart + ((segment + 1) % segments);

                        triangles.Add(current);
                        triangles.Add(lower);
                        triangles.Add(next);
                        triangles.Add(next);
                        triangles.Add(lower);
                        triangles.Add(lowerNext);
                    }
                }

                var lastRingStart = 1 + (rings - 2) * segments;
                for (var segment = 0; segment < segments; segment++)
                {
                    var current = lastRingStart + segment;
                    var next = lastRingStart + ((segment + 1) % segments);
                    triangles.Add(current);
                    triangles.Add(bottomIndex);
                    triangles.Add(next);
                }

                return FinalizeClosedFlatMesh("AI_LowPolyBlob", vertices, triangles);
            }

            public static Mesh CreateRock(System.Random random)
            {
                var mesh = CreateLowPolyBlob(random, 8, 4);
                mesh.name = "AI_LowPolyRock";
                return mesh;
            }

            public static Mesh CreateHeart(int segments, float depth)
            {
                var points = new List<Vector2>();
                for (var i = 0; i < segments; i++)
                {
                    var t = Mathf.PI * 2f * i / segments;
                    var x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
                    var y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                    points.Add(new Vector2(x / 18f, (y - 2f) / 18f));
                }

                var vertices = new List<Vector3>();
                var frontCenter = vertices.Count;
                vertices.Add(Vector3.zero + Vector3.back * depth);
                var backCenter = vertices.Count;
                vertices.Add(Vector3.zero + Vector3.forward * depth);

                foreach (var point in points)
                {
                    vertices.Add(new Vector3(point.x, point.y, -depth));
                }

                foreach (var point in points)
                {
                    vertices.Add(new Vector3(point.x, point.y, depth));
                }

                var triangles = new List<int>();
                for (var i = 0; i < segments; i++)
                {
                    var next = (i + 1) % segments;
                    var front = 2 + i;
                    var frontNext = 2 + next;
                    var back = 2 + segments + i;
                    var backNext = 2 + segments + next;

                    triangles.Add(frontCenter);
                    triangles.Add(front);
                    triangles.Add(frontNext);

                    triangles.Add(backCenter);
                    triangles.Add(backNext);
                    triangles.Add(back);

                    triangles.Add(front);
                    triangles.Add(back);
                    triangles.Add(backNext);
                    triangles.Add(front);
                    triangles.Add(backNext);
                    triangles.Add(frontNext);
                }

                return FinalizeClosedFlatMesh("AI_Heart", vertices, triangles);
            }

            public static Mesh CreateMountain(System.Random random)
            {
                var vertices = new[]
                {
                    new Vector3(-0.6f, 0f, -0.35f),
                    new Vector3(0.62f, 0f, -0.28f),
                    new Vector3(0.48f, 0f, 0.36f),
                    new Vector3(-0.52f, 0f, 0.32f),
                    new Vector3(RandomRange(random, -0.12f, 0.12f), 1f, RandomRange(random, -0.08f, 0.08f))
                };
                var triangles = new[]
                {
                    0, 4, 1,
                    1, 4, 2,
                    2, 4, 3,
                    3, 4, 0,
                    0, 1, 2,
                    0, 2, 3
                };
                return FinalizeClosedFlatMesh("AI_Mountain", new List<Vector3>(vertices), new List<int>(triangles));
            }

            private static Mesh FinalizeMesh(string name, Vector3[] vertices, int[] triangles)
            {
                var mesh = new Mesh { name = name };
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }

            private static Mesh FinalizeFlatMesh(string name, List<Vector3> sourceVertices, List<int> sourceTriangles)
            {
                return FinalizeFlatMesh(name, sourceVertices, sourceTriangles, false);
            }

            private static Mesh FinalizeClosedFlatMesh(string name, List<Vector3> sourceVertices, List<int> sourceTriangles)
            {
                return FinalizeFlatMesh(name, sourceVertices, sourceTriangles, true);
            }

            private static Mesh FinalizeFlatMesh(string name, List<Vector3> sourceVertices, List<int> sourceTriangles, bool orientOutward)
            {
                var vertices = new Vector3[sourceTriangles.Count];
                var triangles = new int[sourceTriangles.Count];

                for (var i = 0; i < sourceTriangles.Count; i++)
                {
                    vertices[i] = sourceVertices[sourceTriangles[i]];
                    triangles[i] = i;
                }

                if (orientOutward)
                {
                    OrientTrianglesOutward(vertices, triangles);
                }

                return FinalizeMesh(name, vertices, triangles);
            }

            private static void OrientTrianglesOutward(Vector3[] vertices, int[] triangles)
            {
                if (vertices.Length == 0)
                {
                    return;
                }

                var min = vertices[0];
                var max = vertices[0];
                for (var i = 1; i < vertices.Length; i++)
                {
                    min = Vector3.Min(min, vertices[i]);
                    max = Vector3.Max(max, vertices[i]);
                }

                var center = (min + max) * 0.5f;

                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var a = triangles[i];
                    var b = triangles[i + 1];
                    var c = triangles[i + 2];

                    var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    var faceCenter = (vertices[a] + vertices[b] + vertices[c]) / 3f;
                    if (Vector3.Dot(normal, faceCenter - center) >= 0f)
                    {
                        continue;
                    }

                    triangles[i + 1] = c;
                    triangles[i + 2] = b;
                }
            }
        }
    }
}
