using UnityEditor;
using UnityEngine.SceneManagement;

namespace AIBuilder.EditorTools.CreatureStage
{
    // 保留原菜单路径，避免打断已有用户操作习惯。
    internal static class StageEnglishMenu
    {
        [MenuItem("Tools/Scene Story/Open Story Editor", false, 0)]
        private static void Open() => StageStoryWindow.Open();
        [MenuItem("Tools/Scene Story/Create Story for Selection", false, 20)]
        private static void Create() => StageBasicExample.CreateForSelection();
        [MenuItem("Tools/Scene Story/Create Basic Example", false, 30)]
        private static void Example() => StageBasicExample.CreateExample();
        [MenuItem("Tools/Scene Story/Ground Binding", false, 40)]
        private static void Ground() => StageGroundBindingWindow.Open(SceneManager.GetActiveScene());
        [MenuItem("Tools/Scene Story/User Guide", false, 100)]
        private static void Guide() => StageTutorial.Open();
    }
}
