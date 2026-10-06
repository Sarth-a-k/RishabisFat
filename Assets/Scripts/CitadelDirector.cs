using UnityEngine;

namespace SunkenPrism
{
    /// <summary>Regional exploration with the Sunken Prism's playable lunar trial.</summary>
    public sealed class CitadelDirector : MonoBehaviour
    {
        public static CitadelDirector Instance { get; private set; }
        public bool[] Attuned { get; private set; } = new bool[4];
        public bool[] Visited { get; private set; } = new bool[4];
        public string Subtitle { get; private set; }
        public string RegionName { get; private set; } = "THE NEXUS";
        public bool VisorOn { get; private set; }
        public bool VisorAvailable { get { return Attuned[1]; } }
        public int RegionIndex { get; private set; } = -1;
        public int AttunedCount { get { return Count(Attuned); } }
        public int VisitedCount { get { return Count(Visited); } }
        public readonly Vector3[] SocketPositions = {
            CitadelLayout.Map(1, new Vector3(-80, 1.9f, 22)), CitadelLayout.Map(2, new Vector3(72, -.5f, 4)),
            CitadelLayout.Map(3, new Vector3(0, 1.7f, -66)), CitadelLayout.Map(4, new Vector3(0, 5.9f, 90))
        };
        public static readonly string[] RegionNames = {
            "THE SUNKEN PRISM", "THE EMBER CRYPT", "THE VIOLET SANCTUM", "THE ECLIPSE KEEP"
        };
        public static readonly Color[] RegionColors = {
            new Color(.45f,.84f,.87f), new Color(1f,.48f,.19f),
            new Color(.7f,.45f,.98f), new Color(.85f,.77f,.54f)
        };
        readonly Renderer[][] socketRenderers = new Renderer[4][];
        MaterialPropertyBlock glow;
        float subtitleUntil;
        Light visorLight;

        void Awake()
        {
            Instance = this;
            glow = new MaterialPropertyBlock();
            Application.targetFrameRate = 90;
            ExplorerController player = ExplorerController.Build(CitadelLayout.SpawnPosition, transform);
            player.transform.rotation = Quaternion.Euler(0, CitadelLayout.SpawnYaw, 0);
            player.View.farClipPlane = 380f;
            // The enclosed citadel relies on the F-key hand lamp between sparse torches.
            var handLamp = player.View.GetComponentInChildren<Light>();
            handLamp.intensity = 2.16f;
            handLamp.range = 18f;
            handLamp.spotAngle = 76f;
            handLamp.innerSpotAngle = 44f;
            handLamp.shadows = LightShadows.Soft;
            gameObject.AddComponent<CitadelHUD>();
            gameObject.AddComponent<PrismAmbience>();
            gameObject.AddComponent<CitadelSmokeTest>();
            gameObject.AddComponent<LunarPuzzleSmokeTest>();
            gameObject.AddComponent<CitadelLightBudget>();
            var lamp = new GameObject("Thermal survey illumination");
            lamp.transform.SetParent(player.View.transform, false);
            visorLight = lamp.AddComponent<Light>();
            visorLight.type = LightType.Point;
            visorLight.color = new Color(1f,.39f,.14f);
            visorLight.range = 14f;
            visorLight.intensity = .8f;
            visorLight.shadows = LightShadows.None;
            visorLight.enabled = false;
        }

        void Start()
        {
            for (int i = 0; i < 4; i++)
            {
                if (i == 0 && LunarPrismPuzzle.Instance != null)
                {
                    SocketPositions[0] = LunarPrismPuzzle.Instance.PrismOrigin.position;
                    continue;
                }
                int region = i;
                GameObject socket = GameObject.Find("PuzzleSocket_" + (i + 1));
                if (socket == null)
                {
                    Debug.LogWarning("Citadel survey socket missing: PuzzleSocket_" + (i + 1));
                    continue;
                }
                SocketPositions[i] = socket.transform.position;
                var control = socket.GetComponent<PrismInteractable>();
                if (control == null) control = socket.AddComponent<PrismInteractable>();
                control.Text = () => Attuned[region]
                    ? RegionNames[region] + "  /  SURVEY RECORDED\nE  Inspect puzzle socket"
                    : "E  Attune regional survey sigil\n" + RegionNames[region];
                control.Use = () => Attune(region);
                if (socket.GetComponentInChildren<Collider>() == null)
                {
                    var collider = socket.AddComponent<SphereCollider>();
                    collider.radius = .7f;
                }
                socketRenderers[i] = socket.GetComponentsInChildren<Renderer>();
                RefreshSocket(i);
            }
            Say("Three mirrors, a dead torch, and a locked crypt. The ancients have made shopping unnecessarily complicated.", 9f);
        }

        void Update()
        {
            if (Time.unscaledTime > subtitleUntil) Subtitle = "";
            ExplorerController player = ExplorerController.Instance;
            if (player == null) return;
            Vector3 position = player.transform.position;
            RegionIndex = RegionAt(position);
            if (RegionIndex >= 0)
            {
                RegionName = RegionNames[RegionIndex];
                if (!Visited[RegionIndex])
                {
                    Visited[RegionIndex] = true;
                    string[] lines = {
                        "Matching shadows, a moon, and a light puzzle. These ancients really lacked imagination. 1 equips my lighter.",
                        "A crypt with central heating. The dead had excellent taste.",
                        "Even their flowers glow ominously. Someone was committed to the aesthetic.",
                        "A throne beneath an eclipse. Subtle. I wonder what it fetches."
                    };
                    Say(lines[RegionIndex]);
                }
            }
            else if (CitadelLayout.Contains(0, position))
                RegionName = "THE NEXUS";
            else RegionName = "THE CITADEL PASSAGES";
        }

        public static int RegionAt(Vector3 position)
        {
            for (int room = 1; room < CitadelLayout.Centers.Length; room++)
                if (CitadelLayout.Contains(room, position)) return room - 1;
            return -1;
        }

        public void Attune(int index)
        {
            if (index < 0 || index >= Attuned.Length) return;
            if (index == 0 && LunarPrismPuzzle.Instance != null && !LunarPrismPuzzle.Instance.Solved) return;
            if (Attuned[index])
            {
                Say("Survey recorded. This plinth is a reserved puzzle socket for the region's future trials.");
                return;
            }
            Attuned[index] = true;
            Visited[index] = true;
            RefreshSocket(index);
            if (index == 0 && LunarPrismPuzzle.Instance != null)
            {
                Say("Three seals awakened. The Ember Crypt is open. Finally, some return on my investment.", 10f);
                return;
            }
            if (AttunedCount == 4)
                Say("Four sigils charted. The citadel is surveyed. Now someone just has to explain the architecture to my appraiser.", 10f);
            else if (index == 1)
                Say("Ember sigil recorded. Thermal survey acquired: V reveals the warm signature of each regional beacon.", 9f);
            else
                Say(RegionNames[index] + " — survey recorded. " + AttunedCount + " of 4 regional sigils attuned.", 7f);
        }

        public void ToggleVisor()
        {
            if (!VisorAvailable)
            {
                Say("Thermal survey is dormant. Attune the Ember Crypt's sigil to wake it.");
                return;
            }
            VisorOn = !VisorOn;
            if (visorLight != null) visorLight.enabled = VisorOn;
            Say(VisorOn ? "Thermal survey active. Warm markers identify the four regional beacons." : "Thermal survey disengaged.");
        }

        public void ResetSurvey()
        {
            for (int i = 0; i < 4; i++)
            {
                Attuned[i] = i == 0 && LunarPrismPuzzle.Instance != null && LunarPrismPuzzle.Instance.Solved;
                RefreshSocket(i);
            }
            VisorOn = false;
            if (visorLight != null) visorLight.enabled = false;
            Say("Sigils cleared. Your charted halls and position are preserved. The citadel awaits another appraisal.");
        }

        public void Say(string text, float duration = 7f)
        {
            Subtitle = text;
            subtitleUntil = Time.unscaledTime + duration;
        }

        void RefreshSocket(int index)
        {
            if (socketRenderers[index] == null) return;
            foreach (var renderer in socketRenderers[index])
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(glow);
                glow.SetColor("_EmissionColor", RegionColors[index] * (Attuned[index] ? .20f : .045f));
                renderer.SetPropertyBlock(glow);
            }
        }

        static int Count(bool[] values)
        {
            int result = 0;
            foreach (bool value in values) if (value) result++;
            return result;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
