using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using AIBuilder.CreatureStage;

// 仅供独立宣传演示工程使用，不进入商品包。
[InitializeOnLoad]
public static class RendezvousBuild
{
    const string F = "Assets/RendezvousDemo";
    const string C = "Assets/Starter Assets/Runtime/ThirdPersonController/Character/";
    static PlayableDirector director;
    static TimelineAsset timeline;
    static Collider floor;
    static Material stone, dark, trim;
    static RendezvousBuild() { EditorApplication.delayCall += Auto; }
    static void Auto()
    {
        if(File.Exists("FixRendezvous.request")) {File.Delete("FixRendezvous.request");FixClips();return;}
        if(File.Exists("AuditRendezvous.request")) {File.Delete("AuditRendezvous.request");Audit();return;}
        if (File.Exists("PolishRendezvous.request")) { File.Delete("PolishRendezvous.request"); Polish(); return; }
        if (File.Exists("CaptureRendezvous.request")) { File.Delete("CaptureRendezvous.request"); Capture(true); return; }
        if (!File.Exists("BuildRendezvous.request") || EditorApplication.isCompiling) return;
        File.Delete("BuildRendezvous.request");
        try { Build(); File.WriteAllText("Rendezvous-build.txt", "SUCCESS"); }
        catch(Exception e) { File.WriteAllText("Rendezvous-build.txt", e.ToString()); Debug.LogException(e); }
    }
    static void FixClips()
    {
        var d=UnityEngine.Object.FindObjectOfType<PlayableDirector>();
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before patching demo clips.");
        foreach(var track in ((TimelineAsset)d.playableAsset).GetOutputTracks().OfType<AnimationTrack>())
        foreach(var clip in track.GetClips())
        {
            var ap=clip.asset as AnimationPlayableAsset;if(ap==null||ap.clip==null)continue;
            string path=F+"/"+ap.clip.name+"-Timeline.anim";var copy=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(copy==null){copy=UnityEngine.Object.Instantiate(ap.clip);copy.name=ap.clip.name;AnimationUtility.SetAnimationEvents(copy,Array.Empty<AnimationEvent>());AssetDatabase.CreateAsset(copy,path);}
            ap.clip=copy;EditorUtility.SetDirty(ap);
        }
        EditorUtility.SetDirty(d.playableAsset);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        File.WriteAllText("Rendezvous-fix.txt","Event-free demo clips created; source FBX unchanged.");
    }
    static Material Mat(string name, Color color, bool glow=false)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",.25f);
        if(glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",color*2); }
        AssetDatabase.CreateAsset(m,F+"/"+name+".mat"); return m;
    }
    static GameObject Box(string name,Vector3 p,Vector3 size,Material mat)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name;
        o.transform.position=p; o.transform.localScale=size; o.GetComponent<Renderer>().sharedMaterial=mat; return o;
    }
    [MenuItem("Tools/Scene Story Demo/Build Rendezvous")]
    public static void Build()
    {
        if(Directory.Exists(F)) throw new Exception("Demo already exists; no overwrite.");
        if(EditorSceneManager.GetActiveScene().isDirty) throw new Exception("Save current scene before generation.");
        Directory.CreateDirectory(F); AssetDatabase.Refresh();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        stone=Mat("Limestone",new Color(.52f,.47f,.38f)); dark=Mat("Basalt",new Color(.10f,.16f,.19f)); trim=Mat("Signal",new Color(.08f,.8f,.72f),true);
        floor=Box("Plaza",new Vector3(0,-.3f,2),new Vector3(24,.6f,22),stone).GetComponent<Collider>();
        for(int x=-5;x<=5;x++) Box("Paving joint",new Vector3(x*2,.002f,2),new Vector3(.025f,.008f,22),dark);
        for(int z=-4;z<=6;z++) Box("Paving joint",new Vector3(0,.003f,z*2),new Vector3(24,.008f,.025f),dark);
        for(int i=0;i<7;i++)
        {
            float x=-9+i*3;
            Box("Ruined column",new Vector3(x,2.1f,10),new Vector3(.8f,4.2f,.8f),stone);
            Box("Column cap",new Vector3(x,4.25f,10),new Vector3(1.1f,.3f,1.1f),dark);
        }
        Box("Rear lintel",new Vector3(0,4.6f,10),new Vector3(20,.4f,1),stone);
        Box("Beacon base",new Vector3(0,.25f,3),new Vector3(1.8f,.5f,1.8f),dark);
        Box("Beacon",new Vector3(0,1.2f,3),new Vector3(.4f,1.4f,.4f),trim);
        for(int i=0;i<5;i++) Box("Exit marker",new Vector3(5+i, .018f,5),new Vector3(.55f,.025f,.12f),trim);
        var ramp=new GameObject("Walkable ramp"); var mesh=new Mesh();
        mesh.vertices=new[]{new Vector3(-9,0,0),new Vector3(-6,0,0),new Vector3(-9,2,8),new Vector3(-6,2,8)};
        mesh.triangles=new[]{0,2,1,1,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,F+"/Ramp.asset");
        ramp.AddComponent<MeshFilter>().sharedMesh=mesh;ramp.AddComponent<MeshRenderer>().sharedMaterial=dark;var slope=ramp.AddComponent<MeshCollider>();slope.sharedMesh=mesh;
        var sun=new GameObject("Golden hour").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.7f;sun.color=new Color(1,.87f,.67f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(38,-35,0);
        RenderSettings.ambientLight=new Color(.42f,.49f,.56f);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(15,11,-18);camera.transform.LookAt(new Vector3(0,1,3));camera.fieldOfView=43;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.11f,.18f,.23f);camera.gameObject.AddComponent<AudioListener>();
        director=new GameObject("RENDEZVOUS — Main Timeline").AddComponent<PlayableDirector>();timeline=ScriptableObject.CreateInstance<TimelineAsset>();AssetDatabase.CreateAsset(timeline,F+"/Rendezvous.playable");director.playableAsset=timeline;director.extrapolationMode=DirectorWrapMode.Hold;director.playOnAwake=false;
        var playback=director.gameObject.AddComponent<StageStoryPlayback>();playback.director=director;playback.sceneCamera=camera;
        var a=Actor("01 Scout",new Vector3(-3,0,-5),new Color(.05f,.8f,.74f));
        Segment(a,0,6,new Vector3(-3,0,-5),new Vector3(-2,0,2),"Walk",false);
        Segment(a,6,20,new Vector3(-2,0,2),new Vector3(-2,0,2),"Wait for the team",true);
        Segment(a,26,10,new Vector3(-2,0,2),new Vector3(9,0,7),"Depart together",false);
        var b=Actor("02 Courier",new Vector3(10,0,-5),new Color(1,.4f,.08f));
        Segment(b,0,6,new Vector3(10,0,-5),new Vector3(10,0,-5),"Stand by",true);
        Segment(b,6,6,new Vector3(10,0,-5),new Vector3(2,0,1),"Run to rendezvous",false,true);
        Segment(b,12,15,new Vector3(2,0,1),new Vector3(2,0,1),"Wait",true);
        Segment(b,27,9,new Vector3(2,0,1),new Vector3(10,0,6),"Follow scout",false);
        var c=Actor("03 Ranger",new Vector3(-7.5f,2,8),new Color(.6f,.28f,.9f));
        Segment(c,0,10,new Vector3(-7.5f,2,8),new Vector3(-7.5f,2,8),"Lookout",true);
        Segment(c,10,8,new Vector3(-7.5f,2,8),new Vector3(-7.5f,0,0),"Descend ramp",false,false,new[]{floor,slope});
        Segment(c,18,6,new Vector3(-7.5f,0,0),new Vector3(-1,0,0),"Join team",false);
        Segment(c,24,4,new Vector3(-1,0,0),new Vector3(-1,0,0),"Ready",true);
        Segment(c,28,8,new Vector3(-1,0,0),new Vector3(8,0,5),"Leave plaza",false);
        EditorUtility.SetDirty(timeline);EditorUtility.SetDirty(director);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),F+"/Rendezvous.unity");
        Selection.activeGameObject=director.gameObject; Capture(false);
    }
    static StagePerformanceTrack Actor(string name,Vector3 p,Color color)
    {
        var root=new GameObject(name);root.transform.position=p;
        var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(C+"Models/Armature.fbx"),root.transform);model.name="Character";model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
        var animator=model.GetComponent<Animator>();if(animator==null)animator=model.AddComponent<Animator>();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        if(animator.avatar==null) animator.avatar=AssetDatabase.LoadAllAssetsAtPath(C+"Models/Armature.fbx").OfType<Avatar>().First();
        var m=Mat(name.Replace(" ","-")+"-Suit",color);
        foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.sharedMaterials=Enumerable.Repeat(m,renderer.sharedMaterials.Length).ToArray();
        var actor=root.AddComponent<StageActorDriver>();actor.animator=animator;actor.routeColor=color;
        var track=timeline.CreateTrack<StagePerformanceTrack>(null,name+" / Route");track.animationTrack=timeline.CreateTrack<AnimationTrack>(null,name+" / Animation");track.animationTrack.trackOffset=TrackOffset.ApplySceneOffsets;
        director.SetGenericBinding(track,actor);director.SetGenericBinding(track.animationTrack,animator);return track;
    }
    static void Segment(StagePerformanceTrack track,double start,double duration,Vector3 from,Vector3 to,string label,bool hold,bool run=false,Collider[] ground=null)
    {
        var clip=track.CreateClip<StagePerformanceClip>();clip.start=start;clip.duration=duration;clip.displayName=label;var data=(StagePerformanceClip)clip.asset;data.mode=hold?StagePerformanceMode.Hold:StagePerformanceMode.Move;data.followPrevious=false;
        if(hold)
        {
            var point=new GameObject(track.name+" / "+label).transform;point.SetParent(director.transform);point.position=from;point.rotation=Quaternion.LookRotation(new Vector3(0,0,3)-new Vector3(from.x,0,from.z));
            data.station.exposedName=GUID.Generate().ToString();director.SetReferenceValue(data.station.exposedName,point);
            data.animatedFacing=true;data.facingCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe((float)duration*.35f,25),new Keyframe((float)duration*.75f,-20),new Keyframe((float)duration,0));
        }
        else
        {
            var route=new GameObject(track.name+" / "+label).AddComponent<StageRoute>();route.transform.SetParent(director.transform);route.points=new[]{from,to};route.curved=false;route.autoBindGround=false;route.snapToGround=true;route.groundSurfaces=ground??new[]{floor};route.limitGroundProjection=true;route.groundStepUp=.5f;route.groundSlope=60;
            data.groundMode=StageGroundMode.Ground;data.route.exposedName=GUID.Generate().ToString();director.SetReferenceValue(data.route.exposedName,route);
        }
        var path=C+"Animations/"+(hold?"Stand--Idle":run?"Locomotion--Run_N":"Locomotion--Walk_N")+".anim.fbx";
        var source=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(x=>!x.name.StartsWith("__"));
        var ac=track.animationTrack.CreateClip<AnimationPlayableAsset>();ac.start=start;ac.duration=duration;ac.displayName=hold?"Idle":run?"Run":"Walk";var ap=(AnimationPlayableAsset)ac.asset;ap.clip=source;ap.loop=AnimationPlayableAsset.LoopMode.On;ap.removeStartOffset=true;ap.applyFootIK=true;
        if(!hold) ac.timeScale=Mathf.Clamp(Vector3.Distance(from,to)/(float)duration/(run?4f:1.4f),.5f,1.6f);
    }
    [MenuItem("Tools/Scene Story Demo/Capture Video Frames")]
    public static void Video() { Capture(true); }
    static void Polish()
    {
        foreach(var actor in UnityEngine.Object.FindObjectsOfType<StageActorDriver>())
        {
            actor.animator.fireEvents=false;
            foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var material=materials[i]; material.SetColor("_BaseColor", Color.Lerp(actor.routeColor,Color.gray,.45f));material.SetFloat("_Metallic",.45f);material.SetFloat("_Smoothness",.4f);EditorUtility.SetDirty(material);
                }
            }
        }
        var camera=Camera.main;camera.transform.position=new Vector3(10,6,-12);camera.transform.LookAt(new Vector3(0,1,2));camera.fieldOfView=48;
        var rig=camera.GetComponent<RendezvousCamera>();if(rig==null)rig=camera.gameObject.AddComponent<RendezvousCamera>();rig.director=UnityEngine.Object.FindObjectOfType<PlayableDirector>();
        Physics.SyncTransforms();
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());Capture(false);
    }
    static void Frame(Camera camera,double time)
    {
        Vector3 position, target;
        if(time<6) {position=Vector3.Lerp(new Vector3(10,7,-14),new Vector3(8,5,-10),(float)time/6);target=new Vector3(-1,1,1);}
        else if(time<12) {position=new Vector3(9,4,-9);target=new Vector3(2,1,0);}
        else if(time<20) {position=new Vector3(2,5,-9);target=new Vector3(-5,1,3);}
        else if(time<28) {position=new Vector3(5,3,-7);target=new Vector3(-.5f,1,2);}
        else {position=new Vector3(15,7,-9);target=Vector3.Lerp(new Vector3(2,1,3),new Vector3(7,1,5),(float)(time-28)/8);}
        camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=48;
    }
    static void Audit()
    {
        var d=UnityEngine.Object.FindObjectOfType<PlayableDirector>();var a=UnityEngine.Object.FindObjectsOfType<StageActorDriver>().First(x=>x.name.StartsWith("01"));
        var report=new System.Text.StringBuilder();
        foreach(double time in new[]{1.0,1.2,1.4,1.6,1.8}) {
            d.time=time;d.Evaluate();var bone=a.animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            report.AppendLine(time+" position="+a.transform.position+" bone="+bone.localRotation+" human="+a.animator.isHuman);
        }
        d.Stop();File.WriteAllText("Rendezvous-animation-audit.txt",report.ToString());
    }
    static void Capture(bool video)
    {
        director=UnityEngine.Object.FindObjectOfType<PlayableDirector>();var camera=Camera.main;
        Physics.SyncTransforms();
        var output=Path.GetFullPath("MarketingRendezvous");Directory.CreateDirectory(output);
        var errors=new System.Collections.Generic.List<string>();
        Application.LogCallback handler=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception) errors.Add(message);};
        Application.logMessageReceived+=handler;
        var actors=UnityEngine.Object.FindObjectsOfType<StageActorDriver>().OrderBy(a=>a.name).ToArray();
        var samples=new System.Text.StringBuilder("time,actor,x,y,z\n");
        var rt=new RenderTexture(1920,1080,24);var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try {
            int count=video?1080:4;
            for(int i=0;i<count;i++) {
                director.time=video?i/30.0:new[]{4.0,11,21,25}[i];director.Evaluate();Frame(camera,director.time);
                foreach(var actor in actors) {var p=actor.transform.position;samples.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0:F4},{1},{2:F5},{3:F5},{4:F5}\n",director.time,actor.name,p.x,p.y,p.z);}
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();
                File.WriteAllBytes(Path.Combine(output,(video?"frame-"+i.ToString("D4"):"Still-"+i)+".png"),tex.EncodeToPNG());
            }
        } finally {camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);director.Stop();Application.logMessageReceived-=handler;}
        File.WriteAllText(Path.Combine(output,"Positions.csv"),samples.ToString());File.WriteAllLines(Path.Combine(output,"CaptureErrors.txt"),errors);
        File.WriteAllText(Path.Combine(output,video?"VideoCapture.txt":"Stills.txt"),"Actual Unity camera renders; Timeline evaluated. Not an editor interaction recording.");
    }
}
