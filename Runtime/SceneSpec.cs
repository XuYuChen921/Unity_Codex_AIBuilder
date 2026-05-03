using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIBuilder
{
    [Serializable]
    public sealed class SceneSpec
    {
        public string sceneName = "GeneratedScene";
        public string generatedRootName = "AI_Generated_Scene";
        public string theme = "stylized_low_poly";
        public string generatorVersion = "v0";
        public int seed = 12345;
        public ReferenceImageSpec referenceImage = new ReferenceImageSpec();
        public string[] visualNotes = Array.Empty<string>();
        public CameraSpec camera = new CameraSpec();
        public LightingSpec lighting = new LightingSpec();
        public TerrainSpec terrain = new TerrainSpec();
        public SceneFeatureSpec features = new SceneFeatureSpec();
        public CentralFeatureSpec centralFeature = new CentralFeatureSpec();
        public List<ScatterGroupSpec> scatterGroups = new List<ScatterGroupSpec>();
    }

    [Serializable]
    public sealed class ReferenceImageSpec
    {
        public string assetPath;
        public int width;
        public int height;
        public string analysisPreset = "custom_reference_scene";
    }

    [Serializable]
    public sealed class SceneFeatureSpec
    {
        public bool centralFeature;
        public bool magicPools;
        public bool heroForegroundTrees;
        public bool clouds;
        public bool postProcessing = true;
    }

    [Serializable]
    public sealed class CameraSpec
    {
        public Vector3 position = new Vector3(0f, 8f, -18f);
        public Vector3 lookAt = new Vector3(0f, 1.3f, 5f);
        public float fieldOfView = 42f;
        public float nearClipPlane = 0.1f;
        public float farClipPlane = 120f;
    }

    [Serializable]
    public sealed class LightingSpec
    {
        public Vector3 sunEuler = new Vector3(35f, -35f, 0f);
        public Color sunColor = new Color(1f, 0.86f, 0.48f, 1f);
        public float sunIntensity = 2.2f;
        public Color ambientSkyColor = new Color(0.58f, 0.82f, 0.78f, 1f);
        public Color ambientEquatorColor = new Color(0.48f, 0.62f, 0.35f, 1f);
        public Color ambientGroundColor = new Color(0.25f, 0.32f, 0.18f, 1f);
        public Color fogColor = new Color(0.68f, 0.9f, 0.82f, 1f);
        public float fogDensity = 0.012f;
        public float bloomIntensity = 0.55f;
    }

    [Serializable]
    public sealed class TerrainSpec
    {
        public Vector2 size = new Vector2(42f, 32f);
        public float groundYOffset = 0f;
        public int grassPatchCount = 90;
        public Color baseGrassColor = new Color(0.55f, 0.78f, 0.12f, 1f);
        public Color patchGrassA = new Color(0.67f, 0.84f, 0.16f, 1f);
        public Color patchGrassB = new Color(0.42f, 0.66f, 0.13f, 1f);
    }

    [Serializable]
    public sealed class CentralFeatureSpec
    {
        public Vector3 position = new Vector3(0f, 0f, 7.5f);
        public float altarRadius = 2.3f;
        public float altarHeight = 0.65f;
        public float heartHeight = 2.45f;
        public float glowIntensity = 4f;
        public Color glowColor = new Color(0.47f, 1f, 0.08f, 1f);
    }

    [Serializable]
    public sealed class ScatterGroupSpec
    {
        public string id;
        public string type;
        public int count = 1;
        public Vector3 areaMin;
        public Vector3 areaMax;
        public Vector2 scaleRange = new Vector2(1f, 1f);
        public float edgeBias;
        public string[] assetKeywords = Array.Empty<string>();
    }
}
