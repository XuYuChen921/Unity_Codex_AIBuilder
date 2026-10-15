历史记录：该工程随后已迁移到 Assets 安装，当前更新方式请看 UnityPackage-Update-Policy.md。

# 已安装 UPM 工程的升级说明

SceneStory-1.0.0-rc1.unitypackage 用于 Assets 方式安装。若工程已有 Packages/com.xuyuchen.scene-story，不能将 unitypackage 当成 UPM 升级包混合导入。

已观察到的情况：Unity 根据已有 GUID 将旧脚本更新到 Packages，新增 StageLanguage.cs / StageEnglishMenu.cs 则落在 Assets/SceneStory/Editor。两组代码不在同一程序集，出现 CS0103 StageLanguage does not exist。

本次 dinosaurtosky 工程修复：将新增的两个 .cs 连同各自 .meta 一起移动到 Packages/com.xuyuchen.scene-story/Editor。英文教程资源已位于该包 Resources 下，无需移动。保留其他示例、场景和剧情资产。刷新后 SceneStory.Editor 编译成功，Unity Console 编译错误归零。

以后升级继续沿用原安装方式：UPM 使用完整包更新；Assets 安装使用 unitypackage。若需要转换安装方式，应先备份并完整迁移所有代码与 meta，不能仅导入新包或删除原包。

此次为实际工程安装布局修复，没有修改角色、Timeline、路线算法或重新运行 Play 验收。

