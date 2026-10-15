"""生成本地商店资料与原创几何品牌图；不上传、不替代真实功能截图。"""
from pathlib import Path
import json, shutil
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Distribution~/SceneStory-UnityStore-Kit'
OUT.mkdir(exist_ok=True)
for name in ['Artwork', 'Screenshots', 'Video', 'SupportSite', 'Evidence']:
    (OUT / name).mkdir(exist_ok=True)

def write(name, text):
    (OUT / name).write_text(text.strip() + '\n', encoding='utf-8')

write('Listing.en.md', '''
# Scene Story - Actor Routes for Timeline

## Summary
Build multi-actor sequences with editable scene routes, Hold and Move clips, and native Timeline animation and audio tracks.

## Description
Scene Story helps you place actors, edit their movement through a scene and arrange the sequence on one Timeline. Select a scene object, add it as an actor, then insert Hold or Move clips. Drag route points in Scene view and preview the result while editing.

Use it for authored character staging, interactive exhibits and scripted scene sequences. It provides a route-editing workflow alongside Unity Timeline, with per-actor route colors and a compact sequence editor.

### Included features
- Add scene objects as actors in the current Director.
- Insert after the selected clip; reorder clips while reconnecting route starts.
- Edit straight or curved paths and select the active actor by clicking a route point.
- Follow ground, slopes and steps, or use free-space routes.
- Scan static meshes and exclude vegetation by object roots or name keywords.
- Blend ground height and facing between clips. Key facing offsets within a clip.
- Adjust movement speed curves independently from animation speed.
- Synchronize looping walk/run animations with calibrated travel distance.
- Use Timeline animation and audio tracks alongside movement.
- English and Simplified Chinese authoring interface, offline guide and primitive-based examples.

### Included examples
BasicStory: holding, movement, scale animation, turning and free-space movement.
MultiActor: three actors scheduled on one Timeline.
GroundLayers: a step, a slope and an overhead collider.
AudioAndFacing: animated heading offsets and a generated audio test tone.

### Requirements
Unity 2022.3 or later and Unity Timeline. Consult Compatibility.md for exact tested versions. Install com.unity.timeline through Package Manager before importing. No external online service, API key or AI service is needed to use this tool.

### Scope and limitations
Movement follows authored routes. Automatic pathfinding, obstacle avoidance, dialogue, camera direction, foot IK and automatic animation retargeting are not included. Embedded clip animation workflows are validated with Generic rigs; Humanoid retargeting is not advertised as tested. Distance synchronization requires a calibrated distance per animation cycle.

Vegetation filtering operates on objects/meshes. Grass and walkable ground merged into a single mesh need separate walking surfaces. Multiple overlapping walkable levels inside one combined MeshCollider are not guaranteed to select the desired surface.

Some runtime diagnostics and component tooltips are bilingual English/Chinese. Existing scene and clip names are not renamed when switching language. The included saved BasicStory example retains some original Chinese object names.

Do not install alongside a UPM or AIBuilder installation containing the same Scene Story classes. Update by importing over Assets/SceneStory with matching GUIDs. Keep your own scenes and assets outside the supplied Samples folder.

No third-party character models, textures or music are included. Screenshots and videos show included primitive-based examples. Unity Timeline is a separate Unity Registry dependency; see Third-Party Notices.txt.

Publisher: GINDXRSTU
Support: chrowder7@163.com
''')
write('AI-Disclosure.txt', '''
OpenAI Codex assisted with C# implementation, editor layout, localization and documentation. The code was revised for multi-actor Timeline authoring, route continuity, ground selection and compatibility with existing scene bindings. Automated tests, import checks and editor inspection were used to verify the package; exact results are in Compatibility.md. Marketing scene images and motion footage are rendered from the included Unity examples. Brand artwork uses simple geometric shapes and typography. The earlier AI-generated editor design mockup is not included as a product screenshot.
''')
write('Publisher-Profile.txt', '''
Publisher name: GINDXRSTU
Support email: chrowder7@163.com
Short description:
GINDXRSTU creates practical scene-authoring tools for Unity. Our focus is on editable workflows for actor movement and Timeline sequencing.

Website: publish the included SupportSite directory and enter its real public URL here. No live URL is claimed.
Legal identity, billing address, tax information and payout details: enter directly in your Unity account.
''')
write('Submission-Fields.json', json.dumps({
    'title': 'Scene Story - Actor Routes for Timeline', 'publisher': 'GINDXRSTU',
    'support_email': 'chrowder7@163.com', 'category_suggestion': 'Tools/Animation',
    'price_suggestion_usd': 29.99, 'price_approved': False,
    'keywords': ['Timeline', 'character', 'route', 'movement', 'cinematic', 'scene', 'animation'],
    'website_url': None, 'published': False, 'submitted': False,
    'description_file': 'Listing.en.md', 'ai_description_file': 'AI-Disclosure.txt'
}, ensure_ascii=False, indent=2))
write('QuickStart.en.md', '''
# Scene Story: installation and workflow

## 1. Install
Use a backup or a clean project. Install Timeline from Window > Package Manager > Unity Registry. Import the entire unitypackage, including the Editor scripts and Resources. The installation root is Assets/SceneStory. Do not combine it with the old com.xuyuchen.scene-story UPM package or another AIBuilder installation containing the same types.

Open Tools > Scene Story > Open Story Editor. Choose English or 简体中文 in the header. The language switch changes labels and newly created names; it does not rename existing scene objects.

## 2. Try a sample
Open Samples/StoreExamples/MultiActor.unity to see three actors on one Director. GroundLayers demonstrates ground projection. AudioAndFacing contains a generated test tone and a turn-and-return heading curve. BasicExample/BasicStory.unity also demonstrates scale and free-space movement.

For a sample created with the current render pipeline, use Tools > Scene Story > Create Basic Example and save into a new folder. Saved store examples use Built-in Standard materials; do not assume they are ready for URP/HDRP without conversion.

## 3. Create your story
Select an individual scene model in Hierarchy. Choose Tools > Scene Story > Create Story for Selection and save the Timeline outside Samples. To add another actor, select the model and click + Actor in the story window. Use the actor dropdown to switch tracks and the color chip to distinguish routes.

Select the outermost Prefab root when adding a Prefab instance. Avoid scene roots that contain other actors or Directors. The parent world scale must be one. An object with multiple Animators must be narrowed to one actor model.

## 4. Arrange clips
Select a clip and click + Hold or + Move to insert after it; an empty track starts with its first clip. Drag row handles to reorder. Route starts reconnect in Timeline order. A hold can follow the preceding endpoint. Disable following or auto-connect for independent placement.

The list and properties scroll separately. The selected clip header remains visible. Expand property groups to edit timing, placement, movement speed, facing and gait. Ripple later clips determines whether a duration edit shifts subsequent clips.

## 5. Edit paths and ground
Use Locate, select the route in Scene view and move the route points. A connected start is controlled by the preceding endpoint. Select a route point to switch the active actor track. Turn off ground following to edit world-space heights for flying motion.

Open Ground binding. Choose Static meshes and optionally an environment root. Set excluded keywords or excluded parent objects for vegetation. Add stone or slope roots to the include list when needed; explicit exclusions take priority. Scan and inspect the results before Apply. Applying reuses MeshColliders or adds missing colliders; Undo is supported.

Adjust allowed rise, drop and maximum slope. Keep route points near the intended walking level. Ground and vegetation in one combined mesh need separate walking surfaces. Ground transitions blend from the preceding segment height over the configured time.

## 6. Animation and heading
Use the linked native Timeline animation track for animation clips. Overlap animation clips in that track to blend. Movement clips themselves must not overlap. Facing transition time controls the turn from the preceding clip. Enable animated facing and edit the curve in seconds/degrees for turn, hold and return within one clip; the 90-degree preset provides a starting point.

Distance-synchronized gait is for cyclic walking/running. Calibrate the metres travelled in one animation cycle before enabling it. It overrides manual animation speed; holds pause gait phase. It is not automatic foot locking.

## 7. Preview and sound
Use the editor Preview button or native Timeline Preview separately. Stop preview to restore the actor before structural edits. The colored overview selects clips on click and scrubs time when dragged. In Play mode, StageStoryPlayback advances the Director with its native clock.

For audio, bind a Timeline AudioTrack to an enabled AudioSource and keep an enabled AudioListener in the scene. Check Game-view Mute Audio, AudioSource volume, clip timing and mixer settings. The example test tone is intentionally quiet.

## 8. Save and upgrade
Save both scene and Timeline after changes. Keep your production assets outside Samples. Import future packages over Assets/SceneStory with all files selected. unitypackage imports do not remove obsolete files; follow release notes if a future version supplies migration steps.

## Troubleshooting
- Duplicate types: remove the duplicate installation after backing up and checking bindings; do not keep both UPM and Assets copies.
- StageLanguage missing: reimport the complete package; do not import only StageStoryWindow.cs.
- Empty track cannot be edited: exit Play/Timeline Preview and check track locking.
- Route jumps to another level: move points close to the intended surface, review rise/drop limits and split stacked walking geometry if necessary.
- Missing ground: scan again after changing scene filters, check colliders and increase limits only as needed.
- Floating/sliding feet: calibrate cycle distance and use suitable animation. This tool does not supply foot IK.

Support: chrowder7@163.com. Include package version, Unity version, Timeline version, render pipeline, exact Console stack trace and reproduction steps. Send a small reproduction without confidential project content when possible.
''')
write('Reviewer-Notes.txt', '''
Suggested category: Tools / Animation.
Start with Assets/SceneStory/Samples/StoreExamples/MultiActor.unity.
Open Tools > Scene Story > Open Story Editor.
This is a route and multi-actor Timeline authoring tool. It requires Unity Timeline, not ProBuilder or an online AI service.
The package contains no dinosaurtosky art or production scenes. Examples use primitives, authored transform curves and a generated audio tone.
Runtime messages are English-readable bilingual diagnostics. Chinese legacy example/object names are preserved to avoid unintended renaming.
See Compatibility.md and Evidence for the exact validation scope. Tests and publishing utilities are not distributed as product assets.
''')

# 原创纯几何品牌图，不假装是产品截图。
FONT = Path('C:/Windows/Fonts/segoeuib.ttf')
REG = Path('C:/Windows/Fonts/segoeui.ttf')
def artwork(size, kind):
    w,h=size; im=Image.new('RGB',size,'#101e26'); d=ImageDraw.Draw(im)
    teal='#4ad0b6'; amber='#e9b15d'; muted='#a6bdc4'
    if kind=='icon':
        d.rounded_rectangle((w*.13,h*.16,w*.87,h*.84),radius=int(w*.08),outline=teal,width=max(2,int(w*.025)))
        for y,end,c in [(h*.34,w*.67,teal),(h*.51,w*.53,amber),(h*.68,w*.76,teal)]:
            d.line((w*.28,y,end,y),fill=c,width=max(3,int(w*.055)));d.ellipse((w*.2,y-w*.025,w*.25,y+w*.025),fill=c)
    else:
        def text(x,y,t,s,c='white',bold=False):d.text((int(x*w),int(y*h)),t,font=ImageFont.truetype(str(FONT if bold else REG),max(10,int(s*h))),fill=c)
        text(.075,.075,'GINDXRSTU',.024,muted,True)
        text(.075,.24,'SCENE',.115,'white',True);text(.075,.36,'STORY',.115,'white',True)
        text(.08,.57,'Actor routes for Timeline',.038,muted)
        text(.08,.83,'AUTHOR  /  ARRANGE  /  PREVIEW',.025,teal,True)
        for i,c in enumerate([teal,amber,'#839df0']):
            y=h*(.29+.15*i);x=w*.66;end=w*(.87-.06*(i%2))
            d.line((x,y,end,y),fill=c,width=max(3,int(h*.012)))
            r=h*.018;d.ellipse((x-r,y-r,x+r,y+r),fill=c);d.ellipse((end-r,y-r,end+r,y+r),outline=c,width=max(2,int(h*.004)))
        d.line((w*.73,h*.17,w*.73,h*.74),fill='#476471',width=max(1,int(h*.003)))
    im.save(OUT/'Artwork'/f'{kind}-{w}x{h}.png')
for size,kind in [((160,160),'icon'),((420,280),'card'),((1950,1300),'cover'),((1200,630),'social')]:artwork(size,kind)
write('Artwork/README.md', '''
Original geometric brand artwork, not product screenshots. Font: Windows Segoe UI, rasterized into artwork; no font files distributed. No Unity logo used. Dimensions follow the Unity 2022.3 publisher image documentation; confirm the actual upload slots in the current portal before submitting. Editable generation source: Tools~/prepare_unity_store_kit.py.
https://docs.unity3d.com/2022.3/Documentation/Manual/AssetStorePkgDetails.html
''')

shutil.copy2(OUT/'Artwork/social-1200x630.png',OUT/'SupportSite/cover.png')
shutil.copy2(OUT/'QuickStart.en.md',OUT/'SupportSite/QuickStart.md')
write('SupportSite/index.html', '''
<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Scene Story · GINDXRSTU</title>
<meta name="description" content="Actor routes and multi-actor sequencing for Unity Timeline. Documentation and support from GINDXRSTU.">
<style>*{box-sizing:border-box}body{margin:0;background:#101e26;color:#eef5f7;font:17px/1.65 system-ui,sans-serif}main{max-width:1060px;margin:auto;padding:32px 24px 64px}nav{display:flex;justify-content:space-between;gap:20px;border-bottom:1px solid #314650;padding-bottom:20px}a{color:#5ce0c6;text-underline-offset:5px}h1{font-size:clamp(42px,8vw,78px);line-height:1.08;margin:0 0 24px}h2{line-height:1.25;font-size:28px}header{padding:68px 0 48px;max-width:790px}.kicker{color:#5ce0c6;font-size:13px;letter-spacing:2px}.lead{font-size:23px;color:#c2d5dc}.grid{display:grid;grid-template-columns:1fr 1fr;gap:24px}section{padding:26px 0;border-top:1px solid #314650}.box{padding:24px;background:#172b35;border:1px solid #314650;border-radius:8px}.button{display:inline-block;background:#5ce0c6;color:#10242b;padding:12px 20px;text-decoration:none;font-weight:650;border-radius:5px}footer{margin-top:40px;color:#a6bdc4;font-size:14px}img{max-width:100%;border-radius:8px}li{margin:8px 0}@media(max-width:650px){.grid{grid-template-columns:1fr}header{padding-top:44px}nav{font-size:14px}}a:focus-visible{outline:3px solid #e9b15d;outline-offset:4px}</style>
<main><nav><strong>GINDXRSTU</strong><span><a href="#guide">Documentation</a> · <a href="#support">Support</a></span></nav>
<header><p class="kicker">SCENE AUTHORING FOR UNITY</p><h1>Place actors.<br>Shape their story.</h1><p class="lead">Editable routes and multi-actor sequences on one Timeline.</p><p>Scene Story brings Hold and Move clips, scene route editing, ground projection and facing transitions into a compact authoring window.</p><a class="button" href="QuickStart.md">Read the quick start</a></header>
<div class="grid"><div class="box"><h2>Arrange the sequence</h2><p>Add a scene object as an actor. Insert clips after the current selection and reorder connected movement.</p></div><div class="box"><h2>Shape the movement</h2><p>Edit route points, follow ground or move freely, and control how heading changes between clips.</p></div></div>
<section id="guide"><h2>Documentation</h2><p><a href="QuickStart.md">Installation, workflow and troubleshooting</a></p><p>Requires Unity Timeline. Supplied store scenes use Built-in materials. Check the package compatibility report for tested editor versions and pipelines.</p></section>
<section><h2>What it covers</h2><p>Authored routes, ground filtering, facing curves, animation tracks and optional gait synchronization. It does not provide automatic pathfinding, obstacle avoidance, dialogue, camera direction, foot IK or animation retargeting.</p></section>
<section id="support"><h2>Support</h2><p>Email <a href="mailto:chrowder7@163.com">chrowder7@163.com</a>.</p><p>Include your Scene Story version, Unity and Timeline versions, render pipeline, reproduction steps and the complete Console error. A small reproduction is more useful than a full production project.</p><p>Do not include passwords, API keys or confidential project assets.</p></section>
<section><h2>Privacy</h2><p>This static support page has no analytics, cookies, forms or third-party scripts. Contacting support opens your email application. Scene Story does not require an online account or AI service to run.</p></section>
<footer>Scene Story by GINDXRSTU. Unity is a trademark of Unity Technologies. This product is independently developed and is not endorsed by Unity.</footer></main></html>
''')
print(OUT)
