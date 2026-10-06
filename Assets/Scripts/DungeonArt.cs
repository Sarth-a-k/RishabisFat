using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SunkenPrism
{
    public static class DungeonArt
    {
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        public static Material Mat(string name, Color color, float emission = 0f)
        {
            if (cache.TryGetValue(name, out var existing) && existing) return existing;
            var m = new Material((Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))) { name = name, color = color };
            m.SetFloat("_Glossiness", name.Contains("Metal") ? .65f : .22f); m.SetFloat("_Smoothness", name.Contains("Metal") ? .65f : .22f); m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", name.Contains("Metal") ? .6f : .06f);
            if (emission > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * emission); }
            cache[name] = m;
            return m;
        }
        public static GameObject Box(string name, Vector3 position, Vector3 scale, Material mat, Transform parent = null)
            => Primitive(PrimitiveType.Cube, name, position, scale, mat, parent);
        public static GameObject Cylinder(string name, Vector3 position, Vector3 scale, Material mat, Transform parent = null)
        {
            var o=Primitive(PrimitiveType.Cylinder,name,position,scale,mat,parent);
            var capsule=o.GetComponent<Collider>();capsule.enabled=false;
            if(Application.isPlaying)Object.Destroy(capsule);else Object.DestroyImmediate(capsule);
            o.AddComponent<MeshCollider>().sharedMesh=o.GetComponent<MeshFilter>().sharedMesh;
            return o;
        }
        static GameObject Primitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            var o = GameObject.CreatePrimitive(type); o.name = name;
            o.transform.SetParent(parent, false); o.transform.position = pos; o.transform.localScale = scale;
            o.GetComponent<Renderer>().sharedMaterial = mat; return o;
        }
        public static GameObject Crystal(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent = null)
        {
            var o = new GameObject(name); o.transform.SetParent(parent, false); o.transform.position = pos; o.transform.localScale = scale;
            var mesh = new Mesh { name = "FacetedCrystal" };
            var v = new[] { new Vector3(0,1,0), new Vector3(-.5f,0,-.5f), new Vector3(.5f,0,-.5f), new Vector3(.5f,0,.5f), new Vector3(-.5f,0,.5f), new Vector3(0,-.7f,0) };
            int[] triangles = {0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1};
            var vertices = new Vector3[triangles.Length]; var ids = new int[triangles.Length];
            for (int i=0;i<triangles.Length;i++){vertices[i]=v[triangles[i]];ids[i]=i;}
            mesh.vertices=vertices; mesh.triangles=ids; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            o.AddComponent<MeshFilter>().sharedMesh = mesh; o.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var collider = o.AddComponent<BoxCollider>(); collider.center=new Vector3(0,.15f,0); collider.size=new Vector3(1,1.7f,1);
            return o;
        }
        public static GameObject Beam(string name, Vector3 a, Vector3 b, Color color, Transform parent = null, float width = .045f)
        {
            var o = new GameObject(name); o.transform.SetParent(parent, false); o.layer=2;
            var line=o.AddComponent<LineRenderer>(); line.useWorldSpace=true; line.positionCount=2; line.SetPosition(0,a); line.SetPosition(1,b);
            var m = Mat("Beam_"+ColorUtility.ToHtmlStringRGB(color), color, 2.5f);
            line.sharedMaterial=m;line.startWidth=width;line.endWidth=width;line.numCapVertices=4;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            return o;
        }
        public static GameObject Label(string name, Vector3 pos, string text, float size, Color color, Transform parent = null)
        {
            var o=new GameObject(name);o.transform.SetParent(parent,false);o.transform.position=pos;o.layer=2;
            var label=o.AddComponent<TextMesh>();label.text=text;label.characterSize=size*.16f;label.fontSize=64;
            label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=color;
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var fm=new Material(label.font.material){name="Depth tested inscription"};fm.shader=Shader.Find("SunkenPrism/WorldText");
            o.GetComponent<Renderer>().sharedMaterial=fm;
            return o;
        }
        public static GameObject PointLight(string name, Vector3 pos, Color color, float intensity, float range, Transform parent = null)
        {
            var o=new GameObject(name);o.transform.SetParent(parent,false);o.transform.position=pos;
            var light=o.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;
            light.shadows=LightShadows.None;return o;
        }
        public static GameObject Ring(string name, Vector3 pos, float radius, float width, Material mat, Transform parent)
        {
            var o=new GameObject(name);o.transform.SetParent(parent,false);o.transform.position=pos;
            int n=64;var vertices=new Vector3[n*2];var indices=new int[n*6];
            for(int i=0;i<n;i++){
                float angle=i*Mathf.PI*2/n;var dir=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                vertices[i*2]=dir*radius;vertices[i*2+1]=dir*(radius-width);int j=(i+1)%n;
                int t=i*6;indices[t]=i*2;indices[t+1]=j*2;indices[t+2]=i*2+1;indices[t+3]=i*2+1;indices[t+4]=j*2;indices[t+5]=j*2+1;
            }
            var mesh=new Mesh {name="EngravedRing"};mesh.vertices=vertices;mesh.triangles=indices;mesh.RecalculateNormals();
            o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=mat;return o;
        }
        public static void RemoveCollider(GameObject o){var c=o.GetComponent<Collider>();if(c){if(Application.isPlaying)Object.Destroy(c);else Object.DestroyImmediate(c);}o.layer=2;}
        public static GameObject Rock(string name,Vector3 pos,Vector3 scale,Material mat,Transform parent)
        {
            var o=new GameObject(name);o.transform.SetParent(parent,false);o.transform.position=pos;o.transform.localScale=scale;
            var points=new List<Vector3>();var triangles=new List<int>();const int n=9;
            for(int ring=0;ring<4;ring++)for(int i=0;i<n;i++){
                float a=i*2*Mathf.PI/n+(ring%2)*.15f;float r=(ring==3?.3f:ring==0?.65f:1)*(.85f+.17f*Mathf.Sin(i*17+ring*3));
                points.Add(new Vector3(Mathf.Cos(a)*r,ring*.65f-1,Mathf.Sin(a)*r));
            }
            for(int ring=0;ring<3;ring++)for(int i=0;i<n;i++){
                int a=ring*n+i,b=ring*n+(i+1)%n,c=(ring+1)*n+i,d=(ring+1)*n+(i+1)%n;
                triangles.AddRange(new[]{a,c,b,b,c,d});
            }
            for(int i=1;i<n-1;i++)triangles.AddRange(new[]{3*n,3*n+i+1,3*n+i});
            var vertices=new Vector3[triangles.Count];var ids=new int[triangles.Count];for(int i=0;i<ids.Length;i++){vertices[i]=points[triangles[i]];ids[i]=i;}
            var mesh=new Mesh {name="FracturedBedrock"};mesh.vertices=vertices;mesh.triangles=ids;mesh.RecalculateNormals();mesh.RecalculateBounds();
            o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=mat;return o;
        }
    }
}
