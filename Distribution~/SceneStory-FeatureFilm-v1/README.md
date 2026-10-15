# Scene Story 商店功能演示视频

上传文件：SceneStory-Features-ZH-EN-1080p.mp4
规格：88 秒，1920×1080，30 fps，无音轨，中英文字幕已烧录。

## 内容
- 00:00 场景中的角色表演
- 00:06 多角色共用 Timeline
- 00:14 选中片段后插入停留，后续路线片段顺延
- 00:25 重排片段，重新衔接路线
- 00:35 选择场景路线，切换当前角色
- 00:43 朝向曲线：转向、保持、回正
- 00:59 校准步态距离并启用动画距离同步
- 01:06.5 缩短移动时长，观察步态推进
- 01:14 前后定位预览
- 01:24 品牌与演示素材说明

## 录制与验证
编辑器部分来自独立 Unity 工程中的真实窗口，通过脚本执行插件操作；视频明确标注自动演示，未添加虚假鼠标点击。编辑器采样为 10 fps，输出重复帧至 30 fps；片头为真实 Unity 场景渲染。完整视频解码通过，抽查插入片段、朝向、步态和片尾字幕画面。此检查不等于完整插件运行验收。

步态周期距离约 1.815811 米，以角色局部坐标下支撑脚运动进行初始校准；不是脚部锁定或零滑步保证。插入演示顺延的是路线片段，原生动画轨道独立编辑。缩短移动演示同时调整对应动画片段长度。

演示人物不包含在插件发布包中。未修改已有发布包，未上传或提交商店。

原始录制工程：E:\X\Validation\SceneStoryFeatureFilm_20260916
原始帧：该工程 FeatureFilmCapture 目录
录制脚本：Tools~/StoryFeatureFilm.cs
合成脚本：Tools~/RenderFeatureFilm.py
字幕：Captions.zh-CN.srt、Captions.en.srt、Captions.bilingual.srt；烧录样式为 Captions.ass。
Evidence 保存操作与校准记录。RenderCheck.json 保存解码验证结果。

## 当前上架操作
Unity Publisher Portal → Media → Screenshots & videos → Browse，选择上述完整 MP4。
不要上传 Intro.mp4、Editor.mp4、Outro.mp4 中间片段。Marketing images 是另一组封面图片字段。
