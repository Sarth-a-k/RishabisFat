using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace SunkenPrism
{
    /// <summary>Opt-in standalone smoke run: -prismSmokeTest. Never active during ordinary play.</summary>
    public sealed class PrismRuntimeCheck : MonoBehaviour
    {
        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-prismSmokeTest")<0) yield break;
            Application.logMessageReceived+=(condition,stack,type)=>{if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)Application.Quit(1);};
            yield return null;
            var p=ExplorerController.Instance;
            if(p==null||p.View==null||PuzzleGame.Instance==null)throw new Exception("Runtime services did not initialize");
            p.enabled=false;p.transform.rotation=Quaternion.identity;p.View.transform.localRotation=Quaternion.identity;
            Debug.Log("PRISM_RUNTIME_INITIALIZED");
            yield return new WaitForSeconds(.6f);
            string capture=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Renders/05-Playable-FPS.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(capture));
            ScreenCapture.CaptureScreenshot(capture);
            yield return new WaitForSeconds(.5f);
            p.enabled=false;
            p.Teleport(new Vector3(0,.1f,4));Physics.SyncTransforms();
            Step(p,Vector2.zero,30);
            float start=p.transform.position.z;Step(p,Vector2.up,60);float walked=p.transform.position.z-start;
            Require(walked>3.1f&&walked<3.9f,"walking distance");
            p.Teleport(new Vector3(0,.1f,4));Physics.SyncTransforms();Step(p,Vector2.zero,30);
            start=p.transform.position.z;Step(p,Vector2.up,60,true);float sprinted=p.transform.position.z-start;
            Require(sprinted>walked*1.4f,"sprint faster than walk");
            float baseY=p.transform.position.y;p.SimulationStep(Vector2.zero,false,false,true,1f/60);
            float peak=p.transform.position.y;for(int i=0;i<65;i++){p.SimulationStep(Vector2.zero,false,false,false,1f/60);peak=Mathf.Max(peak,p.transform.position.y);}
            Require(peak-baseY>.9f&&Mathf.Abs(p.transform.position.y-baseY)<.12f,"jump and grounded landing");
            for(int i=0;i<30;i++)p.SimulationStep(Vector2.zero,false,true,false,1f/60);
            Require(p.GetComponent<CharacterController>().height<1.2f,"crouch capsule");
            Step(p,Vector2.zero,30);
            p.Teleport(new Vector3(0,.1f,36));Physics.SyncTransforms();Step(p,Vector2.up,120);
            Require(p.transform.position.z<38.7f,"unsolved seal blocks movement");
            p.Teleport(new Vector3(8,.1f,25));Physics.SyncTransforms();Step(p,Vector2.right,120);
            Require(p.transform.position.x<10.5f,"stone wall collision");
            Debug.Log("PRISM_MOTOR_PASS: walk, sprint, jump, landing, crouch, locked gate, wall collision");
            PuzzleGame.Instance.RunSelfTest();Physics.SyncTransforms();
            foreach(float gateZ in new[]{39f,73f,103f,135f}){
                p.Teleport(new Vector3(1,.1f,gateZ-1.5f));Physics.SyncTransforms();Step(p,Vector2.up,90);
                Require(p.transform.position.z>gateZ+1,"unlocked seal traversable at z="+gateZ);
            }
            p.Teleport(new Vector3(1,.1f,136));Physics.SyncTransforms();Step(p,Vector2.up,480);
            Require(p.transform.position.z>162&&p.transform.position.y < -5.5f&&p.transform.position.y > -6.3f,"descending passage connects to lower chamber; feet="+p.transform.position);
            Debug.Log("PRISM_ROUTE_PASS: all gates traversable; descent reaches lower floor");
            p.Teleport(new Vector3(2,.1f,50));p.transform.rotation=Quaternion.identity;p.View.transform.localRotation=Quaternion.Euler(0,-14,0);
            yield return new WaitForSeconds(.3f);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Renders/06-Prism-Solved-FPS.png")));
            yield return new WaitForSeconds(.3f);
            Debug.Log("PRISM_RUNTIME_SMOKE_PASS");
            yield return null;
            Application.Quit(0);
        }
        static void Step(ExplorerController p,Vector2 axes,int frames,bool sprint=false){for(int i=0;i<frames;i++)p.SimulationStep(axes,sprint,false,false,1f/60);}
        static void Require(bool ok,string condition){if(!ok)throw new Exception("PRISM_RUNTIME_CHECK_FAILED: "+condition);}
    }
}
