# Scene Story 1.0 商店候选版

本目录用于准备上架资料，不代表已提交或审核通过。发行商：GINDXRSTU；公开支持邮箱：chrowder7@163.com；网站待提供。

定位：基于 Unity Timeline 的多角色走位与剧情编排工具。
英文名称建议：Scene Story — Actor Routes for Timeline。
中文名称建议：Scene Story 场景剧情编排。
建议首发定价：国际商店 USD 29.99，中国商店 CNY 199；以后台价格规则及所有者最终选择为准。

## 交付与验收

- 使用独立发布工程导出 Assets/SceneStory，不包含 dinosaurtosky 场景、角色、美术或其他项目依赖。
- 保留脚本 GUID 和序列化字段，现有角色、片段、路线名称不会随显示语言切换而改变。
- 候选版增加主要编排界面、地面窗口及路线编辑器中英文切换；默认英文。一部分底层诊断、组件 Inspector 和旧示例名称仍为中文，正式国际版前需要完成这些剩余本地化。
- 英文、中文快速入门随包提供。依赖 Timeline，不能与包含同名类型的 UPM 包或 AIBuilder 重复安装。
- 编译、EditMode、导入后场景采样与 Play/独立构建是不同验证层级。具体已完成项见候选包 Validation.json，不以一种结果替代另一种。
- 团结引擎版本与全部渲染管线兼容性仅在实际验证后填写。

## 提交前仍需完成

1. 网站/联系页面及收款税务信息；发布者名称和邮箱已提供。
2. 国际版剩余 Inspector/诊断本地化和人工英文界面检查。
3. 实机团结引擎导入、编辑、播放和构建验证。
4. 真人操作录屏：90 秒功能介绍和完整快速入门；图片以实际编辑器截图为准。
5. Asset Store Publishing Tools 的最新 Validator 检查。
6. 所有者核对第三方依赖、版权和 AI 披露；完成商店草稿后再提交审核。

本目录不保存账号密码、税号、收款凭据。

## 本次实测结果

- Unity 2022.3.62f3 / Timeline 1.7.7：核心 76/76；全新导入 5/5（含进入 Play 检查）。
- Unity 6000.3.21f1 / Timeline 1.7.7：全新导入成功；核心与导入合计 81/81（含进入 Play 检查）。
- Unity 2022.3 Windows x64：构建 0 错误、0 警告；AudioAndFacing 示例独立程序自动检查通过，角色移动、Timeline 时钟、AudioSource.isPlaying 均正常。
- 测试工程为 Built-in 管线；未以这些结果推断 URP/HDRP、团结、移动端或 XR 设备兼容。音频未人工试听。
- Evidence 内三张 PNG 是真实 Camera 渲染的功能示例图，不是最终商品封面或编辑器 UI 验收图。
- dinosaurtosky 已完成候选版导入，并于 2026-09-16 统一迁到 Assets/SceneStory；后续只用 .unitypackage 更新。详见 UnityPackage-Update-Policy.md。

