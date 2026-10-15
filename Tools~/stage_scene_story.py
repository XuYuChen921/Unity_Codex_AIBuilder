import json, shutil
from pathlib import Path
root=Path(__file__).resolve().parents[1]
out=root/'Distribution~/SceneStory-1.0.0'
pkg=out/'package'
project=Path('E:/X/Validation/SceneStoryRelease_20260911')
runtime=[p for p in (root/'Runtime/CreatureStage').glob('*.cs') if p.name!='StageDemoPlayer.cs']
editors=['StageAnimationTrackSetup','StageBasicExample','StageGaitAnimationEditor','StageGroundRepair','StageGroundSettingsEditor','StageRouteEditor','StageRouteOverlay','StageStoryHierarchy','StageStoryPreviewClock','StageStoryStart','StageStoryWindow','StageStoryMultiActor','StageTutorial']
for folder, files in [('Runtime',runtime),('Editor',[root/f'Editor/CreatureStage/{name}.cs' for name in editors]),('Tests/Editor',[root/'Tests/Editor/StageStoryTests.cs',root/'Tests/Editor/StageStoryMultiActorTests.cs'])]:
 (pkg/folder).mkdir(parents=True,exist_ok=True)
 for p in files:
  shutil.copy2(p,pkg/folder/p.name)
  if p.with_suffix(p.suffix+'.meta').exists():shutil.copy2(p.with_suffix(p.suffix+'.meta'),pkg/folder/(p.name+'.meta'))
(pkg/'Editor/Resources').mkdir(exist_ok=True)
for p in (root/'Editor/CreatureStage/Resources').glob('SceneStoryTutorial.txt*'):shutil.copy2(p,pkg/'Editor/Resources'/p.name)
for folder,name,refs,platform in [('Runtime','SceneStory.Runtime',['Unity.Timeline'],[]),('Editor','SceneStory.Editor',['SceneStory.Runtime','Unity.Timeline','Unity.Timeline.Editor'],['Editor']),('Tests/Editor','SceneStory.Editor.Tests',['SceneStory.Runtime','SceneStory.Editor','Unity.Timeline','Unity.Timeline.Editor'],['Editor'])]:
 d=dict(name=name,rootNamespace='AIBuilder.CreatureStage',references=refs,includePlatforms=platform,autoReferenced=folder!='Tests/Editor')
 if folder=='Tests/Editor':d['optionalUnityReferences']=['TestAssemblies']
 (pkg/folder/(name+'.asmdef')).write_text(json.dumps(d,indent=2),encoding='utf-8')
manifest=dict(name='com.xuyuchen.scene-story',version='1.0.0',displayName='Scene Story 场景剧情',description='Scene route and Timeline animation authoring with editable primitives sample.',unity='2022.3',dependencies={'com.unity.timeline':'1.7.7'},author={'name':'XuYuChen921'},samples=[{'displayName':'基础几何体剧情','description':'Unity primitives with scale, rotation, grounded and free-space routes.','path':'Samples~/BasicExample'}])
(pkg/'package.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
(pkg/'Documentation~').mkdir(exist_ok=True)
shutil.copy2(root/'Editor/CreatureStage/Resources/SceneStoryTutorial.txt',pkg/'Documentation~/使用教程.md')
(pkg/'README.md').write_text('# Scene Story 场景剧情 1.0.0\n\nUnity 2022.3 / Timeline 1.7.7。入口：Tools > 场景剧情 > 使用教程。\n\nUPM：Add package from tarball 安装 .tgz；在 Samples 导入基础示例，或用菜单创建当前渲染管线适用的新示例。\n\n.unitypackage：先安装 Timeline，再导入。两种方式选一种。不要与已含剧情代码的 AIBuilder 同时安装。\n\n源代码和 GUID 保留兼容已有剧情；本包不含 ProBuilder、URP 插件、模型、纹理或中华龙鸟素材。\n\n详细操作见 Documentation~/使用教程.md，或 Unity 内使用教程窗口。\n',encoding='utf-8')
(pkg/'CHANGELOG.md').write_text('# 1.0.0\n\n首次独立分发：场景路线、Timeline 动画混合、片段编辑、地面匹配、距离步态同步、编辑预览和基础几何体示例。\n',encoding='utf-8')
(project/'Assets/Editor').mkdir(parents=True,exist_ok=True)
(project/'Packages').mkdir(exist_ok=True)
(project/'ProjectSettings').mkdir(exist_ok=True)
(project/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 2022.3.62f3\n')
deps={'com.unity.timeline':'1.7.7','com.unity.test-framework':'1.1.33'}
for m in ['animation','audio','director','imgui','physics','ui','uielements','jsonserialize']:deps['com.unity.modules.'+m]='1.0.0'
(project/'Packages/manifest.json').write_text(json.dumps({'dependencies':deps},indent=2))
# .unitypackage 内容在 Assets 下编译，验证无宿主插件依赖。
asset=project/'Assets/SceneStory'
for folder in ['Runtime','Editor','Tests']:
 shutil.copytree(pkg/folder,asset/folder,dirs_exist_ok=True)
shutil.copy2(pkg/'README.md',asset/'README.md')
shutil.copy2(pkg/'Documentation~/使用教程.md',asset/'使用教程.md')
print(out)
print(project)
