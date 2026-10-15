# 场景剧情界面优化 · 2026-09-14

**最新为第二版排版修正**，见 [Revision2.md](Revision2.md) 和 `Actual-v2-*.png`。下方记录为第一版历史结果。

入口：Unity 菜单 `Tools > 场景剧情 > 打开编排窗口`。窗口宽度达到 850 时为双栏；更窄时切换为上下布局。顶部保留预览操作，内容区可滚动。

## 本次修改

- 深色编辑器卡片、分组标题与统一间距；移动为青绿、停留为暖金，并保留文字标签。
- 双行片段列表显示顺序、名称、时间范围和时长；保留拖动排序与删除。
- 顶部时间总览显示片段分布、当前选择和播放位置，可点击切换选中片段。
- 参数分为基本信息、空间与动作、速度与衔接、动画步态同步。
- Timeline 独立动画轨接管后，隐藏不可编辑的旧动画字段，原有数据保留。
- 空状态提供基础示例、为选中场景对象创建剧情、使用教程入口。

## 图片

- `Concept.png`：内置图像生成工具制作的概念参考；提示词见 `Design-prompt.md`。
- `Actual-wide.png`：1120 × 850，真实 Unity 深色编辑器双栏窗口。
- `Actual-narrow.png`：460 × 780，上下布局。
- `Actual-narrow-inspector.png`：窄屏滚动到参数与步态设置。
- `Actual-empty.png`：未指定剧情对象的空状态。

## 验证

Unity 2022.3.62f3，隔离工程 `E:/X/Validation/SceneStoryRelease_20260911`。
最终 `StageStoryTests` 38/38 通过；结果位于该工程的 `StoryUI-tests-final.xml`，日志为 `StoryUI-tests-final.log`。

实际窗口使用鼠标事件验证：总览切换片段、播放按钮、预览时间推进、停止按钮、角色位置恢复、窄屏滚动。检查宽屏、窄屏、滚动参数区和空状态截图；`StoryUI-review-final.log` 未出现 C# 编译错误、GUI 布局错误或异常。简要记录见 `UI-capture.txt`。

已验证窗口源文件与当前 AIBuilder 源码 SHA256 一致：`B37A7966758316FEA82951329039C0D7FCBD85BF25FB5306598BA60B72906B60`。

本次交付为 AIBuilder 源码界面优化。`Distribution~/SceneStory-1.0.0` 历史安装包未重新打包；浅色主题未做截图验收，未新增设备或角色步态验收结论。
