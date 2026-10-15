using UnityEngine;
using UnityEngine.Playables;

namespace AIBuilder.CreatureStage
{
    public sealed class StageStoryPlayback : MonoBehaviour
    {
        public PlayableDirector director;
        public bool playing = true;
        public bool repeat;
        public Camera sceneCamera;
        public Transform actor;
        private bool follow;
        private int resetFrame = -1;
        private DirectorUpdateMode previousUpdateMode;
        private bool ownsClock;
        private Vector3 cameraPosition;
        private Quaternion cameraRotation;
        private Font font;
        private GUIStyle label, button;
        private void Start()
        {
            if (sceneCamera != null) { cameraPosition = sceneCamera.transform.position; cameraRotation = sceneCamera.transform.rotation; }
            if (director != null)
            {
                var autoPlay = playing;
                previousUpdateMode = director.timeUpdateMode; ownsClock = true;
                director.timeUpdateMode = DirectorUpdateMode.Manual;
                Restart(); playing = autoPlay;
            }
        }
        private void Update()
        {
            if (Time.frameCount != resetFrame) AdvancePlayback(Time.unscaledDeltaTime);
            if (sceneCamera != null && actor != null && follow)
            {
                var center = actor.position + Vector3.up * 0.28f;
                sceneCamera.transform.SetPositionAndRotation(center + new Vector3(1.8f, 1.1f, -1.8f), Quaternion.LookRotation(new Vector3(-1.8f, -1.1f, 1.8f)));
            }
        }
        public void Restart()
        {
            if (director == null) return;
            resetFrame = Time.frameCount;
            director.time = 0;
            director.Evaluate();
            playing = true;
        }
        public void AdvancePlayback(double elapsed)
        {
            if (director == null || !playing || !StageMotionPlan.IsFinite(elapsed) || elapsed < 0) return;
            // 加载或切换编辑器造成长帧时，不把数秒耗时一次灌进剧情时间。
            var next = director.time + System.Math.Min(.1, elapsed);
            if (next >= director.duration)
            {
                if (repeat) { Restart(); return; }
                next = director.duration; playing = false;
            }
            director.time = next; director.Evaluate();
        }
        private void OnDisable()
        {
            if (director != null && ownsClock)
            {
                director.Stop();
                director.timeUpdateMode = previousUpdateMode;
            }
            ownsClock = false;
        }
        private void OnDestroy() { if (font != null) Destroy(font); }
        private void OnGUI()
        {
            if (director == null) return;
            if (label == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 18);
                label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18 };
                label.normal.textColor = Color.white;
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 17 };
            }
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            GUI.Box(new Rect(15, 15, 1250, 78), "");
            GUI.Label(new Rect(32, 23, 1210, 28), "场景剧情预览：路线与停留由剧情轨控制，动作由动画轨控制", label);
            GUI.Label(new Rect(32, 55, 1210, 28), "编辑路线：退出 Play，打开「场景剧情编排」，选片段后在 Scene 拖动控制点。", label);
            GUI.Box(new Rect(15, 646, 1250, 60), "");
            if (GUI.Button(new Rect(30, 659, 90, 34), playing ? "暂停" : "播放", button)) playing = !playing;
            if (GUI.Button(new Rect(130, 659, 95, 34), "从头播放", button)) Restart();
            if (GUI.Button(new Rect(235, 659, 105, 34), follow ? "全景" : "跟随角色", button))
            { follow = !follow; if (!follow && sceneCamera != null) sceneCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation); }
            var t = GUI.HorizontalSlider(new Rect(360, 672, 660, 22), (float)director.time, 0, (float)director.duration);
            if (Mathf.Abs(t - (float)director.time) > 0.00001) { playing = false; director.time = t; director.Evaluate(); }
            GUI.Label(new Rect(1040, 662, 210, 30), $"{director.time:F1} / {director.duration:F1} 秒", label);
            GUI.matrix = oldMatrix;
        }
    }
}
