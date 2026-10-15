using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace AIBuilder.EditorTools
{
    [Serializable]
    internal sealed class UVOptimizationSettings
    {
        public GameObject SourceModel;
        public Texture2D SourceTexture;
        public Material SourceMaterial;
        public string BlenderPath;
        public string OutputFolder = "Assets/AIBuilder/UVOptimized";
        public string MeshObjectName = string.Empty;
        public int TextureSize = 4096;
        public int BakeMargin = 64;
        public bool SaveBlend = true;
        public bool OverwriteExisting;
    }

    [Serializable]
    internal sealed class UVOptimizationJobConfig
    {
        public string source_fbx;
        public string source_texture;
        public string output_fbx;
        public string output_texture;
        public string output_blend;
        public string output_report;
        public string material_name;
        public string mesh_object_name;
        public int texture_size;
        public int bake_margin;
        public bool save_blend;
    }

    [Serializable]
    internal sealed class UVOptimizationReportSummary
    {
        public string status;
        public string error;
    }

    internal static class UVOptimizationRunner
    {
        private const string BlenderEditorPref = "AIBuilder.UVOptimization.BlenderPath";
        private const string StagePrefix = "AIBUILDER_UV_STAGE=";

        private static readonly object LogLock = new object();
        private static readonly StringBuilder ProcessLog = new StringBuilder();

        private static Process process;
        private static UVOptimizationSettings activeSettings;
        private static UVOptimizationJobConfig activeJob;
        private static string activeOutputFbxAssetPath;
        private static string activeOutputTextureAssetPath;
        private static string activeOutputMaterialAssetPath;
        private static string activeOutputReportAssetPath;
        private static string activeLogPath;

        public static bool IsRunning { get { return process != null; } }
        public static string Status { get; private set; } = "等待开始";
        public static string LastResult { get; private set; } = string.Empty;
        public static string LastOutputFolder { get; private set; } = string.Empty;
        public static string LastReportAssetPath { get; private set; } = string.Empty;

        public static event Action Completed;

        public static string FindDefaultBlenderPath()
        {
            string saved = EditorPrefs.GetString(BlenderEditorPref, string.Empty);
            if (File.Exists(saved))
            {
                return saved;
            }

            string[] candidates =
            {
                @"D:\Blender5.1\blender.exe",
                @"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe",
                @"C:\Program Files\Blender Foundation\Blender 5.0\blender.exe",
                @"C:\Program Files\Blender Foundation\Blender 4.5\blender.exe",
                @"C:\Program Files\Blender Foundation\Blender 4.2\blender.exe"
            };
            return candidates.FirstOrDefault(File.Exists) ?? string.Empty;
        }

        public static Material FindSourceMaterial(GameObject sourceModel, Texture2D sourceTexture)
        {
            if (sourceModel == null)
            {
                return null;
            }

            Material first = null;
            foreach (Renderer renderer in sourceModel.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        continue;
                    }
                    if (first == null)
                    {
                        first = material;
                    }
                    if (sourceTexture != null && MaterialUsesTexture(material, sourceTexture))
                    {
                        return material;
                    }
                }
            }
            return first;
        }

        public static Texture2D FindSourceTexture(GameObject sourceModel)
        {
            Material material = FindSourceMaterial(sourceModel, null);
            if (material == null)
            {
                return null;
            }
            if (material.HasProperty("_BaseMap"))
            {
                Texture2D baseMap = material.GetTexture("_BaseMap") as Texture2D;
                if (baseMap != null)
                {
                    return baseMap;
                }
            }
            return material.mainTexture as Texture2D;
        }

        public static bool TryStart(UVOptimizationSettings settings, out string error)
        {
            error = Validate(settings);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string sourceModelAssetPath = AssetDatabase.GetAssetPath(settings.SourceModel);
            string sourceTextureAssetPath = AssetDatabase.GetAssetPath(settings.SourceTexture);
            string sourceName = Path.GetFileNameWithoutExtension(sourceModelAssetPath);
            string textureName = Path.GetFileNameWithoutExtension(sourceTextureAssetPath);
            string materialBaseName = settings.SourceMaterial != null
                ? settings.SourceMaterial.name
                : sourceName;
            materialBaseName = SanitizeFileName(materialBaseName);

            string outputFolder = NormalizeAssetPath(settings.OutputFolder).TrimEnd('/');
            string outputFbxAssetPath = outputFolder + "/" + sourceName + "_UVOptimized.fbx";
            string outputTextureAssetPath = outputFolder + "/" + textureName + "_UVOptimized.png";
            string outputMaterialAssetPath = outputFolder + "/" + materialBaseName + "_UVOptimized.mat";
            string outputReportAssetPath = outputFolder + "/" + sourceName + "_UVOptimizationReport.json";

            string outputFbx = AssetPathToAbsolute(outputFbxAssetPath, projectRoot);
            string outputTexture = AssetPathToAbsolute(outputTextureAssetPath, projectRoot);
            string outputReport = AssetPathToAbsolute(outputReportAssetPath, projectRoot);
            string outputFolderAbsolute = AssetPathToAbsolute(outputFolder, projectRoot);
            Directory.CreateDirectory(outputFolderAbsolute);

            string[] outputFiles =
            {
                outputFbx,
                outputTexture,
                outputReport,
                AssetPathToAbsolute(outputMaterialAssetPath, projectRoot)
            };
            if (!settings.OverwriteExisting && outputFiles.Any(File.Exists))
            {
                error = "输出目录已有同名结果。请更换目录，或勾选“覆盖已有优化结果”。";
                return false;
            }

            string workFolder = Path.Combine(projectRoot, "UVOptimizationWork");
            Directory.CreateDirectory(workFolder);
            string configPath = Path.Combine(workFolder, sourceName + "_UVOptimizationJob.json");
            string outputBlend = Path.Combine(workFolder, sourceName + "_UVOptimized.blend");
            string logPath = Path.Combine(workFolder, sourceName + "_UVOptimization.log");

            UVOptimizationJobConfig job = new UVOptimizationJobConfig
            {
                source_fbx = AssetPathToAbsolute(sourceModelAssetPath, projectRoot),
                source_texture = AssetPathToAbsolute(sourceTextureAssetPath, projectRoot),
                output_fbx = outputFbx,
                output_texture = outputTexture,
                output_blend = outputBlend,
                output_report = outputReport,
                material_name = SanitizeFileName(materialBaseName + "_UVOptimized"),
                mesh_object_name = settings.MeshObjectName ?? string.Empty,
                texture_size = settings.TextureSize,
                bake_margin = settings.BakeMargin,
                save_blend = settings.SaveBlend
            };
            File.WriteAllText(configPath, JsonUtility.ToJson(job, true), new UTF8Encoding(false));

            UnityEditor.PackageManager.PackageInfo packageInfo =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UVOptimizationRunner).Assembly);
            string packageRoot = packageInfo != null
                ? packageInfo.resolvedPath
                : Path.Combine(projectRoot, "Packages", "com.xuyuchen.ai-builder");
            string scriptPath = Path.GetFullPath(
                Path.Combine(packageRoot, "Editor", "UVOptimization", "Blender", "uv_optimize.py"));
            if (!File.Exists(scriptPath))
            {
                error = "找不到 UV 优化 Blender 脚本: " + scriptPath;
                return false;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!PrepareOutputModelAsset(sourceModelAssetPath, outputFbxAssetPath, out error))
            {
                return false;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = settings.BlenderPath,
                Arguments = "--background --factory-startup --python " + Quote(scriptPath)
                    + " -- --config " + Quote(configPath),
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            ProcessLog.Clear();
            Process newProcess = new Process { StartInfo = startInfo };
            newProcess.OutputDataReceived += OnProcessOutput;
            newProcess.ErrorDataReceived += OnProcessOutput;
            try
            {
                if (!newProcess.Start())
                {
                    error = "Blender 进程启动失败。";
                    newProcess.Dispose();
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = "无法启动 Blender: " + exception.Message;
                newProcess.Dispose();
                return false;
            }

            activeSettings = CopySettings(settings);
            activeJob = job;
            activeOutputFbxAssetPath = outputFbxAssetPath;
            activeOutputTextureAssetPath = outputTextureAssetPath;
            activeOutputMaterialAssetPath = outputMaterialAssetPath;
            activeOutputReportAssetPath = outputReportAssetPath;
            activeLogPath = logPath;
            LastOutputFolder = outputFolder;
            LastReportAssetPath = string.Empty;
            LastResult = string.Empty;
            Status = "Blender 已启动";
            process = newProcess;
            EditorPrefs.SetString(BlenderEditorPref, settings.BlenderPath);
            newProcess.BeginOutputReadLine();
            newProcess.BeginErrorReadLine();
            EditorApplication.update += PollProcess;
            return true;
        }

        public static void RevealLastOutput()
        {
            if (string.IsNullOrEmpty(LastOutputFolder))
            {
                return;
            }
            Object folder = AssetDatabase.LoadAssetAtPath<Object>(LastOutputFolder);
            if (folder != null)
            {
                EditorGUIUtility.PingObject(folder);
                Selection.activeObject = folder;
            }
            else
            {
                EditorUtility.RevealInFinder(AssetPathToAbsolute(
                    LastOutputFolder, Directory.GetParent(Application.dataPath).FullName));
            }
        }

        public static void SelectLastReport()
        {
            if (string.IsNullOrEmpty(LastReportAssetPath))
            {
                return;
            }
            Object report = AssetDatabase.LoadAssetAtPath<Object>(LastReportAssetPath);
            if (report != null)
            {
                Selection.activeObject = report;
                EditorGUIUtility.PingObject(report);
            }
        }

        private static string Validate(UVOptimizationSettings settings)
        {
            if (IsRunning)
            {
                return "已有 UV 优化任务正在运行。";
            }
            if (settings == null || settings.SourceModel == null)
            {
                return "请选择角色 FBX。";
            }
            string modelPath = AssetDatabase.GetAssetPath(settings.SourceModel);
            if (!string.Equals(Path.GetExtension(modelPath), ".fbx", StringComparison.OrdinalIgnoreCase))
            {
                return "角色模型必须是项目内的 FBX 资源。";
            }
            if (settings.SourceTexture == null)
            {
                return "请选择原始 BaseColor 贴图。";
            }
            string texturePath = AssetDatabase.GetAssetPath(settings.SourceTexture);
            if (string.IsNullOrEmpty(texturePath) || !texturePath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return "原始贴图必须位于当前 Unity 项目的 Assets 目录。";
            }
            string textureExtension = Path.GetExtension(texturePath).ToLowerInvariant();
            string[] supportedExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".bmp", ".exr" };
            if (!supportedExtensions.Contains(textureExtension))
            {
                return "原始贴图格式不受 Blender 后台烘焙支持，请使用 PNG、JPEG、TGA、TIFF、BMP 或 EXR。";
            }
            if (string.IsNullOrWhiteSpace(settings.BlenderPath) || !File.Exists(settings.BlenderPath))
            {
                return "Blender 路径无效。";
            }
            string outputFolder = NormalizeAssetPath(settings.OutputFolder).TrimEnd('/');
            if (!outputFolder.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return "输出目录必须位于 Assets 下。";
            }
            if (settings.TextureSize != 2048 && settings.TextureSize != 4096 && settings.TextureSize != 8192)
            {
                return "贴图尺寸仅支持 2048、4096 或 8192。";
            }
            if (settings.BakeMargin < 8 || settings.BakeMargin > 256)
            {
                return "烘焙扩边必须在 8 到 256 像素之间。";
            }
            return string.Empty;
        }

        private static void OnProcessOutput(object sender, DataReceivedEventArgs arguments)
        {
            if (string.IsNullOrEmpty(arguments.Data))
            {
                return;
            }
            lock (LogLock)
            {
                ProcessLog.AppendLine(arguments.Data);
                if (arguments.Data.StartsWith(StagePrefix, StringComparison.Ordinal))
                {
                    Status = arguments.Data.Substring(StagePrefix.Length);
                }
            }
        }

        private static void PollProcess()
        {
            if (process == null || !process.HasExited)
            {
                return;
            }

            EditorApplication.update -= PollProcess;
            process.WaitForExit();
            int exitCode = process.ExitCode;
            process.Dispose();
            process = null;

            string log;
            lock (LogLock)
            {
                log = ProcessLog.ToString();
            }
            try
            {
                File.WriteAllText(activeLogPath, log, new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("UV优化日志写入失败: " + exception.Message);
            }

            Finish(exitCode, log);
            Completed?.Invoke();
        }

        private static void Finish(int exitCode, string log)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            UVOptimizationReportSummary report = ReadReport(activeJob.output_report);
            if (exitCode != 0 || report == null || report.status != "success")
            {
                Status = "失败";
                LastReportAssetPath = File.Exists(activeJob.output_report)
                    ? activeOutputReportAssetPath
                    : string.Empty;
                string reportError = report != null ? report.error : string.Empty;
                LastResult = !string.IsNullOrEmpty(reportError)
                    ? reportError
                    : LastNonEmptyLine(log) ?? "Blender 处理失败，请查看日志。";
                Debug.LogError("UV优化失败: " + LastResult + "\n日志: " + activeLogPath);
                return;
            }

            try
            {
                ConfigureOutputTexture(activeOutputTextureAssetPath, activeSettings.TextureSize);
                Material material = CreateOrUpdateMaterial(
                    activeSettings,
                    activeOutputTextureAssetPath,
                    activeOutputMaterialAssetPath);
                RemapOutputModelMaterials(activeOutputFbxAssetPath, material);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            catch (Exception exception)
            {
                Status = "FBX 已生成，Unity 材质接入失败";
                LastResult = exception.Message;
                LastReportAssetPath = activeOutputReportAssetPath;
                Debug.LogException(exception);
                return;
            }

            Status = "完成";
            LastResult = "已生成独立 FBX、扩边贴图、材质和验证报告；原资源未修改。";
            LastReportAssetPath = activeOutputReportAssetPath;
            Debug.Log("UV优化完成: " + activeOutputFbxAssetPath);
        }

        private static UVOptimizationReportSummary ReadReport(string absolutePath)
        {
            if (!File.Exists(absolutePath))
            {
                return null;
            }
            try
            {
                return JsonUtility.FromJson<UVOptimizationReportSummary>(File.ReadAllText(absolutePath));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("UV优化报告读取失败: " + exception.Message);
                return null;
            }
        }

        private static void ConfigureOutputTexture(string textureAssetPath, int textureSize)
        {
            TextureImporter importer = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("无法导入优化贴图: " + textureAssetPath);
            }
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.anisoLevel = 4;
            importer.maxTextureSize = textureSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static Material CreateOrUpdateMaterial(
            UVOptimizationSettings settings,
            string textureAssetPath,
            string materialAssetPath)
        {
            Texture2D outputTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            if (outputTexture == null)
            {
                throw new InvalidOperationException("优化贴图导入后不可用: " + textureAssetPath);
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
            if (material == null)
            {
                if (settings.SourceMaterial != null)
                {
                    material = new Material(settings.SourceMaterial);
                }
                else
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (shader == null)
                    {
                        throw new InvalidOperationException("找不到可用于优化材质的 Shader。 ");
                    }
                    material = new Material(shader);
                }
                material.name = Path.GetFileNameWithoutExtension(materialAssetPath);
                AssetDatabase.CreateAsset(material, materialAssetPath);
            }
            else if (settings.SourceMaterial != null)
            {
                EditorUtility.CopySerialized(settings.SourceMaterial, material);
                material.name = Path.GetFileNameWithoutExtension(materialAssetPath);
            }

            ReplaceBaseColorTextures(material, settings.SourceMaterial, settings.SourceTexture, outputTexture);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void ReplaceBaseColorTextures(
            Material outputMaterial,
            Material sourceMaterial,
            Texture2D sourceTexture,
            Texture2D outputTexture)
        {
            if (sourceMaterial != null)
            {
                int propertyCount = ShaderUtil.GetPropertyCount(sourceMaterial.shader);
                for (int index = 0; index < propertyCount; index++)
                {
                    if (ShaderUtil.GetPropertyType(sourceMaterial.shader, index) != ShaderUtil.ShaderPropertyType.TexEnv)
                    {
                        continue;
                    }
                    string propertyName = ShaderUtil.GetPropertyName(sourceMaterial.shader, index);
                    if (sourceMaterial.GetTexture(propertyName) == sourceTexture && outputMaterial.HasProperty(propertyName))
                    {
                        outputMaterial.SetTexture(propertyName, outputTexture);
                    }
                }
            }
            if (outputMaterial.HasProperty("_BaseMap"))
            {
                outputMaterial.SetTexture("_BaseMap", outputTexture);
            }
            if (outputMaterial.HasProperty("_MainTex"))
            {
                outputMaterial.SetTexture("_MainTex", outputTexture);
            }
            outputMaterial.mainTexture = outputTexture;
        }

        private static void RemapOutputModelMaterials(string modelAssetPath, Material material)
        {
            ModelImporter importer = AssetImporter.GetAtPath(modelAssetPath) as ModelImporter;
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
            if (importer == null || model == null)
            {
                throw new InvalidOperationException("优化 FBX 导入后不可用: " + modelAssetPath);
            }

            HashSet<string> materialNames = new HashSet<string>();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material importedMaterial in renderer.sharedMaterials)
                {
                    if (importedMaterial != null)
                    {
                        materialNames.Add(importedMaterial.name);
                    }
                }
            }
            foreach (string materialName in materialNames)
            {
                importer.AddRemap(
                    new AssetImporter.SourceAssetIdentifier(typeof(Material), materialName),
                    material);
            }
            importer.SaveAndReimport();
        }

        private static bool PrepareOutputModelAsset(
            string sourceModelAssetPath,
            string outputModelAssetPath,
            out string error)
        {
            error = string.Empty;
            if (AssetDatabase.LoadMainAssetAtPath(outputModelAssetPath) != null
                && !AssetDatabase.DeleteAsset(outputModelAssetPath))
            {
                error = "无法替换旧的优化 FBX: " + outputModelAssetPath;
                return false;
            }
            if (!AssetDatabase.CopyAsset(sourceModelAssetPath, outputModelAssetPath))
            {
                error = "无法复制源 FBX 的模型导入设置。";
                return false;
            }
            AssetDatabase.ImportAsset(outputModelAssetPath, ImportAssetOptions.ForceSynchronousImport);
            return true;
        }

        private static bool MaterialUsesTexture(Material material, Texture texture)
        {
            if (material == null || material.shader == null || texture == null)
            {
                return false;
            }
            int propertyCount = ShaderUtil.GetPropertyCount(material.shader);
            for (int index = 0; index < propertyCount; index++)
            {
                if (ShaderUtil.GetPropertyType(material.shader, index) == ShaderUtil.ShaderPropertyType.TexEnv
                    && material.GetTexture(ShaderUtil.GetPropertyName(material.shader, index)) == texture)
                {
                    return true;
                }
            }
            return false;
        }

        private static string AssetPathToAbsolute(string assetPath, string projectRoot)
        {
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static UVOptimizationSettings CopySettings(UVOptimizationSettings source)
        {
            return new UVOptimizationSettings
            {
                SourceModel = source.SourceModel,
                SourceTexture = source.SourceTexture,
                SourceMaterial = source.SourceMaterial,
                BlenderPath = source.BlenderPath,
                OutputFolder = source.OutputFolder,
                MeshObjectName = source.MeshObjectName,
                TextureSize = source.TextureSize,
                BakeMargin = source.BakeMargin,
                SaveBlend = source.SaveBlend,
                OverwriteExisting = source.OverwriteExisting
            };
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }
            return value;
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string LastNonEmptyLine(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            string[] lines = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length > 0 ? lines[lines.Length - 1] : null;
        }
    }
}
