using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SunkenPrism
{
    /// <summary>Opt-in standalone integration checks using the real CharacterController.
    /// Run the built game with -citadelSmokeTest. Ordinary exploration never runs checks.</summary>
    public sealed class CitadelSmokeTest : MonoBehaviour
    {
        readonly List<string> failures = new List<string>();
        bool fatalLog;
        const float StepTime = 1f / 60f;

        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(),"-citadelSmokeTest") < 0) yield break;
            Application.logMessageReceived += OnLog;
            yield return null;
            yield return new WaitForSeconds(.15f);
            ExplorerController player = ExplorerController.Instance;
            CitadelDirector director = CitadelDirector.Instance;
            if (player == null || player.View == null || director == null)
            {
                Debug.LogError("CITADEL_SMOKE_FAILED: runtime services did not initialize");
                Application.Quit(1);
                yield break;
            }
            player.enabled = false;
            player.transform.rotation = Quaternion.identity;
            player.View.transform.localRotation = Quaternion.identity;
            Debug.Log("CITADEL_SMOKE_INITIALIZED");
            Check("Nexus spawn and linear room footprints", () => {
                Require(PlanarDistance(player.SpawnPosition,CitadelLayout.SpawnPosition)<.01f,"configured spawn is not inside the Nexus");
                Require(PlanarDistance(player.transform.position,CitadelLayout.SpawnPosition)<.15f,"player did not enter at the Nexus");
                VerifyRegionFootprints();
            });
            Check("removed exits are closed and floors stop at the smaller room bounds",VerifyBoundaries);
            Check("room and hallway torch counts are preserved",VerifyTorchCounts);
            // Prove the only forward gate blocks the real motor before completing the optics.
            var lunar=LunarPrismPuzzle.Instance;
            Check("Ember gate blocks progression before the lunar trial",()=>VerifyClosedGate(player,lunar));
            if(lunar!=null)
            {
                lunar.Initialize(); lunar.ToggleLighter(); lunar.AdvanceIgnition(true,true,2.6f);
                for(int i=0;i<3;i++)
                {
                    lunar.TryPickUpMirror(i,player.View.transform);lunar.TryPlaceMirror(i);
                    for(int turn=0;turn<8&&!lunar.IsMirrorAligned(i);turn++)lunar.RotateMirror(i);
                }
                Physics.SyncTransforms();lunar.RefreshBeams();
                for(int i=0;i<3;i++)lunar.RotatePrism();
                Check("lunar trial unlocks the single forward gate",()=>Require(lunar.Solved,"reflection and lunar progression"));
                yield return new WaitForSeconds(3.5f);
            }
            for(int passage=0;passage<4;passage++)
                Route(player,"eastbound passage "+(passage+1),
                    CitadelLayout.PassageStart(passage)+Vector3.left*1.5f+Vector3.up*.1f,
                    CitadelLayout.PassageEnd(passage)+Vector3.right*1.5f+Vector3.up*.1f);
            Route(player,"complete Nexus to Eclipse Keep expedition",ExpeditionRoute());
            Route(player,"throne dais ascending ramp",
                CitadelLayout.Map(4,new Vector3(0,2.1f,77)),CitadelLayout.Map(4,new Vector3(0,4.3f,88)));
            Check("four sigils, visor, reset and socket bindings",() => VerifySurvey(player,director));
            player.Teleport(CitadelLayout.SpawnPosition);
            player.transform.rotation = Quaternion.Euler(0,CitadelLayout.SpawnYaw,0);
            Physics.SyncTransforms();
            if (failures.Count == 0 && !fatalLog)
            {
                Debug.Log("CITADEL_RUNTIME_SMOKE_PASS: Nexus spawn; four linear passages; closed former exits; smaller floors; gate progression; full expedition; torch counts; raised dais; regional survey state");
                yield return null;
                Application.Quit(0);
            }
            else
            {
                Debug.LogError("CITADEL_RUNTIME_SMOKE_FAILED: " + failures.Count + " checks failed\n" + string.Join("\n",failures));
                yield return null;
                Application.Quit(1);
            }
        }

        void OnLog(string message,string stackTrace,LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) fatalLog = true;
        }

        void Check(string label,Action action)
        {
            try
            {
                action();
                Debug.Log("CITADEL_CHECK_PASS: " + label);
            }
            catch (Exception error)
            {
                string failure = label + " — " + error.Message;
                failures.Add(failure);
                Debug.LogWarning("CITADEL_CHECK_FAIL: " + failure);
            }
        }

        void Route(ExplorerController player,string label,params Vector3[] points)
        {
            Check(label,() => {
                Require(points.Length >= 2,"route needs two points");
                SettleAt(player,points[0]);
                for (int i = 1; i < points.Length; i++) Drive(player,points[i],label + " leg " + i);
            });
        }

        static void SettleAt(ExplorerController player,Vector3 start)
        {
            player.Teleport(start);
            Physics.SyncTransforms();
            Step(player,Vector2.zero,35);
            Require(Mathf.Abs(player.transform.position.y - start.y) < .7f,
                "start has no supported floor: " + start.ToString("F2") + "; feet " + player.transform.position.ToString("F2"));
        }

        static void Drive(ExplorerController player,Vector3 destination,string label)
        {
            float initialDistance = PlanarDistance(player.transform.position,destination);
            int limit = Mathf.CeilToInt(initialDistance / 3.5f / StepTime) + 240;
            Vector3 checkpoint = player.transform.position;
            for (int i = 0; i < limit; i++)
            {
                Vector3 delta = destination - player.transform.position;
                delta.y = 0;
                if (delta.magnitude < .55f)
                {
                    Step(player,Vector2.zero,25);
                    Require(Mathf.Abs(player.transform.position.y - destination.y) < .7f,
                        label + " incorrect elevation; target " + destination.ToString("F2") + "; feet " + player.transform.position.ToString("F2"));
                    return;
                }
                Vector3 local = player.transform.InverseTransformDirection(delta.normalized);
                player.SimulationStep(new Vector2(local.x,local.z),false,false,false,StepTime);
                Require(player.transform.position.y > -6f,
                    label + " fell below the map at " + player.transform.position.ToString("F2"));
                if (i > 0 && i % 90 == 0)
                {
                    if (PlanarDistance(player.transform.position,checkpoint) < .12f)
                        throw new InvalidOperationException(label + " blocked at " + player.transform.position.ToString("F2") + "; target " + destination.ToString("F2") + "; " + BlockingGeometry(player,delta.normalized));
                    checkpoint = player.transform.position;
                }
            }
            throw new InvalidOperationException(label + " timed out at " + player.transform.position.ToString("F2") + "; target " + destination.ToString("F2") + "; " + BlockingGeometry(player,(destination-player.transform.position).normalized));
        }

        static string BlockingGeometry(ExplorerController player,Vector3 direction)
        {
            string forward = "no forward hit";
            if (Physics.SphereCast(player.transform.position + Vector3.up * .9f,.25f,direction,out RaycastHit hit,2f,~(1<<2),QueryTriggerInteraction.Ignore))
                forward = "forward collider=" + hit.collider.name + " at " + hit.point.ToString("F2");
            Collider[] nearby = Physics.OverlapSphere(player.transform.position + Vector3.up,.9f,~(1<<2),QueryTriggerInteraction.Ignore);
            return forward + "; nearby=" + string.Join(", ",nearby.Select(c => c.name + "@" + c.transform.position.ToString("F1")).ToArray());
        }

        static float PlanarDistance(Vector3 a,Vector3 b)
        {
            return new Vector2(a.x-b.x,a.z-b.z).magnitude;
        }

        static void Step(ExplorerController player,Vector2 axes,int frames,bool sprint=false,bool crouch=false)
        {
            for(int i=0;i<frames;i++) player.SimulationStep(axes,sprint,crouch,false,StepTime);
        }

        static void VerifyRegionFootprints()
        {
            for(int room=0;room<5;room++)
            {
                Require(CitadelLayout.Contains(room,CitadelLayout.Centers[room]),"missing room footprint "+room);
                Require(CitadelDirector.RegionAt(CitadelLayout.Centers[room])==room-1,"room classification "+room);
                Vector3 outside=CitadelLayout.Centers[room]+Vector3.forward*(CitadelLayout.Sizes[room].y*.5f+1);
                Require(!CitadelLayout.Contains(room,outside)&&CitadelDirector.RegionAt(outside)==-1,"room footprint extends beyond its north wall "+room);
            }
            Require(CitadelDirector.RegionAt(CitadelLayout.Map(3,new Vector3(25,0,-43)))==2,"clipped octagon interior classification");
            Require(CitadelDirector.RegionAt(CitadelLayout.Map(3,new Vector3(29,0,-37)))==-1,"clipped octagon exterior classification");
            for(int passage=0;passage<4;passage++)
            {
                Vector3 a=CitadelLayout.PassageStart(passage),b=CitadelLayout.PassageEnd(passage);
                Require(b.x>a.x&&Mathf.Abs(a.z)<.001f&&Mathf.Abs(b.z)<.001f,"passage is not eastbound "+passage);
                Require(Mathf.Abs(b.x-a.x-(passage==0?14f:16f))<.02f,"passage length "+passage);
            }
        }

        static Vector3[] ExpeditionRoute()
        {
            var points=new List<Vector3>{CitadelLayout.SpawnPosition};
            for(int passage=0;passage<4;passage++)
            {
                points.Add(CitadelLayout.PassageStart(passage)+Vector3.up*.1f);
                points.Add(CitadelLayout.PassageEnd(passage)+Vector3.right*2+Vector3.up*.1f);
                int room=passage+1;Vector3 center=CitadelLayout.Centers[room]+Vector3.up*.1f;
                if(room==1)
                {
                    // The shallow east pool touches the axis; keep the route in the dry southern aisle.
                    points.Add(CitadelLayout.PassageEnd(0)+new Vector3(3,.1f,-2));
                    points.Add(CitadelLayout.PassageStart(1)+new Vector3(-3,.1f,-2));
                    points.Add(CitadelLayout.PassageStart(1)+new Vector3(-2,.1f,0));
                }
                else if(room==3)
                {
                    points.Add(center+Vector3.left*5);
                    points.Add(center+new Vector3(-5,0,-4));
                    points.Add(center+new Vector3(5,0,-4));
                    points.Add(center+Vector3.right*5);
                }
                else points.Add(center);
            }
            return points.ToArray();
        }

        static void VerifyClosedGate(ExplorerController player,LunarPrismPuzzle lunar)
        {
            Require(lunar!=null,"the lunar trial is missing");lunar.Initialize();
            Require(!lunar.Solved&&lunar.EmberGates.Length==1,"expected one locked Ember entrance");
            Vector3 entrance=CitadelLayout.PassageEnd(1);
            Require(Vector3.Distance(lunar.EmberGates[0].position,entrance)<.02f,"gate is not at Ember's west entrance");
            Vector3 start=Vector3.Lerp(CitadelLayout.PassageStart(1),entrance,1-3f/16f)+Vector3.up*.1f;
            SettleAt(player,start);
            for(int i=0;i<180;i++)player.SimulationStep(new Vector2(1,0),false,false,false,StepTime);
            Require(player.transform.position.x<entrance.x-.4f&&player.transform.position.x>entrance.x-1.5f,"unsolved gate did not stop forward movement at the entrance");
            Require(Physics.SphereCast(player.transform.position+Vector3.up*.9f,.25f,Vector3.right,out RaycastHit hit,1.2f,~(1<<2),QueryTriggerInteraction.Ignore)
                &&hit.collider.transform.IsChildOf(lunar.EmberGates[0]),"the motor was stopped by something other than the puzzle gate");
        }

        static void VerifyBoundaries()
        {
            Physics.SyncTransforms();
            for(int room=0;room<5;room++)
            {
                var center=CitadelLayout.Centers[room];var half=CitadelLayout.Sizes[room]*.5f;
                foreach(int sign in new[]{-1,1})
                {
                    Vector3 direction=Vector3.forward*sign;
                    Require(Physics.Raycast(center+direction*(half.y-1.5f)+Vector3.up*1.2f,direction,4,~(1<<2),QueryTriggerInteraction.Ignore),"removed north/south exit is still open in room "+room);
                    Require(Physics.Raycast(center+direction*(half.y-2)+Vector3.up,Vector3.down,2,~(1<<2),QueryTriggerInteraction.Ignore),"floor missing inside resized room "+room);
                    var outside=center+direction*(half.y+3)+Vector3.up;
                    Require(!Physics.RaycastAll(outside,Vector3.down,2,~(1<<2),QueryTriggerInteraction.Ignore).Any(h=>h.normal.y>.9f&&Mathf.Abs(h.point.y-center.y)<.5f),"walkable floor extends past resized room "+room);
                }
            }
            foreach(int room in new[]{0,4})
            {
                Vector3 direction=room==0?Vector3.left:Vector3.right;
                var origin=CitadelLayout.Centers[room]+direction*(CitadelLayout.Sizes[room].x*.5f-1.5f)+Vector3.up*1.2f;
                Require(Physics.Raycast(origin,direction,4,~(1<<2),QueryTriggerInteraction.Ignore),"terminal room has an unwanted exterior doorway "+room);
            }
        }

        static void VerifyTorchCounts()
        {
            var world=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t=>t.name.StartsWith("THE FOURFOLD CITADEL"));
            int[] expected={6,9,6,8,9};
            for(int room=0;room<5;room++)
            {
                string prefix=room.ToString("00")+" •";
                var group=world.Cast<Transform>().Single(t=>t.name.StartsWith(prefix));
                Require(group.GetComponentsInChildren<TorchFlame>(true).Length==expected[room],"decorative torch count in room "+room);
            }
            var network=world.Cast<Transform>().Single(t=>t.name.StartsWith("CONNECTIVE NETWORK"));
            var torches=network.GetComponentsInChildren<TorchFlame>(true);
            Require(torches.Length==12,"expected twelve hallway torches");
            Require(network.GetComponentsInChildren<BoxCollider>().Count(c=>c.name=="Continuous stone passage floor")==4,"expected only four connecting passages");
            for(int passage=0;passage<4;passage++)
            {
                float start=CitadelLayout.PassageStart(passage).x,end=CitadelLayout.PassageEnd(passage).x;
                Require(torches.Count(t=>t.transform.position.x>start&&t.transform.position.x<end)==3,"expected three torches in passage "+passage);
            }
        }

        static void VerifySurvey(ExplorerController player,CitadelDirector director)
        {
            director.ResetSurvey();
            int completedTrial=LunarPrismPuzzle.Instance!=null&&LunarPrismPuzzle.Instance.Solved?1:0;
            Require(director.AttunedCount==completedTrial && !director.VisorAvailable && !director.VisorOn,"reset preserves completed trial and clears survey/visor");
            director.ToggleVisor();
            Require(!director.VisorOn,"visor should be locked before Ember attunement");
            for(int i=completedTrial;i<4;i++)
            {
                GameObject socket=GameObject.Find("PuzzleSocket_"+(i+1));
                Require(socket!=null,"missing socket " +(i+1));
                var interaction=socket.GetComponent<PrismInteractable>();
                Require(interaction!=null && !string.IsNullOrEmpty(interaction.Prompt),"missing interaction " +(i+1));
                Require(socket.GetComponentInChildren<Collider>()!=null,"missing focus collider " +(i+1));
                interaction.Interact();
                Require(director.Attuned[i],"E binding did not attune socket " +(i+1));
                director.Attune(i);
                Require(director.AttunedCount==i+1,"duplicate attunement should be idempotent");
            }
            Require(director.AttunedCount==4 && director.VisitedCount==4,"complete survey counts");
            director.ToggleVisor();
            Require(director.VisorOn,"visor unlock after Ember attunement");
            director.ToggleVisor();
            Require(!director.VisorOn,"visor toggles off");
            director.ToggleVisor();
            Vector3 position=player.transform.position;
            director.ResetSurvey();
            Require(director.AttunedCount==completedTrial && director.VisitedCount==4,"G reset clears survey sigils and preserves trial/explored halls");
            Require(!director.VisorOn && !director.VisorAvailable,"G reset clears thermal survey state");
            Require(player.transform.position==position,"G reset must not teleport player");
        }

        static void Require(bool success,string condition)
        {
            if(!success) throw new InvalidOperationException(condition);
        }

        void OnDestroy()
        {
            Application.logMessageReceived-=OnLog;
        }
    }
}
