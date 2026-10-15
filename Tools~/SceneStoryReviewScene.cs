// 独立发布工程准备示例场景；界面操作与截图由桌面操作工具完成。
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;

public static class SceneStoryReviewScene
{
    public static void Open()
    {
        EditorSceneManager.OpenScene("Assets/SceneStory/Samples/StoreExamples/MultiActor.unity");
        Selection.activeGameObject = Object.FindObjectOfType<PlayableDirector>().gameObject;
    }
}
