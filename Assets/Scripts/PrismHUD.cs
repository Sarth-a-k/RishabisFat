using UnityEngine;
using UnityEngine.SceneManagement;

namespace SunkenPrism
{
    /// <summary>Resolution-independent field journal, map, captions, and exploration interface.</summary>
    public sealed class PrismHUD : MonoBehaviour
    {
        public bool Visible = true;
        readonly Color ink = new Color(.035f, .055f, .068f, .96f);
        readonly Color slate = new Color(.075f, .105f, .123f, .97f);
        readonly Color cyan = new Color(.44f, .9f, .94f);
        readonly Color gold = new Color(.94f, .76f, .41f);
        readonly Color paper = new Color(.91f, .92f, .87f);
        readonly Color muted = new Color(.78f, .86f, .87f);
        GUIStyle small, body, heading, title, right, centered, button, italic;
        Texture2D pixel;
        bool initialized;
        Vector2 journalScroll;
        const float W = 1600f, H = 900f;

        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            pixel = new Texture2D(1, 1);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            small = Style(font, 15, muted);
            body = Style(font, 19, paper);
            heading = Style(font, 22, paper, FontStyle.Bold);
            title = Style(font, 42, paper, FontStyle.Bold);
            right = Style(font, 17, paper);
            right.alignment = TextAnchor.UpperRight;
            centered = Style(font, 18, paper);
            centered.alignment = TextAnchor.MiddleCenter;
            italic = Style(font, 21, paper, FontStyle.Italic);
            italic.alignment = TextAnchor.MiddleCenter;
            button = Style(font, 18, paper, FontStyle.Bold);
            button.alignment = TextAnchor.MiddleCenter;
            button.normal.background = pixel;
            button.hover.background = pixel;
            button.active.background = pixel;
            button.normal.textColor = paper;
            button.hover.textColor = Color.white;
            button.padding = new RectOffset(12, 12, 8, 8);
        }

        GUIStyle Style(Font font, int size, Color color, FontStyle weight = FontStyle.Normal)
        {
            return new GUIStyle { font = font, fontSize = size, fontStyle = weight,
                wordWrap = true, richText = true, normal = { textColor = color } };
        }

        void OnGUI()
        {
            if (!Visible || !Application.isPlaying) return;
            var player = ExplorerController.Instance;
            if (player == null) return;
            Initialize();
            Matrix4x4 old = GUI.matrix;
            float scale = Mathf.Min(Screen.width / W, Screen.height / H);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - W * scale) * .5f,
                (Screen.height - H * scale) * .5f, 0), Quaternion.identity, Vector3.one * scale);
            var game = PuzzleGame.Instance;
            DrawFrame(player, game);
            if (player.JournalOpen) DrawJournal(player, game);
            if (player.MapOpen) DrawMap(player, game);
            if (player.Paused) DrawPause(player, game);
            GUI.matrix = old;
            GUI.color = Color.white;
        }

        void Fill(Rect area, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, pixel);
            GUI.color = previous;
        }

        void Text(float x, float y, float width, float height, string value, GUIStyle style)
        {
            GUI.Label(new Rect(x, y, width, height), value ?? "", style);
        }

        void Diamond(float x, float y, float size, Color color)
        {
            Matrix4x4 previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(45, new Vector2(x, y));
            Fill(new Rect(x - size * .5f, y - size * .5f, size, size), color);
            GUI.matrix = previous;
        }

        string Room(float z)
        {
            if (z < 13f) return "THE SUNKEN THRESHOLD";
            if (z < 42f) return "01  /  THE HALL OF REFLECTIONS";
            if (z < 76f) return "02  /  THE CHROMATIC ENGINE";
            if (z < 106f) return "03  /  THE SHADOW THEATER";
            if (z < 139f) return "04  /  THE GREAT CONFLUENCE";
            if (z < 159f) return "THE DESCENT";
            return "REGION II  /  BEYOND THE VISIBLE";
        }

        void DrawFrame(ExplorerController player, PuzzleGame game)
        {
            if (game != null && game.VisorOn)
            {
                Fill(new Rect(0, 0, W, H), new Color(.1f, .7f, .85f, .045f));
                Fill(new Rect(0, 0, W, 3), cyan);
                Fill(new Rect(0, H - 3, W, 3), cyan);
                Text(50, 168, 330, 25, "THERMAL VISION  /  ACTIVE", small);
            }
            Fill(new Rect(30, 27, 510, 114), new Color(.018f, .032f, .042f, .77f));
            Fill(new Rect(30, 27, 3, 114), gold);
            Diamond(60, 55, 14, gold);
            Text(81, 43, 425, 25, "THE SUNKEN PRISM", heading);
            Text(51, 83, 465, 32, game != null && !string.IsNullOrEmpty(game.AreaTitle)
                ? game.AreaTitle.ToUpperInvariant() : Room(player.transform.position.z), small);
            Text(51, 113, 420, 23, "REGION 01    /    VISIBLE LIGHT", small);

            Fill(new Rect(1105, 27, 465, 114), new Color(.018f, .032f, .042f, .77f));
            Text(1129, 44, 418, 24, "CURRENT OBJECTIVE", small);
            Text(1129, 77, 418, 57, game != null ? game.Objective : "Enter the crystalline halls.", body);

            if (game != null && game.Completed)
            {
                Fill(new Rect(565, 29, 510, 71), new Color(.018f, .045f, .05f, .9f));
                Fill(new Rect(565, 29, 510, 2), gold);
                Text(580, 39, 480, 30, "REGION I  /  COMPLETE", centered);
                Text(580, 70, 480, 24, "The spectrum continues beyond what the eye can see.", centered);
            }

            if (!player.JournalOpen && !player.MapOpen && !player.Paused)
            {
                Color reticle = player.Focus != null ? gold : new Color(.9f, .96f, 1f, .75f);
                Fill(new Rect(797.5f, 447.5f, 5, 5), reticle);
                if (player.Focus != null)
                {
                    string prompt = player.Focus.Prompt;
                    float promptHeight = Mathf.Max(56, centered.CalcHeight(new GUIContent(prompt), 740) + 26);
                    Fill(new Rect(408, 615, 784, promptHeight), new Color(.02f, .04f, .05f, .91f));
                    Fill(new Rect(408, 615, 3, promptHeight), gold);
                    Text(431, 628, 738, promptHeight - 20, prompt, centered);
                }
                if (game != null && !string.IsNullOrEmpty(game.Subtitle))
                {
                    float linesHeight = Mathf.Max(60, italic.CalcHeight(new GUIContent(game.Subtitle), 990) + 26);
                    Fill(new Rect(270, 724 - linesHeight * .5f, 1060, linesHeight), new Color(.015f, .025f, .035f, .88f));
                    Text(305, 737 - linesHeight * .5f, 990, linesHeight - 26, game.Subtitle, italic);
                }
            }

            Fill(new Rect(30, 818, 1540, 55), new Color(.018f, .032f, .042f, .85f));
            Text(52, 836, 1370, 27, "WASD  Move     SPACE  Jump     SHIFT  Sprint     E  Interact     Q / R  Rotate     F  Lamp     V  Visor     TAB  Journal     M  Map", small);
            Text(1378, 836, 165, 27, "ESC  Pause", right);
            string inventory = string.Format("RELICS  {0:00}     /     LAMP  {1}", game != null ? game.Relics : 0, player.FlashlightOn ? "ON" : "OFF");
            Text(51, 781, 460, 26, inventory, small);
            if (player.IsCrouching) Text(1320, 781, 220, 26, "CROUCHING", right);
            else if (player.IsSprinting) Text(1320, 781, 220, 26, "SPRINTING", right);
            else if (game != null && game.VisorAvailable) Text(1270, 781, 275, 26, "THERMAL VISOR  /  READY", right);
        }

        void Modal(string eyebrow, string name, string subtitle)
        {
            Fill(new Rect(-800, -800, 3200, 2500), new Color(.004f, .013f, .022f, .82f));
            Fill(new Rect(155, 100, 1290, 700), ink);
            Fill(new Rect(155, 100, 1290, 3), gold);
            Text(203, 128, 1150, 25, eyebrow, small);
            Text(200, 166, 1130, 59, name, title);
            Text(202, 228, 1120, 44, subtitle, body);
            Fill(new Rect(202, 293, 1194, 1), new Color(.26f, .35f, .36f));
        }

        bool Button(Rect rect, string text)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(.16f, .25f, .28f);
            bool clicked = GUI.Button(rect, text, button);
            GUI.backgroundColor = previous;
            return clicked;
        }

        void DrawJournal(ExplorerController player, PuzzleGame game)
        {
            Modal("FIELD NOTES  /  ARTIFACT HUNTER", "Light is the lock.",
                "The ancients left instructions. Naturally, they made those a puzzle too.");
            Text(203, 315, 500, 35, "EXPLORER CONTROLS", heading);
            string[,] controls = {
                { "W A S D", "Move" }, { "MOUSE", "Look around" },
                { "SPACE", "Jump" }, { "LEFT SHIFT", "Sprint while moving forward" },
                { "C / CTRL", "Hold to crouch" }, { "E", "Use, clear debris, collect, confirm" },
                { "Q / R", "Rotate the mechanism you are looking at" },
                { "F", "Toggle your hand lamp" }, { "G", "Reset the current puzzle" },
                { "V", "Toggle the thermal visor when acquired" },
                { "M / TAB", "Map / field journal" }, { "ESC", "Pause and release the cursor" }
            };
            for (int i = 0; i < controls.GetLength(0); i++)
            {
                float y = 357 + i * 28;
                Text(204, y, 140, 28, controls[i, 0], small);
                Text(353, y - 2, 370, 30, controls[i, 1], body);
            }
            Fill(new Rect(761, 320, 1, 386), new Color(.2f, .3f, .32f));
            Text(801, 315, 570, 35, "RULES OF THE HALLS", heading);
            Rect scrollRect = new Rect(800, 354, 590, 356);
            string[] rules = game != null && game.JournalLines != null && game.JournalLines.Length > 0
                ? game.JournalLines : new[] {
                    "REFLECTION • Clear obstructed mirrors before turning them. Follow the light from one surface to the next.",
                    "COLOR • Route red and blue light into the same receptor to produce magenta. The green beam has another purpose.",
                    "PERSPECTIVE • Rotate the artifacts until their shadows complete the symbol on the wall.",
                    "FIELD EQUIPMENT • Your lamp helps you see. Only the ancient beam sources can power mechanisms."
                };
            float contentHeight = 0;
            foreach (var line in rules) contentHeight += body.CalcHeight(new GUIContent(line ?? ""), 548) + 24;
            journalScroll = GUI.BeginScrollView(scrollRect, journalScroll, new Rect(0, 0, 552, Mathf.Max(350, contentHeight)));
            float position = 0;
            foreach (var line in rules)
            {
                float height = body.CalcHeight(new GUIContent(line ?? ""), 548);
                Text(0, position, 548, height, line, body);
                position += height + 24;
            }
            GUI.EndScrollView();
            Text(203, 747, 840, 28, "AIM at a mechanism to see its available action.   •   Progress is kept for this play session.", small);
            if (Button(new Rect(1185, 736, 210, 42), "CLOSE  /  TAB")) player.CloseOverlays();
        }

        void DrawMap(ExplorerController player, PuzzleGame game)
        {
            Modal("SURVEY  /  REGION 01", "The Chromatic Halls", "A sequence of four optical locks, descending toward a world without sunlight.");
            // x spans -34 to +34; z spans 0 to 179. North points toward the deeper chambers.
            Rect map = new Rect(575, 316, 440, 420);
            Fill(new Rect(791, 333, 8, 382), new Color(.25f, .37f, .38f));
            MapRoom(map, -8, 0, 16, 13, "", muted);
            MapRoom(map, -15, 13, 30, 26, "01", gold);
            MapRoom(map, -17, 45, 34, 28, "02", new Color(.63f, .63f, .96f));
            MapRoom(map, -32, 54, 13, 15, "R", new Color(.95f, .36f, .38f));
            MapRoom(map, 19, 54, 13, 15, "B", new Color(.34f, .66f, 1f));
            Fill(MapRect(map, -21, 60, 42, 3), new Color(.26f, .37f, .41f));
            MapRoom(map, -17, 45, 34, 28, "02", new Color(.63f, .63f, .96f));
            MapRoom(map, -15, 79, 30, 24, "03", muted);
            MapRoom(map, -18, 109, 36, 26, "04", gold);
            MapRoom(map, -5, 139, 10, 20, "", muted);
            MapRoom(map, -13, 159, 26, 18, "IR", cyan);
            Vector2 dot = MapPoint(map, player.transform.position.x, player.transform.position.z);
            float pulse = .7f + Mathf.Sin(Time.unscaledTime * 4) * .3f;
            Diamond(dot.x, dot.y, 12, Color.Lerp(cyan, Color.white, pulse));
            Text(1027, 314, 110, 26, "N  ↑", small);
            Text(203, 321, 330, 40, "THE ROUTE", heading);
            MapLegend(203, 375, "01", "Hall of Reflections", "Clear. Turn. Redirect.", gold);
            MapLegend(203, 450, "02", "Chromatic Engine", "Split white light into color.", new Color(.63f, .63f, .96f));
            MapLegend(203, 525, "03", "Shadow Theater", "Perspective becomes the key.", muted);
            MapLegend(203, 600, "04", "Great Confluence", "Bring the spectrum together.", gold);
            Text(1100, 343, 270, 32, "SIDE CHAMBERS", heading);
            Text(1100, 395, 265, 60, "R  /  Reliquary\nA treasure for your trouble.", body);
            Text(1100, 483, 265, 70, "B  /  Mirror workshop\nA tool for the path ahead.", body);
            Text(1100, 585, 265, 90, "IR  /  The descent\nVisible light ends here.\nHeat leaves a trace.", body);
            Diamond(213, 749, 10, cyan);
            Text(233, 738, 850, 32, "YOUR POSITION    •    Schematic map; room proportions simplified", small);
            if (Button(new Rect(1185, 736, 210, 42), "CLOSE  /  M")) player.CloseOverlays();
        }

        void MapLegend(float x, float y, string number, string name, string description, Color color)
        {
            Fill(new Rect(x, y, 40, 40), color * .5f);
            Text(x, y, 40, 40, number, centered);
            Text(x + 54, y - 1, 290, 32, name, body);
            Text(x + 54, y + 29, 305, 33, description, small);
        }

        Vector2 MapPoint(Rect rect, float x, float z)
        {
            return new Vector2(rect.x + (x + 34f) / 68f * rect.width,
                rect.yMax - Mathf.Clamp(z, 0, 179) / 179f * rect.height);
        }

        Rect MapRect(Rect rect, float x, float z, float width, float depth)
        {
            Vector2 p = MapPoint(rect, x, z + depth);
            return new Rect(p.x, p.y, width / 68f * rect.width, depth / 179f * rect.height);
        }

        void MapRoom(Rect map, float x, float z, float width, float depth, string label, Color color)
        {
            Rect rect = MapRect(map, x, z, width, depth);
            Fill(rect, color * .43f);
            Fill(new Rect(rect.x, rect.y, rect.width, 2), color);
            Fill(new Rect(rect.x, rect.yMax - 2, rect.width, 2), color);
            Fill(new Rect(rect.x, rect.y, 2, rect.height), color);
            Fill(new Rect(rect.xMax - 2, rect.y, 2, rect.height), color);
            GUI.Label(rect, label, centered);
        }

        void DrawPause(ExplorerController player, PuzzleGame game)
        {
            Fill(new Rect(-800, -800, 3200, 2500), new Color(.004f, .013f, .022f, .8f));
            Fill(new Rect(505, 215, 590, 490), ink);
            Fill(new Rect(505, 215, 590, 3), gold);
            Text(550, 252, 500, 40, "THE WORLD CAN WAIT.", heading);
            Text(550, 313, 500, 60, "Even priceless artifacts deserve an occasional break.", body);
            if (Button(new Rect(550, 395, 500, 52), "RESUME EXPLORATION")) player.SetPaused(false);
            if (Button(new Rect(550, 462, 500, 52), "RESET CURRENT PUZZLE"))
            {
                if (game != null) game.ResetCurrentPuzzle();
                player.SetPaused(false);
            }
            if (Button(new Rect(550, 529, 500, 52), "RESTART EXPEDITION"))
            {
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Scene activeScene = SceneManager.GetActiveScene();
                if (activeScene.buildIndex >= 0) SceneManager.LoadScene(activeScene.buildIndex);
                else SceneManager.LoadScene(activeScene.name);
                GUIUtility.ExitGUI();
            }
            Text(550, 607, 500, 28, "Restart begins a new expedition and clears current progress.", small);
            Text(550, 654, 500, 38, "ESC to return  •  Click the game to capture your mouse", small);
        }

        void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
        }
    }
}
