# 遗迹会合 / Rendezvous — 预览版

演示工程：E:/X/Validation/SceneStoryCharacterDemo_20260916

场景：Assets/RendezvousDemo/Rendezvous.unity。直接进入 Play 自动播放，36 秒后停留在结束画面。

三名角色共用同一个 PlayableDirector、三条 Scene Story 路线轨和三条原生 Animation Track。采用用户导入的 First Person + Third Person Character Controllers 2.0 人形模型与 Humanoid 动画。环境几何、配色和分镜为本演示制作。

## 文件

- SceneStory-Rendezvous-1080p.mp4：36 秒、1920×1080、30fps、无声，真实 Unity 场景逐帧渲染。
- Still-0.png 至 Still-3.png：同场景四个时刻的 1920×1080 截图。
- Positions.csv：三角色各 1080 帧的位置记录。
- CaptureErrors.txt：完整逐帧捕获的错误记录（本轮为空）。

## 验证和边界

- 本次 Unity 2022.3.62f3、URP 14.0.12、Timeline 1.7.7。不是全部引擎和管线的兼容性承诺。
- 根节点最大单帧距离：Scout 0.0403m、Courier 0.0556m、Ranger 0.0429m，无瞬移；Humanoid 腿部骨骼变化已抽样确认。
- 这是剧情演示，不是编辑器操作录像。镜头由演示专用脚本编排，非工具自动镜头功能。
- 走跑速率为演示近似匹配，尚未完成精确脚掌滑步校准。基础动作之间使用原生轨道切换，进一步动作过渡美化仍可进行。
- 演示关闭原玩家控制器所需的脚步动画事件，未提供声效。
- 第三方人物不包含于现有商品包。当前目录只提供渲染媒体和验证记录。若用于商店，说明“Character models and animations shown are from Unity Starter Assets and are not included. Available separately from Unity Asset Store.”
- 指定资源随附许可在导入目录内尚未找到，未据此授权打包第三方源素材。现有 RC3 商品包及旧媒体保持不变。

模型来源：https://assetstore.unity.com/packages/3d/characters/first-person-third-person-character-controllers-196526

完整 Play 播放复测：脚步事件初始化修复后，播放到 36 秒，Console 0 错误/0 警告。当前播放器暂停会释放角色根变换并恢复起点；视频以结束前最后一帧收尾。

旧版视频存在蒙皮画面冻结问题，已由 ../SceneStory-Rendezvous-v2/SceneStory-Rendezvous-v2-1080p.mp4 替代；请勿用旧视频上架。
