using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SunkenPrism
{
 public static partial class CitadelBuilder
 {
  const string LunarAssets = "Assets/Imported/LunarPuzzle/";
  static Transform lunarGateRoot;
  static readonly Vector3[] mirrorCenters = {new Vector3(-87,1.9f,-12),new Vector3(-87,1.9f,8),new Vector3(-69,1.9f,8)};

  [MenuItem("Tools/Fourfold Citadel/Play Moon Trial")]
  public static void PlayMoonTrial()
  {
   if(SceneManager.GetActiveScene().path!=ScenePath)EditorSceneManager.OpenScene(ScenePath);
   SessionState.SetBool("FourfoldMoonPreview",true);
   EditorApplication.update-=PreviewArrival;EditorApplication.update+=PreviewArrival;
   EditorApplication.isPlaying=true;
  }
  [InitializeOnLoadMethod]
  static void RestoreMoonPreview(){if(SessionState.GetBool("FourfoldMoonPreview",false))EditorApplication.update+=PreviewArrival;}
  static void PreviewArrival()
  {
   if(!EditorApplication.isPlaying){if(!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool("FourfoldMoonPreview",false);EditorApplication.update-=PreviewArrival;}return;}
   var player=ExplorerController.Instance;if(!player||!LunarPrismPuzzle.Instance)return;
   EditorApplication.update-=PreviewArrival;
   SessionState.SetBool("FourfoldMoonPreview",false);
   player.Teleport(CitadelLayout.Map(1,new Vector3(-94,.12f,-14.5f)));player.transform.rotation=Quaternion.Euler(0,-39,0);
   player.View.GetComponentInChildren<Light>().enabled=true;
   var gameView=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");if(gameView!=null)EditorWindow.GetWindow(gameView).Focus();
  }

  [MenuItem("Tools/Fourfold Citadel/Update Sunken Prism Moon Trial")]
  public static void UpdateSunkenPrism()
  {
   if(EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode before rebuilding the Sunken Prism.");
   if(SceneManager.GetActiveScene().path!=ScenePath) throw new Exception("Open FourfoldCitadel first.");
   world=SceneManager.GetActiveScene().GetRootGameObjects().Single(o=>o.name.StartsWith("THE FOURFOLD CITADEL")).transform;
   EditorTools.LunarAssetImport.ConfigureExisting();
   rng=new System.Random(44219); modules.Clear(); roofs.Clear(); MatInit();
   var previous=world.Cast<Transform>().Single(t=>t.name.StartsWith("01 •"));
   UnityEngine.Object.DestroyImmediate(previous.gameObject);
   BuildPrism();
   AddRoomTorches("01 •",C1,new Vector3(-100,0,5),new Vector3(-86,0,-17),new Vector3(-61,0,-16),new Vector3(-61,0,25),new Vector3(-37,0,9));
   ResizeRoom(1);
   BuildLunarGates(); SaveAssets();
   var room=world.Cast<Transform>().Single(t=>t.name.StartsWith("01 •"));
   foreach(var r in room.GetComponentsInChildren<Renderer>(true))
    if(!r.GetComponentInParent<LunarPrismPuzzle>()&&!r.GetComponentInParent<TorchFlame>()) GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.BatchingStatic);
   EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
   EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
   Debug.Log("LUNAR_ROOM_READY: existing other regions and passages preserved.");
  }

  static Transform Child(string name,Transform parent,Vector3 pos)
  {
   var o=new GameObject(name);o.transform.SetParent(parent,false);o.transform.position=pos;return o.transform;
  }
  static void BuildLunarPrismRoom()
  {
   var room=Group("01 • The Sunken Prism / lunar reflection cathedral");
   Floor(room,-68,4,74,56,0);
   Walls(room,-105,-31,-24,32,0,11,None,None,new[]{4f},new[]{4f});
   // Perimeter aisles leave the complete optical path unobstructed at eye height.
   foreach(float x in new[]{-101f,-54f,-36f}) foreach(float z in new[]{-18f,26f}) Column(new Vector3(x,0,z),12.5f,room,2.1f);
   foreach(float x in new[]{-97f,-56f,-38f}) Arch(new Vector3(x,0,4),45,15,room,90);
   Vault(new Vector3(-105,11,4),new Vector3(-31,11,4),56,6,room,12);
   Pool(new Vector3(-44,.04f,13),new Vector2(8,18),room);
   Pool(new Vector3(-99,.04f,20),new Vector2(5,10),room);
   foreach(var point in new[]{new Vector3(-98,0,-19),new Vector3(-78,0,27),new Vector3(-42,0,-18),new Vector3(-38,0,25)}) Brazier(point,C1,room);
   foreach(float x in new[]{-102f,-34f}) Banner(new Vector3(x,9,15),5,room,x<-80?90:270);
   Label("I  /  THE SUNKEN PRISM",new Vector3(-33,7.5f,0),.3f,room,90);
   Label("THE LUNAR CONCORDANCE",new Vector3(-80,9.8f,30.55f),.34f,room);
   Label("KINDLE  •  REFLECT  •  AWAKEN",new Vector3(-77,2.3f,-21.8f),.22f,room,180);
   Label("RECTANGLE   >   OVAL   >   CIRCLE   >   PRISM",new Vector3(-83,1.1f,-19.8f),.17f,room,180);
   var mechanism=Child("LUNAR TRIAL • torch, three mirrors and moon seals",room,Vector3.zero);
   var puzzle=mechanism.gameObject.AddComponent<LunarPrismPuzzle>();
   var shadowInk=DungeonArt.Mat("Light-absorbing mirror shadow",new Color(.003f,.003f,.003f));
   BuildPuzzleTorch(puzzle,mechanism);
   puzzle.MirrorRoots=new Transform[3];puzzle.MirrorSockets=new Transform[3];
   string[] shapes={"Rectangle","Oval","Circle"};
   Vector3[] scatter={new Vector3(-93,1.37f,-3),new Vector3(-77,1.37f,-7),new Vector3(-59,1.37f,9)};
   int[] order={2,0,1}; // A reproducible shuffled starting arrangement makes resets recoverable.
   for(int i=0;i<3;i++)
   {
    var socket=Child(shapes[i]+" shadow socket",mechanism,mirrorCenters[i]);puzzle.MirrorSockets[i]=socket;
    var basePos=mirrorCenters[i];basePos.y=.25f;
    var plinth=DungeonArt.Cylinder(shapes[i]+" mounting stone",basePos,new Vector3(3.2f,.25f,3.2f),ash,socket);
    var shadow=ShapeMesh(shapes[i]+" carved shadow",i,mirrorCenters[i]+Vector3.down*1.385f,shadowInk,socket);
    shadow.transform.localScale=new Vector3(i==2?2.65f:1.65f,1,i==2?2.65f:2.65f);
    OutlineShadow(i,shadow.transform,bronze);
    var hit=socket.gameObject.AddComponent<BoxCollider>();hit.center=new Vector3(0,-1.47f,0);hit.size=new Vector3(3.2f,.65f,3.2f);
    Label(shapes[i].ToUpperInvariant(),mirrorCenters[i]+new Vector3(0,-1.1f,-1.6f),.12f,socket);
    var root=Child("Movable "+shapes[i]+" Mirror",mechanism,scatter[order[i]]);root.rotation=Quaternion.Euler(0,25+i*73,0);puzzle.MirrorRoots[i]=root;
    var source=AssetDatabase.LoadAssetAtPath<GameObject>(LunarAssets+"Mirrors/Mirror_"+shapes[i]+".fbx");
    if(!source)throw new Exception("Missing supplied "+shapes[i]+" mirror");
    var model=(GameObject)PrefabUtility.InstantiatePrefab(source,root);
    model.name="Supplied "+shapes[i]+" mirror • glass normal corrected";
    // Supplied FBX is metre-based, leaned 9 degrees. Put its glass centre at the optical pivot.
    float scale=3f;model.transform.localScale=Vector3.one*scale;model.transform.localRotation=Quaternion.Euler(9,0,0);
    model.transform.localPosition=-(model.transform.localRotation*new Vector3(0,.469221f,.107616f))*scale;
    foreach(var col in model.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(col);
    var mirrorHit=root.gameObject.AddComponent<BoxCollider>();mirrorHit.center=new Vector3(0,-.16f,-.12f);mirrorHit.size=new Vector3(i==2?2.65f:1.75f,2.7f,.65f);
   }
   BuildLunarPrism(puzzle,mechanism);
   BuildMoonAltar(puzzle,mechanism);
  }

  // Room geometry is compressed in X/Z, but hand-sized puzzle pieces and the
  // complete moon altar retain their authored scale at the new floor positions.
  static void ResizeLunarMechanism(Transform room)
  {
   var mechanism=room.GetComponentInChildren<LunarPrismPuzzle>(true).transform;
   var pieces=mechanism.Cast<Transform>().ToArray();
   var positions=pieces.Select(t=>t.position).ToArray();
   var inheritedScale=room.lossyScale;
   mechanism.localScale=new Vector3(1f/inheritedScale.x,1f/inheritedScale.y,1f/inheritedScale.z);
   for(int i=0;i<pieces.Length;i++)pieces[i].position=positions[i];
  }

  static GameObject ShapeMesh(string name,int shape,Vector3 pos,Material material,Transform parent)
  {
   var vertices=new List<Vector3>{Vector3.zero};int n=shape==0?4:40;
   for(int i=0;i<n;i++){
    if(shape==0)vertices.Add(new Vector3(i==0||i==3?-.5f:.5f,0,i<2?-.5f:.5f));
    else {float a=i*Mathf.PI*2/n;vertices.Add(new Vector3(Mathf.Cos(a)*.5f,0,Mathf.Sin(a)*.5f));}
   }
   var ids=new List<int>();for(int i=0;i<n;i++)ids.AddRange(new[]{0,(i+1)%n+1,i+1});
   var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(ids,0);mesh.RecalculateNormals();
   var o=Child(name,parent,pos).gameObject;o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=material;return o;
  }
  static void OutlineShadow(int shape,Transform parent,Material material)
  {
   var o=new GameObject("Incised matching outline");o.transform.SetParent(parent,false);o.transform.localPosition=Vector3.up*.004f;
   var line=o.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.widthMultiplier=.025f;line.sharedMaterial=material;
   int n=shape==0?4:40;line.positionCount=n;
   for(int i=0;i<n;i++)line.SetPosition(i,shape==0?new Vector3(i==0||i==3?-.5f:.5f,0,i<2?-.5f:.5f):new Vector3(Mathf.Cos(i*Mathf.PI*2/n)*.5f,0,Mathf.Sin(i*Mathf.PI*2/n)*.5f));
   line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
  }

  static void BuildPuzzleTorch(LunarPrismPuzzle puzzle,Transform parent)
  {
   var root=Child("Unlit wooden focusing torch",parent,new Vector3(-96,0,-12));puzzle.TorchRoot=root;
   var source=AssetDatabase.LoadAssetAtPath<GameObject>(LunarAssets+"Torch/TorchStand_LowPoly.fbx");
   if(!source)throw new Exception("Missing supplied puzzle torch");
   var model=(GameObject)PrefabUtility.InstantiatePrefab(source,root);model.transform.localScale=Vector3.one*.95f;
   PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
   var flameRoot=Child("Ignitable flame and embers • initially off",root,new Vector3(-96,1.9f,-12));
   foreach(var t in model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Flame_")||t.name=="Embers").ToArray()){
    t.SetParent(flameRoot,true);
    t.gameObject.SetActive(true);
    if(t.name.StartsWith("Flame_")){
     // FBX exports an extra 90-degree object rotation on the flame children. The mesh itself is Y-up.
     var bounds=t.GetComponent<MeshFilter>().sharedMesh.bounds;
     t.localRotation=Quaternion.identity;t.localScale=Vector3.one*.95f;
     t.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*.95f;
    }
   }
   var wood=DungeonArt.Mat("Puzzle torch warm carved wood",new Color(.27f,.12f,.046f));
   var iron=DungeonArt.Mat("Puzzle torch aged iron Metal",new Color(.07f,.073f,.075f));
   var flameMat=DungeonArt.Mat("Puzzle torch flame white gold",new Color(1,.82f,.45f),1.6f);
   foreach(var r in model.GetComponentsInChildren<Renderer>()) {
    var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++) mats[i]=i==0?wood:iron;r.sharedMaterials=mats;
   }
   foreach(var r in flameRoot.GetComponentsInChildren<Renderer>(true)){r.sharedMaterial=flameMat;r.shadowCastingMode=ShadowCastingMode.Off;}
   // A wood sleeve follows the supplied torch stem while retaining its authored basket and feet.
   var sleeve=DungeonArt.Cylinder("Wooden torch shaft",new Vector3(-96,.91f,-12),new Vector3(.17f,.62f,.17f),wood,root);DungeonArt.RemoveCollider(sleeve);
   puzzle.TorchOrigin=Child("Directed beam origin",root,new Vector3(-96,1.9f,-12));puzzle.TorchOrigin.rotation=Quaternion.LookRotation(Vector3.right);
   puzzle.TorchFlame=flameRoot.gameObject;
   puzzle.TorchLight=DungeonArt.PointLight("Puzzle torch local glow",new Vector3(-96,2.15f,-12),new Color(1,.8f,.42f),1.25f,7,flameRoot).GetComponent<Light>();
   var collider=root.gameObject.AddComponent<BoxCollider>();collider.center=Vector3.up;collider.size=new Vector3(.9f,2.5f,.9f);
   flameRoot.gameObject.SetActive(false);
   Label("THE FIRST FLAME",new Vector3(-96,1.3f,-13),.13f,parent);
  }

  static void BuildLunarPrism(LunarPrismPuzzle puzzle,Transform parent)
  {
   var p=new Vector3(-80,1.9f,22);
   Module("Cube_171",p-Vector3.up*1.9f,new Vector3(2.8f,.65f,2.8f),parent);
   Module("Cube_048",p-Vector3.up*1.25f,new Vector3(1.55f,.5f,1.55f),parent);
   var root=Child("White triangular prism • eight triangles",parent,p);puzzle.PrismVisual=root;puzzle.PrismOrigin=root;
   Vector3[] ends={new Vector3(-.78f,-.7f,-.7f),new Vector3(.78f,-.7f,-.7f),new Vector3(0,.7f,-.7f),new Vector3(-.78f,-.7f,.7f),new Vector3(.78f,-.7f,.7f),new Vector3(0,.7f,.7f)};
   int[][] faces={new[]{0,2,1},new[]{3,4,5},new[]{0,1,4,3},new[]{1,2,5,4},new[]{2,0,3,5}};
   var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
   foreach(var f in faces){int start=vertices.Count;foreach(var i in f)vertices.Add(ends[i]);
    uv.AddRange(f.Length==3?new[]{Vector2.zero,new Vector2(.5f,1),Vector2.right}:new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
    triangles.AddRange(new[]{start,start+1,start+2});if(f.Length==4)triangles.AddRange(new[]{start,start+2,start+3});}
   var mesh=new Mesh{name="True triangular prism • 2 triangles and 3 rectangular faces"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
   root.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
   var mat=DungeonArt.Mat("Pearl white lantern prism",new Color(.86f,.86f,.86f),.20f);mat.mainTexture=LanternGradient();mat.SetTexture("_EmissionMap",mat.mainTexture);mat.SetColor("_EmissionColor",Color.white*.20f);mat.SetFloat("_Glossiness",.55f);
   // Standard's material validation disables _EMISSION when EmissiveIsBlack is left set.
   mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;mat.EnableKeyword("_EMISSION");
   puzzle.PrismRenderer=root.gameObject.AddComponent<MeshRenderer>();puzzle.PrismRenderer.sharedMaterial=mat;
   var col=root.gameObject.AddComponent<BoxCollider>();col.size=new Vector3(1.56f,1.4f,1.4f);
   puzzle.PrismLight=DungeonArt.PointLight("Pearl prism glow",p+Vector3.up*.5f,Color.white,.6f,7,parent).GetComponent<Light>();
   Label("THE CONCORDANCE PRISM",p+new Vector3(0,-.9f,-1.7f),.13f,parent);
  }
  static Texture2D LanternGradient()
  {
   const string path="Assets/Citadel/Generated/WhiteLanternGradient.asset";
   var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(existing)return existing;
   var texture=new Texture2D(64,64,TextureFormat.RGB24,false){name="White lantern pearlescent gradient",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
   for(int y=0;y<64;y++)for(int x=0;x<64;x++){
    float edge=Mathf.Min(Mathf.Min(x,63-x),Mathf.Min(y,63-y))/31f;
    float block=.025f*Mathf.Sin((x/8)*17+(y/8)*31);float g=Mathf.Lerp(.49f,.98f,Mathf.SmoothStep(0,1,edge*.95f))+block;
    texture.SetPixel(x,y,new Color(g,g,g));
   }
   texture.Apply();AssetDatabase.CreateAsset(texture,path);return texture;
  }

  static void BuildMoonAltar(LunarPrismPuzzle puzzle,Transform mechanism)
  {
   var center=new Vector3(-80,4.2f,30.8f);
   // Preserve the circular frame and all rune spacing as one full-size cluster
   // when the room footprint is reduced around this now-solid north wall.
   var parent=Child("Framed lunar altar and three seals",mechanism,center);
   var room=parent;
   Box("Lunar altar ashlar backing",new Vector3(-80,4.8f,31.8f),new Vector3(15,9.6f,1.1f),ash,room);
   var darkPanel=Box("Recessed moon stone",center+Vector3.forward*.15f,new Vector3(5.7f,5.7f,.35f),black,room);
   var frame=DungeonArt.Ring("Bronze lunar frame",center-Vector3.forward*.11f,2.8f,.15f,bronze,room);frame.transform.rotation=Quaternion.Euler(-90,0,0);
   var inner=DungeonArt.Ring("Inner lunar engraving",center-Vector3.forward*.115f,2.61f,.045f,bronze,room);inner.transform.rotation=Quaternion.Euler(-90,0,0);
   var moon=Child("Engraved two dimensional moon",parent,center-Vector3.forward*.055f);puzzle.Moon=moon.gameObject.AddComponent<SpriteRenderer>();
   string[] phaseNames={"White_Full","Green_Crescent","Blue_Half","Red_Full"};puzzle.MoonPhases=new Sprite[4];
   for(int i=0;i<4;i++){
    string path=LunarAssets+"Moon/Moon_"+phaseNames[i]+"_DiscOnly.png";
    var importer=AssetImporter.GetAtPath(path) as TextureImporter;
    if(importer!=null&&(importer.textureType!=TextureImporterType.Sprite||importer.spritePixelsPerUnit!=128)){
     importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=128;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.maxTextureSize=512;importer.SaveAndReimport();}
    puzzle.MoonPhases[i]=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(!puzzle.MoonPhases[i])throw new Exception("Missing moon sprite: "+path);
   }
   puzzle.Moon.sprite=puzzle.MoonPhases[0];puzzle.Moon.color=new Color(.13f,.125f,.115f,1);
   puzzle.Moon.shadowCastingMode=ShadowCastingMode.Off;puzzle.Moon.receiveShadows=false;
   puzzle.MoonLight=DungeonArt.PointLight("Lunar light on engraved stone",center-Vector3.forward*1.4f,new Color(1,.94f,.82f),.25f,9,parent).GetComponent<Light>();
   var halo=GameObject.CreatePrimitive(PrimitiveType.Quad);halo.name="Soft lunar halo";halo.transform.SetParent(parent,false);halo.transform.position=center-Vector3.forward*.06f;halo.transform.localScale=Vector3.one*7f;DungeonArt.RemoveCollider(halo);
   var haloMat=new Material(Shader.Find("SunkenPrism/LunarHalo")){name="Subtle lunar aureole"};haloMat.SetColor("_Color",new Color(1,.96f,.88f,.035f));
   puzzle.MoonHalo=halo.GetComponent<Renderer>();puzzle.MoonHalo.sharedMaterial=haloMat;puzzle.MoonHalo.shadowCastingMode=ShadowCastingMode.Off;puzzle.MoonHalo.receiveShadows=false;
   puzzle.LeftRune=Rune(0,center+new Vector3(-4,0,-.10f),parent,room);
   puzzle.UpperRune=Rune(1,center+new Vector3(0,4,-.10f),parent,room);
   puzzle.RightRune=Rune(2,center+new Vector3(4,0,-.10f),parent,room);
   Label("CRESCENT   /   HALF   /   FULL",center+new Vector3(0,-3.2f,-.20f),.16f,room);
  }
  static Renderer[] Rune(int index,Vector3 center,Transform parent,Transform room)
  {
   Box("Engraved rune tablet "+index,center+Vector3.forward*.16f,new Vector3(2.55f,2.55f,.26f),ash,room);
   var group=Child("Lunar seal "+index,parent,center);
   var mat=DungeonArt.Mat("Dormant lunar seal "+index,new Color(.24f,.23f,.21f),.025f);
   mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;mat.EnableKeyword("_EMISSION");
   Action<string,Vector3[]> stroke=(name,points)=>{
    var o=Child(name,group,center).gameObject;var line=o.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=points.Length;line.SetPositions(points);line.widthMultiplier=.095f;line.numCapVertices=2;line.numCornerVertices=2;line.generateLightingData=true;line.sharedMaterial=mat;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
   };
   if(index==0){stroke("Three-bar vertical seal",new[]{new Vector3(0,-.84f,0),new Vector3(0,.84f,0)});foreach(float y in new[]{-.84f,0,.84f})stroke("Engraved crossbar",new[]{new Vector3(-.68f,y,0),new Vector3(.68f,y,0)});}
   if(index==1){stroke("Triangle seal",new[]{new Vector3(-.91f,-.8f,0),new Vector3(0,.94f,0),new Vector3(.91f,-.8f,0),new Vector3(-.91f,-.8f,0)});stroke("Inner upright",new[]{new Vector3(0,-.4f,0),new Vector3(0,.24f,0)});}
   if(index==2){var points=new Vector3[49];for(int i=0;i<points.Length;i++){float t=i/48f,a=.45f+t*Mathf.PI*3.5f,r=Mathf.Lerp(.08f,.99f,t);points[i]=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0);}stroke("Spiral seal",points);}
   return group.GetComponentsInChildren<Renderer>();
  }

  static void BuildLunarGates()
  {
   var old=world.Find("LUNAR SEAL • Ember Crypt portcullises");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
   lunarGateRoot=Group("LUNAR SEAL • Ember Crypt portcullises");
   var puzzle=world.GetComponentInChildren<LunarPrismPuzzle>();if(!puzzle)throw new Exception("Sunken moon puzzle missing");
   puzzle.EmberGates=new Transform[1];
   Vector3[] points={CitadelLayout.Map(2,new Vector3(36,-2,0))};
   for(int i=0;i<points.Length;i++){
    var gate=Child("Ember gate "+i+" • requires three moon seals",lunarGateRoot,points[i]);gate.rotation=Quaternion.Euler(0,90,0);puzzle.EmberGates[i]=gate;
    var slab=Box("Sealed carved stone door",points[i]+Vector3.up*3.7f,new Vector3(6.05f,7.4f,.75f),ash,gate);slab.transform.rotation=gate.rotation;
    for(int k=-2;k<=2;k++){
     var rib=Box("Bronze portcullis bar",points[i]+gate.right*k+Vector3.up*3.7f,new Vector3(.10f,7.4f,.16f),bronze,gate,false);rib.transform.rotation=gate.rotation;rib.transform.position-=gate.forward*.43f;
    }
    foreach(float y in new[]{1.2f,6.4f}) {var band=Box("Gate lintel band",points[i]+Vector3.up*y-gate.forward*.45f,new Vector3(6,.12f,.12f),bronze,gate,false);band.transform.rotation=gate.rotation;}
    var label= DungeonArt.Label("Ember gate seal instruction",points[i]+Vector3.up*3-gate.forward*.52f,"EMBER CRYPT\nTHREE MOON SEALS REQUIRED",.19f,new Color(.8f,.69f,.46f),gate);label.transform.rotation=gate.rotation;
   }
  }
 }
}
