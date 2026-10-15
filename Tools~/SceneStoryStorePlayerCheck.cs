// 仅用于候选包验证，不随商店包分发。
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Playables;
using AIBuilder.CreatureStage;

public sealed class SceneStoryStorePlayerCheck : MonoBehaviour
{
    private float started;
    private string report;
    private int errors;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"--story-report");
        if(index<0 || index+1>=args.Length)return;
        var check=new GameObject("Store validation only").AddComponent<SceneStoryStorePlayerCheck>();
        check.report=args[index+1];check.started=Time.realtimeSinceStartup;Application.runInBackground=true;
        Application.logMessageReceived+=check.OnLog;
    }
    private void OnLog(string message,string stack,LogType type)
    {if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errors++;}
    private void Update()
    {
        var d=FindObjectOfType<PlayableDirector>();
        if(d!=null && d.time>=1.25)
        {
            var actor=FindObjectOfType<StageActorDriver>();
            var audio=d.GetComponent<AudioSource>();
            bool ok=actor!=null && actor.transform.position.z>.3f && audio!=null && audio.isPlaying && errors==0;
            File.WriteAllText(report,"{\"passed\":"+(ok?"true":"false")+",\"directorTime\":"+d.time.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"audioScheduled\":"+(audio!=null&&audio.isPlaying?"true":"false")+",\"errors\":"+errors+"}");
            Application.Quit(ok?0:1);enabled=false;
        }
        else if(Time.realtimeSinceStartup-started>30)
        {File.WriteAllText(report,"{\"passed\":false,\"reason\":\"playback timeout\"}");Application.Quit(1);enabled=false;}
    }
}
