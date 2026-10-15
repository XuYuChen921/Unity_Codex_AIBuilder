using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIBuilder
{
    [Serializable]
    public sealed class ProBuilderModelSpec
    {
        public int schemaVersion = 2;
        public string blockoutType = "object_blockout";
        public string displayName;
        public string modelName = "GeneratedModel";
        public string generatedRootName = "AI_ProBuilder_Model";
        public string generatorVersion = "model_v2";
        public string sourceMode = "visual_analysis";
        public List<ProBuilderModelReferenceImageSpec> referenceImages = new List<ProBuilderModelReferenceImageSpec>();
        public string scaleHint;
        public string[] codexSuggestions = Array.Empty<string>();
        public string[] visualNotes = Array.Empty<string>();
        public string[] modelingNotes = Array.Empty<string>();
        public ProBuilderModelCameraSpec camera = new ProBuilderModelCameraSpec();
        public ProBuilderReferenceFrameSpec referenceFrame = new ProBuilderReferenceFrameSpec();
        public ProBuilderSceneAnalysisSpec sceneAnalysis = new ProBuilderSceneAnalysisSpec();
        public bool savePrefab = true;
        public string outputFolder = "Assets/AIBuilder/GeneratedModels";
        public Vector3 origin = Vector3.zero;
        public List<ProBuilderModelPartSpec> parts = new List<ProBuilderModelPartSpec>();
    }

    [Serializable]
    public sealed class ProBuilderModelCameraSpec
    {
        public bool enabled;
        public Vector3 position = new Vector3(0f, 3f, -10f);
        public Vector3 lookAt = Vector3.zero;
        public float fieldOfView = 50f;
    }

    [Serializable]
    public sealed class ProBuilderModelReferenceImageSpec
    {
        public string assetPath;
        public string view = "unknown";
        public int width;
        public int height;
        public string notes;
    }

    [Serializable]
    public sealed class ProBuilderReferenceFrameSpec
    {
        public bool enabled;
        public int width;
        public int height;
        public bool lockAspect = true;
        [Range(0f, 1f)] public float horizonY = 0.5f;
        public string coordinateSystem = "normalized_top_left";
        public string notes;
    }

    [Serializable]
    public sealed class ProBuilderSceneAnalysisSpec
    {
        public bool enabled;
        public float humanHeightMeters = 1.75f;
        public List<ProBuilderSceneElementSpec> elements = new List<ProBuilderSceneElementSpec>();
        public List<ProBuilderSceneRelationSpec> relations = new List<ProBuilderSceneRelationSpec>();
        public List<ProBuilderSceneLandmarkSpec> landmarks = new List<ProBuilderSceneLandmarkSpec>();
        public string[] assumptions = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ProBuilderSceneElementSpec
    {
        public string id;
        public string label;
        public string role = "solid";
        public string category;
        public string depthBand;
        public string partName;
        public bool generateFromScreen;
        public List<Vector2> screenPolygon = new List<Vector2>();
        public float depthMeters = 10f;
        public float thicknessMeters = 1f;
        public Vector3 estimatedRotation = Vector3.zero;
        public float sizeInHumanHeights;
        public string[] occludes = Array.Empty<string>();
        public string[] occludedBy = Array.Empty<string>();
        [Range(0f, 1f)] public float confidence = 1f;
        public string notes;
    }

    [Serializable]
    public sealed class ProBuilderSceneRelationSpec
    {
        public string subjectId;
        public string predicate;
        public string objectId;
        public string notes;
    }

    [Serializable]
    public sealed class ProBuilderSceneLandmarkSpec
    {
        public string name;
        public string elementId;
        public Vector2 screenPoint;
        [Range(0f, 1f)] public float tolerance = 0.03f;
        public string notes;
    }

    [Serializable]
    public sealed class ProBuilderModelPartSpec
    {
        public string name = "Part";
        public string parentName;
        public string analysisElementId;
        public string shape = "box";
        public Vector3 position = Vector3.zero;
        public Vector3 rotation = Vector3.zero;
        public Vector3 size = Vector3.one;
        public Color color = Color.gray;
        public string materialName;
        public Color emissionColor = Color.black;
        public float emissionIntensity;
        public int segments = 12;
        public int subdivisions = 1;
        public int heightCuts;
        public float thickness = 0.18f;
        public float archAngle = 180f;
        public bool smooth;
        public bool addCollider;
        public string notes;
    }
}
