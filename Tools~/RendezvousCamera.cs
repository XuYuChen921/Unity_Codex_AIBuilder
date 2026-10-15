using UnityEngine;
using UnityEngine.Playables;

// 演示专用镜头编排，不属于 Scene Story 自动镜头功能。
public class RendezvousCamera : MonoBehaviour
{
    public PlayableDirector director;
    void Awake()
    {
        // fireEvents 不随场景序列化，运行时需重新关闭原控制器专用事件。
        foreach(var actor in FindObjectsOfType<AIBuilder.CreatureStage.StageActorDriver>())
            if(actor.animator!=null) actor.animator.fireEvents=false;
    }
    void LateUpdate()
    {
        if(director==null)return;
        var time=director.time;Vector3 position,target;
        if(time<6){position=Vector3.Lerp(new Vector3(10,7,-14),new Vector3(8,5,-10),(float)time/6);target=new Vector3(-1,1,1);}
        else if(time<12){position=new Vector3(9,4,-9);target=new Vector3(2,1,0);}
        else if(time<20){position=new Vector3(2,5,-9);target=new Vector3(-5,1,3);}
        else if(time<28){position=new Vector3(5,3,-7);target=new Vector3(-.5f,1,2);}
        else{position=new Vector3(15,7,-9);target=Vector3.Lerp(new Vector3(2,1,3),new Vector3(7,1,5),(float)(time-28)/8);}
        transform.position=position;transform.LookAt(target);GetComponent<Camera>().fieldOfView=48;
    }
}
