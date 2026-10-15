using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Playables;
using AIBuilder.CreatureStage;

// 每张图片跨越真实 PlayerLoop，确保蒙皮网格更新，而非同一编辑器帧循环截图。
public class RendezvousRecorder : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin()
    {
        var marker=Path.Combine(Application.dataPath,"../RecordRendezvous.request");
        if(!File.Exists(marker))return;File.Delete(marker);
        Camera.main.gameObject.AddComponent<RendezvousRecorder>();
    }
    IEnumerator Start()
    {
        var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../MarketingRendezvous-v2"));Directory.CreateDirectory(output);
        var d=FindObjectOfType<PlayableDirector>();var playback=d.GetComponent<StageStoryPlayback>();playback.enabled=false;
        d.timeUpdateMode=DirectorUpdateMode.Manual;d.Play();
        var cam=GetComponent<Camera>();var rt=new RenderTexture(1920,1080,24);var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        var evidence=new StringBuilder("frame,actor,kneeX,kneeY,kneeZ,kneeW\n");var errors=new StringBuilder();
        Application.LogCallback log=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)errors.AppendLine(m);};Application.logMessageReceived+=log;
        var actors=FindObjectsOfType<StageActorDriver>();Time.captureFramerate=30;
        for(int i=0;i<1080;i++)
        {
            d.time=i/30.0;d.Evaluate();
            yield return new WaitForEndOfFrame();
            foreach(var a in actors){var q=a.animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).localRotation;evidence.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5}\n",i,a.name,q.x,q.y,q.z,q.w);}
            var previous=RenderTexture.active;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();
            File.WriteAllBytes(Path.Combine(output,"frame-"+i.ToString("D4")+".png"),tex.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=previous;
        }
        Time.captureFramerate=0;Application.logMessageReceived-=log;File.WriteAllText(Path.Combine(output,"Bones.csv"),evidence.ToString());File.WriteAllText(Path.Combine(output,"Errors.txt"),errors.ToString());File.WriteAllText(Path.Combine(output,"Complete.txt"),"1080 real PlayerLoop frames captured");Destroy(tex);Destroy(rt);
    }
}
