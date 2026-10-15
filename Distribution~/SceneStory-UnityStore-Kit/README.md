# Scene Story — Unity Asset Store 提交资料包

发布者：GINDXRSTU · 售后：chrowder7@163.com
版本：1.0.0-rc3 · 整理日期：2026-09-16
仅准备 Unity 商店；尚未上传或提交审核。

## 已完成

- SceneStory-1.0.0-rc3.unitypackage：英文补齐、缺失锚点选择异常修复；含 Timeline 依赖声明，保留旧版资源 GUID。
- Listing.en.md：英文名称、简介、详细功能和限制，适合填写商店页面。
- Submission-Fields.json：可复制字段；建议价格 $29.99，尚未由发布者确认。
- Publisher-Profile.txt：发布者资料。AI-Disclosure.txt：AI 辅助开发说明。Reviewer-Notes.txt：审核员操作说明。
- QuickStart.en.md：英文安装和使用指南；包内另含中文教程及离线指南。
- Artwork：160×160 图标、420×280 卡片、1950×1300 封面、1200×630 社交图。
- Screenshots：真实英文工具窗口、三角色 Timeline、示例渲染图。不是效果图。
- Video：24 秒实际示例运动视频（720p/30fps，无声），另附示例测试音频。不是鼠标操作录屏。
- SupportSite：可部署的静态售后页面，包含邮箱和指南链接；尚未托管。
- Evidence、Validation.json、Compatibility.md：测试与兼容记录。

最终 Unity 2022.3.62f3 和 Unity 6000.3.21f1 各通过 82/82 项检查；官方公开版 Validator 34/34 通过。Windows 示例构建与运行检查通过，音频调度正常，无运行错误。不能据此声称所有设备、渲染管线或人工听音验收已通过。

## 接下来需要发布者处理的事项

1. 登录 Unity Publisher Portal，创建或确认 GINDXRSTU 的发布者资料；涉及协议由本人确认。
2. 在 Unity 官方页面完成需要的实名/企业、收款和税务资料。不要把身份证、银行卡或税务信息发进本资料包。
3. 确认商品名称、最终英文措辞、$29.99 定价以及作品权属。文案经过 AI 辅助，需发布者最终审阅。
4. 提供现有网站托管位置，或自行部署 SupportSite 并填写真实 HTTPS 地址。浏览器工具不允许预览本地文件，因此支持页尚缺桌面/手机实际视觉验收；部署前按 DEPLOY.md 检查。
5. 若采用演示视频，上传到自己控制的视频账号，获得公开可嵌入链接；本次没有代为上传。
6. 使用当前 Publisher Portal / Asset Store Publishing Tools 上传候选包和图片、填写字段，重新跑当前版 Validator，然后最终确认提交审核。本地工具提示有 12.0.5 新版，本次实际使用的官方公开工具为 12.0.0。

本包是本地可审阅交付物，不代表已上线或保证审核通过。保留了首次并行 Unity 6 音频检查失败后单独及完整复测通过的记录说明。具体边界见 Compatibility.md。

## 更新安装

先备份，完整覆盖 Assets/SceneStory，不漏选 Editor/StageLanguage.cs 或 Resources。勿与旧 UPM 版、包含相同类的 AIBuilder 并装。自建场景和 Timeline 放在 Samples 以外。

商店流程参考：
- https://marketplace.unity.com/publishing/submission-guidelines
- https://docs.unity.com/en-us/asset-store/publishing/asset-packages/create-package
- https://docs.unity.com/en-us/asset-store/publishing/asset-packages/upload
- https://github.com/Unity-Technologies/com.unity.asset-store-tools
