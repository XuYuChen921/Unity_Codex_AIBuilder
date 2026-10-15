# Game UI 与运行音频

移除 StageStoryPlayback 的 OnGUI 提示、按钮和进度条，以及仅由这些按钮使用的摄像机跟随逻辑。保留序列化摄像机、角色引用，避免破坏现有工具调用。

运行播放从逐帧修改时间并 Evaluate 改为 PlayableDirector 的 UnscaledGameTime 原生连续播放；playing 控制暂停/继续，repeat 控制重新开始，停用时恢复原时钟。手动 AdvancePlayback 仍供显式定位调用使用，运行 Update 不再重复推进时间。

目标工程 dinosaurtosky_3.0_beta1 的当前主 Timeline 音轨均有 AudioSource 绑定，唯一启用的 Main Camera AudioListener 音量为 1、未暂停。运行中采样到 BGM、环境音、skin、Rap 和科萌的非零 AudioSource 输出。未改动场景音轨片段、空间音效设置、模型或灯光。

只读现场证据保存在目标工程 Library/CreatureStage/audio-native-playback.txt 和 audio-native-dialogue.txt；这是 Unity 音频输出验证，不等同于扬声器或头显实听验收。
