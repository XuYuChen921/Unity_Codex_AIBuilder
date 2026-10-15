# Unitypackage 安装与更新约定

当前 dinosaurtosky_3.0_beta1 已于 2026-09-16 完成一次性迁移，正式使用 Assets/SceneStory。后续只导入 SceneStory 的 .unitypackage 更新，不再安装同名 UPM 包。

已将 Runtime、Editor 及其资源和 .meta 完整迁入；67 个文件字节一致。已有 Assets/SceneStory/Editor 文件夹保留自己的 GUID，所有脚本、程序集和资源文件 GUID 保持不变。旧包及开发测试、旧文档均完整保留在工程根目录 SceneStoryMigrationBackup~/UnityPackage-20260916-142100。

Packages/manifest.json 仅移除了 com.xuyuchen.scene-story 依赖；Timeline 与其他依赖保留。Unity 已更新 packages-lock.json。旧包已移出 Packages。

迁移前后当前已加载场景的组件序列化数据、脚本 GUID、角色位置、Timeline 片段和绑定引用快照完全一致；脚本重载成功。未保存或重建用户场景，未执行 Play 验收。迁移临时编辑器脚本已移出 Assets 并归档。

后续发布保持 Assets/SceneStory 路径和脚本 GUID；修改序列化字段时提供数据兼容。unitypackage 不负责删除旧文件，删除/替换脚本须提供专门迁移步骤。发布物仍从独立发布工程导出，不打包用户剧情、美术或迁移备份。

本说明取代 UPM-Upgrade-Repair.md 中针对该工程“后续沿用 UPM”的建议；该文件保留作为前一次故障处理记录。
