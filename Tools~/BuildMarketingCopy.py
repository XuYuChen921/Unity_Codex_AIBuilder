from pathlib import Path
import json

root = Path(__file__).resolve().parents[1]
out = root / 'Distribution~' / 'SceneStory-Marketing-Copy-v2'
out.mkdir(parents=True, exist_ok=True)
en_summary = 'Stage multiple actors on one Unity Timeline. Edit routes, revise Hold and Move clips, control facing, and sync calibrated walk/run animations to travel distance.'
zh_summary = '在同一条 Unity Timeline 上编排多个角色的剧情走位。直接编辑场景路线、插入和调整停留与移动片段、控制转向，并让经过校准的走跑动画跟随移动距离。'
en = '''# Scene Story - Actor Routes for Timeline

## Summary
{summary}

## Description
Plan the performance. Revise the blocking.

Scene Story is a Unity Timeline tool for multi-actor staging and editable character routes. Arrange where actors move, when they wait, and how they turn in one sequence. Edit paths directly in Scene view and scrub the Timeline to inspect a moment without replaying the whole scene.

When the scene changes, revise the sequence where you work: insert a Hold or Move after the selected clip, reorder route clips, and keep automatically connected route starts following the preceding endpoint. Use ripple timing when changing a clip's duration to shift subsequent route clips on that actor's track. Independent animation, audio and other actor tracks remain separately authored.

### Control the performance
- **Stage several actors together.** Add scene objects to the current story and coordinate their timing on a shared Timeline.
- **Revise connected routes.** Edit straight or curved paths, insert clips and rearrange movement without manually reconnecting each automatic route start.
- **Direct facing over time.** Set heading offsets within a clip for a turn, a hold and a return; adjust the transition into the next clip. Facing controls the actor's overall orientation.
- **Link gait to distance.** After calibrating distance per animation cycle, synchronize looping walk/run playback to traveled distance, including movement-speed changes. Calibration and suitable animation are required; foot locking is not included.
- **Work on ground or in free space.** Bind walkable static meshes, exclude vegetation objects, and adjust grounded/free-space height transitions.
- **Find the right actor quickly.** Use per-actor route colors and select a route point to switch the active actor track.
- **Keep native Timeline workflows.** Combine authored movement with Unity's animation and audio tracks.

### Where it helps
Use Scene Story for cutscene blocking, narrative scene previews, scripted character performances and fixed exhibit sequences. Its focus is repeated authoring and revision of timed movement, rather than autonomous NPC decisions.

### Technical details
Requires Unity Timeline. Consult the release's compatibility and validation reports for exact tested Unity versions, rigs and render pipelines. Main authoring controls support English and Simplified Chinese. The release includes primitive-based examples and quick-start documentation. No external AI service or API key is needed to use the tool.

### Scope
Automatic pathfinding, dynamic avoidance, dialogue, camera direction, head-look IK, foot locking and automatic animation retargeting are not included. Vegetation filtering works at object/mesh level. Combined meshes containing overlapping walkable floors or both vegetation and ground may need separate walking surfaces. Do not install alongside another package containing the same Scene Story classes.

Promotional humanoid characters are demonstration assets and are not included in the Scene Story package. Camera shots used in promotional videos are demo-specific. The final compatibility table takes precedence over promotional examples.

### Support
GINDXRSTU · chrowder7@163.com · https://www.gindxrstu.com/
'''.format(summary=en_summary)
zh = '''# Scene Story — Unity Timeline 多角色剧情编排

## 摘要
{summary}

## 介绍
安排角色表演，随时修改走位。

Scene Story 是基于 Unity Timeline 的多角色剧情走位工具。把角色去哪里、何时停留、如何转身放在同一段演出中编排。直接在 Scene 视图编辑路径，拖动 Timeline 检查某个时刻，无需每次从头播放。

剧情修改时，可以在选中片段后插入停留或移动，调整路线片段顺序，让开启自动连接的后段起点继续跟随前段终点。修改时长时，可用“顺延后续片段”调整当前角色后续路线片段的时间。独立动画、音频和其他角色轨道仍由作者分别编排。

### 把表演细节变成可编辑的参数
- **多角色统一编排：** 将场景物体加入当前剧情，在共同时间轴上安排出发、等待和配合时机。
- **修改片段，接续路线：** 编辑直线或曲线路径，插入和调整移动顺序，减少手工重连自动路线起点的工作。
- **控制转向与回正：** 在片段内安排转向、保持、回正，调节跨片段朝向过渡时间；控制的是角色整体朝向。
- **让步态跟随移动距离：** 校准每个动画循环的移动距离后，使循环走跑动画跟随实际路程及移动速度变化。需要合适动画与校准，不包含脚部锁定。
- **编排地面与空中路线：** 绑定可行走静态 Mesh、排除植被对象，并控制贴地与自由空间之间的高度过渡。
- **直接找到要改的角色：** 独立路线配色，点击 Scene 路径点切换当前角色轨道。
- **配合原生 Timeline：** 路线编排与 Unity 动画轨、音频轨共同使用。

### 适用工作
适合过场动画走位预演、剧情分镜、多角色固定演出与展陈叙事。重点是精确安排和反复修改演出时间及路线。

### 技术说明
依赖 Unity Timeline。具体已验证的 Unity 版本、Rig 和渲染管线以发布包的兼容性及验证报告为准。主要编排界面提供中英文，附基础几何体示例与快速入门。使用工具无需外部 AI 服务或 API 密钥。

### 功能边界
不提供自动寻路、动态避障、对话系统、自动镜头、头部注视 IK、脚部锁定或自动动画重定向。植被筛选在对象/Mesh 层级执行；多层可走面或植被和地面合并在同一 Mesh 时，可能需要拆分行走碰撞面。不要与包含相同 Scene Story 类型的其他包重复安装。

宣传片中的人形角色属于演示素材，不包含在插件包内。宣传片镜头为演示专用编排。兼容范围以正式兼容性表为准。

### 联系方式
GINDXRSTU · chrowder7@163.com · https://www.gindxrstu.com/
'''.format(summary=zh_summary)
for lang, body in [('en',en),('zh-CN',zh)]:
    (out/f'Listing.{lang}.md').write_text(body,encoding='utf-8')
    (root/'Documentation~'/'SceneStoryStore'/f'Listing.{lang}.md').write_text(body,encoding='utf-8')

cues = [
 (0,7,'多角色，一条 Timeline','Stage multiple actors on one Timeline'),
 (8,15,'在选中片段后插入停留','Insert a Hold after the selected clip'),
 (16,23,'后续路线片段顺延，起点保持衔接','Shift later route clips; keep starts connected'),
 (24,30,'调整片段顺序，更新路线连接','Reorder clips and update route connections'),
 (31,36,'点击路径点，切换角色轨道','Select a route point to switch actor tracks'),
 (37,44,'边走边转向，自定义朝向曲线','Shape a heading curve while moving'),
 (45,52,'控制保持与回正的时间','Set when to hold and return the heading'),
 (53,60,'校准步幅，让动画跟随移动距离','Calibrate gait and sync animation to distance'),
 (61,68,'改变路程与时长，检查步态变化','Change distance and duration; inspect the gait'),
 (69,79,'拖动时间轴，检查关键时刻','Scrub the Timeline to inspect key moments'),
 (80,89,'编排角色表演，随时修改走位','Stage the performance. Revise the blocking.')]
def stamp(sec):return f'00:{sec//60:02d}:{sec%60:02d},000'
for lang in ['en','zh-CN','bilingual']:
    blocks=[]
    for i,(a,b,z,e) in enumerate(cues,1):
        line=e if lang=='en' else z if lang=='zh-CN' else z+'\n'+e
        blocks.append(f'{i}\n{stamp(a)} --> {stamp(b)}\n{line}\n')
    (out/f'StoryRevision-DRAFT.{lang}.srt').write_text('\n'.join(blocks),encoding='utf-8')
fields={'status':'local draft; not uploaded','title':'Scene Story - Actor Routes for Timeline','summary_en':en_summary,'summary_zh_CN':zh_summary,'website':'https://www.gindxrstu.com/','keywords':['Timeline','Cutscene','Cinematic','Character','Animation','Movement','Path','Route','Sequencing','Staging','Editor Tools','Multi Actor'],'subtitle_status':'planned timings for the new 90-second edit; not aligned to old 36-second video'}
assert 10<=len(en_summary)<=200 and 10<=len(zh_summary)<=200
assert len(fields['keywords'])<=15
(out/'Submission-Copy.json').write_text(json.dumps(fields,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'folder':str(out),'summary_chars_en':len(en_summary),'summary_chars_zh':len(zh_summary),'cues_per_language':len(cues)},ensure_ascii=False))
