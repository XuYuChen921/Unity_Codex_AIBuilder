// 仅用于独立发布工程，不随商品包导出。
using System;
using System.IO;
using System.Linq;
using AIBuilder.CreatureStage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class SceneStoryStoreBuild
{
    private const string Folder = "Assets/SceneStory/Samples/StoreExamples";
    private static Material actorMaterial, groundMaterial;
    public static void Run()
    {
        if (Directory.Exists(Folder)) throw new Exception("示例目录已存在，避免覆盖。");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        actorMaterial = Material("Actor", new Color(.15f,.7f,.8f));
        groundMaterial = Material("Ground", new Color(.35f,.4f,.45f));
        MultiActor(); Ground(); AudioFacing();
        AssetDatabase.SaveAssets();
        Export();
    }
    public static void Export()
    {
        AssetDatabase.ExportPackage(new[] { "Assets/SceneStory/Runtime", "Assets/SceneStory/Editor", "Assets/SceneStory/Samples", "Assets/SceneStory/README.md", "Assets/SceneStory/使用教程.md", "Assets/SceneStory/QuickStart.md", "Assets/SceneStory/Third-Party Notices.txt" },
            Environment.GetEnvironmentVariable("SCENE_STORY_EXPORT"), ExportPackageOptions.Recurse);
    }
    private static Material Material(string name, Color color)
    {
        var result = new Material(Shader.Find("Standard")) { color = color };
        AssetDatabase.CreateAsset(result, Folder + "/" + name + ".mat"); return result;
    }
    private static PlayableDirector Scene(string name)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var d = new GameObject("Story - " + name).AddComponent<PlayableDirector>();
        var t = ScriptableObject.CreateInstance<TimelineAsset>();
        AssetDatabase.CreateAsset(t, Folder + "/" + name + ".playable");
        d.playableAsset = t; d.playOnAwake = false; d.extrapolationMode = DirectorWrapMode.Hold;
        var playback = d.gameObject.AddComponent<StageStoryPlayback>(); playback.director = d;
        var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.position = new Vector3(13,12,-14); camera.transform.LookAt(new Vector3(1,0,4));
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f,.1f,.16f);
        camera.gameObject.AddComponent<AudioListener>(); playback.sceneCamera = camera;
        var light = new GameObject("Directional Light").AddComponent<Light>(); light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(45,-25,0);
        Box("Ground", new Vector3(0,-.5f,4), new Vector3(20,1,20));
        return d;
    }
    private static Collider Box(string name, Vector3 position, Vector3 scale)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.isStatic = true;
        obj.transform.position = position; obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = groundMaterial; return obj.GetComponent<Collider>();
    }
    private static StagePerformanceTrack Actor(PlayableDirector d, string name, Vector3 position, Color color)
    {
        var root = new GameObject(name); root.transform.position = position;
        var skin = new GameObject("AnimationRoot"); skin.transform.SetParent(root.transform, false);
        var visual = new GameObject("Visual"); visual.transform.SetParent(skin.transform, false);
        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule); capsule.transform.SetParent(visual.transform, false);
        capsule.transform.localPosition = Vector3.up;
        UnityEngine.Object.DestroyImmediate(capsule.GetComponent<Collider>());
        capsule.GetComponent<Renderer>().sharedMaterial = actorMaterial;
        var actor = root.AddComponent<StageActorDriver>(); actor.animator = skin.AddComponent<Animator>(); actor.routeColor = color;
        var timeline = (TimelineAsset)d.playableAsset;
        var track = timeline.CreateTrack<StagePerformanceTrack>(null, name + " - Movement");
        track.animationTrack = timeline.CreateTrack<AnimationTrack>(null, name + " - Animation");
        track.animationTrack.trackOffset = TrackOffset.ApplySceneOffsets;
        d.SetGenericBinding(track, actor); d.SetGenericBinding(track.animationTrack, actor.animator);
        return track;
    }
    private static StagePerformanceClip Move(PlayableDirector d, StagePerformanceTrack track, Vector3 from, Vector3 to, double start, double duration, Collider[] surfaces = null)
    {
        var route = new GameObject(track.name + " Route").AddComponent<StageRoute>(); route.transform.SetParent(d.transform, false);
        route.points = new[] {from,to}; route.curved = false; route.snapToGround = surfaces != null;
        route.autoBindGround = false; route.groundSurfaces = surfaces ?? Array.Empty<Collider>();
        route.limitGroundProjection = true; route.groundStepUp = 1.5f; route.groundSlope = 60;
        var clip = track.CreateClip<StagePerformanceClip>(); clip.start = start; clip.duration = duration; clip.displayName = "Move";
        var data = (StagePerformanceClip)clip.asset; data.mode = StagePerformanceMode.Move;
        data.groundMode = surfaces == null ? StageGroundMode.FreeSpace : StageGroundMode.Ground;
        data.route.exposedName = new PropertyName(Guid.NewGuid().ToString()); d.SetReferenceValue(data.route.exposedName,route);
        return data;
    }
    private static void Hold(PlayableDirector d, StagePerformanceTrack track, Vector3 position, double start, double duration)
    {
        var anchor = new GameObject(track.name + " Hold").transform; anchor.SetParent(d.transform,false); anchor.position = position;
        var clip = track.CreateClip<StagePerformanceClip>(); clip.start=start; clip.duration=duration; clip.displayName="Hold";
        var data=(StagePerformanceClip)clip.asset; data.mode=StagePerformanceMode.Hold;
        data.station.exposedName=new PropertyName(Guid.NewGuid().ToString());d.SetReferenceValue(data.station.exposedName,anchor);
    }
    private static void Save(PlayableDirector d, string name)
    {
        EditorUtility.SetDirty(d.playableAsset); EditorUtility.SetDirty(d);
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(d.gameObject.scene,Folder+"/"+name+".unity");
    }
    private static void MultiActor()
    {
        var d=Scene("MultiActor");
        for(int i=0;i<3;i++)
        {
            var start=new Vector3(-4+i*4,0,0); var end=start+Vector3.forward*8;
            var track=Actor(d,"Actor "+(i+1),start,Color.HSVToRGB(i/3f,.8f,1));
            Hold(d,track,start,0,1+i);Move(d,track,start,end,1+i,5);Hold(d,track,end,6+i,2);
        }
        Save(d,"MultiActor");
    }
    private static void Ground()
    {
        var d=Scene("GroundLayers");
        var floor=GameObject.Find("Ground").GetComponent<Collider>();
        var cube=Box("Step",new Vector3(-2,.5f,4),new Vector3(3,1,3));
        var overhead=Box("Overhead - keep this enabled",new Vector3(-2,8,4),new Vector3(5,.2f,10));
        // 保留检测面，但使示例主视角能看清下方路线。
        overhead.GetComponent<Renderer>().enabled=false;
        var ramp=new GameObject("Hill"); ramp.isStatic=true;
        var mesh=new Mesh(); mesh.vertices=new[]{new Vector3(2,0,0),new Vector3(6,0,0),new Vector3(2,3,4),new Vector3(6,3,4),new Vector3(2,0,8),new Vector3(6,0,8)};
        mesh.triangles=new[]{0,2,1,1,2,3,2,4,3,3,4,5};mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Folder+"/Hill.asset");ramp.AddComponent<MeshFilter>().sharedMesh=mesh;
        ramp.AddComponent<MeshRenderer>().sharedMaterial=groundMaterial;var slope=ramp.AddComponent<MeshCollider>();slope.sharedMesh=mesh;
        var a=Actor(d,"Step actor",new Vector3(-2,0,0),Color.cyan);
        var b=Actor(d,"Hill actor",new Vector3(4,0,0),Color.yellow);
        Move(d,a,new Vector3(-2,0,0),new Vector3(-2,0,8),0,8,new[]{floor,cube,overhead});
        var hill=Move(d,b,new Vector3(4,0,0),new Vector3(4,0,8),0,8,new Collider[]{floor,slope,overhead});
        hill.route.Resolve(d).groundStepUp=.5f;
        Save(d,"GroundLayers");
    }
    private static void AudioFacing()
    {
        var d=Scene("AudioAndFacing");var track=Actor(d,"Performer",Vector3.zero,Color.magenta);
        var data=Move(d,track,Vector3.zero,Vector3.forward*8,0,8);
        data.animatedFacing=true;data.facingCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(2,90),new Keyframe(4,90),new Keyframe(6,0),new Keyframe(8,0));
        var clip=track.animationTrack.CreateClip<AnimationPlayableAsset>();clip.start=0;clip.duration=8;
        var anim=(AnimationPlayableAsset)clip.asset;anim.clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/SceneStory/Samples/BasicExample/Pulse.anim");
        if(anim.clip==null)throw new Exception("Missing primitive animation sample");anim.loop=AnimationPlayableAsset.LoopMode.On;
        var path=Folder+"/ExampleTone.wav";
        using(var writer=new BinaryWriter(File.Create(path)))
        {
            const int rate=22050, samples=rate*8;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples*2);
            for(int i=0;i<samples;i++){double envelope=Math.Min(1,Math.Min(i/1102.0,(samples-i)/1102.0));writer.Write((short)(Math.Sin(i*2*Math.PI*440/rate)*32767*.08*envelope));}
        }
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var audio=((TimelineAsset)d.playableAsset).CreateTrack<AudioTrack>(null,"Example audio");
        var audioClip=audio.CreateClip<AudioPlayableAsset>();audioClip.start=0;audioClip.duration=8;
        ((AudioPlayableAsset)audioClip.asset).clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        var source=d.gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;d.SetGenericBinding(audio,source);
        Save(d,"AudioAndFacing");
    }
}
