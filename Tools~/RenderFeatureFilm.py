"""真实编辑器采集帧 + 实际 Unity 渲染开场，生成双语功能演示。"""
from pathlib import Path
import subprocess, json

ROOT=Path(__file__).resolve().parents[1]
CAP=Path(r'E:\X\Validation\SceneStoryFeatureFilm_20260916\FeatureFilmCapture')
OUT=ROOT/'Distribution~'/'SceneStory-FeatureFilm-v1'
OUT.mkdir(parents=True,exist_ok=True)
FF=Path(r'C:\Users\ADMIN\.codex\visualizations\2026\08\27\01a04253-59a5-7670-a9b8-36e7ecfcf846\ffmpeg\extract\ffmpeg-9.0.1-essentials_build\bin\ffmpeg.exe')
INTRO=ROOT/'Distribution~'/'SceneStory-Rendezvous-v2'/'SceneStory-Rendezvous-v2-1080p.mp4'
def run(args):subprocess.run([str(FF),'-hide_banner','-loglevel','error','-y',*args],check=True,cwd=OUT)
def ass_time(sec):return f'{int(sec)//3600}:{int(sec)//60%60:02d}:{int(sec)%60:02d}.{round((sec%1)*100):02d}'

# 所有字幕以实际自动演示动作的采集时间为准，不沿用未录制的旧字幕草稿。
cues=[
(0,6,'Scene Story · 多角色剧情编排','Actor routes and staging for Unity Timeline'),
(6,14,'同一时间轴，检查多个角色的走位','Inspect multiple actors on one Timeline'),
(14,17.5,'选中片段，在它后面插入停留','Select a clip; insert a Hold after it'),
(17.5,23.5,'后续路线顺延，停留衔接前段终点','Later route clips shift; the Hold follows the endpoint'),
(25,35,'调整移动片段顺序，查看路线重新连接','Reorder Move clips and inspect route connections'),
(35,43,'选择场景路线，切换当前角色轨道','Select a scene route to switch actor tracks'),
(43,51,'用朝向曲线安排转向与保持','Shape when the actor turns and holds its heading'),
(51,59,'回正时间可控，移动路线保持独立','Control the return while preserving the movement route'),
(59,66.5,'启用距离同步，使用动画循环距离','Enable gait sync using distance per animation cycle'),
(66.5,74,'缩短移动时长，步态跟随路程推进','Shorten the move; gait follows traveled distance'),
(74,84,'前后定位，检查多角色关键时刻','Seek backward and forward to inspect the performance'),
(84,88,'Scene Story · 编排表演，随时改戏','Stage the performance. Revise the blocking.')]

header='''[Script Info]
ScriptType: v4.00+
PlayResX: 1920
PlayResY: 1080
WrapStyle: 2

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: CN,Microsoft YaHei,34,&H00FFFFFF,&H00FFFFFF,&H00302318,&H00302318,0,0,0,0,100,100,0,0,1,1,0,2,60,60,61,1
Style: EN,Segoe UI,28,&H00D9E2E5,&H00FFFFFF,&H00302318,&H00302318,0,0,0,0,100,100,0,0,1,1,0,2,60,60,24,1
Style: Badge,Microsoft YaHei,18,&H00C4D5DB,&H00FFFFFF,&H00302318,&H80302318,0,0,0,0,100,100,0,0,1,0,0,8,32,30,9,1
Style: Title,Segoe UI,76,&H00D6D95C,&H00FFFFFF,&H00302318,&H80302318,1,0,0,0,100,100,0,0,1,1,0,5,50,50,0,1
Style: SubTitle,Microsoft YaHei,30,&H00FFFFFF,&H00FFFFFF,&H00302318,&H80302318,0,0,0,0,100,100,0,0,1,1,0,5,50,50,0,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
'''
lines=[header]
for a,b,z,e in cues:
    lines += [f'Dialogue: 0,{ass_time(a)},{ass_time(b)},CN,,0,0,0,,{z}\n',f'Dialogue: 0,{ass_time(a)},{ass_time(b)},EN,,0,0,0,,{e}\n']
lines += ['Dialogue: 1,0:00:06.00,0:01:24.00,Badge,,0,0,0,,真实编辑器 · 自动演示 / Automated editor demonstration\n']
lines += [r'Dialogue: 2,0:01:24.00,0:01:28.00,Title,,0,0,0,,{\pos(960,400)}Scene Story'+'\n',r'Dialogue: 2,0:01:24.00,0:01:28.00,SubTitle,,0,0,0,,{\pos(960,495)}Actor Routes for Timeline'+'\n',r'Dialogue: 2,0:01:24.00,0:01:28.00,SubTitle,,0,0,0,,{\pos(960,650)}GINDXRSTU · gindxrstu.com'+'\n',r'Dialogue: 2,0:01:24.00,0:01:28.00,EN,,0,0,0,,{\pos(960,860)}演示人物不包含在插件中 / Demo characters not included'+'\n']
(OUT/'Captions.ass').write_text(''.join(lines),encoding='utf-8-sig')
def srt_time(s):
    ms=round(s*1000)
    return f'{ms//3600000:02d}:{ms//60000%60:02d}:{ms//1000%60:02d},{ms%1000:03d}'
for lang in ['zh-CN','en','bilingual']:
    blocks=[]
    for i,(a,b,z,e) in enumerate(cues,1):
        txt=z if lang=='zh-CN' else e if lang=='en' else z+'\n'+e
        blocks.append(f'{i}\n{srt_time(a)} --> {srt_time(b)}\n{txt}\n')
    (OUT/f'Captions.{lang}.srt').write_text('\n'.join(blocks),encoding='utf-8')

if not (CAP/'Complete.txt').exists():
    print('Caption assets ready; waiting for complete capture.');raise SystemExit(0)
frames=sorted(CAP.glob('frame-*.png'))
assert len(frames)==780, len(frames)
common=['-an','-c:v','libx264','-preset','fast','-crf','18','-pix_fmt','yuv420p']
run(['-ss','7','-t','6','-i',str(INTRO),'-vf','scale=1706:960,pad=1920:1080:107:0:0x182330,fps=30',*common,'Intro.mp4'])
run(['-framerate','10','-i',str(CAP/'frame-%04d.png'),'-vf','scale=1656:924,pad=1920:1080:132:36:0x182330,fps=30',*common,'Editor.mp4'])
run(['-loop','1','-framerate','30','-i',str(CAP/'frame-0000.png'),'-t','4','-vf','scale=1720:960,pad=1920:1080:100:0:0x182330,drawbox=x=0:y=0:w=iw:h=960:color=0x182330@0.88:t=fill',*common,'Outro.mp4'])
(OUT/'concat.txt').write_text("file 'Intro.mp4'\nfile 'Editor.mp4'\nfile 'Outro.mp4'\n",encoding='utf-8')
run(['-f','concat','-safe','0','-i','concat.txt','-vf','ass=Captions.ass',*common,'-movflags','+faststart','SceneStory-Features-ZH-EN-1080p.mp4'])
run(['-i','SceneStory-Features-ZH-EN-1080p.mp4','-f','null','-'])
(OUT/'RenderCheck.json').write_text(json.dumps({'frames':780,'editor_seconds':78,'total_seconds':88,'resolution':'1920x1080','fps':30,'decode':'passed','audio':'none','footage':'real Unity editor auto demonstration + real Unity scene render'},indent=2),encoding='utf-8')
print(OUT/'SceneStory-Features-ZH-EN-1080p.mp4')
