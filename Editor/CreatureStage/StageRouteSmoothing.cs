using System.IO;
using AIBuilder.CreatureStage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AIBuilder.EditorTools.CreatureStage
{
    [InitializeOnLoad]
    public static class StageRouteSmoothing
    {
        private const string Request = "Library/CreatureStage/smooth-routes.request.txt";
        static StageRouteSmoothing() { EditorApplication.update += Poll; }
        private static void Poll()
        {
            if (Application.isBatchMode || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            File.Delete(Request);
            File.WriteAllText("Library/CreatureStage/smooth-routes.result.txt", Apply());
        }
        public static void ApplyMenu() { Debug.Log(Apply()); }
        public static string Apply()
        {
            StageStoryWindow.StopAllPreviews();
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            var count = 0;
            foreach (var route in Object.FindObjectsOfType<StageRoute>())
            {
                if (!route.IsUsedByStory()) continue;
                Undo.RecordObject(route, "圆滑剧情路线");
                route.curved = true; route.smoothness = 0.5f; route.smoothConnections = true;
                route.Invalidate(); EditorUtility.SetDirty(route);
                EditorSceneManager.MarkSceneDirty(route.gameObject.scene); count++;
            }
            Undo.CollapseUndoOperations(group);
            SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate();
            return "已圆滑 " + count + " 条使用中的路线，圆滑度 0.5；保留路线节点。";
        }
    }
}
