using UnityEngine;

namespace SunkenPrism
{
    public class DungeonRuntime : MonoBehaviour
    {
        void Awake()
        {
            Application.targetFrameRate=90;
            PuzzleGame.Build(transform);
            ExplorerController.Build(new Vector3(0,.12f,4),transform);
            gameObject.AddComponent<PrismHUD>();
            gameObject.AddComponent<PrismAmbience>();
            gameObject.AddComponent<DungeonMood>();
            gameObject.AddComponent<PrismRuntimeCheck>();
        }
    }
    public class PrismAmbience : MonoBehaviour
    {
        void Start()
        {
            const int rate=22050;int samples=rate*8;float[] data=new float[samples];
            for(int i=0;i<samples;i++){
                float t=(float)i/rate;float envelope=.72f+.28f*Mathf.Sin(t*Mathf.PI*.25f);
                data[i]=(Mathf.Sin(t*2*Mathf.PI*55)*.022f+Mathf.Sin(t*2*Mathf.PI*82.5f)*.012f+Mathf.Sin(t*2*Mathf.PI*110)*.008f)*envelope;
            }
            var clip=AudioClip.Create("Submerged chamber resonance",samples,1,rate,false);clip.SetData(data,0);
            var source=gameObject.AddComponent<AudioSource>();source.clip=clip;source.loop=true;source.volume=.4f;source.spatialBlend=0;source.Play();
        }
    }
}
