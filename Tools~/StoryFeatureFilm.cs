using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using AIBuilder.CreatureStage;
using AIBuilder.EditorTools.CreatureStage;

// 宣传演示编排器：调用真实插件方法；不模拟或绘制鼠标点击。
[InitializeOnLoad]
public static class StoryFeatureFilm
{
    const string Folder="Assets/FeatureFilmDemoTake6";
    const string Output="FeatureFilmCapture";
    static StageStoryWindow story;
    static SceneView scene;
    static TimelineEditorWindow timelineWindow;
    static PlayableDirector director;
    static StagePerformanceTrack[] tracks;
    static int frame, phase=-1;
    static double next;
    static bool running;
    static Texture2D image;
    static float calibratedCycleDistance;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static StoryFeatureFilm(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(File.Exists("FeatureFilm.request"))
        {
            File.Delete("FeatureFilm.request");
            try{Setup();}catch(Exception e){File.WriteAllText("FeatureFilm-error.txt",e.ToString());Debug.LogException(e);}
        }
        if(!running || EditorApplication.timeSinceStartup<next)return;
        try
        {
            // 上一次实际编辑器重绘完成后采图，再设置下一帧。
            if(frame>0)
            {
                var pixels=InternalEditorUtility.ReadScreenPixel(new Vector2(10,70),1900,1060);
                image.SetPixels(pixels);image.Apply();
                File.WriteAllBytes(Output+"/frame-"+(frame-1).ToString("D4")+".png",image.EncodeToPNG());
            }
            if(frame>=780){Finish();return;}
            Advance(frame/10.0);
            frame++;next=EditorApplication.timeSinceStartup+.1;
            EditorApplication.QueuePlayerLoopUpdate();scene.Repaint();story.Repaint();timelineWindow.Repaint();
        }
        catch(Exception e){running=false;File.WriteAllText("FeatureFilm-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Field(string name,object value)=>typeof(StageStoryWindow).GetField(name,Flags).SetValue(story,value);
    static void Call(string name,params object[] values)=>typeof(StageStoryWindow).GetMethod(name,Flags).Invoke(story,values);
    static void Trace(string text)=>File.AppendAllText(Output+"/Actions.txt",frame/10.0+"s "+text+"\n");
    static void Select(int actor,int clip)
    {
        story.StopPreview();Selection.activeObject=null;
        Field("selectedTrack",tracks[actor]);Field("selected",clip);Field("clipList",null);
        Field("scroll",Vector2.zero);Field("listScroll",Vector2.zero);
        TimelineEditor.selectedClip=tracks[actor].GetClips().OrderBy(c=>c.start).ElementAt(clip);
    }
    static void Setup()
    {
        Directory.CreateDirectory(Output);
        if(!Directory.Exists(Folder))AssetDatabase.CreateFolder("Assets","FeatureFilmDemoTake6");
        StageStoryWindow.StopAllPreviews();
        if(AnimationMode.InAnimationMode())AnimationMode.StopAnimationMode();
        var active=EditorSceneManager.GetActiveScene();
        if(active.isDirty)throw new Exception("当前场景有未保存修改；未切换场景。");
        EditorSceneManager.OpenScene("Assets/RendezvousDemo/Rendezvous.unity");
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),Folder+"/FeatureFilm.unity",true);
        EditorSceneManager.OpenScene(Folder+"/FeatureFilm.unity");
        director=UnityEngine.Object.FindObjectOfType<PlayableDirector>();
        var original=(TimelineAsset)director.playableAsset;
        if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original),Folder+"/FeatureFilm.playable"))throw new Exception("请使用新的演示目录，避免覆盖已存在资产。");
        var copy=AssetDatabase.LoadAssetAtPath<TimelineAsset>(Folder+"/FeatureFilm.playable");
        var oldTracks=original.GetOutputTracks().ToArray();var newTracks=copy.GetOutputTracks().ToArray();
        director.playableAsset=copy;
        for(int i=0;i<oldTracks.Length;i++)director.SetGenericBinding(newTracks[i],director.GetGenericBinding(oldTracks[i]));
        tracks=copy.GetOutputTracks().OfType<StagePerformanceTrack>().ToArray();
        foreach(var t in tracks)
        {
            var old=t.animationTrack;
            t.animationTrack=newTracks.OfType<AnimationTrack>().First(x=>x.name==old.name);
            foreach(var c in t.GetClips())((StagePerformanceClip)c.asset).followPrevious=true;
            var a=(StageActorDriver)director.GetGenericBinding(t);a.animator.fireEvents=false;
        }
        calibratedCycleDistance=CalibrateWalk();
        // 预览绑定到复制的 Timeline；原场景与原 Timeline 保留。
        EditorUtility.SetDirty(copy);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        scene=ScriptableObject.CreateInstance<SceneView>();scene.ShowUtility();scene.position=new Rect(10,70,1240,640);
        scene.LookAtDirect(new Vector3(0,0,2),Quaternion.Euler(48,-18,0),15);
        var native=TimelineEditor.GetOrCreateWindow();var nativeType=native.GetType();native.Close();
        timelineWindow=(TimelineEditorWindow)ScriptableObject.CreateInstance(nativeType);timelineWindow.ShowUtility();timelineWindow.position=new Rect(10,730,1240,400);timelineWindow.SetTimeline(director);timelineWindow.locked=true;
        story=ScriptableObject.CreateInstance<StageStoryWindow>();story.ShowUtility();story.position=new Rect(1260,70,650,1060);
        Field("director",director);Field("selectedTrack",tracks[0]);Field("advancedSettings",true);Field("gaitSettings",true);
        image=new Texture2D(1900,1060,TextureFormat.RGB24,false);
        frame=0;phase=-1;next=EditorApplication.timeSinceStartup+3;running=true;
        File.WriteAllText(Output+"/Actions.txt","真实 Unity 编辑器自动演示；未模拟人工鼠标操作。\n");
        File.WriteAllText("FeatureFilm-ready.txt","Ready");
    }
    static void Advance(double t)
    {
        var state=timelineWindow.GetType().GetProperty("state",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)?.GetValue(timelineWindow); state?.GetType().GetMethod("SetTimeAreaShownRange")?.Invoke(state,new object[]{0f,40f}); int p=t<8?0:t<19?1:t<29?2:t<37?3:t<53?4:t<68?5:6;
        if(p!=phase)
        {
            director.Stop();story.StopPreview();
            phase=p;
            if(p==0){Select(0,0);Trace("三角色共用一个 Director/Timeline。");}
            if(p==1){Select(0,0);Trace("选择首段，下一步调用插件 AddClip(false)。");}
            if(p==2)
            {
                Select(2,1);StageStoryWindow.ReorderClip(tracks[2],1,2);
                Field("clipList",null);TimelineEditor.Refresh(RefreshReason.ContentsModified);
                Trace("调用 StageStoryWindow.ReorderClip：调换巡查者两段路线。");
            }
            if(p==3)
            {
                StageStoryWindow.ReorderClip(tracks[2],2,1);
                var route=((StagePerformanceClip)tracks[1].GetClips().ElementAt(1).asset).route.Resolve(director);
                StageStoryWindow.SelectSceneRoute(route);story.SelectScenePoint(route.gameObject);
                Trace("调用 SelectSceneRoute/SelectScenePoint 切到橙色角色。");
            }
            if(p==4)
            {
                scene.LookAtDirect(new Vector3(5,1,-2),Quaternion.Euler(20,-15,0),7);Select(1,1);var c=tracks[1].GetClips().ElementAt(1);var d=(StagePerformanceClip)c.asset;
                d.animatedFacing=true;d.facingCurve=new AnimationCurve(new Keyframe(0,0),new Keyframe(1.5f,80),new Keyframe(3,80),new Keyframe(5.5f,0));
                d.facingTransitionDuration=1;Field("basicSettings",false);Field("placementSettings",false);Field("movementSettings",false);
                Trace("真实朝向曲线：0→80°→保持→0；整体朝向而非头部 IK。");
            }
            if(p==5)
            {
                scene.LookAtDirect(new Vector3(-2.5f,1,-1),Quaternion.Euler(20,-35,0),6);Select(0,0);var routeClip=tracks[0].GetClips().First();
                var ac=tracks[0].animationTrack.GetClips().First();var gait=StageGaitAnimationEditor.Enable(ac,director);
                gait.cycleDistance=calibratedCycleDistance;gait.synchronizeDistance=true;
                Field("movementSettings",true);Field("gaitSettings",true);Field("advancedSettings",false);
                Trace("启用 StageGaitAnimationAsset；循环距离采用近地支撑脚后移速度的中位数×动画周期。");
            }
            if(p==6){scene.LookAtDirect(new Vector3(0,0,2),Quaternion.Euler(48,-18,0),15);Select(2,2);Trace("来回定位 Timeline，三角色在同一时刻求值。");}
            director.RebuildGraph();
        }
        if(frame==115)
        {
            Call("AddClip",false);Trace("执行 AddClip(false)：当前角色新停留3秒，后续路线片段顺延。独立动画轨未自动重排。");
        }
        if(frame==175)
        {
            StageStoryWindow.DeleteClip(tracks[0],1);Field("clipList",null);director.RebuildGraph();Trace("恢复基线供后续章节演示。");
        }
        if(frame==605)
        {
            var c=tracks[0].GetClips().First();c.duration=4;
            tracks[0].animationTrack.GetClips().First().duration=4;
            director.RebuildGraph();Trace("移动与动画片段时长从6秒改为4秒；同步由距离驱动。");
        }
        double sample=p==0?t*3:p==1?3:p==2?19:p==3?9:p==4?6+(t-37)%6:p==5?(t-53)%4:36*(.5+.5*Math.Sin((t-68)*.65));
        if(p!=1&&p!=2&&p!=3)
        {
            Field("time",sample);Field("message",null);director.time=sample;director.Evaluate();
        }
        timelineWindow.SetTimeline(director);timelineWindow.locked=true;TimelineEditor.Refresh(RefreshReason.SceneNeedsUpdate);
    }
    static void Finish()
    {
        running=false;story.StopPreview();director.Stop();
        File.WriteAllText(Output+"/Complete.txt","780 frames; 78 seconds at 10 fps; real editor auto demonstration.");
        UnityEngine.Object.DestroyImmediate(image);
    }
    static float CalibrateWalk()
    {
        var actor=(StageActorDriver)director.GetGenericBinding(tracks[0]);
        var ac=tracks[0].animationTrack.GetClips().First();
        var anim=((AnimationPlayableAsset)ac.asset).clip;var rate=ac.timeScale;ac.timeScale=1;
        director.RebuildGraph();
        const int count=121;
        var left=new Vector3[count];var right=new Vector3[count];
        var log=new System.Text.StringBuilder("time,leftY,leftZ,rightY,rightZ\n");
        for(int i=0;i<count;i++)
        {
            director.time=anim.length*i/(count-1);director.Evaluate();
            left[i]=actor.transform.InverseTransformPoint(actor.animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
            right[i]=actor.transform.InverseTransformPoint(actor.animator.GetBoneTransform(HumanBodyBones.RightFoot).position);
            log.AppendLine(string.Join(",",director.time.ToString(System.Globalization.CultureInfo.InvariantCulture),left[i].y.ToString(System.Globalization.CultureInfo.InvariantCulture),left[i].z.ToString(System.Globalization.CultureInfo.InvariantCulture),right[i].y.ToString(System.Globalization.CultureInfo.InvariantCulture),right[i].z.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        File.WriteAllText(Output+"/FootCalibration.csv",log.ToString());
        var speeds=new System.Collections.Generic.List<float>();
        foreach(var feet in new[]{left,right})
        {
            float low=feet.Min(p=>p.y);
            for(int i=2;i<count-2;i++)
            {
                float velocity=(feet[i-1].z-feet[i].z)/(anim.length/(count-1));
                if(feet[i].y<low+.035f&&feet[i-1].y<low+.035f&&velocity>.1f)speeds.Add(velocity);
            }
        }
        ac.timeScale=rate;director.Stop();director.RebuildGraph();
        if(speeds.Count<8)throw new Exception("支撑脚采样不足，需人工校准。");
        speeds.Sort();float distance=speeds[speeds.Count/2]*anim.length;
        File.WriteAllText(Output+"/GaitCalibration.txt","Clip="+anim.name+"\nPeriod="+anim.length+"\nContact velocity samples="+speeds.Count+"\nMedian backward stance speed="+speeds[speeds.Count/2]+"\nCycle distance="+distance+"\nMethod: foot positions in actor-local coordinates, near minimum height + 0.035 m. Initial calibration; not foot locking.");
        if(distance<.2f||distance>4)throw new Exception("测量的步幅超出合理演示范围："+distance);
        return distance;
    }
}





