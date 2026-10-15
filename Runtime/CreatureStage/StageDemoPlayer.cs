using System;
using UnityEngine;

namespace AIBuilder.CreatureStage
{
    // 入门示例只展示原地素材，不能作为已经校准的运动或防滑步效果。
    public sealed class StageDemoPlayer : MonoBehaviour
    {
        public Animator actor;
        public AnimationClip[] clips = Array.Empty<AnimationClip>();
        public string[] labels = Array.Empty<string>();
        public Camera viewCamera;
        public Vector3 viewCenter;
        public float viewDistance = 2.5f;
        private StagePoseSession session;
        private Font font;
        private GUIStyle titleStyle, textStyle, buttonStyle, smallStyle;
        private bool playing = true;
        private bool automatic = true;
        private double sequenceTime;
        private float speed = 1;
        private float yaw = 100;
        public int SelectedAction { get; private set; }
        public double SourceTime { get; private set; }
        public bool IsPlaying => playing;
        public bool IsAutomatic => automatic;
        public double Duration => clips != null && SelectedAction < clips.Length && clips[SelectedAction] != null ? clips[SelectedAction].length : 0;

        private void OnEnable()
        {
            if (clips == null || clips.Length == 0 || actor == null) return;
            SelectAction(0);
            playing = true;
            automatic = true;
            sequenceTime = 0;
        }

        public void SelectAction(int index)
        {
            if (clips == null || index < 0 || index >= clips.Length || clips[index] == null)
                throw new ArgumentOutOfRangeException(nameof(index));
            session?.Dispose();
            session = null;
            SelectedAction = index;
            SourceTime = 0;
            automatic = false;
            session = new StagePoseSession(actor, clips[index]);
            session.Evaluate(0);
        }

        public void SetPlaying(bool value) { playing = value; }

        public void Seek(double time)
        {
            if (!StageMotionPlan.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time));
            automatic = false;
            playing = false;
            SourceTime = Math.Max(0, Math.Min(Duration, time));
            session?.Evaluate(SourceTime);
        }

        public void StartTour()
        {
            SelectAction(0);
            sequenceTime = 0;
            automatic = true;
            playing = true;
        }

        public void Advance(double delta)
        {
            if (!StageMotionPlan.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            if (!playing || session == null) return;
            if (automatic)
            {
                sequenceTime = (sequenceTime + delta * speed) % (clips.Length * 4d);
                var next = (int)(sequenceTime / 4);
                if (next != SelectedAction) { SelectAction(next); automatic = true; }
                SourceTime = (sequenceTime % 4) % Duration;
            }
            else SourceTime = (SourceTime + delta * speed) % Duration;
            session.Evaluate(SourceTime);
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
            if (viewCamera != null)
            {
                var rotation = Quaternion.Euler(12, yaw, 0);
                viewCamera.transform.SetPositionAndRotation(viewCenter - rotation * Vector3.forward * viewDistance, rotation);
            }
        }

        private void OnDisable() { session?.Dispose(); session = null; }
        private void OnDestroy() { if (font != null) Destroy(font); }

        private void OnGUI()
        {
            if (session == null) return;
            if (titleStyle == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);
                titleStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 27, fontStyle = FontStyle.Bold };
                textStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true };
                smallStyle = new GUIStyle(textStyle) { fontSize = 14 };
                buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 18 };
                titleStyle.normal.textColor = textStyle.normal.textColor = smallStyle.normal.textColor = Color.white;
            }
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            var oldBackground = GUI.backgroundColor;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            try
            {
                GUI.color = new Color(0.045f, 0.085f, 0.11f, 0.98f);
                GUI.DrawTexture(new Rect(0, 0, 344, 720), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(26, 28, 310, 45), "中华龙鸟 · 动作示例", titleStyle);
                GUI.Label(new Rect(28, 85, 290, 66), "已经配好，直接观看。\n点动作按钮，也可以自己切换。", textStyle);
                for (var i = 0; i < clips.Length; i++)
                {
                    GUI.backgroundColor = i == SelectedAction ? new Color(0.25f, 0.8f, 0.68f) : Color.white;
                    var label = labels != null && i < labels.Length ? labels[i] : clips[i].name;
                    if (GUI.Button(new Rect(28, 169 + i * 55, 288, 43), (i == SelectedAction ? "●  " : "    ") + label, buttonStyle))
                    { SelectAction(i); playing = true; }
                }
                GUI.backgroundColor = Color.white;
                if (GUI.Button(new Rect(28, 407, 137, 44), playing ? "暂停" : "继续播放", buttonStyle)) playing = !playing;
                if (GUI.Button(new Rect(179, 407, 137, 44), "自动演示", buttonStyle)) StartTour();
                GUI.Label(new Rect(28, 474, 290, 30), $"动作进度   {SourceTime:F2} / {Duration:F2} 秒", textStyle);
                var time = GUI.HorizontalSlider(new Rect(28, 514, 288, 22), (float)SourceTime, 0, (float)Duration);
                if (Mathf.Abs(time - (float)SourceTime) > 0.00001f) Seek(time);
                GUI.Label(new Rect(28, 552, 290, 30), $"播放速度   {speed:F1} 倍", textStyle);
                speed = GUI.HorizontalSlider(new Rect(28, 591, 288, 22), speed, 0.25f, 1.5f);
                GUI.Label(new Rect(28, 639, 290, 58), "拖进度会暂停，方便观察姿态。\n本例展示原地动作，尚未接入防滑步。", smallStyle);
                GUI.Label(new Rect(382, 28, 770, 35), automatic ? "自动演示  /  每 4 秒切换一个动作" : "自由查看  /  点击左侧动作，拖动进度", textStyle);
                GUI.Label(new Rect(395, 649, 140, 28), "观察角度", textStyle);
                yaw = GUI.HorizontalSlider(new Rect(530, 660, 430, 22), yaw, -180, 180);
            }
            finally { GUI.color = oldColor; GUI.backgroundColor = oldBackground; GUI.matrix = oldMatrix; }
        }
    }
}
