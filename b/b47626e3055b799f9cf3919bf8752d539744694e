using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SunkenPrism {
 public static partial class CitadelBuilder {
  public const string ScenePath="Assets/Scenes/FourfoldCitadel.unity";
  static Transform world;
  static Material atlas,ash,black,bronze,water,cloth,violet,cyan,ember,pale,vaultStone;
  static System.Random rng;
  static Dictionary<string,Mesh> modules=new Dictionary<string,Mesh>();
  static List<Renderer> roofs=new List<Renderer>();
  sealed class TilePatch { public Transform Group;public Vector3 Center;public float Width,Depth;public Quaternion Rotation;public bool Octagon;public float Corner; }
  static readonly List<TilePatch> tilePatches=new List<TilePatch>();
  static readonly Color C1=new Color(.18f,.65f,.74f),C2=new Color(1,.31f,.07f),C3=new Color(.52f,.28f,.86f),C4=new Color(.66f,.76f,.93f);
  static float R(float min,float max)=>min+(float)rng.NextDouble()*(max-min);
  static Transform Group(string name){var o=new GameObject(name);o.transform.SetParent(world);return o.transform;}
  static GameObject Box(string name,Vector3 pos,Vector3 scale,Material material,Transform p,bool solid=true){var o=DungeonArt.Box(name,pos,scale,material,p);if(!solid)DungeonArt.RemoveCollider(o);return o;}
  static void MatInit(){
   vaultStone=VaultMaterial();
   atlas=new Material((Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))){name="Mayan carved limestone • supplied atlas",color=new Color(.72f,.78f,.79f)};
   var tex=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Imported/Mayan/Textures/11524_Dungeon_DefaultMaterial_AlbedoTransparency.jpg");atlas.mainTexture=tex;atlas.SetFloat("_Glossiness",.24f);
   string normalPath="Assets/Imported/Mayan/Textures/1901148_Dungeon_DefaultMaterial_Normal.png";
   var importer=AssetImporter.GetAtPath(normalPath) as TextureImporter;if(importer!=null&&importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();}
   atlas.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));atlas.EnableKeyword("_NORMALMAP");atlas.SetFloat("_BumpScale",.7f);
   ash=DungeonArt.Mat("Citadel weathered ashlar",new Color(.24f,.29f,.31f));black=DungeonArt.Mat("Citadel obsidian",new Color(.055f,.074f,.082f));bronze=DungeonArt.Mat("Citadel antique bronze Metal",new Color(.39f,.25f,.10f));
   ash.mainTexture=vaultStone.mainTexture;
   cloth=DungeonArt.Mat("Citadel oxblood banners",new Color(.20f,.025f,.035f));
   water=DungeonArt.Mat("Sunken still water",new Color(.027f,.12f,.14f));water.SetFloat("_Glossiness",.97f);water.SetFloat("_Metallic",.72f);
   cyan=DungeonArt.Mat("Prismatic crystal",C1,.05f);ember=DungeonArt.Mat("Smouldering embers",C2,.07f);violet=DungeonArt.Mat("Violet witchlight",C3,.07f);pale=DungeonArt.Mat("Eclipse silver fire",C4,.05f);
  }
  static Mesh LoadMesh(string id){
   if(modules.TryGetValue(id,out var result)&&result)return result;
   string path="Assets/Imported/Mayan/Modules/"+id+".obj";
   if(id.StartsWith("Gothic_"))path="Assets/Imported/Gothic/"+id.Substring(7)+".obj";
   result=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().FirstOrDefault();
   if(!result)throw new Exception("Missing supplied module "+path);
   modules[id]=result;return result;
  }
  static GameObject Module(string id,Vector3 p,Vector3 size,Transform parent,float yaw=0,bool solid=false,Material material=null){
   Mesh mesh=LoadMesh(id);var o=new GameObject(id+" • reused module");o.transform.SetParent(parent,false);o.transform.position=p;o.transform.rotation=Quaternion.Euler(0,yaw,0);
   var child=new GameObject("Textured mesh");child.transform.SetParent(o.transform,false);var s=mesh.bounds.size;
   child.transform.localScale=new Vector3(size.x/Mathf.Max(.001f,s.x),size.y/Mathf.Max(.001f,s.y),size.z/Mathf.Max(.001f,s.z));
   child.transform.localPosition=-Vector3.Scale(new Vector3(mesh.bounds.center.x,mesh.bounds.min.y,mesh.bounds.center.z),child.transform.localScale);
   child.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=child.AddComponent<MeshRenderer>();renderer.sharedMaterials=Enumerable.Repeat(material??atlas,mesh.subMeshCount).ToArray();
   if(solid){var col=o.AddComponent<BoxCollider>();col.center=Vector3.up*size.y*.5f;col.size=size;}return o;
  }
  [MenuItem("Tools/Fourfold Citadel/Rebuild Linear Dungeon")]
  public static void Build(){
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);rng=new System.Random(44219);roofs.Clear();modules.Clear();tilePatches.Clear();
   world=new GameObject("THE FOURFOLD CITADEL • linear regional dungeon").transform;MatInit();Lighting();
   BuildHub();BuildPrism();BuildCrypt();BuildSanctum();BuildKeep();AddExtraTorchLayout();
   for(int room=0;room<5;room++)ResizeRoom(room);
   BuildRoutes();BuildLandscape();BuildLunarGates();
   new GameObject("Explorer • survey • region map").AddComponent<CitadelDirector>();
   SaveAssets();
   foreach(var r in world.GetComponentsInChildren<Renderer>())if(!r.GetComponentInParent<TorchFlame>()&&!r.GetComponentInParent<LunarPrismPuzzle>()&&!r.transform.IsChildOf(lunarGateRoot))GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.BatchingStatic);
   EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);
   EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene("Assets/Scenes/SunkenPrism.unity",true)};
   PlayerSettings.productName="The Fourfold Citadel";PlayerSettings.companyName="Chromatic Expeditions";PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
   PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
   AssetDatabase.SaveAssets();Debug.Log("CITADEL_SCENE_READY");
  }
  static void Lighting(){
   RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;
   RenderSettings.ambientSkyColor=new Color(.008f,.01f,.015f);RenderSettings.ambientEquatorColor=new Color(.004f,.006f,.009f);RenderSettings.ambientGroundColor=new Color(.002f,.003f,.005f);
   RenderSettings.ambientIntensity=1;RenderSettings.reflectionIntensity=.04f;RenderSettings.sun=null;
   RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogDensity=.009f;RenderSettings.fogColor=new Color(.003f,.005f,.008f);
   QualitySettings.pixelLightCount=8;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.shadowDistance=95;QualitySettings.antiAliasing=4;QualitySettings.vSyncCount=1;
  }
  static Material VaultMaterial(){
   const string texturePath="Assets/Citadel/Generated/VaultAshlar.asset";
   var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
   if(!texture){
    texture=new Texture2D(512,512,TextureFormat.RGB24,true){name="Weathered vault masonry",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=8};
    var colors=new Color[512*512];
    for(int y=0;y<512;y++)for(int x=0;x<512;x++){
     int row=y/64,offset=(row%2)*64,xx=(x+offset)%128,yy=y%64;
     float edge=Mathf.Min(Mathf.Min(xx,127-xx),Mathf.Min(yy,63-yy));
     float noise=Mathf.PerlinNoise(x*.13f,y*.13f)*.07f+Mathf.PerlinNoise(x*.037f,y*.037f)*.045f;
     float block=.025f*Mathf.Sin(row*31+(x+offset)/128*17);
     float value=edge<2?.07f:.27f+noise+block;value*=Mathf.Lerp(.60f,1,Mathf.Clamp01((edge-1)/5));
     colors[y*512+x]=new Color(value*.91f,value*.95f,value);
    }
    texture.SetPixels(colors);texture.Apply();Directory.CreateDirectory("Assets/Citadel/Generated");AssetDatabase.CreateAsset(texture,texturePath);
   }
   var mat=new Material((Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))){name="Vault coursed stone",mainTexture=texture,color=new Color(.8f,.83f,.86f)};
   mat.SetFloat("_Glossiness",.1f);return mat;
  }
  static void SurfaceQuad(List<Vector3> v,List<Vector2> uv,List<int> ids,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 ud){
   int n=v.Count;v.AddRange(new[]{a,b,c,d,d,c,b,a});uv.AddRange(new[]{ua,ub,uc,ud,ud,uc,ub,ua});
   ids.AddRange(new[]{n,n+1,n+2,n,n+2,n+3,n+4,n+5,n+6,n+4,n+6,n+7});
  }
  static GameObject RoofMesh(string name,List<Vector3> vertices,List<Vector2> uv,List<int> indices,Transform p){
   var mesh=new Mesh{name=name};if(vertices.Count>65000)mesh.indexFormat=IndexFormat.UInt32;
   mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
   var o=new GameObject("Roof_ • "+name);o.transform.SetParent(p,false);o.AddComponent<MeshFilter>().sharedMesh=mesh;
   var r=o.AddComponent<MeshRenderer>();r.sharedMaterial=vaultStone;roofs.Add(r);
   o.AddComponent<MeshCollider>().sharedMesh=mesh;return o;
  }
  static void Vault(Vector3 a,Vector3 b,float width,float rise,Transform p,float ribSpacing){
   Vector3 along=(b-a).normalized,side=Vector3.Cross(Vector3.up,along).normalized;
   float length=Vector3.Distance(a,b),arc=Mathf.PI*(width*.5f+rise)*.5f;
   var v=new List<Vector3>();var uv=new List<Vector2>();var ids=new List<int>();const int slices=40;
   int bays=Mathf.Max(1,Mathf.CeilToInt(length/4));
   Func<float,float,Vector3> point=(t,f)=>Vector3.Lerp(a,b,f)+side*(Mathf.Cos(t*Mathf.PI)*width*.5f)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*rise);
   for(int z=0;z<bays;z++)for(int x=0;x<slices;x++){
    float t0=(float)x/slices,t1=(float)(x+1)/slices,f0=(float)z/bays,f1=(float)(z+1)/bays;
    SurfaceQuad(v,uv,ids,point(t0,f0),point(t1,f0),point(t1,f1),point(t0,f1),new Vector2(t0*arc/3,f0*length/3),new Vector2(t1*arc/3,f0*length/3),new Vector2(t1*arc/3,f1*length/3),new Vector2(t0*arc/3,f1*length/3));
   }
   // Closed gable spandrels join the vault to the existing walls.
   foreach(float f in new[]{0f,1f})for(int x=0;x<slices;x++){
    float t0=(float)x/slices,t1=(float)(x+1)/slices;var c=Vector3.Lerp(a,b,f);
    var lo=c+side*Mathf.Cos(t0*Mathf.PI)*width*.5f;var hi=c+side*Mathf.Cos(t1*Mathf.PI)*width*.5f;
    SurfaceQuad(v,uv,ids,lo,hi,point(t1,f),point(t0,f),new Vector2(t0*width/3,0),new Vector2(t1*width/3,0),new Vector2(t1*width/3,Mathf.Sin(t1*Mathf.PI)*rise/3),new Vector2(t0*width/3,Mathf.Sin(t0*Mathf.PI)*rise/3));
   }
   RoofMesh("continuous barrel vault",v,uv,ids,p);
   if(ribSpacing<=0)return;
   int count=Mathf.Max(1,Mathf.CeilToInt(length/ribSpacing));
   for(int z=0;z<=count;z++)for(int i=0;i<24;i++){
    var aa=point(i/24f,z/(float)count)-Vector3.up*.12f;var bb=point((i+1)/24f,z/(float)count)-Vector3.up*.12f;
    RoofRib(aa,bb,p,.23f);
   }
  }
  static void RoofRib(Vector3 a,Vector3 b,Transform p,float width){
   if((b-a).sqrMagnitude<.00001f)return;
   var rib=Box("Roof_ • carved vault rib",(a+b)*.5f,new Vector3(width,width*1.3f,Vector3.Distance(a,b)+.04f),ash,p,false);
   rib.transform.rotation=Quaternion.LookRotation(b-a);roofs.Add(rib.GetComponent<Renderer>());
  }
  static void Dome(Vector3 center,float width,float depth,float corner,float rise,Transform p){
   float x=width*.5f,z=depth*.5f;
   Vector3[] outline=corner>0?new[]{new Vector3(-x+corner,0,-z),new Vector3(x-corner,0,-z),new Vector3(x,0,-z+corner),new Vector3(x,0,z-corner),new Vector3(x-corner,0,z),new Vector3(-x+corner,0,z),new Vector3(-x,0,z-corner),new Vector3(-x,0,-z+corner)}:new[]{new Vector3(-x,0,-z),new Vector3(x,0,-z),new Vector3(x,0,z),new Vector3(-x,0,z)};
   var perimeter=new List<Vector3>();for(int i=0;i<outline.Length;i++)for(int j=0;j<8;j++)perimeter.Add(Vector3.Lerp(outline[i],outline[(i+1)%outline.Length],j/8f));
   var v=new List<Vector3>();var uv=new List<Vector2>();var ids=new List<int>();int n=perimeter.Count;const int rings=18;
   Func<Vector3,float,Vector3> point=(edge,t)=>center+edge*Mathf.Cos(t*Mathf.PI*.5f)+Vector3.up*(Mathf.Sin(t*Mathf.PI*.5f)*rise);
   for(int j=0;j<rings;j++)for(int i=0;i<n;i++){
    float a=(float)j/rings,b=(float)(j+1)/rings;var e=perimeter[i];var f=perimeter[(i+1)%n];
    var aa=point(e,a);var bb=point(f,a);var cc=point(f,b);var dd=point(e,b);
    SurfaceQuad(v,uv,ids,aa,bb,cc,dd,new Vector2(aa.x/3,aa.z/3),new Vector2(bb.x/3,bb.z/3),new Vector2(cc.x/3,cc.z/3),new Vector2(dd.x/3,dd.z/3));
   }
   RoofMesh("closed ribbed dome",v,uv,ids,p);
   foreach(var edge in outline)for(int j=0;j<24;j++)RoofRib(point(edge,j/24f)-Vector3.up*.13f,point(edge,(j+1)/24f)-Vector3.up*.13f,p,.25f);
  }

  static void Floor(Transform p,float cx,float cz,float width,float depth,float y,bool octagon=false){
   if(octagon)OctagonalFoundation(new Vector3(cx,y-.2f,cz),width,depth,p);
   else Box("Deep bedrock foundation",new Vector3(cx,y-2,cz),new Vector3(width+1,3.6f,depth+1),black,p);
   var collision=p.gameObject.AddComponent<BoxCollider>();collision.center=new Vector3(cx,y-.22f,cz);collision.size=new Vector3(width,.44f,depth);
   TileSurface(new Vector3(cx,y,cz),width,depth,Quaternion.identity,p,octagon);
  }
  static void TileSurface(Vector3 center,float width,float depth,Quaternion rotation,Transform parent,bool octagon=false,float corner=9){
   int nx=Mathf.CeilToInt(width/3),nz=Mathf.CeilToInt(depth/3);float dx=width/nx,dz=depth/nz;
   var group=new GameObject("Continuous carved floor tiles").transform;group.SetParent(parent,true);
   tilePatches.Add(new TilePatch{Group=group,Center=center,Width=width,Depth=depth,Rotation=rotation,Octagon=octagon,Corner=corner});
   for(int ix=0;ix<nx;ix++)for(int iz=0;iz<nz;iz++){
    float x=-width/2+(ix+.5f)*dx,z=-depth/2+(iz+.5f)*dz;
    if(octagon&&Mathf.Abs(x)+Mathf.Abs(z)>(width+depth)*.5f-corner)continue;
    string id=(ix+iz)%9==0?"Cube_170":(ix+iz)%3==0?"Cube_145":"Cube_008";
    var tile=Module(id,center+rotation*new Vector3(x,-.245f,z),new Vector3(dx-.015f,.25f,dz-.015f),group);
    // Rectangular cells retain their dimensions on ramps; quarter-turns would create gaps.
    tile.transform.rotation=rotation*Quaternion.Euler(0,180*((ix+iz)%2),0);
   }
  }
  static void OctagonalFoundation(Vector3 center,float width,float depth,Transform p){
   float a=width*.5f,b=depth*.5f;var outline=new[]{new Vector3(-a+9,0,-b),new Vector3(a-9,0,-b),new Vector3(a,0,-b+9),new Vector3(a,0,b-9),new Vector3(a-9,0,b),new Vector3(-a+9,0,b),new Vector3(-a,0,b-9),new Vector3(-a,0,-b+9)};
   var vertices=new List<Vector3>();var indices=new List<int>();
   for(int i=0;i<8;i++){int j=(i+1)%8,n=vertices.Count;vertices.AddRange(new[]{Vector3.zero,outline[j],outline[i],outline[i],outline[j],outline[j]-Vector3.up*3.7f,outline[i]-Vector3.up*3.7f});indices.AddRange(new[]{n,n+1,n+2,n+3,n+4,n+5,n+3,n+5,n+6});}
   var mesh=new Mesh{name="Octagonal foundation"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();var o=new GameObject("Eight-sided bedrock foundation");o.transform.SetParent(p);o.transform.position=center;o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=black;
  }
  static void Walls(Transform p,float x0,float x1,float z0,float z1,float y,float h,float[] south,float[] north,float[] west,float[] east){
   Wall(new Vector3(x0,y,z0),new Vector3(x1,y,z0),h,south.Select(x=>x-x0).ToArray(),p);
   Wall(new Vector3(x0,y,z1),new Vector3(x1,y,z1),h,north.Select(x=>x-x0).ToArray(),p);
   Wall(new Vector3(x0,y,z0),new Vector3(x0,y,z1),h,west.Select(z=>z-z0).ToArray(),p);
   Wall(new Vector3(x1,y,z0),new Vector3(x1,y,z1),h,east.Select(z=>z-z0).ToArray(),p);
  }
  static readonly float[] None=new float[0];
  static void Wall(Vector3 a,Vector3 b,float height,float[] gates,Transform parent){
   Vector3 delta=b-a;float length=new Vector2(delta.x,delta.z).magnitude;Vector3 direction=delta/length;float yaw=-Mathf.Atan2(delta.z,delta.x)*Mathf.Rad2Deg;
   var cuts=new List<float>{0,length};foreach(float gate in gates){cuts.Add(Mathf.Max(0,gate-4));cuts.Add(Mathf.Min(length,gate+4));}cuts.Sort();
   for(int c=0;c<cuts.Count-1;c++){
    float start=cuts[c],end=cuts[c+1],mid=(start+end)*.5f;if(gates.Any(g=>Mathf.Abs(mid-g)<3.99f))continue;
    int n=Mathf.CeilToInt((end-start)/3);float w=(end-start)/n;
    for(int i=0;i<n;i++){
     Vector3 pos=a+direction*(start+(i+.5f)*w);int rows=Mathf.CeilToInt(height/3.22f);float hh=height/rows;
     for(int row=0;row<rows;row++)Module("Cube_049",pos+Vector3.up*row*hh,new Vector3(w+.018f,hh,1.1f),parent,yaw);
     if(i%3==0)Column(pos,height+.4f,parent,1.2f);
    }
    var barrier=Box("Structural wall collision",a+direction*mid+Vector3.up*height*.5f,Vector3.one,black,parent);barrier.transform.rotation=Quaternion.Euler(0,yaw,0);barrier.GetComponent<BoxCollider>().size=new Vector3(end-start,height,1.3f);barrier.GetComponent<Renderer>().enabled=false;
   }
   foreach(float gate in gates)Arch(a+direction*gate,8,height,parent,yaw);
  }
  static void Column(Vector3 basePos,float height,Transform parent,float width=1.6f){
   Module("Cube_171",basePos,new Vector3(width*1.3f,.45f,width*1.3f),parent);
   Module("Cube_048",basePos+Vector3.up*.4f,new Vector3(width,height-.9f,width),parent,0,true);
   Module("Cube_171",basePos+Vector3.up*(height-.5f),new Vector3(width*1.35f,.55f,width*1.35f),parent);
   foreach(float y in new[]{1f,height*.65f})Box("Bronze pillar fillet",basePos+Vector3.up*y,new Vector3(width*1.05f,.12f,width*1.05f),bronze,parent,false);
  }
  static void Arch(Vector3 pos,float width,float height,Transform parent,float yaw=0){
   var p=new GameObject("Pointed portal • regional threshold").transform;p.SetParent(parent);p.position=pos;p.rotation=Quaternion.Euler(0,yaw,0);
   float shoulder=height*.52f;
   foreach(int s in new[]{-1,1})Column(p.TransformPoint(new Vector3(s*(width/2+.35f),0,0)),shoulder+.1f,p,1.3f);
   Vector3 last=new Vector3(-width/2,shoulder,0);
   for(int i=1;i<=16;i++){
    float t=(float)i/16;float x=Mathf.Lerp(-width/2,width/2,t),y=shoulder+Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.75f)*(height-shoulder);
    var now=new Vector3(x,y,0);var seg=Box("Carved arch voussoir",p.TransformPoint((last+now)*.5f),new Vector3((now-last).magnitude+.06f,.62f,1.7f),ash,p,false);
    seg.transform.rotation=p.rotation*Quaternion.Euler(0,0,Mathf.Atan2(now.y-last.y,now.x-last.x)*Mathf.Rad2Deg);last=now;
   }
   Module("Cube_090",p.TransformPoint(new Vector3(0,height-.35f,-.95f)),new Vector3(1.2f,1.2f,.2f),p,yaw);
  }
  static void Brazier(Vector3 p,Color color,Transform parent,float size=1){
   var root=new GameObject("Torch • supplied design").transform;root.SetParent(parent,false);root.position=p;
   var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported/Torch/Torch.obj");
   if(!source)throw new Exception("Missing supplied Torch.obj");
   var model=(GameObject)PrefabUtility.InstantiatePrefab(source,root);model.name="Imported torch handle and basket";
   model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one*(1.6f*size);
   TorchFlame.Create(root,p+Vector3.up*(1.94f*size),color,.85f*size);
  }
  static void AddRoomTorches(string prefix,Color color,params Vector3[] positions){
   var room=world.Cast<Transform>().Single(t=>t.name.StartsWith(prefix));
   if(room.Find("Additional room torches"))return;
   var group=new GameObject("Additional room torches").transform;group.SetParent(room,false);
   foreach(var pos in positions)Brazier(pos,color,group);
  }
  static void AddExtraTorchLayout(){
   AddRoomTorches("00 •",new Color(.8f,.58f,.3f),new Vector3(-10,0,5),new Vector3(10,0,-5),new Vector3(-5,0,-10),new Vector3(5,0,10));
   AddRoomTorches("01 •",C1,new Vector3(-100,0,5),new Vector3(-86,0,-17),new Vector3(-61,0,-16),new Vector3(-61,0,25),new Vector3(-37,0,9));
   AddRoomTorches("02 •",C2,new Vector3(50,-2,-6),new Vector3(62,-2,8),new Vector3(83,-2,-7),new Vector3(92,-2,6));
   AddRoomTorches("03 •",C3,new Vector3(15,0,-59),new Vector3(12,0,-78),new Vector3(-12,0,-79),new Vector3(-14,0,-70),new Vector3(0,0,-90));
   AddRoomTorches("04 •",C4,new Vector3(-10,2,61),new Vector3(10,2,67),new Vector3(-27,2,86),new Vector3(27,2,86),new Vector3(8,4.2f,91));
  }
  [MenuItem("Tools/Fourfold Citadel/Update Torch Placement")]
  public static void UpdateTorchPlacement(){
   if(SceneManager.GetActiveScene().path!=ScenePath)throw new Exception("Open the Fourfold Citadel scene first.");
   world=SceneManager.GetActiveScene().GetRootGameObjects().Single(o=>o.name.StartsWith("THE FOURFOLD CITADEL")).transform;
   AddExtraTorchLayout();
   foreach(var torch in world.GetComponentsInChildren<TorchFlame>()){torch.RefreshLighting();EditorUtility.SetDirty(torch.GetComponentInChildren<Light>());}
   SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  }
  static void Banner(Vector3 top,float height,Transform parent,float yaw=0){
   var mesh=new Mesh{name="Tattered heraldry"};mesh.vertices=new[]{new Vector3(-1.15f,0,0),new Vector3(1.15f,0,0),new Vector3(1.1f,-height+.8f,0),new Vector3(.45f,-height,0),new Vector3(0,-height+.65f,0),new Vector3(-.8f,-height+.3f,0)};mesh.triangles=new[]{0,1,2,0,2,3,0,3,4,0,4,5,2,1,0,3,2,0,4,3,0,5,4,0};mesh.RecalculateNormals();
   var o=new GameObject("Torn oxblood standard");o.transform.SetParent(parent);o.transform.position=top;o.transform.rotation=Quaternion.Euler(0,yaw,0);o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=cloth;
   var bar=Box("Banner hanging rod",top+Vector3.up*.1f,new Vector3(2.7f,.1f,.1f),bronze,parent,false);bar.transform.rotation=o.transform.rotation;
   DungeonArt.Crystal("Heraldic gold stitch",top+Quaternion.Euler(0,yaw,0)*new Vector3(0,-height*.35f,-.03f),new Vector3(.35f,.65f,.045f),bronze,parent);
  }
  static void Socket(int id,Vector3 p,Color c,Transform parent){
   DungeonArt.Cylinder("Regional puzzle dais",p+Vector3.up*.15f,new Vector3(5,.15f,5),black,parent);
   DungeonArt.Ring("Puzzle boundary engraving",p+Vector3.up*.315f,2.2f,.08f,bronze,parent);
   Module("Cube_171",p+Vector3.up*.3f,new Vector3(1.6f,.45f,1.6f),parent);
   Module("Cube_048",p+Vector3.up*.7f,new Vector3(.75f,.5f,.75f),parent);
   DungeonArt.Crystal("PuzzleSocket_"+id,p+Vector3.up*1.7f,new Vector3(.7f,1.05f,.7f),DungeonArt.Mat("Regional sigil "+id,c,.05f),parent);
   DungeonArt.Label("Region socket inscription "+id,p+new Vector3(0,.8f,-1.5f),"REGION 0"+id+"  /  SURVEY SIGIL",.18f,new Color(.86f,.8f,.61f),parent);
  }
  static void BuildHub(){
   var p=Group("00 • The Nexus / arrival court");Floor(p,0,0,36,36,0);Walls(p,-18,18,-18,18,0,8,None,None,None,new[]{0f});
   foreach(float r in new[]{4f,7f,12f})DungeonArt.Ring("Crossroads astrolabe",new Vector3(0,.02f,0),r,.08f,bronze,p);
   // The arrival court has one eastbound exit; corner pillars preserve spawn clearance.
   foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1}){Column(new Vector3(x*12,0,z*12),11,p,2);if(x==z)Brazier(new Vector3(x*9,0,z*9),new Color(.8f,.58f,.3f),p);}
   var orb=DungeonArt.Crystal("Suspended black sun",new Vector3(0,8,0),new Vector3(3.5f,4,3.5f),black,p);DungeonArt.RemoveCollider(orb);
   for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;var v=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));DungeonArt.Beam("Astrolabe tether",new Vector3(0,9,0),v*12+Vector3.up*11,new Color(.15f,.15f,.11f),p,.075f);}
   Dome(new Vector3(0,8,0),36,36,0,5,p);
   Label("THE NEXUS",new Vector3(0,3.2f,12),.35f,p);
  }
  static void BuildPrism(){
   BuildLunarPrismRoom();
  }
  static void BuildCrypt(){
   var p=Group("02 • The Ember Crypt / low hypostyle burial hall");Floor(p,67,0,62,40,-2);Walls(p,36,98,-20,20,-2,7,None,None,new[]{0f},new[]{0f});
   // Dense, low columns and side aisles oppose the tall open cathedral.
   foreach(float x in new[]{46f,58f,76f,88f})foreach(float z in new[]{-11f,11f}){Column(new Vector3(x,-2,z),6.8f,p,2.2f);if((x==46&&z<0)||(x==88&&z>0))Brazier(new Vector3(x,-2,z>0?16:-16),C2,p);}
   foreach(float x in new[]{47f,58f,79f,90f})foreach(float z in new[]{-16f,16f}){
    Box("Ancient sarcophagus",new Vector3(x,-1.3f,z),new Vector3(4,1.4f,2.1f),black,p);Module("Cube_171",new Vector3(x,-.6f,z),new Vector3(4.3f,.5f,2.4f),p);
    Module("Cube_090",new Vector3(x,-.08f,z),new Vector3(.8f,.8f,.2f),p,90);
   }
   Vault(new Vector3(36,5,0),new Vector3(98,5,0),40,3,p,10);
   foreach(float x in new[]{54f,80f}){Box("Ash trench",new Vector3(x,-1.96f,0),new Vector3(1.5f,.02f,28),black,p,false);for(int k=0;k<12;k++)Box("Heat fissure",new Vector3(x+R(-.35f,.35f),-1.93f,-13+k*2.3f),new Vector3(.15f,.025f,.8f),ember,p,false);}
   Socket(2,new Vector3(72,-2,4),C2,p);Label("II  /  THE EMBER CRYPT",new Vector3(95,2.7f,0),.27f,p,90);
   GothicBay(new Vector3(41,-2,0),new Vector3(7,6,7),p,90);
  }
  static void BuildSanctum(){
   var p=Group("03 • The Violet Sanctum / octagonal ritual chamber");Floor(p,0,-66,60,60,0,true);
   var corners=new[]{new Vector3(-21,0,-96),new Vector3(21,0,-96),new Vector3(30,0,-87),new Vector3(30,0,-45),new Vector3(21,0,-36),new Vector3(-21,0,-36),new Vector3(-30,0,-45),new Vector3(-30,0,-87)};
   for(int i=0;i<8;i++){var a=corners[i];var b=corners[(i+1)%8];bool gate=(i==2||i==6);Wall(a,b,10,gate?new[]{Vector3.Distance(a,b)/2}:None,p);}
   foreach(float radius in new[]{7f,11f,17f,22f})DungeonArt.Ring("Concentric ritual path",new Vector3(0,.028f,-66),radius,.1f,radius==11?violet:bronze,p);
   for(int i=0;i<8;i++){
    float a=i*Mathf.PI/4+Mathf.PI/8;var point=new Vector3(Mathf.Sin(a)*21,0,-66+Mathf.Cos(a)*21);
    Column(point,11.6f,p,1.7f);
    if(i%3==0)Brazier(new Vector3(Mathf.Sin(a)*22,0,-66+Mathf.Cos(a)*22),C3,p);
   }
   Socket(3,new Vector3(0,0,-66),C3,p);
   var halo=DungeonArt.Ring("Roof_ • suspended ritual oculus",new Vector3(0,12,-66),15,1.4f,black,p);roofs.Add(halo.GetComponent<Renderer>());
   var underside=DungeonArt.Ring("Roof_ • oculus underside",new Vector3(0,11.97f,-66),15,1.4f,black,p);underside.transform.rotation=Quaternion.Euler(180,0,0);roofs.Add(underside.GetComponent<Renderer>());
   var runeRim=DungeonArt.Ring("Roof_ • violet oculus rim",new Vector3(0,11.94f,-66),13.75f,.1f,violet,p);runeRim.transform.rotation=Quaternion.Euler(180,0,0);roofs.Add(runeRim.GetComponent<Renderer>());
   for(int i=0;i<8;i++){float a=i*Mathf.PI/4;DungeonArt.Beam("Ancient suspension chain",new Vector3(Mathf.Sin(a)*15,12,-66+Mathf.Cos(a)*15),new Vector3(Mathf.Sin(a)*21,11.6f,-66+Mathf.Cos(a)*21),new Color(.15f,.12f,.17f),p,.08f);}
   Label("III  /  THE VIOLET SANCTUM",new Vector3(0,6,-94),.32f,p,180);
   Dome(new Vector3(0,10,-66),60,60,9,8,p);
  }
  static void BuildKeep(){
   var p=Group("04 • Eclipse Keep / buttressed throne hall");Floor(p,0,72,66,64,2);Walls(p,-33,33,40,104,2,14,None,None,new[]{72f},None);
   foreach(float x in new[]{-33f,33f})foreach(float z in new[]{40f,104f})KeepTower(new Vector3(x,2,z),p);
   foreach(float x in new[]{-21f,21f})foreach(float z in new[]{49f,64f,81f,96f}){Column(new Vector3(x,2,z),17,p,2.8f);Banner(new Vector3(x,15,z-1.6f),6.5f,p);}
   foreach(float z in new[]{50f,67.5f,94f})Arch(new Vector3(0,2,z),39,20,p);
   // Raised throne platform has a wide ramp and side stairs for puzzle staging.
   Box("Royal dais",new Vector3(0,3.1f,93),new Vector3(26,2.2f,16),black,p);
   TileSurface(new Vector3(0,4.2f,93),26,16,Quaternion.identity,p);
   Ramp(new Vector3(0,2,79),new Vector3(0,4.2f,85),10,p,"Keep dais approach");
   foreach(int s in new[]{-1,1})for(int i=0;i<8;i++){Box("Side stair to the throne",new Vector3(s*11,2+(i+1)*.1375f,79+i),new Vector3(3,(i+1)*.275f,1),ash,p);TileSurface(new Vector3(s*11,2+(i+1)*.275f,79+i),3,1,Quaternion.identity,p);}
   Column(new Vector3(-6,4.2f,96),9,p,1.7f);Column(new Vector3(6,4.2f,96),9,p,1.7f);
   Box("Throne silhouette",new Vector3(0,7,98),new Vector3(5,5.6f,1.7f),black,p);
   var eclipse=DungeonArt.Ring("Black sun throne seal",new Vector3(0,11,96.8f),3.5f,.38f,bronze,p);eclipse.transform.rotation=Quaternion.Euler(90,0,0);
   for(int i=0;i<12;i++){float a=i*Mathf.PI/6;var spike=DungeonArt.Crystal("Eclipse crown spike",new Vector3(Mathf.Sin(a)*3.9f,11+Mathf.Cos(a)*3.9f,96.8f),new Vector3(.4f,1.2f,.35f),bronze,p);spike.transform.rotation=Quaternion.Euler(0,0,-a*Mathf.Rad2Deg);DungeonArt.RemoveCollider(spike);}
   foreach(float x in new[]{-15f,15f})foreach(float z in new[]{47f,86f})Brazier(new Vector3(x,2,z),C4,p);
   Socket(4,new Vector3(0,4.2f,90),C4,p);
   // Runtime socket is reachable on the ramped dais; survey region remains the same.
   Label("IV  /  ECLIPSE KEEP",new Vector3(0,14,102),.42f,p);
   Vault(new Vector3(0,16,40),new Vector3(0,16,104),66,7,p,11);
   GothicBay(new Vector3(0,2,46),new Vector3(12,13,10),p);
  }
  static void KeepTower(Vector3 pos,Transform p){
   Box("Buttressed corner tower",pos+Vector3.up*9,new Vector3(7,18,7),black,p);
   foreach(float yy in new[]{0f,4f,8f,12f,16f}){
    Module("Cube_171",pos+Vector3.up*yy,new Vector3(7.5f,.5f,7.5f),p);
    foreach(int s in new[]{-1,1}){Module("Cube_048",pos+new Vector3(s*3.1f,yy+.4f,0),new Vector3(1.4f,3.6f,6.4f),p);Module("Cube_048",pos+new Vector3(0,yy+.4f,s*3.1f),new Vector3(6.4f,3.6f,1.4f),p);}
   }
   foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Box("Tower merlon",pos+new Vector3(x*2.8f,19.2f,z*2.8f),new Vector3(1.8f,2.3f,1.8f),ash,p);
  }
  static void Ramp(Vector3 a,Vector3 b,float width,Transform p,string name){
   var delta=b-a;float horizontal=new Vector2(delta.x,delta.z).magnitude;var o=Box(name,(a+b)*.5f-Vector3.up*.19f,new Vector3(width,.38f,delta.magnitude),ash,p);
   o.transform.rotation=Quaternion.LookRotation(new Vector3(delta.x,0,delta.z))*Quaternion.Euler(-Mathf.Atan2(delta.y,horizontal)*Mathf.Rad2Deg,0,0);
   o.GetComponent<Renderer>().enabled=false;
   TileSurface((a+b)*.5f,width,Vector3.Distance(a,b),o.transform.rotation,p);
  }
  static void Passage(Vector3 a,Vector3 b,Color c,Transform p,bool roof=true){
   Vector3 d=b-a;float len=new Vector2(d.x,d.z).magnitude;Vector3 side=Vector3.Cross(Vector3.up,d).normalized;
   Ramp(a,b,8,p,"Continuous stone passage floor");
   PassageTorches(a,b,side,len,c,p);if(roof)Vault(a+Vector3.up*6,b+Vector3.up*6,8.6f,2.25f,p,0);
   Wall(a+side*4,b+side*4,6,None,p);Wall(a-side*4,b-side*4,6,None,p);
   int count=Mathf.Max(1,Mathf.FloorToInt(len/10));float yaw=Mathf.Abs(d.z)>Mathf.Abs(d.x)?0:90;
   for(int i=0;i<count;i++){
    var v=Vector3.Lerp(a,b,(i+.5f)/count);Arch(v,8,8,p,yaw);
   }
  }
   // Three alternating wayfinding flames per retained passage; luminosity is unchanged.
  static void PassageTorches(Vector3 a,Vector3 b,Vector3 side,float len,Color c,Transform p){
   int count=3;
   for(int i=0;i<count;i++)Brazier(Vector3.Lerp(a,b,(i+.5f)/count)+side*(i%2==0?3.1f:-3.1f),c,p,.85f);
  }
  // Architecture keeps its vertical clearance; room footprints use the requested factors.
  static void ResizeRoom(int index){
   var room=world.Cast<Transform>().Single(t=>t.name.StartsWith(index.ToString("00")+" •"));
   float scale=CitadelLayout.Scales[index];
   var torches=room.GetComponentsInChildren<TorchFlame>(true).Select(t=>t.transform.parent).Distinct().ToArray();
   var lines=room.GetComponentsInChildren<LineRenderer>(true).Where(l=>l.useWorldSpace).ToArray();
   foreach(var line in lines){var points=new Vector3[line.positionCount];line.GetPositions(points);for(int i=0;i<points.Length;i++)points[i]=CitadelLayout.Map(index,points[i]);line.SetPositions(points);}
   room.localScale=new Vector3(scale,1,scale);
   Vector3 legacy=CitadelLayout.LegacyCenters[index],center=CitadelLayout.Centers[index];
   room.position=new Vector3(center.x-legacy.x*scale,0,center.z-legacy.z*scale);
   // Move torch positions with the walls while retaining the full imported handles and flames.
   foreach(var torch in torches)torch.localScale=Vector3.Scale(torch.localScale,new Vector3(1/scale,1,1/scale));
   if(index==1)ResizeLunarMechanism(room);
   if(index==4){
    // A pitched box under a nonuniformly scaled parent is skewed. Rebuild this collider
    // in world space so its top meets the raised dais after the footprint compression.
    var ramp=room.GetComponentsInChildren<BoxCollider>().Single(c=>c.name=="Keep dais approach");
    var a=CitadelLayout.Map(4,new Vector3(0,2,79));var b=CitadelLayout.Map(4,new Vector3(0,4.2f,85));
    var delta=b-a;var rotation=Quaternion.LookRotation(new Vector3(delta.x,0,delta.z))*Quaternion.Euler(-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg,0,0);
    var anchor=new GameObject("Unscaled throne ramp collision").transform;anchor.SetParent(room,true);
    ramp.transform.SetParent(anchor,true);ramp.transform.rotation=rotation;
    ramp.transform.position=(a+b)*.5f-rotation*Vector3.up*.19f;
    ramp.transform.localScale=new Vector3(7.5f,.38f,delta.magnitude+.08f);
   }
   // Re-tile the smaller surfaces at the same three-metre design pitch as the corridors.
   foreach(var patch in tilePatches.Where(t=>t.Group&&t.Group.IsChildOf(room)).ToArray()){
    Vector3 forward=Vector3.Scale(patch.Rotation*Vector3.forward,new Vector3(scale,1,scale));
    Vector3 right=Vector3.Scale(patch.Rotation*Vector3.right,new Vector3(scale,1,scale));
    var rotation=Quaternion.LookRotation(forward.normalized,Vector3.Cross(forward,right).normalized);
    UnityEngine.Object.DestroyImmediate(patch.Group.gameObject);
    TileSurface(CitadelLayout.Map(index,patch.Center),patch.Width*right.magnitude,patch.Depth*forward.magnitude,rotation,room,patch.Octagon,patch.Corner*scale);
    tilePatches.Remove(patch);
   }
  }
  static void BuildRoutes(){
   var network=Group("CONNECTIVE NETWORK • four linear passages");
   string[] names={"Nexus to Sunken Prism","Sunken Prism to Ember Crypt","Ember Crypt to Violet Sanctum","Violet Sanctum to Eclipse Keep"};
   Color[] colors={C1,C2,C3,C4};
   for(int i=0;i<4;i++){
    var passage=new GameObject("Passage "+(i+1)+" • "+names[i]).transform;passage.SetParent(network,false);
    Passage(CitadelLayout.PassageStart(i),CitadelLayout.PassageEnd(i),colors[i],passage);
   }
  }
  static void GothicBay(Vector3 pos,Vector3 size,Transform parent,float yaw=0){
   string path="Assets/Imported/Gothic/Hallsingle.obj";if(!File.Exists(path))return;
   var bay=Module("Gothic_Hallsingle",pos,size,parent,yaw,false,ash);bay.name="Gothic rib vault • supplied modularDungeon FBX";
   // Preserve traversal width: decorative vaulted ribs have no mesh collider.
  }
  static void Pool(Vector3 pos,Vector2 size,Transform p){
   Box("Shallow black water",pos,new Vector3(size.x,.08f,size.y),water,p,false);
   foreach(int s in new[]{-1,1}){
    Box("Pool curb",pos+new Vector3(s*(size.x/2+.25f),.15f,0),new Vector3(.5f,.3f,size.y+1),ash,p);
    Box("Pool curb",pos+new Vector3(0,.15f,s*(size.y/2+.25f)),new Vector3(size.x+1,.3f,.5f),ash,p);
   }
  }
  static void CrystalCluster(Vector3 pos,Color c,Transform p){
   var mat=DungeonArt.Mat("Raw mineral "+ColorUtility.ToHtmlStringRGB(c),c,0);
   for(int i=0;i<4;i++){var o=DungeonArt.Crystal("Wild cavern crystal",pos+new Vector3(R(-1.3f,1.3f),R(.9f,1.7f),R(-1,1)),new Vector3(R(.45f,1),R(1.2f,3),R(.45f,1)),mat,p);o.transform.rotation=Quaternion.Euler(R(-15,15),R(0,180),R(-20,20));}
  }
  static void Label(string text,Vector3 pos,float size,Transform p,float yaw=0){var label=DungeonArt.Label(text,pos,text,size,new Color(.79f,.72f,.53f),p);label.transform.rotation=Quaternion.Euler(0,yaw,0);}
  static void BuildLandscape(){
   var p=Group("CAVERN • fractured bedrock and buttresses");
   for(int room=0;room<5;room++){
    var center=CitadelLayout.Centers[room];var size=CitadelLayout.Sizes[room];
    for(int i=0;i<14;i++){
     float a=i*Mathf.PI*2/14;
     if(Mathf.Abs(Mathf.Sin(a))<.35f)continue; // Keep both east/west portals clear.
     var pos=center+new Vector3(Mathf.Cos(a)*(size.x*.5f+5),-2,Mathf.Sin(a)*(size.y*.5f+5));
     DungeonArt.Rock("Cavern bedrock",pos,new Vector3(R(2,4),R(4,7),R(2,4)),black,p);
    }
   }
  }
  static void SaveAssets(){
   Directory.CreateDirectory("Assets/Citadel/Generated");var seen=new HashSet<UnityEngine.Object>();int index=0;
   // Cached flame meshes can already be persistent on a second menu rebuild.
   // Keep their asset paths reserved while replacing obsolete generated geometry.
   var filters=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None);
   var reserved=new HashSet<string>(filters.Where(f=>f.sharedMesh&&AssetDatabase.Contains(f.sharedMesh)).Select(f=>AssetDatabase.GetAssetPath(f.sharedMesh)));
   AssetDatabase.StartAssetEditing();
   try{
    foreach(var f in filters){var m=f.sharedMesh;if(m&&!AssetDatabase.Contains(m)&&seen.Add(m)){
     while(reserved.Contains("Assets/Citadel/Generated/Mesh_"+index+".asset"))index++;
     AssetDatabase.CreateAsset(m,"Assets/Citadel/Generated/Mesh_"+index+++".asset");
    }}
    foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))foreach(var m in r.sharedMaterials)if(m&&!AssetDatabase.Contains(m)&&seen.Add(m)){
     string n=string.Join("_",m.name.Split(Path.GetInvalidFileNameChars()));AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath("Assets/Citadel/Generated/"+n+".mat"));
    }
   }finally{AssetDatabase.StopAssetEditing();}
  }
  static void Capture(Camera c,string file,Vector3 pos,Vector3 target,bool ortho,float lens,int w,int h){
   c.transform.position=pos;c.transform.LookAt(target);c.orthographic=ortho;c.orthographicSize=lens;c.fieldOfView=ortho?60:lens;
   var rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32){antiAliasing=4};c.targetTexture=rt;c.Render();RenderTexture.active=rt;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(file,tex.EncodeToPNG());c.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
  }
  [MenuItem("Tools/Fourfold Citadel/Render Linear Dungeon")]
  public static void Render(){
   Directory.CreateDirectory("Renders/LinearLayout");var obj=new GameObject("Temporary showcase camera");var c=obj.AddComponent<Camera>();c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.013f,.023f,.035f);c.farClipPlane=650;c.nearClipPlane=.08f;
   bool fog=RenderSettings.fog;Color ambientSky=RenderSettings.ambientSkyColor,ambientEquator=RenderSettings.ambientEquatorColor;
   var roofRenderers=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.name.StartsWith("Roof_")).ToArray();
   var enabled=roofRenderers.Select(r=>r.enabled).ToArray();
   var keyObject=new GameObject("Temporary cutaway key light");var key=keyObject.AddComponent<Light>();key.type=LightType.Directional;key.intensity=.85f;key.color=new Color(.80f,.87f,1f);key.transform.rotation=Quaternion.Euler(55,-30,0);
   var fillObject=new GameObject("Temporary cutaway fill light");var overviewFill=fillObject.AddComponent<Light>();overviewFill.type=LightType.Directional;overviewFill.intensity=.45f;overviewFill.color=new Color(1,.84f,.65f);overviewFill.transform.rotation=Quaternion.Euler(65,160,0);
   try {
    RenderSettings.fog=false;RenderSettings.ambientSkyColor=new Color(.48f,.53f,.60f);RenderSettings.ambientEquatorColor=new Color(.24f,.27f,.33f);
    foreach(var r in roofRenderers)r.enabled=false;
    Capture(c,"Renders/LinearLayout/01-linear-citadel-cutaway.png",new Vector3(110,235,-205),new Vector3(124,0,0),true,53,3000,1100);
    Capture(c,"Renders/LinearLayout/02-linear-floor-plan.png",new Vector3(125,300,0),new Vector3(125,0,.01f),true,47,3000,1000);
    key.enabled=false;overviewFill.enabled=false;
    for(int i=0;i<roofRenderers.Length;i++)roofRenderers[i].enabled=enabled[i];
    RenderSettings.fog=fog;RenderSettings.ambientSkyColor=ambientSky;RenderSettings.ambientEquatorColor=ambientEquator;
    var fill=new GameObject("Preview hand lamp");fill.transform.SetParent(c.transform,false);var lamp=fill.AddComponent<Light>();lamp.type=LightType.Spot;lamp.intensity=2.16f;lamp.range=18;lamp.spotAngle=76;lamp.innerSpotAngle=44;lamp.color=new Color(1,.88f,.7f);
    Capture(c,"Renders/LinearLayout/03-nexus-arrival.png",new Vector3(0,1.7f,0),new Vector3(16,2,0),false,76,1600,900);
    Capture(c,"Renders/LinearLayout/04-sunken-prism.png",CitadelLayout.Map(1,new Vector3(-38,3.6f,-9)),CitadelLayout.Map(1,new Vector3(-80,4.2f,24)),false,73,1600,900);
    Capture(c,"Renders/LinearLayout/05-ember-crypt.png",CitadelLayout.Map(2,new Vector3(47,1,-5)),CitadelLayout.Map(2,new Vector3(80,.3f,5)),false,70,1600,900);
    Capture(c,"Renders/LinearLayout/06-violet-sanctum.png",CitadelLayout.Map(3,new Vector3(-20,3,-86)),CitadelLayout.Map(3,new Vector3(2,4,-65)),false,72,1600,900);
    Capture(c,"Renders/LinearLayout/07-eclipse-keep.png",CitadelLayout.Map(4,new Vector3(0,4.3f,55)),CitadelLayout.Map(4,new Vector3(0,10,96)),false,75,1600,900);
   }finally{
    for(int i=0;i<roofRenderers.Length;i++)roofRenderers[i].enabled=enabled[i];
    RenderSettings.fog=fog;RenderSettings.ambientSkyColor=ambientSky;RenderSettings.ambientEquatorColor=ambientEquator;
    UnityEngine.Object.DestroyImmediate(obj);UnityEngine.Object.DestroyImmediate(keyObject);UnityEngine.Object.DestroyImmediate(fillObject);
   }
   Debug.Log("CITADEL_LINEAR_RENDERS_READY");
  }
  public static void BuildAll(){
   Build();Render();VerifyGeometry();EditorSceneManager.OpenScene(ScenePath);Directory.CreateDirectory("Build/Windows");
   var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Select(s=>s.path).ToArray(),locationPathName="Build/Windows/The Fourfold Citadel.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
   if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Citadel build failed: "+report.summary.result);
   File.WriteAllText("Documentation/Verification.txt","Unity "+Application.unityVersion+"\nLinear scene generated.\nSeven linear-layout Unity renders generated.\nFour regional sockets and geometry verified.\nWindows x64 build: "+report.summary.result+"\n");Debug.Log("CITADEL_BUILD_SUCCESS");
  }
  static void VerifyGeometry(){
   if(!UnityEngine.Object.FindFirstObjectByType<LunarPrismPuzzle>())throw new Exception("Missing Sunken Prism trial");
   for(int i=2;i<=4;i++)if(!GameObject.Find("PuzzleSocket_"+i))throw new Exception("Missing region socket "+i);
   Physics.SyncTransforms();var feet=new[]{CitadelLayout.SpawnPosition,CitadelLayout.Map(1,new Vector3(-78,0,10)),CitadelLayout.Map(2,new Vector3(72,-2,4)),CitadelLayout.Centers[3],CitadelLayout.Map(4,new Vector3(0,4.2f,90))};
   foreach(var p in feet)if(!Physics.Raycast(p+Vector3.up*1.5f,Vector3.down,4,~(1<<2),QueryTriggerInteraction.Ignore))throw new Exception("Missing walkable floor near "+p);
   Debug.Log("CITADEL_GEOMETRY_PASS");
  }
 }
}
