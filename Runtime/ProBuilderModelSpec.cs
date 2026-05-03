using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIBuilder
{
    [Serializable]
    public sealed class ProBuilderModelSpec
    {
        public string modelName = "GeneratedModel";
        public string generatedRootName = "AI_ProBuilder_Model";
        public string generatorVersion = "model_v1";
        public string sourceMode = "codex_visual_analysis";
        public List<ProBuilderModelReferenceImageSpec> referenceImages = new List<ProBuilderModelReferenceImageSpec>();
        public string[] visualNotes = Array.Empty<string>();
        public string[] modelingNotes = Array.Empty<string>();
        public bool savePrefab = true;
        public string outputFolder = "Assets/AIBuilder/GeneratedModels";
        public Vector3 origin = Vector3.zero;
        public List<ProBuilderModelPartSpec> parts = new List<ProBuilderModelPartSpec>();
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
    public sealed class ProBuilderModelPartSpec
    {
        public string name = "Part";
        public string parentName;
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
