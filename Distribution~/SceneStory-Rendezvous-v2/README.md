# Rendezvous 动画修正版 v2

演示工程：E:\X\Validation\SceneStoryCharacterDemo_20260916
场景：Assets/RendezvousDemo/Rendezvous.unity
视频：SceneStory-Rendezvous-v2-1080p.mp4，1920×1080、30 fps、36 秒，无音轨。

修复：旧录制循环在同一编辑器帧连续 Evaluate/Render，骨骼虽变化，渲染蒙皮未逐帧刷新。新版跨真实 PlayerLoop 帧录制，已对不同时间图片检查跑步、下坡步行动作。Bones.csv 留存三个角色的逐帧骨骼旋转。Errors.txt 为空，1080 帧录制完成；FFmpeg 完整解码通过。

演示用动画复制为无控制器专属动画事件的 .anim，消除无接收者脚步事件报错。原始 FBX 不变。录制器仅在工程根目录存在 RecordRendezvous.request 时启用，普通 Play 不启用录制器。

这是动画修复预览，尚非商店最终宣传片：无配音、音乐、脚步音，场景美术及镜头节奏仍可打磨。镜头脚本是演示专用，不代表工具自带自动导演功能。普通播放结束回到初始位置的问题未在本次修改。第三方人物素材仅用于本地演示和媒体，本目录没有重新分发其模型源文件。

补充验证：未启用录制器的普通 Play 已从 0 自动推进至 36 秒，结束时 Console 显示 0 错误、0 警告。
