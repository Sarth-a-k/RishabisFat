using System;
using System.Collections.Generic;
using UnityEngine;

namespace SunkenPrism
{
    public abstract class PuzzleInteractable : MonoBehaviour
    {
        public abstract string Prompt { get; }
        public abstract void Interact();
        public virtual void Rotate(int direction) { }
    }

    public sealed class PrismInteractable : PuzzleInteractable
    {
        public Func<string> Text;
        public Action Use;
        public Action<int> Turn;
        public override string Prompt { get { return Text == null ? "Inspect" : Text(); } }
        public override void Interact() { if (Use != null) Use(); }
        public override void Rotate(int direction) { if (Turn != null) Turn(direction); }
    }

    public sealed class PuzzleGame : MonoBehaviour
    {
        public static PuzzleGame Instance;
        public string Objective { get; private set; }
        public string AreaTitle { get; private set; }
        public string Subtitle { get; private set; }
        public string[] JournalLines { get; private set; }
        public int Relics { get; private set; }
        public bool VisorAvailable { get; private set; }
        public bool VisorOn { get; private set; }
        public bool Completed { get; private set; }
        public bool ReflectionSolved { get { return reflectionSolved; } }
        public bool PrismSolved { get { return prismSolved; } }
        public bool ShadowSolved { get { return shadowSolved; } }
        public bool ConfluenceSolved { get { return confluenceSolved; } }

        readonly List<Gate> gates = new List<Gate>();
        readonly List<GameObject> rayObjects = new List<GameObject>();
        readonly List<GameObject> thermalObjects = new List<GameObject>();
        readonly int[] mirrorSteps = new int[2];
        readonly int[] mixingSteps = new int[2];
        readonly int[] shadowSteps = new int[3];
        readonly int[] shadowTargets = { 1, 3, 2 };
        readonly bool[] shadowPlaced = new bool[3];
        readonly int[] filters = new int[3];
        readonly Transform[] mirrorPivots = new Transform[2];
        readonly Transform[] mixingPivots = new Transform[2];
        readonly Transform[] shadowPieces = new Transform[3];
        readonly GameObject[] floorArtifacts = new GameObject[3];
        readonly MeshFilter[] shadowProjections = new MeshFilter[3];
        readonly Vector3[] shadowCenters = { new Vector3(3.15f, 3.4f, 101.34f), new Vector3(6f, 3.4f, 101.34f), new Vector3(7.8f, 3.05f, 101.34f) };
        readonly Vector3 shadowLamp = new Vector3(6f, 3.5f, 81f);
        readonly Color[] spectrum = { new Color(.9f,.92f,1), new Color(1,.035f,.04f), new Color(.08f,1,.16f), new Color(.12f,.36f,1) };
        readonly Vector3[] mirrorCenters = { new Vector3(0,1.65f,21), new Vector3(0,1.65f,32) };
        Gate reflectionGate, redGate, blueGate, prismGate, shadowGate, finalGate;
        GameObject debris, prism, mirrorPickup, relicPickup, visorPickup;
        Transform rays;
        Material stone, metal, gold, glass, pale, violet, black;
        bool initialized, debrisCleared, reflectionSolved, redOpened, blueOpened, hasMirror, mirrorInstalled, prismSolved, shadowSolved, confluenceSolved;
        int prismMode, carrying = -1;
        float speechUntil;
        readonly List<PrismInteractable> mirrorControls = new List<PrismInteractable>();
        PrismInteractable prismControl, installControl, partControl, lootControl, visorControl, exitControl;
        readonly PrismInteractable[] mixControls = new PrismInteractable[2];
        readonly PrismInteractable[] artifactControls = new PrismInteractable[3];
        readonly PrismInteractable[] plinthControls = new PrismInteractable[3];
        readonly PrismInteractable[] filterControls = new PrismInteractable[3];

        public static PuzzleGame Build(Transform parent)
        {
            GameObject host = new GameObject("Region 01 • Interactive light mechanisms");
            host.transform.SetParent(parent, false);
            PuzzleGame game = host.AddComponent<PuzzleGame>();
            Instance = game;
            game.Initialize();
            return game;
        }

        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            stone = DungeonArt.Mat("Puzzle basalt", new Color(.12f,.17f,.19f));
            metal = DungeonArt.Mat("Aged brass", new Color(.3f,.24f,.12f));
            gold = DungeonArt.Mat("Artifact gold", new Color(.92f,.6f,.16f), .3f);
            glass = DungeonArt.Mat("Silver mirror", new Color(.48f,.85f,.91f), .65f);
            pale = DungeonArt.Mat("Sun crystal", new Color(.85f,.97f,1), 1.8f);
            violet = DungeonArt.Mat("Magenta receptor", new Color(.85f,.05f,1), .8f);
            black = Unlit("Projected silhouette", new Color(.035f,.043f,.05f));
            rays = new GameObject("Visible spectrum light paths").transform;
            rays.SetParent(transform, false);
            reflectionGate = MakeGate("I • Reflection seal", new Vector3(0,0,39), 0, Color.white);
            redGate = MakeGate("Ruby side vault", new Vector3(-13,0,59), 90, Color.red);
            blueGate = MakeGate("Sapphire workshop", new Vector3(13,0,59), 90, Color.blue);
            prismGate = MakeGate("II • Magenta seal", new Vector3(0,0,73), 0, new Color(.85f,.1f,1));
            shadowGate = MakeGate("III • Silhouette seal", new Vector3(0,0,103), 0, new Color(.4f,.95f,1));
            finalGate = MakeGate("IV • White seal", new Vector3(0,0,135), 0, Color.white);
            BuildReflection();
            BuildPrism();
            BuildShadows();
            BuildConfluence();
            BuildInfrared();
            JournalLines = new[] {
                "REGION 01 / THE SUNKEN PRISM",
                "I  REFLECTION: sunlight → mirror A → mirror B → crystal. Clear B. Both brass dials bear III.",
                "II  PRISM: routes I / II reveal the ruby vault and sapphire workshop. Route III feeds the mixing gallery.",
                "Carry the workshop mirror to the empty blue plinth. Red dial II + blue dial IV converge on the same receptor.",
                "Red + blue light overlap at a surface to make magenta. Crossing beams in air does not mix them.",
                "III  SHADOWS: collect the bow, shaft and teeth; place each on its matching plinth. Rotate to cover the key guide.",
                "IV  CONFLUENCE: choose three different primary filters. Red + green + blue at one lens becomes white.",
                "BEYOND: take the visor beside the descent. Press V to follow heat through the darkness.",
                "E use / collect / place • Q / R turn • G reset current puzzle • V infrared visor"
            };
            AreaTitle = "THE SUNKEN PRISM";
            RefreshAll();
            Say("Let me guess. Giant door, dusty mirrors, a light puzzle. These ancients really lacked imagination.");
        }

        void Update()
        {
            foreach (Gate gate in gates) gate.Tick(Time.deltaTime);
            if (Time.unscaledTime > speechUntil) Subtitle = "";
            if (Camera.main != null)
            {
                float z = Camera.main.transform.position.z;
                AreaTitle = z < 13 ? "THE SUNKEN PRISM / THRESHOLD" : z < 45 ? "01 / HALL OF REFLECTION" : z < 79 ? "02 / CHROMATIC HALLS" : z < 109 ? "03 / SHADOW THEATER" : z < 135 ? "04 / THE CONFLUENCE" : "REGION 02 / THE INFRARED DESCENT";
            }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }
        public void Say(string line) { Subtitle = line; speechUntil = Time.unscaledTime + Mathf.Max(5f, line.Length * .058f); }
        static int Mod(int x, int n) { return (x % n + n) % n; }

        PrismInteractable Bind(GameObject obj, Func<string> prompt, Action use, Action<int> turn = null)
        {
            PrismInteractable control = obj.AddComponent<PrismInteractable>();
            control.Text = prompt; control.Use = use; control.Turn = turn;
            return control;
        }

        Transform Pedestal(string name, Vector3 position, float height = 1f)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(transform, false);
            DungeonArt.Cylinder(name+" footing", position + Vector3.up*.12f, new Vector3(1.3f,.12f,1.3f), stone, root.transform);
            DungeonArt.Cylinder(name+" shaft", position+Vector3.up*(height*.5f), new Vector3(.64f,height*.5f,.64f), stone, root.transform);
            DungeonArt.Cylinder(name+" brass collar", position+Vector3.up*height, new Vector3(1.12f,.09f,1.12f), metal, root.transform);
            return root.transform;
        }

        Transform Mirror(string name, Vector3 center, Transform parent)
        {
            GameObject pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.position = center;
            DungeonArt.Box(name+" brass frame", center, new Vector3(1.75f,2.15f,.15f), metal, pivot.transform);
            DungeonArt.Box(name+" reflective face", center+new Vector3(0,0,-.09f), new Vector3(1.47f,1.87f,.035f), glass, pivot.transform);
            DungeonArt.Box(name+" reverse face", center+new Vector3(0,0,.09f), new Vector3(1.47f,1.87f,.035f), glass, pivot.transform);
            return pivot.transform;
        }

        void BuildReflection()
        {
            DungeonArt.Crystal("Sunlight collector", new Vector3(-7,1.65f,21), new Vector3(.4f,.9f,.4f), pale, transform);
            DungeonArt.PointLight("Sunlight at source",new Vector3(-7,2,21),Color.white,2,7,transform);
            for (int i=0;i<2;i++)
            {
                int id=i;
                Transform plinth=Pedestal("Mirror "+(i==0?"A":"B"), new Vector3(0,0,mirrorCenters[i].z), .55f);
                mirrorPivots[i]=Mirror("Pivoting mirror "+(i==0?"A":"B"),mirrorCenters[i],plinth);
                mirrorControls.Add(Bind(plinth.gameObject,
                    () => id==1&&!debrisCleared ? "E  Clear vines from mirror B" : "Mirror "+(id==0?"A":"B")+" • station "+Roman(mirrorSteps[id]+1)+" / IV   [Q / R] turn",
                    () => { if(id==1&&!debrisCleared) { debrisCleared=true; debris.SetActive(false); Say("Priceless ancient artifact, reduced to a glorified flashlight. Don't worry, I'll take good care of you at the auction house."); RefreshAll(); } else Say("The inscription reads: 'Third station faces the path.' Q and R turn the brass dial."); },
                    direction => { if(id==1&&!debrisCleared) { Say("Hard to reflect anything through a century of weeds. E to clear it."); return; } mirrorSteps[id]=Mod(mirrorSteps[id]+direction,4); RefreshAll(); }));
                DungeonArt.Label("Mirror engraving "+i, new Vector3(0,.7f,mirrorCenters[i].z-1), (i==0?"A":"B")+" / III", .23f, new Color(1,.73f,.3f),transform);
            }
            mirrorSteps[0]=0; mirrorSteps[1]=1;
            debris=new GameObject("Dense removable vines"); debris.transform.SetParent(mirrorPivots[1],false);
            Material vines = DungeonArt.Mat("Dry vines",new Color(.22f,.29f,.13f));
            for(int i=0;i<7;i++)
            {
                GameObject vine=DungeonArt.Box("Vine "+i,mirrorCenters[1]+new Vector3((i-3)*.2f,0,-.15f),new Vector3(.12f,2.1f,.12f),vines,debris.transform);
                vine.transform.localRotation=Quaternion.Euler(0,0,(i%2==0?1:-1)*23);
            }
            DungeonArt.Crystal("Reflection receptor",new Vector3(7,1.65f,32),new Vector3(.5f,.8f,.5f),pale,transform);
            Pedestal("Receptor stand",new Vector3(7,0,32),.85f);
            DungeonArt.Label("Reflection instructions",new Vector3(-7,2.4f,31.5f),"REFLECT THE SUN\nA → B → SEAL\nCLEAR / TURN / ALIGN",.27f,new Color(.72f,.86f,.88f),transform);
        }

        void BuildPrism()
        {
            Transform stand=Pedestal("Prism navigator",new Vector3(0,0,56),.8f);
            prism=DungeonArt.Crystal("The primary prism",new Vector3(0,1.65f,56),new Vector3(1.05f,1.65f,1.05f),glass,stand);
            prismControl=Bind(stand.gameObject,()=>"Prism • route "+Roman(prismMode+1)+" / III   [Q / R] rotate",()=>Say("Three routes: ruby vault, sapphire workshop, then the mixing gallery. Q and R rotate the prism."),d=>{prismMode=Mod(prismMode+d,3);RefreshAll();});
            DungeonArt.Crystal("Prism sunlight inlet",new Vector3(0,3.8f,49),new Vector3(.4f,.7f,.4f),pale,transform);
            DungeonArt.Label("Prism legend",new Vector3(-6,2.5f,52),"I  RUBY VAULT\nII  SAPPHIRE WORKSHOP\nIII  CONVERGENCE",.28f,new Color(.79f,.87f,.86f),transform);
            relicPickup=DungeonArt.Crystal("Auctionable sun idol",new Vector3(-21,1.4f,59),new Vector3(.55f,.8f,.55f),gold,transform);
            Pedestal("Relic altar",new Vector3(-21,0,59),.9f);
            lootControl=Bind(relicPickup,()=>"E  Pocket the gilded sun idol",()=>{if(!redOpened||!relicPickup.activeSelf)return;Relics++;relicPickup.SetActive(false);Say("A sun idol. Finally, a security system that pays for itself.");RefreshObjective();});
            Transform partAltar=Pedestal("Workshop mirror stand",new Vector3(21,0,59),.65f);
            mirrorPickup=Mirror("Portable silver mirror",new Vector3(21,1.7f,59),partAltar).gameObject;
            partControl=Bind(mirrorPickup,()=>"E  Take the portable mirror",()=>{if(!blueOpened||hasMirror||mirrorInstalled)return;hasMirror=true;mirrorPickup.SetActive(false);Say("A portable mirror. Good. I was beginning to think the ancients had no travel accessories.");RefreshAll();});
            for(int i=0;i<2;i++)
            {
                int id=i;
                Vector3 pos=new Vector3(i==0?-6:6,0,65);
                Transform plinth=Pedestal(i==0?"Ruby mixing mirror":"Empty sapphire mixing plinth",pos,.6f);
                mixingPivots[i]=Mirror(i==0?"Red convergence mirror":"Installed blue convergence mirror",pos+Vector3.up*1.7f,plinth);
                mixControls[i]=Bind(plinth.gameObject,
                    ()=> id==1&&!mirrorInstalled ? hasMirror?"E  Install portable mirror":"Empty blue plinth • mirror missing" : (id==0?"Red":"Blue")+" mirror • station "+Roman(mixingSteps[id]+1)+" / IV   [Q / R] turn",
                    ()=>{if(id==1&&!mirrorInstalled){if(!hasMirror){Say("A suspiciously mirror-shaped gap. Try the sapphire workshop.");return;}hasMirror=false;mirrorInstalled=true;Say("There. Now red and blue need to strike the same crystal together.");RefreshAll();}else Say(id==0?"The ruby dial is inscribed II. Q / R to align.":"The sapphire dial is inscribed IV. Q / R to align.");},
                    d=>{if(id==1&&!mirrorInstalled)return;mixingSteps[id]=Mod(mixingSteps[id]+d,4);RefreshAll();});
                DungeonArt.Label("Mixing dial clue "+i,pos+new Vector3(0,.72f,-1),i==0?"R / II":"B / IV",.22f,i==0?new Color(1,.35f,.3f):new Color(.3f,.55f,1),transform);
            }
            installControl=mixControls[1];
            mixingPivots[1].gameObject.SetActive(false);
            DungeonArt.Crystal("Shared magenta receptor",new Vector3(0,2,72),new Vector3(.65f,1,.65f),violet,transform);
            DungeonArt.Label("Additive mixing inscription",new Vector3(0,3.4f,71.7f),"RED + BLUE\nONE RECEPTOR / ONE MOMENT",.25f,new Color(.96f,.6f,1),transform);
        }

        void BuildShadows()
        {
            Material panel=Unlit("Illuminated shadow screen",new Color(.65f,.7f,.64f));
            DungeonArt.Box("Shadow projection screen",new Vector3(6,3.1f,101.6f),new Vector3(10,4.4f,.25f),panel,transform);
            DungeonArt.Label("Shadow theater title",new Vector3(6,5.25f,101.42f),"ASSEMBLE THE KEY IN LIGHT",.24f,new Color(.87f,.91f,.75f),transform);
            DungeonArt.Crystal("Theater point source",shadowLamp,new Vector3(.35f,.5f,.35f),pale,transform);
            DungeonArt.PointLight("Theater projector glow",shadowLamp,Color.white,2,10,transform);
            Material targetMat=Unlit("Key outline guide",new Color(.83f,.79f,.48f));
            float projectionScale=(101.34f-shadowLamp.z)/(94-shadowLamp.z);
            for(int i=0;i<3;i++)
            {
                int id=i;
                Mesh shape=KeyMesh(i);
                GameObject guide=MeshObject("Key target "+i,shape,targetMat);
                guide.transform.position=shadowCenters[i]+new Vector3(0,0,.09f);
                Vector3 worldCenter=shadowLamp+(shadowCenters[i]-shadowLamp)/projectionScale;
                Transform plinth=Pedestal("Shadow plinth "+(i+1),new Vector3(worldCenter.x,0,94),worldCenter.y-.85f);
                GameObject piece=MeshObject("Projection artifact "+i,shape,gold);
                piece.transform.position=worldCenter;
                piece.transform.localScale=Vector3.one/projectionScale;
                piece.transform.SetParent(plinth,true);
                shadowPieces[i]=piece.transform;
                piece.SetActive(false);
                BoxCollider pieceCollider=piece.AddComponent<BoxCollider>();pieceCollider.size=new Vector3(i==1?3.9f:1.9f,1.9f,.22f);
                GameObject shadow=MeshObject("Perspective shadow "+i,new Mesh(),black);
                shadowProjections[i]=shadow.GetComponent<MeshFilter>();
                shadow.SetActive(false);
                plinthControls[i]=Bind(plinth.gameObject,
                    ()=>!shadowPlaced[id] ? "E  Place "+PieceName(id)+" on plinth "+(id+1) : PieceName(id)+" • orientation "+Roman(shadowSteps[id]+1)+" / IV   [Q / R] turn",
                    ()=>{if(!shadowPlaced[id]) {if(carrying!=id){Say(carrying<0?"Find the "+PieceName(id).ToLower()+" on the theater floor.":"That belongs on a different plinth. Match the numbered plaques.");return;}carrying=-1;shadowPlaced[id]=true;shadowPieces[id].gameObject.SetActive(true);Say("Ah, shadow puppets. The pinnacle of ancient security systems.");RefreshAll();} else Say("Turn the artifact until its shadow covers the gold key guide. Q / R to rotate.");},
                    d=>{if(!shadowPlaced[id])return;shadowSteps[id]=Mod(shadowSteps[id]+d,4);RefreshAll();});
                DungeonArt.Label("Shadow plinth number "+i,new Vector3(worldCenter.x,1,93.24f),(i+1)+" / "+PieceName(i).ToUpper(),.14f,new Color(1,.76f,.34f),transform);
                GameObject floor=MeshObject("Loose "+PieceName(i),shape,gold);
                floor.transform.position=new Vector3(-8+i*3,1.05f,86+(i%2)*2);
                floor.transform.localScale=Vector3.one*.48f;
                floor.transform.rotation=Quaternion.Euler(0,15*(i-1),28+30*i);
                BoxCollider floorCollider=floor.AddComponent<BoxCollider>();floorCollider.size=new Vector3(i==1?4:2,2,.35f);
                floorArtifacts[i]=floor;
                artifactControls[i]=Bind(floor,()=>"E  Carry artifact "+(id+1)+" / "+PieceName(id),()=>{if(carrying>=0){Say("One priceless artifact at a time. Put this one on its plinth first.");return;}if(shadowPlaced[id])return;carrying=id;floorArtifacts[id].SetActive(false);Say("Carrying the "+PieceName(id).ToLower()+". Find plinth "+(id+1)+" in front of the screen.");RefreshObjective();});
                Pedestal("Loose artifact rest "+i,new Vector3(-8+i*3,0,86+(i%2)*2),.45f);
            }
            DungeonArt.Label("Shadow control hint",new Vector3(-7,2.5f,96),"COLLECT / PLACE / ROTATE\nA KEY HAS THREE PARTS\nE TO CARRY • Q / R TO TURN",.25f,new Color(.7f,.86f,.86f),transform);
        }

        void BuildConfluence()
        {
            for(int i=0;i<3;i++)
            {
                int id=i;float x=(i-1)*8;
                Transform console=Pedestal("Filter console "+(i+1),new Vector3(x,0,119),1.25f);
                DungeonArt.Crystal("Filter lens "+i,new Vector3(x,1.8f,119),new Vector3(.65f,.8f,.3f),glass,console);
                filterControls[i]=Bind(console.gameObject,()=>"Channel "+(id+1)+" • "+FilterName(filters[id])+"   [Q / R] change filter",()=>Say("White sunlight in; choose a red, green or blue filter. The great lens needs all three primaries together."),d=>{filters[id]=Mod(filters[id]+d,4);RefreshAll();});
                DungeonArt.Crystal("White inlet "+i,new Vector3(x,3.7f,112),new Vector3(.38f,.8f,.38f),pale,transform);
                DungeonArt.Label("Confluence console number "+i,new Vector3(x,1,118.1f),"CHANNEL "+(i+1),.18f,new Color(.73f,.87f,.91f),transform);
            }
            DungeonArt.Crystal("Great additive lens",new Vector3(0,3,130),new Vector3(1.35f,2,1.35f),pale,transform);
            Pedestal("Great lens foundation",new Vector3(0,0,130),1.4f);
            DungeonArt.Label("Confluence inscription",new Vector3(0,4.65f,133.5f),"THREE PRIMARIES / ONE LIGHT\nRED + GREEN + BLUE = WHITE",.28f,new Color(.8f,.93f,1),transform);
        }

        void BuildInfrared()
        {
            visorPickup=DungeonArt.Box("Ancient infrared visor",new Vector3(1.7f,.65f,137),new Vector3(.72f,.25f,.3f),metal,transform);
            DungeonArt.Box("Visor amber lenses",new Vector3(1.7f,.67f,136.81f),new Vector3(.59f,.13f,.07f),gold,visorPickup.transform);
            Pedestal("Visor reliquary",new Vector3(1.7f,-.55f,137),1);
            visorControl=Bind(visorPickup,()=>"E  Take the thermal visor",()=>{if(VisorAvailable)return;VisorAvailable=true;visorPickup.SetActive(false);Say("Great. Who turned off the sun? Guess we're doing this the hard way. V activates the thermal visor.");RefreshObjective();});
            Material hot=DungeonArt.Mat("Infrared heat signature",new Color(1,.23f,.025f),2.5f);
            for(int i=0;i<17;i++)
            {
                float z=139+i*2;
                float y=z<159?-6f*(z-135)/24f:-6;
                GameObject marker=DungeonArt.Box("Thermal trace "+i,new Vector3((i%2==0?-.45f:.45f),y+.04f,z),new Vector3(.28f,.025f,.63f),hot,transform);
                Collider col=marker.GetComponent<Collider>();if(col!=null)DestroyImmediate(col);
                thermalObjects.Add(marker);marker.SetActive(false);
            }
            GameObject thermalLamp=DungeonArt.PointLight("Thermal vision illumination",new Vector3(0,-2.5f,166),new Color(1,.35f,.1f),2.7f,21,transform);
            thermalObjects.Add(thermalLamp);thermalLamp.SetActive(false);
            Transform terminal=Pedestal("Infrared region threshold",new Vector3(0,-6,174),1.1f);
            GameObject seal=DungeonArt.Crystal("Warm threshold seal",new Vector3(0,-4.1f,174),new Vector3(.75f,1.5f,.75f),hot,terminal);
            exitControl=Bind(terminal.gameObject,()=>VisorOn?"E  Enter the infrared region":"A hidden mechanism • thermal vision required",()=>{if(!confluenceSolved){Say("The white seal still holds. Return to the confluence.");return;}if(!VisorOn){Say("Nothing visible. The visor should see what I can't.");return;}Completed=true;Say("Visible spectrum, conquered. Let's see what the darkness is hiding.");RefreshObjective();});
            GameObject heatLabel=DungeonArt.Label("Infrared threshold label",new Vector3(0,-2.9f,174),"REGION 02\nINFRARED",.3f,new Color(1,.43f,.15f),transform);
            thermalObjects.Add(heatLabel);heatLabel.SetActive(false);
            thermalObjects.Add(seal);seal.SetActive(false);
        }

        void RefreshAll()
        {
            ClearRays();
            DrawReflection();
            DrawPrism();
            DrawShadows();
            DrawConfluence();
            RefreshObjective();
        }

        void DrawReflection()
        {
            for(int i=0;i<2;i++)mirrorPivots[i].rotation=Quaternion.Euler(0,45+45*mirrorSteps[i],0);
            Vector3 a=mirrorCenters[0],b=mirrorCenters[1];
            Ray("Sun → A",new Vector3(-7,1.65f,21),a,Color.white);
            Vector3 reflectedA=Vector3.Reflect(Vector3.right,mirrorPivots[0].forward).normalized;
            bool hitsB=Vector3.Dot(reflectedA,Vector3.forward)>.999f;
            Ray("A reflected ray",a,a+reflectedA*(hitsB?11:7),Color.white);
            bool complete=false;
            if(hitsB&&debrisCleared)
            {
                Vector3 reflectedB=Vector3.Reflect(Vector3.forward,mirrorPivots[1].forward).normalized;
                bool hitsSeal=Vector3.Dot(reflectedB,Vector3.right)>.999f;
                Ray("B reflected ray",b,b+reflectedB*7,Color.white);
                complete=hitsSeal;
            }
            if(complete&&!reflectionSolved){reflectionSolved=true;reflectionGate.Open();Say("Light goes in, door goes up. Astonishing. I should charge a consultation fee.");}
        }

        void DrawPrism()
        {
            Vector3 center=new Vector3(0,1.65f,56);
            prism.transform.localRotation=Quaternion.Euler(0,prismMode*120,0);
            Ray("Prism white sunlight",new Vector3(0,3.8f,49),center,Color.white,.075f);
            if(prismMode==0)
            {
                Ray("Ruby vault ray",center,new Vector3(-12.8f,2,59),spectrum[1]);
                Ray("Green spectrum",center,new Vector3(8,1.1f,61),spectrum[2]);
                Ray("Blue spectrum",center,new Vector3(8,1.1f,54),spectrum[3]);
                if(reflectionSolved&&!redOpened){redOpened=true;redGate.Open();Say("Ruby vault unlocked. An optional detour, with a very persuasive glint of gold.");}
            }
            else if(prismMode==1)
            {
                Ray("Sapphire workshop ray",center,new Vector3(12.8f,2,59),spectrum[3]);
                Ray("Red spectrum",center,new Vector3(-8,1.1f,54),spectrum[1]);
                Ray("Green spectrum",center,new Vector3(-8,1.1f,61),spectrum[2]);
                if(reflectionSolved&&!blueOpened){blueOpened=true;blueGate.Open();Say("The sapphire workshop. At last, somewhere these people kept spare parts.");}
            }
            else
            {
                Ray("Prism green waste ray",center,new Vector3(0,.25f,62),spectrum[2]);
                for(int i=0;i<2;i++)
                {
                    Vector3 m=new Vector3(i==0?-6:6,1.7f,65);
                    Color color=spectrum[i==0?1:3];
                    Ray("Primary → mixing mirror "+i,center,m,color);
                    if(i==1&&!mirrorInstalled)continue;
                    bool aligned=mixingSteps[i]==(i==0?1:3);
                    Vector3 target=aligned?new Vector3(0,2,72):m+Quaternion.Euler(0,90*(mixingSteps[i]-(i==0?1:3)),0)*(new Vector3(0,2,72)-m);
                    Ray("Mixing mirror → receptor "+i,m,target,color);
                }
                if(reflectionSolved&&mirrorInstalled&&mixingSteps[0]==1&&mixingSteps[1]==3&&!prismSolved){prismSolved=true;prismGate.Open();Say("Red and blue, together. Magenta. Apparently the door has expensive taste.");}
            }
            for(int i=0;i<2;i++)
            {
                Vector3 inDir=(mixingPivots[i].position-center).normalized;
                Vector3 outDir=(new Vector3(0,2,72)-mixingPivots[i].position).normalized;
                Vector3 normal=(inDir-outDir).normalized;
                Quaternion solvedRotation=Quaternion.LookRotation(normal,Vector3.up);
                mixingPivots[i].rotation=Quaternion.Euler(0,45*(mixingSteps[i]-(i==0?1:3)),0)*solvedRotation;
            }
            mixingPivots[1].gameObject.SetActive(mirrorInstalled);
        }

        void DrawShadows()
        {
            bool complete=true;
            for(int i=0;i<3;i++)
            {
                if(!shadowPlaced[i]){complete=false;continue;}
                Transform piece=shadowPieces[i];
                piece.rotation=Quaternion.Euler(0,0,90*(shadowSteps[i]-shadowTargets[i]));
                Mesh source=piece.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] points=source.vertices;
                for(int v=0;v<points.Length;v++)
                {
                    Vector3 world=piece.TransformPoint(points[v]);
                    float t=(101.34f-shadowLamp.z)/(world.z-shadowLamp.z);
                    points[v]=shadowLamp+(world-shadowLamp)*t;
                }
                Mesh projected=shadowProjections[i].sharedMesh;
                projected.Clear();projected.vertices=points;projected.triangles=source.triangles;projected.RecalculateNormals();projected.RecalculateBounds();
                shadowProjections[i].gameObject.SetActive(true);
                if(shadowSteps[i]!=shadowTargets[i])complete=false;
            }
            if(complete&&prismSolved&&!shadowSolved){shadowSolved=true;shadowGate.Open();Say("A key made of shadows. Brilliant. Impossible to sell, but brilliant.");}
        }

        void DrawConfluence()
        {
            bool red=false,green=false,blue=false;
            for(int i=0;i<3;i++)
            {
                Vector3 filter=new Vector3((i-1)*8,1.8f,119);
                Ray("White light into filter "+i,new Vector3((i-1)*8,3.7f,112),filter,Color.white);
                if(filters[i]>0)Ray("Filtered light channel "+i,filter,new Vector3(0,3,130),spectrum[filters[i]],.065f);
                if(filters[i]==1)red=true;if(filters[i]==2)green=true;if(filters[i]==3)blue=true;
            }
            if(red&&green&&blue)
            {
                Ray("White confluence",new Vector3(0,3,130),new Vector3(0,2.7f,134.8f),Color.white,.16f);
                if(shadowSolved&&!confluenceSolved){confluenceSolved=true;finalGate.Open();Say("All that color, just to get white again. Somewhere, an ancient architect is very pleased with himself.");}
            }
        }

        void RefreshObjective()
        {
            if(Completed)Objective="REGION 01 COMPLETE • The infrared journey awaits. Explore, or restart from the pause menu.";
            else if(!reflectionSolved)Objective=!debrisCleared?"Clear the vines from mirror B. Aim mirror A → B → the white receptor.":"Rotate both reflection mirrors to station III. Guide sunlight to the receptor.";
            else if(!blueOpened)Objective="Rotate the central prism: route I opens the ruby vault; route II opens the sapphire workshop.";
            else if(!mirrorInstalled)Objective=hasMirror?"Carry the mirror to the empty blue mixing plinth and press E to install it.":"Collect the portable mirror from the sapphire workshop. Ruby vault loot is optional.";
            else if(!prismSolved)Objective="Set prism route III. Red mirror II + blue mirror IV must illuminate the same magenta receptor.";
            else if(!shadowSolved)Objective=carrying>=0?"Carrying "+PieceName(carrying)+" • place it on plinth "+(carrying+1)+" with E.":"Collect and place all three artifacts. Q / R rotates their shadows to form the gold key guide.";
            else if(!confluenceSolved)Objective="At the confluence, choose a red, a green and a blue filter. Unite all three at the great lens.";
            else if(!VisorAvailable)Objective="The white seal is open. Collect the infrared visor beside the descending passage.";
            else if(!VisorOn)Objective="Visible light ends here. Press V to activate the thermal visor.";
            else Objective="Follow the warm footprints down into darkness. Use the thermal seal to enter Region 02.";
        }

        public void ToggleVisor()
        {
            if(!VisorAvailable){Say("No thermal visor yet. There should be one beyond the white seal.");return;}
            VisorOn=!VisorOn;
            foreach(GameObject thermal in thermalObjects)if(thermal!=null)thermal.SetActive(VisorOn);
            Say(VisorOn?"Thermal vision engaged. Heat leaves a trail.":"Back to visible light.");
            RefreshObjective();
        }

        public void ResetCurrentPuzzle()
        {
            float z=Camera.main==null?0:Camera.main.transform.position.z;
            if(z<45){mirrorSteps[0]=0;mirrorSteps[1]=1;Say("Reflection dials reset. Cleared debris stays cleared.");}
            else if(z<79){prismMode=0;mixingSteps[0]=0;mixingSteps[1]=0;Say("Prism and mixing dials reset. Collected treasures stay collected.");}
            else if(z<109){for(int i=0;i<3;i++)shadowSteps[i]=0;Say("Shadow orientations reset. Artifacts remain on their plinths.");}
            else{for(int i=0;i<3;i++)filters[i]=0;Say("Confluence filters closed. Q / R selects a primary color.");}
            RefreshAll();
        }

        void ClearRays()
        {
            foreach(GameObject ray in rayObjects)if(ray!=null){ray.SetActive(false);if(Application.isPlaying)Destroy(ray);else DestroyImmediate(ray);}
            rayObjects.Clear();
        }
        void Ray(string name,Vector3 a,Vector3 b,Color color,float width=.055f){rayObjects.Add(DungeonArt.Beam(name,a,b,color,rays,width));}
        static string Roman(int i){return i==1?"I":i==2?"II":i==3?"III":"IV";}
        static string PieceName(int i){return i==0?"Bow":i==1?"Shaft":"Teeth";}
        static string FilterName(int i){return i==0?"SHUT":i==1?"RED":i==2?"GREEN":"BLUE";}
        static Material Unlit(string name,Color color){Material mat=new Material(Shader.Find("Unlit/Color"));mat.name=name;mat.color=color;return mat;}

        GameObject MeshObject(string name,Mesh mesh,Material mat)
        {
            GameObject obj=new GameObject(name);obj.transform.SetParent(transform,false);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=mat;
            return obj;
        }
        Mesh KeyMesh(int part)
        {
            List<Vector3> verts=new List<Vector3>();List<int> triangles=new List<int>();
            if(part==0)
            {
                const int n=16;
                for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;verts.Add(new Vector3(Mathf.Cos(a)*.9f,Mathf.Sin(a)*.9f,0));verts.Add(new Vector3(Mathf.Cos(a)*.51f,Mathf.Sin(a)*.51f,0));}
                for(int i=0;i<n;i++){int a=i*2,b=((i+1)%n)*2;Quad(triangles,a,b,b+1,a+1);}
                // A small neck makes the bow's correct orientation visible instead of
                // secretly assigning a solution to a rotationally symmetric ring.
                Rect(verts,triangles,.66f,-.16f,1.16f,.16f);
            }
            else if(part==1){Rect(verts,triangles,-2.02f,-.135f,2.02f,.135f);Rect(verts,triangles,-1.7f,-.34f,-1.43f,.34f);}
            else{Rect(verts,triangles,-.62f,.215f,.62f,.485f);Rect(verts,triangles,-.61f,-.58f,-.3f,.36f);Rect(verts,triangles,.3f,-.58f,.61f,.36f);}
            Mesh mesh=new Mesh();mesh.name="Key "+PieceName(part)+" silhouette";mesh.SetVertices(verts);mesh.SetTriangles(triangles,0);
            Vector3[] normals=new Vector3[verts.Count];for(int i=0;i<normals.Length;i++)normals[i]=Vector3.back;mesh.normals=normals;
            mesh.RecalculateBounds();return mesh;
        }
        static void Rect(List<Vector3> verts,List<int> triangles,float x0,float y0,float x1,float y1){int n=verts.Count;verts.Add(new Vector3(x0,y0,0));verts.Add(new Vector3(x1,y0,0));verts.Add(new Vector3(x1,y1,0));verts.Add(new Vector3(x0,y1,0));Quad(triangles,n,n+1,n+2,n+3);}
        static void Quad(List<int> t,int a,int b,int c,int d){t.AddRange(new[]{a,b,c,a,c,d,c,b,a,d,c,a});}

        Gate MakeGate(string name,Vector3 floor,float yaw,Color color)
        {
            GameObject obj=new GameObject(name);obj.transform.SetParent(transform,false);obj.transform.position=floor;
            DungeonArt.Box("Sealed stone slab",floor+Vector3.up*2.25f,new Vector3(3.95f,4.5f,.48f),stone,obj.transform);
            Material trim=DungeonArt.Mat(name+" light",color,.8f);
            for(int i=-1;i<=1;i++)DungeonArt.Box("Seal light inlay",floor+new Vector3(i*1.05f,2.3f,-.265f),new Vector3(.065f,3.6f,.035f),trim,obj.transform);
            obj.transform.rotation=Quaternion.Euler(0,yaw,0);
            Gate gate=new Gate(obj.transform);gates.Add(gate);return gate;
        }
        sealed class Gate
        {
            readonly Transform root;readonly Vector3 closed;readonly Collider[] colliders;
            public bool IsOpen {get;private set;}
            public Gate(Transform value){root=value;closed=value.position;colliders=value.GetComponentsInChildren<Collider>();}
            public void Open(){IsOpen=true;}
            public void Tick(float dt){if(!IsOpen)return;root.position=Vector3.MoveTowards(root.position,closed+Vector3.up*4.8f,dt*2.2f);if(root.position.y>closed.y+2.7f)foreach(Collider col in colliders)if(col!=null)col.enabled=false;}
        }

        // Editor validation calls the same interaction objects used by the FPS controller.
        // This deliberately requires every step and verifies that incomplete mechanisms stay locked.
        public void RunSelfTest()
        {
            Require(!reflectionSolved&&!prismSolved&&!shadowSolved&&!confluenceSolved,"fresh puzzle state");
            mirrorControls[0].Rotate(2);Require(!reflectionSolved,"vines obstruct reflection");
            mirrorControls[1].Rotate(1);Require(mirrorSteps[1]==1,"obstructed mirror cannot rotate");
            mirrorControls[1].Interact();Require(debrisCleared&&!reflectionSolved,"clearing alone does not solve reflection");
            mirrorControls[1].Rotate(1);Require(reflectionSolved&&reflectionGate.IsOpen,"reflection complete");
            Require(redOpened&&redGate.IsOpen,"red prism route opens ruby vault");
            lootControl.Interact();Require(Relics==1,"optional relic collected");lootControl.Interact();Require(Relics==1,"relic not duplicated");
            prismControl.Rotate(1);Require(blueOpened&&blueGate.IsOpen,"blue prism route opens workshop");
            partControl.Interact();Require(hasMirror,"portable mirror collected");
            installControl.Interact();Require(mirrorInstalled&&!hasMirror,"mirror installed");
            prismControl.Rotate(1);mixControls[0].Rotate(1);Require(!prismSolved,"one primary is insufficient");
            mixControls[1].Rotate(-1);Require(prismSolved&&prismGate.IsOpen,"simultaneous red plus blue");
            for(int i=0;i<3;i++){artifactControls[i].Interact();Require(carrying==i,"artifact pickup "+i);plinthControls[i].Interact();Require(shadowPlaced[i]&&carrying<0,"artifact placement "+i);}
            Require(!shadowSolved,"unrotated shapes do not form key");
            for(int i=0;i<3;i++)plinthControls[i].Rotate(shadowTargets[i]);
            Require(shadowSolved&&shadowGate.IsOpen,"projected key assembled");
            filterControls[0].Rotate(1);filterControls[1].Rotate(1);filterControls[2].Rotate(1);Require(!confluenceSolved,"three red beams do not equal white");
            filterControls[1].Rotate(1);filterControls[2].Rotate(2);Require(confluenceSolved&&finalGate.IsOpen,"RGB confluence opens descent");
            exitControl.Interact();Require(!Completed,"infrared threshold requires visor");
            visorControl.Interact();Require(VisorAvailable,"visor collected");ToggleVisor();Require(VisorOn,"thermal vision enabled");
            exitControl.Interact();Require(Completed,"region transition completed");
            foreach(Gate gate in gates)gate.Tick(10);
            Debug.Log("SUNKEN_PRISM_SELF_TEST: PASS — reflection, debris, prism side vaults, optional loot, mirror installation, simultaneous mixing, perspective shadows, RGB confluence and infrared transition.");
        }
        static void Require(bool condition,string description){if(!condition)throw new InvalidOperationException("SUNKEN_PRISM_SELF_TEST FAILED: "+description);}
    }
}
