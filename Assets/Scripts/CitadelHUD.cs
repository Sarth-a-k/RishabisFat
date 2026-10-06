using UnityEngine;
using UnityEngine.SceneManagement;

namespace SunkenPrism
{
    /// <summary>Exploration, inventory and optical-trial guidance with a faithful four-region map.</summary>
    public sealed class CitadelHUD : MonoBehaviour
    {
        public bool Visible = true;
        const float W = 1600, H = 900;
        readonly Color cream = new Color(.9f,.87f,.79f);
        readonly Color muted = new Color(.64f,.66f,.66f);
        readonly Color gold = new Color(.76f,.63f,.37f);
        readonly Color ink = new Color(.021f,.025f,.032f,.96f);
        readonly Color glass = new Color(.015f,.02f,.027f,.76f);
        readonly Color route = new Color(.3f,.32f,.33f);
        Texture2D pixel;
        GUIStyle micro, small, body, heading, title, center, caption, button, right;
        float guiScale, offsetX, offsetY;

        void Initialize()
        {
            if (pixel != null) return;
            pixel = new Texture2D(1,1);
            pixel.SetPixel(0,0,Color.white);
            pixel.Apply();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            micro = Style(font,13,muted);
            small = Style(font,16,muted);
            body = Style(font,20,cream);
            heading = Style(font,22,cream,FontStyle.Bold);
            title = Style(font,39,cream,FontStyle.Bold);
            center = Style(font,18,cream);
            center.alignment = TextAnchor.MiddleCenter;
            caption = Style(font,21,cream,FontStyle.Italic);
            caption.alignment = TextAnchor.MiddleCenter;
            right = Style(font,16,muted);
            right.alignment = TextAnchor.UpperRight;
            button = Style(font,18,cream,FontStyle.Bold);
            button.alignment = TextAnchor.MiddleCenter;
            button.normal.background = pixel;
            button.hover.background = pixel;
            button.active.background = pixel;
            button.hover.textColor = Color.white;
        }

        static GUIStyle Style(Font font, int size, Color color, FontStyle weight = FontStyle.Normal)
        {
            return new GUIStyle { font = font, fontSize = size, fontStyle = weight,
                richText = false, wordWrap = true, normal = { textColor = color } };
        }

        void OnGUI()
        {
            if (!Visible || !Application.isPlaying) return;
            ExplorerController player = ExplorerController.Instance;
            CitadelDirector game = CitadelDirector.Instance;
            if (player == null || game == null) return;
            Initialize();
            Matrix4x4 oldMatrix = GUI.matrix;
            guiScale = Mathf.Min(Screen.width / W, Screen.height / H);
            offsetX = (Screen.width - W * guiScale) * .5f;
            offsetY = (Screen.height - H * guiScale) * .5f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX,offsetY,0),Quaternion.identity,Vector3.one * guiScale);
            DrawFrame(player,game);
            if (player.MapOpen) DrawMap(player,game);
            if (player.JournalOpen) DrawJournal(player,game);
            if (player.Paused) DrawPause(player,game);
            GUI.matrix = oldMatrix;
            GUI.color = Color.white;
        }

        void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect,pixel);
            GUI.color = old;
        }

        void Text(float x,float y,float width,float height,string text,GUIStyle style)
        {
            GUI.Label(new Rect(x,y,width,height),text ?? "",style);
        }

        void Diamond(Vector2 point,float size,Color color)
        {
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45,point);
            Fill(new Rect(point.x-size*.5f,point.y-size*.5f,size,size),color);
            GUI.matrix = matrix;
        }

        void DrawFrame(ExplorerController player,CitadelDirector game)
        {
            bool exploring = !player.MapOpen && !player.JournalOpen && !player.Paused;
            LunarPrismPuzzle lunar = LunarPrismPuzzle.Instance;
            bool inTrial = lunar != null && CitadelDirector.RegionAt(player.transform.position) == 0;
            if (game.VisorOn && exploring) DrawThermalSurvey(player,game);
            Fill(new Rect(32,29,520,97),glass);
            Fill(new Rect(32,29,2,97),gold);
            Text(54,43,470,28,"THE FOURFOLD CITADEL",heading);
            Text(54,87,474,30,game.RegionName,small);
            Fill(new Rect(1180,29,388,97),glass);
            Text(1201,43,347,27,inTrial ? "THE LUNAR SEAL" : "REGIONAL SURVEY",small);
            Text(1201,80,347,34,inTrial
                ? string.Format("{0} / 3 mirrors     ·     {1} / 3 seals",lunar.PlacedMirrorCount,lunar.ActivatedSymbolCount)
                : string.Format("{0} / 4 halls     ·     {1} / 4 sigils",game.VisitedCount,game.AttunedCount),body);
            if (exploring)
            {
                if (inTrial) DrawTrialState(lunar);
                if (lunar != null) DrawInventory(lunar);
                float yaw = player.transform.eulerAngles.y;
                string[] compass = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
                Text(740,35,120,26,compass[Mathf.RoundToInt(yaw/45f)%8],center);
                Fill(new Rect(780,67,40,1),new Color(.76f,.63f,.37f,.5f));
                Color reticle = player.Focus != null ? gold : new Color(.88f,.9f,.9f,.6f);
                Fill(new Rect(798,448,4,4),reticle);
                if (player.Focus != null)
                {
                    float height = Mathf.Max(64,center.CalcHeight(new GUIContent(player.Focus.Prompt),730)+24);
                    Fill(new Rect(410,610,780,height),glass);
                    Fill(new Rect(410,610,2,height),gold);
                    Text(433,622,734,height-20,player.Focus.Prompt,center);
                }
                if (!string.IsNullOrEmpty(game.Subtitle))
                {
                    float height = Mathf.Max(64,caption.CalcHeight(new GUIContent(game.Subtitle),1000)+24);
                    Fill(new Rect(275,738-height*.5f,1050,height),glass);
                    Text(300,750-height*.5f,1000,height-24,game.Subtitle,caption);
                }
            }
            string objective = lunar == null
                ? "Find the four luminous survey sigils. Aim and press E to attune."
                : inTrial ? lunar.Objective
                : lunar.Complete ? "Continue east: Ember Crypt → Violet Sanctum → Eclipse Keep."
                : "Enter the Sunken Prism and restore its three moon seals to unlock the Ember Crypt.";
            Text(53,778,1060,40,objective,small);
            Text(1160,784,386,25,player.IsCrouching ? "CROUCHING" : player.IsSprinting ? "SPRINTING" : player.FlashlightOn ? "HAND LAMP ON" : "HAND LAMP  /  F",right);
            Fill(new Rect(32,825,1536,47),glass);
            Text(54,839,1280,27,"WASD Move   SPACE Jump   SHIFT Sprint   E Interact   Q / R Rotate   1 Lighter   HOLD LMB Ignite   F Lamp   M Map   TAB Help",small);
            Text(1390,839,154,27,"ESC  Pause",right);
        }

        void DrawTrialState(LunarPrismPuzzle lunar)
        {
            Fill(new Rect(32,139,520,94),glass);
            Text(54,151,474,25,lunar.Complete ? "THREE SEALS RESTORED / EMBER CRYPT OPEN"
                : "TORCH → RECTANGLE → OVAL → CIRCLE → PRISM",micro);
            Text(54,184,474,31,lunar.Powered ? "Prism receiving light  ·  Q cycles moon phases"
                : string.Format("{0}  ·  {1} / 3 mirrors aligned",lunar.TorchLit ? "TORCH LIT" : "TORCH UNLIT",lunar.AlignedMirrorCount),small);
            if (lunar.IgnitionProgress01 > 0f && !lunar.TorchLit)
            {
                Fill(new Rect(600,688,400,38),glass);
                Fill(new Rect(612,715,376,3),new Color(.2f,.21f,.22f));
                Fill(new Rect(612,715,376*Mathf.Clamp01(lunar.IgnitionProgress01),3),gold);
                Text(612,689,376,24,"IGNITING / KEEP HOLDING LEFT CLICK",micro);
            }
        }

        void DrawInventory(LunarPrismPuzzle lunar)
        {
            Fill(new Rect(1180,139,388,70),glass);
            Text(1201,149,347,22,"INVENTORY / HAND",micro);
            Text(1201,176,347,29,!string.IsNullOrEmpty(lunar.HeldMirrorName) ? "Carrying " + lunar.HeldMirrorName
                : lunar.LighterEquipped ? "Lighter equipped  ·  1 to stow" : "1  Equip lighter",small);
        }

        void DrawThermalSurvey(ExplorerController player,CitadelDirector game)
        {
            Fill(new Rect(0,0,W,H),new Color(.76f,.21f,.04f,.07f));
            Fill(new Rect(32,140,350,29),glass);
            Text(44,145,326,26,"THERMAL SURVEY / BEACON SIGNATURES",micro);
            for (int i = 0; i < 4; i++)
            {
                Vector3 screen = player.View.WorldToScreenPoint(game.SocketPositions[i]);
                if (screen.z <= 0) continue;
                float x = (screen.x-offsetX)/guiScale;
                float y = (Screen.height-screen.y-offsetY)/guiScale;
                if (x < 100 || x > W-100 || y < 180 || y > 710) continue;
                Color heat = game.Attuned[i] ? gold : new Color(1f,.42f,.17f,.9f);
                Diamond(new Vector2(x,y),9,heat);
                float distance = Vector3.Distance(player.transform.position,game.SocketPositions[i]);
                Fill(new Rect(x-86,y+17,172,30),glass);
                Text(x-83,y+20,166,26,"SIGIL " + (i+1) + "  ·  " + Mathf.RoundToInt(distance) + " m",center);
            }
        }

        void Modal(string eyebrow,string name,string subtitle)
        {
            Fill(new Rect(-1600,-900,4800,2700),new Color(.005f,.009f,.014f,.82f));
            Fill(new Rect(75,55,1450,790),ink);
            Fill(new Rect(75,55,1450,2),gold);
            Text(117,82,1320,25,eyebrow,small);
            Text(113,119,1330,55,name,title);
            Text(117,184,1320,35,subtitle,body);
            Fill(new Rect(116,228,1368,1),new Color(.2f,.22f,.23f));
        }

        bool Button(Rect rect,string label)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = new Color(.15f,.16f,.17f);
            bool pressed = GUI.Button(rect,label,button);
            GUI.backgroundColor = old;
            return pressed;
        }

        void DrawMap(ExplorerController player,CitadelDirector game)
        {
            Modal("CARTOGRAPHY / THE FOUR REGIONS","The Fourfold Citadel","Nexus → Sunken Prism → Ember Crypt → Violet Sanctum → Eclipse Keep. The route runs east; north is up.");
            Rect map = new Rect(118,264,1363,290);
            // One shared layout supplies the scene builder, region detection and this equal-scale plan.
            for (int passage = 0; passage < 4; passage++)
            {
                Vector3 a = CitadelLayout.PassageStart(passage), b = CitadelLayout.PassageEnd(passage);
                MapRoute(map,new Vector2(a.x,a.z),new Vector2(b.x,b.z),CitadelLayout.PassageWidth);
                Vector2 arrow = MapPoint(map,new Vector2((a.x+b.x)*.5f,0));
                Line(arrow+new Vector2(-5,-5),arrow+new Vector2(2,0),1.5f,gold);
                Line(arrow+new Vector2(-5,5),arrow+new Vector2(2,0),1.5f,gold);
            }
            string[] labels = { "NEXUS", "I", "II", "III", "IV" };
            for (int room = 0; room < CitadelLayout.Centers.Length; room++)
            {
                Vector3 c = CitadelLayout.Centers[room];
                Vector2 size = CitadelLayout.Sizes[room];
                Color color = room == 0 ? gold : CitadelDirector.RegionColors[room-1];
                if (room == 3)
                {
                    DrawOctagon(map,new Vector2(c.x,c.z),size.x*.5f,color);
                    Vector2 label = MapPoint(map,new Vector2(c.x,c.z+size.y*.5f));
                    Text(label.x-35,label.y+5,70,28,labels[room],center);
                }
                else MapRoom(map,c.x-size.x*.5f,c.z-size.y*.5f,size.x,size.y,labels[room],color);
            }
            for (int i=0;i<4;i++)
            {
                Vector2 point = MapPoint(map,new Vector2(game.SocketPositions[i].x,game.SocketPositions[i].z));
                Diamond(point,game.Attuned[i] ? 11 : 7,game.Attuned[i] ? gold : CitadelDirector.RegionColors[i]);
            }
            Vector2 location = MapPoint(map,new Vector2(player.transform.position.x,player.transform.position.z));
            Diamond(location,13,Color.white);
            float angle = player.transform.eulerAngles.y * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Sin(angle),-Mathf.Cos(angle));
            Line(location+direction*9,location+direction*23,2,Color.white);
            Text(1425,256,55,30,"N ↑",small);
            Vector2 spawn = MapPoint(map,Vector2.zero);
            Text(spawn.x-53,spawn.y+57,110,48,"SPAWN\n21.6 × 21.6 m",micro);
            Vector3 gatePosition = CitadelLayout.PassageEnd(1);
            Vector2 gate = MapPoint(map,new Vector2(gatePosition.x,gatePosition.z));
            bool gateOpen = LunarPrismPuzzle.Instance != null && LunarPrismPuzzle.Instance.Complete;
            if (!gateOpen) Line(gate+new Vector2(0,-18),gate+new Vector2(0,18),3,CitadelDirector.RegionColors[1]);
            Text(118,555,1362,26,"◆  YOU     ◇  REGIONAL SIGIL     →  ROUTE     |  MOON-SEAL GATE     ·     FOUR CONNECTING PASSAGES",micro);
            Fill(new Rect(118,590,1363,1),new Color(.2f,.22f,.23f));
            string[] dimensions = { "48.1 × 36.4 m  /  FLOOR 0 m", "46.5 × 30 m  /  FLOOR −2 m", "45 m SPAN  /  FLOOR 0 m", "49.5 × 48 m  /  FLOOR +2 m" };
            string[] notes = {
                "Visible light · Align the mirrors and restore the three moon seals to open the eastern gate.",
                "Infrared · A sunken burial hall. Attune its survey sigil, then continue east to the sanctum.",
                "Ultraviolet · An octagonal ritual court. Its eastern passage leads to Eclipse Keep.",
                "Eclipse · An elevated throne hall at the end of the expedition."
            };
            for (int i=0;i<4;i++)
            {
                float x = 118+i*345;
                Color color = CitadelDirector.RegionColors[i];
                Diamond(new Vector2(x+5,619),9,color);
                Text(x+20,605,305,31,CitadelDirector.RegionNames[i],body);
                Text(x,644,322,22,dimensions[i],micro);
                Text(x,676,322,67,notes[i],small);
                Text(x,750,320,25,game.Attuned[i] ? "ATTUNED" : game.Visited[i] ? "EXPLORED" : "UNCHARTED",micro);
            }
            Text(118,795,1060,29,gateOpen ? "THE MOON-SEAL GATE IS OPEN / CONTINUE EAST" : "THE EMBER CRYPT GATE REQUIRES ALL THREE MOON SEALS",small);
            if (Button(new Rect(1265,789,214,36),"CLOSE / M")) player.CloseOverlays();
        }

        float MapScale(Rect map)
        {
            return Mathf.Min(map.width/288f,map.height/60f);
        }

        Vector2 MapPoint(Rect map,Vector2 world)
        {
            float scale = MapScale(map);
            return new Vector2(map.x+(world.x+18)*scale,map.center.y-world.y*scale);
        }

        void MapRoom(Rect map,float x,float z,float width,float depth,string label,Color color)
        {
            Vector2 topLeft = MapPoint(map,new Vector2(x,z+depth));
            float scale = MapScale(map);
            Rect rect = new Rect(topLeft.x,topLeft.y,width*scale,depth*scale);
            Fill(rect,new Color(color.r*.19f,color.g*.19f,color.b*.19f,1));
            Border(rect,1.3f,color);
            GUI.Label(new Rect(rect.x,rect.y+5,rect.width,28),label,center);
        }

        void Border(Rect rect,float width,Color color)
        {
            Fill(new Rect(rect.x,rect.y,rect.width,width),color);
            Fill(new Rect(rect.x,rect.yMax-width,rect.width,width),color);
            Fill(new Rect(rect.x,rect.y,width,rect.height),color);
            Fill(new Rect(rect.xMax-width,rect.y,width,rect.height),color);
        }

        void MapRoute(Rect map,Vector2 a,Vector2 b,float width)
        {
            Line(MapPoint(map,a),MapPoint(map,b),width*MapScale(map),route);
        }

        void Line(Vector2 a,Vector2 b,float width,Color color)
        {
            Matrix4x4 matrix = GUI.matrix;
            Vector2 delta = b-a;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);
            Fill(new Rect(a.x,a.y-width*.5f,delta.magnitude,width),color);
            GUI.matrix = matrix;
        }

        void DrawOctagon(Rect map,Vector2 centerPoint,float radius,Color color)
        {
            // The original proportions retain the 6.75 m clipped corners at a 45 m span.
            Vector2[] points = new Vector2[8];
            Vector2[] corners = {
                new Vector2(-21,-30),new Vector2(21,-30),new Vector2(30,-21),new Vector2(30,21),
                new Vector2(21,30),new Vector2(-21,30),new Vector2(-30,21),new Vector2(-30,-21)
            };
            for (int i=0;i<8;i++) points[i] = MapPoint(map,centerPoint+corners[i]*(radius/30f));
            float minY=points[0].y,maxY=points[0].y;
            foreach(var point in points) { minY=Mathf.Min(minY,point.y); maxY=Mathf.Max(maxY,point.y); }
            for(float y=minY;y<maxY;y+=2)
            {
                float left=float.MaxValue,rightEdge=float.MinValue;
                for(int i=0;i<8;i++)
                {
                    Vector2 a=points[i],b=points[(i+1)%8];
                    if ((a.y<=y && b.y>y)||(b.y<=y && a.y>y))
                    {
                        float x=Mathf.Lerp(a.x,b.x,(y-a.y)/(b.y-a.y));
                        left=Mathf.Min(left,x);rightEdge=Mathf.Max(rightEdge,x);
                    }
                }
                if (rightEdge>left) Fill(new Rect(left,y,rightEdge-left,2),new Color(color.r*.19f,color.g*.19f,color.b*.19f,1));
            }
            for(int i=0;i<8;i++) Line(points[i],points[(i+1)%8],1.3f,color);
        }

        void DrawJournal(ExplorerController player,CitadelDirector game)
        {
            Modal("FIELD JOURNAL / ARTIFACT HUNTER","The light remembers its path.","Restore the Sunken Prism's moon seals to open the Ember Crypt. The remaining halls can be explored.");
            Text(117,253,635,34,"EXPLORER CONTROLS",heading);
            string[,] controls = {
                {"W A S D / ARROWS","Walk"},{"MOUSE","Look around"},{"SPACE","Jump"},
                {"LEFT SHIFT","Sprint forward"},{"C / LEFT CTRL","Hold to crouch"},
                {"1 / HOLD LEFT CLICK","Equip lighter / ignite the aimed torch"},
                {"E","Pick up or place a matching mirror"},
                {"Q / R","Rotate a mirror; Q cycles the prism"},
                {"F / V","Hand lamp / acquired thermal visor"},
                {"G","Reset this trial while in the Sunken Prism"},
                {"M / TAB","Map / field journal"},{"ESC","Pause, or close the current overlay"}
            };
            for(int i=0;i<controls.GetLength(0);i++)
            {
                float y=306+i*36;
                Text(118,y,202,32,controls[i,0],small);
                Text(321,y-2,435,34,controls[i,1],body);
            }
            Fill(new Rect(791,256,1,505),new Color(.2f,.22f,.23f));
            Text(830,253,602,34,"EXPEDITION NOTES",heading);
            Text(830,306,600,77,"Equip the lighter with 1. Aim at the wooden puzzle torch and hold the left mouse button for 2.5 seconds without looking away.",body);
            Text(830,401,600,96,"Carry each mirror with E to its matching shadow. Rotate with Q or R until the white beam travels rectangle → oval → circle → prism.",body);
            Text(830,514,600,97,"Once the prism glows, press Q at it: green crescent, blue half moon, then red full moon. The three engraved seals retain their colors.",body);
            Text(830,628,600,100,"All three seals open the eastern gate to Ember Crypt. Attune its sigil with E to acquire the thermal visor, then continue to Violet Sanctum and finally Eclipse Keep.",body);
            Text(118,775,1030,34,"Progress lasts for this play session. G elsewhere resets survey sigils; restarting clears the trial.",small);
            if(Button(new Rect(1265,789,214,36),"CLOSE / TAB")) player.CloseOverlays();
        }

        void DrawPause(ExplorerController player,CitadelDirector game)
        {
            Fill(new Rect(-1600,-900,4800,2700),new Color(.005f,.009f,.014f,.81f));
            Fill(new Rect(460,128,680,645),ink);
            Fill(new Rect(460,128,680,2),gold);
            Text(510,166,581,45,"THE WORLD CAN WAIT.",heading);
            Text(510,222,578,61,"An abandoned kingdom. Four regions.\nTime enough to examine every corner.",body);
            if(Button(new Rect(510,319,580,53),"RESUME EXPLORATION")) player.SetPaused(false);
            bool canResetTrial = LunarPrismPuzzle.Instance != null && CitadelDirector.RegionAt(player.transform.position) == 0;
            if(Button(new Rect(510,389,580,53),canResetTrial ? "RESET SUNKEN PRISM TRIAL" : "RESET REGIONAL SIGILS"))
            {
                if (canResetTrial) LunarPrismPuzzle.Instance.ResetPuzzle();
                else game.ResetSurvey();
                player.SetPaused(false);
            }
            if(Button(new Rect(510,459,580,53),"RESTART AT THE NEXUS"))
                LoadScene(SceneManager.GetActiveScene().name);
            if(Button(new Rect(510,529,580,53),"OPEN ORIGINAL OPTICAL TRIAL"))
                LoadScene("SunkenPrism");
            Text(511,604,580,71,canResetTrial
                ? "Resetting this trial returns its mirrors, extinguishes the torch and closes the Ember gate. Changing scenes clears all progress."
                : "Resetting regional sigils preserves the moon trial. The original optical trial opens the earlier prototype and clears this expedition.",small);
            Text(511,712,580,31,"ESC to resume   ·   Click the game to capture your mouse",small);
        }

        static void LoadScene(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                if (CitadelDirector.Instance != null) CitadelDirector.Instance.Say("That scene is not included in this build. Open it from the Unity project's Scenes folder.");
                return;
            }
            Time.timeScale=1;
            Cursor.lockState=CursorLockMode.None;
            Cursor.visible=true;
            SceneManager.LoadScene(sceneName);
            GUIUtility.ExitGUI();
        }

        void OnDestroy()
        {
            if(pixel!=null) Destroy(pixel);
        }
    }
}
