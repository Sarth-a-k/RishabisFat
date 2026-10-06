using UnityEngine;

namespace SunkenPrism
{
    /// <summary>Keep the closest eight decorative torch lights and only two shadow casters.
    /// Flame meshes stay visible at distance; puzzle beams and the hand lamp remain independent.</summary>
    public sealed class CitadelLightBudget : MonoBehaviour
    {
        Light[] torches;
        readonly Light[] nearest = new Light[8];
        readonly float[] distances = new float[8];
        float nextRefresh;
        public int ActiveTorchLights { get; private set; }
        public int ShadowTorchLights { get; private set; }

        void Start()
        {
            var flames = FindObjectsByType<TorchFlame>(FindObjectsSortMode.None);
            torches = new Light[flames.Length];
            for (int i=0;i<flames.Length;i++) torches[i]=flames[i].GetComponentInChildren<Light>();
            RefreshBudget();
        }
        void Update()
        {
            if (Time.unscaledTime<nextRefresh) return;
            nextRefresh=Time.unscaledTime+.25f;
            RefreshBudget();
        }
        public void RefreshBudget()
        {
            var player=ExplorerController.Instance;
            if(player==null||torches==null)return;
            Vector3 position=player.transform.position;
            for(int i=0;i<nearest.Length;i++){nearest[i]=null;distances[i]=float.MaxValue;}
            foreach(var light in torches)
            {
                if(!light)continue;
                light.enabled=false;light.shadows=LightShadows.None;
                float distance=(light.transform.position-position).sqrMagnitude;
                if(distance>32f*32f)continue;
                for(int i=0;i<nearest.Length;i++)
                {
                    if(distance>=distances[i])continue;
                    for(int j=nearest.Length-1;j>i;j--){nearest[j]=nearest[j-1];distances[j]=distances[j-1];}
                    nearest[i]=light;distances[i]=distance;break;
                }
            }
            ActiveTorchLights=0;ShadowTorchLights=0;
            for(int i=0;i<nearest.Length;i++)if(nearest[i])
            {
                nearest[i].enabled=true;ActiveTorchLights++;
                if(i<2&&distances[i]<14f*14f){nearest[i].shadows=LightShadows.Hard;ShadowTorchLights++;}
            }
        }
        void OnDisable()
        {
            if(torches==null)return;
            foreach(var light in torches)if(light)light.enabled=true;
        }
    }
}
