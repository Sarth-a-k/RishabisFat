using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SunkenPrism
{
    public static class DungeonBuilder
    {
        static Transform architecture;
        static Material[] stone, floor;
        static Material trim, dark, gold, teal, rock;
        static System.Random rng;
        const string ScenePath="Assets/Scenes/SunkenPrism.unity";
        static float Rand(float low,float high)=>low+(float)rng.NextDouble()*(high-low);

        [MenuItem("Tools/Sunken Prism/Rebuild Dungeon")]
        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            rng=new System.Random(718);
            var root=new GameObject("THE SUNKEN PRISM • Region 01");
            architecture=new GameObject("Architecture • original modular stonework").transform;architecture.SetParent(root.transform);
            InitMaterials(); Lighting(root.transform);
            Room("00 • The Threshold",0,6.5f,14,13,0,true,true,false,false);
            Room("01 • Hall of Reflection",0,26,22,26,0,true,true,false,false);
            Corridor(39,45,0);
            Room("02 • Chromatic Halls",0,59,26,28,0,true,true,true,true);
            Room("Red Reliquary",-21,59,16,14,0,false,false,false,true);
            Room("Blue Mirror Cache",21,59,16,14,0,false,false,true,false);
            Corridor(73,79,0);
            Room("03 • Shadow Theater",0,91,24,24,0,true,true,false,false);
            Corridor(103,109,0);
            Room("04 • The Confluence",0,122,28,26,0,true,true,false,false);
            Descent();
            Room("05 • The Unseen",0,168,18,18,-6,true,false,false,false);
            DressRooms();
            new GameObject("Runtime • FPS, optical puzzles, journal").AddComponent<DungeonRuntime>();
            SaveGeneratedAssets();
            StaticEditorFlags flags=StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic;
            foreach(var renderer in architecture.GetComponentsInChildren<Renderer>()) GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,flags);
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.companyName="Chromatic Expeditions";PlayerSettings.productName="The Sunken Prism";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.runInBackground=true;PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input=settings.FindProperty("activeInputHandler");if(input!=null){input.intValue=0;settings.ApplyModifiedPropertiesWithoutUndo();}
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaderList=graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach(string shaderName in new[]{"Standard","Unlit/Color","SunkenPrism/WorldText","SunkenPrism/LightShaft"}){
                var shader=Shader.Find(shaderName);bool found=false;for(int i=0;i<shaderList.arraySize;i++)if(shaderList.GetArrayElementAtIndex(i).objectReferenceValue==shader)found=true;
                if(!found&&shader){int index=shaderList.arraySize;shaderList.InsertArrayElementAtIndex(index);shaderList.GetArrayElementAtIndex(index).objectReferenceValue=shader;}
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("SUNKEN_PRISM_SCENE_READY "+ScenePath);
        }
        static void InitMaterials()
        {
            stone=new Material[5];floor=new Material[5];
            for(int i=0;i<5;i++){
                float t=i*.024f;
                stone[i]=DungeonArt.Mat("Limestone "+i,new Color(.19f+t,.245f+t,.26f+t));
                floor[i]=DungeonArt.Mat("Flagstone "+i,new Color(.22f+t,.265f+t,.27f+t));
            }
            trim=DungeonArt.Mat("Pale cut stone",new Color(.37f,.43f,.43f));
            dark=DungeonArt.Mat("Mortar",new Color(.058f,.084f,.094f));
            gold=DungeonArt.Mat("Aged gold Metal",new Color(.56f,.37f,.15f));
            teal=DungeonArt.Mat("Inlaid turquoise",new Color(.08f,.48f,.50f),.2f);
            rock=DungeonArt.Mat("Cavern slate",new Color(.11f,.16f,.18f));
        }
        static void Lighting(Transform parent)
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.24f,.32f,.4f);RenderSettings.ambientEquatorColor=new Color(.11f,.17f,.21f);RenderSettings.ambientGroundColor=new Color(.08f,.09f,.1f);
            RenderSettings.fog=true;RenderSettings.fogColor=new Color(.035f,.067f,.085f);RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogDensity=.008f;
            RenderSettings.skybox=null;
            var sun=new GameObject("Sunlight through the fractured vault");sun.transform.SetParent(parent);sun.transform.rotation=Quaternion.Euler(52,-32,0);
            var l=sun.AddComponent<Light>();l.type=LightType.Directional;l.color=new Color(1,.88f,.66f);l.intensity=1.1f;l.shadows=LightShadows.Soft;l.shadowStrength=.85f;
            QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.shadowDistance=65;
            QualitySettings.pixelLightCount=8;QualitySettings.antiAliasing=4;QualitySettings.vSyncCount=1;
        }
        static void Room(string name,float cx,float cz,float width,float depth,float y,bool south,bool north,bool west,bool east)
        {
            var group=new GameObject(name).transform;group.SetParent(architecture);
            float x0=cx-width/2,x1=cx+width/2,z0=cz-depth/2,z1=cz+depth/2;
            DungeonArt.Box("Foundation",new Vector3(cx,y-.42f,cz),new Vector3(width+1,.8f,depth+1),dark,group);
            for(float x=x0+1;x<x1;x+=2)for(float z=z0+1;z<z1;z+=2){
                float w=Mathf.Min(1.94f,x1-x+.94f),d=Mathf.Min(1.94f,z1-z+.94f);
                var tile=DungeonArt.Box("Hand cut flagstone",new Vector3(x,y-.035f+Rand(-.012f,.012f),z),new Vector3(w,.12f,d),floor[rng.Next(5)],group);DungeonArt.RemoveCollider(tile);
            }
            // A single continuous collision floor prevents snagging on decorative seams.
            var slab=group.gameObject.AddComponent<BoxCollider>();slab.center=new Vector3(cx,y-.16f,cz);slab.size=new Vector3(width,.32f,depth);
            Wall(new Vector3(cx,y,z0),width,false,south,group);
            Wall(new Vector3(cx,y,z1),width,false,north,group);
            Wall(new Vector3(x0,y,cz),depth,true,west,group);
            Wall(new Vector3(x1,y,cz),depth,true,east,group);
            if(width>12){
                foreach(float x in new[]{x0+1.3f,x1-1.3f})foreach(float z in new[]{z0+1.3f,z1-1.3f})Column(new Vector3(x,y,z),group);
                if(depth>20)foreach(float x in new[]{x0+1.3f,x1-1.3f})Column(new Vector3(x,y,cz),group);
            }
            if(y>=0){
                Torch(new Vector3(x0+.8f,y+2.65f,cz-depth*.27f),group,true);
                Torch(new Vector3(x1-.8f,y+2.65f,cz+depth*.26f),group,false);
                DungeonArt.PointLight("Cool chamber fill",new Vector3(cx,y+5,cz),new Color(.27f,.65f,.79f),2.0f,Mathf.Max(width,depth)*.85f,group);
            }
            if(width>18){DungeonArt.Ring("Outer cosmological engraving",new Vector3(cx,y+.038f,cz),width*.28f,.065f,gold,group);DungeonArt.Ring("Inner cosmological engraving",new Vector3(cx,y+.04f,cz),width*.23f,.035f,teal,group);}
            if(y<0) DungeonArt.Box("VaultRoof • subterranean ceiling",new Vector3(cx,y+6,cz),new Vector3(width+1,.6f,depth+1),rock,group);
            else {
                float slitLeft=cx-4.5f,slitRight=cx-1.5f;
                DungeonArt.Box("VaultRoof • fractured west slab",new Vector3((x0+slitLeft)/2,y+6.7f,cz),new Vector3(slitLeft-x0,.6f,depth+.5f),stone[0],group);
                DungeonArt.Box("VaultRoof • fractured east slab",new Vector3((slitRight+x1)/2,y+6.7f,cz),new Vector3(x1-slitRight,.6f,depth+.5f),stone[1],group);
                for(float zz=z0+2;zz<z1;zz+=7)DungeonArt.Box("VaultRoof • transverse rib",new Vector3(cx,y+6.1f,zz),new Vector3(width+.5f,.45f,.7f),trim,group);
            }
        }
        static void Wall(Vector3 center,float length,bool alongZ,bool door,Transform parent)
        {
            var wall=new GameObject(door?"Masonry with archway":"Coursed limestone wall").transform;wall.SetParent(parent);wall.position=center;
            wall.rotation=Quaternion.Euler(0,alongZ?90:0,0);
            float height=5.4f;
            for(int row=0;row<7;row++){
                float start=-length/2;int cells=Mathf.CeilToInt(length/1.7f);float cell=length/cells;
                for(int i=0;i<cells;i++){
                    float x=start+cell*(i+.5f);
                    if(door && Mathf.Abs(x)<2.15f && row<5) continue;
                    var block=DungeonArt.Box("Stone course",Vector3.zero,Vector3.one,stone[rng.Next(5)],wall);
                    block.transform.localPosition=new Vector3(x,row*.74f+.37f,Rand(-.025f,.025f));block.transform.localScale=new Vector3(cell-.045f,.70f,.82f);
                    DungeonArt.RemoveCollider(block);
                }
            }
            foreach(float yy in new[]{.18f,4.95f,5.35f}){
                if(door && yy<1){TrimSegment(wall,-length/2,-2.1f,yy);TrimSegment(wall,2.1f,length/2,yy);}
                else{var band=DungeonArt.Box("Stone cornice",Vector3.zero,Vector3.one,yy>5?trim:dark,wall);band.transform.localPosition=new Vector3(0,yy,0);band.transform.localScale=new Vector3(length+.1f,.18f,1.02f);DungeonArt.RemoveCollider(band);}
            }
            if(door){
                foreach(int side in new[]{-1,1}){
                    var c=wall.gameObject.AddComponent<BoxCollider>();c.center=new Vector3(side*(length/4+1.05f),height/2,0);c.size=new Vector3(length/2-2.1f,height,.9f);
                    for(int i=0;i<5;i++){var b=DungeonArt.Box("Arch jamb",Vector3.zero,Vector3.one,trim,wall);b.transform.localPosition=new Vector3(side*2.23f,i*.65f+.33f,-.03f);b.transform.localScale=new Vector3(.5f,.61f,1.2f);DungeonArt.RemoveCollider(b);}
                }
                var lintel=wall.gameObject.AddComponent<BoxCollider>();lintel.center=new Vector3(0,4.6f,0);lintel.size=new Vector3(4.2f,1.6f,.9f);
                for(int i=0;i<9;i++){
                    float angle=i*Mathf.PI/8;
                    var wedge=DungeonArt.Box("Radial arch stone",Vector3.zero,Vector3.one,trim,wall);
                    wedge.transform.localPosition=new Vector3(Mathf.Cos(angle)*2.23f,3.02f+Mathf.Sin(angle)*1.03f,0);
                    wedge.transform.localRotation=Quaternion.Euler(0,0,angle*Mathf.Rad2Deg-90);
                    wedge.transform.localScale=new Vector3(.73f,.5f,1.15f);DungeonArt.RemoveCollider(wedge);
                }
            }else{var c=wall.gameObject.AddComponent<BoxCollider>();c.center=new Vector3(0,height/2,0);c.size=new Vector3(length,height,.9f);}
        }
        static void TrimSegment(Transform parent,float a,float b,float y){var o=DungeonArt.Box("Foundation moulding",Vector3.zero,Vector3.one,trim,parent);o.transform.localPosition=new Vector3((a+b)/2,y,0);o.transform.localScale=new Vector3(b-a,.32f,1.06f);DungeonArt.RemoveCollider(o);}
        static void Column(Vector3 p,Transform parent)
        {
            DungeonArt.Box("Pillar footing",p+Vector3.up*.18f,new Vector3(1.5f,.36f,1.5f),trim,parent);
            for(int i=0;i<6;i++)DungeonArt.Box("Pillar drum",p+Vector3.up*(.73f+i*.69f),new Vector3(1.06f,.66f,1.06f),stone[(i+2)%5],parent);
            DungeonArt.Box("Pillar capital",p+Vector3.up*5.1f,new Vector3(1.5f,.42f,1.5f),trim,parent);
            foreach(float y in new[]{.5f,3.55f,4.8f})DungeonArt.Box("Gilt pillar band",p+Vector3.up*y,new Vector3(1.12f,.09f,1.12f),gold,parent);
            var inlay=DungeonArt.Box("Turquoise inscription",p+new Vector3(0,2.4f,-.543f),new Vector3(.12f,.9f,.025f),teal,parent);DungeonArt.RemoveCollider(inlay);
        }
        static void Torch(Vector3 p,Transform parent,bool cool)
        {
            DungeonArt.Box("Sconce bracket",p-Vector3.up*.26f,new Vector3(.42f,.65f,.42f),gold,parent);
            var color=cool?new Color(.21f,.82f,1):new Color(1,.62f,.2f);
            var jewel=DungeonArt.Crystal("Lightstone sconce",p,new Vector3(.3f,.43f,.3f),DungeonArt.Mat(cool?"Cyan lightstone":"Amber lightstone",color,2.8f),parent);DungeonArt.RemoveCollider(jewel);
            DungeonArt.PointLight("Sconce glow",p+Vector3.up*.15f,color,3.5f,9,parent);
        }
        static void Corridor(float a,float b,float y)
        {
            var p=new GameObject("Processional passage").transform;p.SetParent(architecture);
            DungeonArt.Box("Passage floor",new Vector3(0,y-.2f,(a+b)/2),new Vector3(6,.4f,b-a),floor[1],p);
            Wall(new Vector3(-3,y,(a+b)/2),b-a,true,false,p);Wall(new Vector3(3,y,(a+b)/2),b-a,true,false,p);
            for(float z=a;z<b;z+=1.5f){var o=DungeonArt.Box("Processional inlay",new Vector3(0,y+.015f,z+.5f),new Vector3(.09f,.015f,.7f),gold,p);DungeonArt.RemoveCollider(o);}
        }
        static void Descent()
        {
            var p=new GameObject("Descent into the unseen").transform;p.SetParent(architecture);
            var ramp=DungeonArt.Box("Continuous descent collision ramp",new Vector3(0,-3.2f,147),new Vector3(6,.4f,Mathf.Sqrt(24*24+6*6)),rock,p);ramp.transform.rotation=Quaternion.Euler(Mathf.Atan2(6,24)*Mathf.Rad2Deg,0,0);
            for(int i=0;i<24;i++){
                float y=-i*.25f;
                var step=DungeonArt.Box("Worn stair tread",new Vector3(0,y-.21f,135.5f+i),new Vector3(5.8f,.25f,.97f),floor[i%5],p);DungeonArt.RemoveCollider(step);
                foreach(int side in new[]{-1,1})DungeonArt.Box("Deep passage wall",new Vector3(side*3,y+2,135.5f+i),new Vector3(.8f,5,1),rock,p);
            }
            for(int i=0;i<5;i++)foreach(int s in new[]{-1,1}){var shard=DungeonArt.Crystal("Subterranean crystal",new Vector3(s*2.45f,-i*1.1f+.25f,138+i*4),new Vector3(.35f,.7f,.35f),DungeonArt.Mat("Dormant mineral",new Color(.16f,.2f,.22f)),p);shard.transform.Rotate(0,22*i,s*28);}
        }
        static void DressRooms()
        {
            Sign("THE SUNKEN PRISM",new Vector3(0,4.8f,12.35f),.17f);
            Sign("I  /  REFLECTION",new Vector3(0,4.8f,38.35f),.135f);
            Sign("II  /  CHROMATIC HALLS",new Vector3(0,4.8f,72.35f),.135f);
            Sign("III  /  SHADOW THEATER",new Vector3(0,4.8f,102.35f),.135f);
            Sign("IV  /  THE CONFLUENCE",new Vector3(0,4.8f,134.35f),.135f);
            var p=new GameObject("Cavern geology and expedition remnants").transform;p.SetParent(architecture);
            // Broken crystals, rubble, pots and carved reliefs stay away from the playable route.
            foreach(float z in new[]{5f,17f,36f,48f,69f,82f,99f,113f,131f})foreach(int side in new[]{-1,1}){
                float x=z<13?5.5f:z<40?9.4f:z<74?11.4f:z<104?10.4f:12.4f;
                for(int i=0;i<4;i++){
                    var rubble=DungeonArt.Crystal("Fallen masonry",new Vector3(side*(x-Rand(0,.8f)),.2f,z+Rand(-1,1)),new Vector3(Rand(.25f,.7f),Rand(.25f,.6f),Rand(.3f,.7f)),stone[i],p);rubble.transform.rotation=Quaternion.Euler(Rand(30,80),Rand(0,180),15);DungeonArt.RemoveCollider(rubble);
                }
            }
            foreach(var pos in new[]{new Vector3(-5,0,7),new Vector3(5,0,9),new Vector3(-9,0,29),new Vector3(11,0,65),new Vector3(-10,0,95)}){
                var pot=DungeonArt.Cylinder("Ancient amphora",pos+Vector3.up*.5f,new Vector3(.7f,.5f,.7f),gold,p);
                DungeonArt.Cylinder("Amphora rim",pos+Vector3.up*1.02f,new Vector3(.5f,.06f,.5f),dark,p);
            }
            // Tall fragmented bedrock around the outer wall suggests the cavern without hiding the map.
            foreach(float z in new[]{5f,24f,43f,78f,96f,116f,133f,169f})foreach(int side in new[]{-1,1}){
                float x=z<14?11:z>150?13:18;
                var outcrop=DungeonArt.Rock("Fractured cavern rock",new Vector3(side*x,1.6f,z),new Vector3(Rand(2,3.5f),Rand(3,5),Rand(3,5)),rock,p);outcrop.transform.Rotate(4,side*27,side*5);
            }
            foreach(float z in new[]{24f,57f,89f,123f}){
                var top=new Vector3(-4,12,z-5);var bottom=new Vector3(1,.15f,z);
                var shaftMaterial=new Material(Shader.Find("SunkenPrism/LightShaft")){name="Soft daylight shaft"};shaftMaterial.SetColor("_Color",new Color(1,.82f,.5f,.026f));
                var shaft=DungeonArt.Cylinder("Shaft of scattered daylight",(top+bottom)/2,new Vector3(1.9f,Vector3.Distance(top,bottom)/2,1.9f),shaftMaterial,p);
                shaft.transform.up=(top-bottom).normalized;DungeonArt.RemoveCollider(shaft);shaft.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;shaft.GetComponent<Renderer>().receiveShadows=false;
                DungeonArt.PointLight("Pool of daylight",new Vector3(1,4,z),new Color(1,.85f,.56f),2.5f,13,p);
            }
            // Guard silhouette at the threshold, with a short authored line.
            var guard=new Vector3(-4.7f,0,3.5f);
            DungeonArt.Cylinder("Silent threshold guardian",guard+Vector3.up*.95f,new Vector3(.7f,.8f,.7f),dark,p);
            DungeonArt.Crystal("Guardian helm",guard+Vector3.up*1.93f,new Vector3(.5f,.46f,.5f),gold,p);
            DungeonArt.Box("Guardian staff",guard+new Vector3(.6f,1.2f,0),new Vector3(.06f,2.4f,.06f),gold,p);
            var plaque=DungeonArt.Label("Guardian warning",guard+new Vector3(0,2.9f,0),"THE GUARDIAN\n\"Only light may pass.\"",.08f,new Color(.76f,.77f,.66f),p);
        }
        static void Sign(string text,Vector3 position,float size)=>DungeonArt.Label(text,position,text,size,new Color(.89f,.76f,.43f),architecture);

        static void SaveGeneratedAssets()
        {
            Directory.CreateDirectory("Assets/Art/Generated");var seen=new HashSet<UnityEngine.Object>();int meshIndex=0;
            foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)){
                var mesh=filter.sharedMesh;if(mesh && !AssetDatabase.Contains(mesh)&&seen.Add(mesh))AssetDatabase.CreateAsset(mesh,"Assets/Art/Generated/Mesh_"+(meshIndex++)+".asset");
            }
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))foreach(var material in renderer.sharedMaterials){
                if(material && !AssetDatabase.Contains(material)&&seen.Add(material)){
                    string safe=string.Join("_",material.name.Split(Path.GetInvalidFileNameChars()));
                    var path="Assets/Art/Generated/"+safe+".mat";
                    AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(path));
                }
            }
        }
        [MenuItem("Tools/Sunken Prism/Render Showcase")]
        public static void RenderShowcase()
        {
            bool fog=RenderSettings.fog;RenderSettings.fog=false;
            var temp=new GameObject("Temporary optical puzzles");PuzzleGame.Build(temp.transform);
            var co=new GameObject("Showcase Camera");var c=co.AddComponent<Camera>();c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.018f,.032f,.047f);c.nearClipPlane=.1f;c.farClipPlane=500;
            Directory.CreateDirectory("Renders");
            var roofs=new List<Renderer>();foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))if(r.name.StartsWith("VaultRoof")){roofs.Add(r);r.enabled=false;}
            Capture(c,"Renders/01-Dungeon-Overview.png",new Vector3(100,156,-62),new Vector3(0,0,84),true,83,1800,1800);
            foreach(var r in roofs)r.enabled=true;
            RenderSettings.fog=fog;
            Capture(c,"Renders/02-Chromatic-Halls.png",new Vector3(9,4.4f,47.5f),new Vector3(-1,1.6f,60),false,64,1920,1080);
            Capture(c,"Renders/03-Reflection-Hall.png",new Vector3(-8,3.5f,15),new Vector3(1,1.5f,30),false,66,1920,1080);
            Capture(c,"Renders/04-Shadow-Theater.png",new Vector3(-8,4.5f,82),new Vector3(2,1.5f,96),false,64,1920,1080);
            UnityEngine.Object.DestroyImmediate(co);UnityEngine.Object.DestroyImmediate(temp);RenderSettings.fog=fog;
            Debug.Log("SUNKEN_PRISM_RENDERS_READY");
        }
        static void Capture(Camera c,string path,Vector3 from,Vector3 toward,bool ortho,float lens,int width,int height)
        {
            c.transform.position=from;c.transform.LookAt(toward);c.orthographic=ortho;if(ortho)c.orthographicSize=lens;else c.fieldOfView=lens;
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=4};c.targetTexture=rt;c.Render();
            RenderTexture.active=rt;var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());c.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);
        }
        public static void BuildAll()
        {
            Build(); RenderShowcase();
            var testRoot=new GameObject("Puzzle verification");var game=PuzzleGame.Build(testRoot.transform);game.RunSelfTest();
            var solvedCameraObject=new GameObject("Solved shadow verification camera");var solvedCamera=solvedCameraObject.AddComponent<Camera>();solvedCamera.clearFlags=CameraClearFlags.SolidColor;solvedCamera.backgroundColor=new Color(.018f,.032f,.047f);
            Capture(solvedCamera,"Renders/07-Shadow-Key-Solved.png",new Vector3(4.5f,3.4f,88),new Vector3(6,3.1f,101),false,58,1920,1080);
            UnityEngine.Object.DestroyImmediate(solvedCameraObject);UnityEngine.Object.DestroyImmediate(testRoot);
            EditorSceneManager.OpenScene(ScenePath);
            Directory.CreateDirectory("Build/Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Build/Windows/The Sunken Prism.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed: "+report.summary.result);
            File.WriteAllText("Documentation/Build-Verification.txt","Unity "+Application.unityVersion+"\nScene generated and saved.\nFive showcase images rendered by Unity (including solved shadow key).\nPuzzle self-test completed without assertion failures.\nWindows x64 build: "+report.summary.result+"\nBuild size: "+report.summary.totalSize+" bytes\n");
            Debug.Log("SUNKEN_PRISM_BUILD_SUCCESS");
        }
        public static void RebuildPlayer()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Build/Windows/The Sunken Prism.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed: "+report.summary.result);
            Debug.Log("SUNKEN_PRISM_BUILD_SUCCESS");
        }
    }
}
