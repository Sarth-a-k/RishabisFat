using UnityEngine;
using UnityEngine.Events;

namespace FPCharacter
{
    public sealed class PrismBeamSplitter : MonoBehaviour, ITorchBeamReceiver
    {
        public bool alwaysLit = true;
        public Transform emitOrigin;
        public SeatBeamTarget[] seats = new SeatBeamTarget[0];
        public float beamWidth = 0.022f;
        public Color beamColor = new Color(1f, 0.86f, 0.6f, 0.9f);
        public Light prismGlow;
        public UnityEvent onSolved;
        public UnityEvent onUnsolved;
        [System.NonSerialized] public float overload;
        public bool Shattered { get; private set; }
        public void Shatter() { Shattered = true; }

        public int ReachedCount { get; private set; }
        public bool Solved { get; private set; }
        public bool IsLit => alwaysLit || Time.time < torchLitUntil;

        LineRenderer[] lines = new LineRenderer[0];
        readonly RaycastHit[] hits = new RaycastHit[24];
        float torchLitUntil = -1f;

        void Awake()
        {
            if (emitOrigin == null) emitOrigin = transform;
            Shader sh = Shader.Find("Sprites/Default");
            Material mat = new Material(sh != null ? sh : Shader.Find("Unlit/Color"));
            lines = new LineRenderer[seats.Length];
            for (int i = 0; i < seats.Length; i++)
            {
                GameObject go = new GameObject("Beam_" + i);
                go.transform.SetParent(transform, false);
                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.material = mat;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                Gradient g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(beamColor, 0.2f), new GradientColorKey(beamColor, 1f) },
                          new[] { new GradientAlphaKey(beamColor.a, 0f), new GradientAlphaKey(beamColor.a * 0.85f, 1f) });
                lr.colorGradient = g;
                lr.enabled = false;
                lines[i] = lr;
            }
        }

        void Update()
        {
            bool lit = IsLit && !Shattered;
            Color hot = Color.Lerp(beamColor, new Color(1f, 0.12f, 0.06f, beamColor.a), overload);
            int reached = 0;
            Vector3 o = emitOrigin.position;
            for (int i = 0; i < seats.Length; i++)
            {
                SeatBeamTarget seat = seats[i];
                if (seat == null) continue;
                Vector3 target = seat.BeamPoint;
                target.y = o.y;
                Vector3 d = target - o;
                float dist = d.magnitude;
                if (dist < 1e-4f) continue;
                d /= dist;
                Vector3 end = target;
                bool blocked = false;
                if (lit)
                {
                    int n = Physics.RaycastNonAlloc(o, d, hits, dist, ~0, QueryTriggerInteraction.Ignore);
                    float best = float.MaxValue;
                    for (int k = 0; k < n; k++)
                    {
                        if (hits[k].distance < best && hits[k].collider.GetComponentInParent<RingBlocker>() != null)
                        {
                            best = hits[k].distance;
                            blocked = true;
                        }
                    }
                    if (blocked) end = o + d * best;
                }
                LineRenderer lr = lines[i];
                lr.enabled = lit;
                if (lit)
                {
                    lr.SetPosition(0, o);
                    lr.SetPosition(1, end);
                    lr.widthMultiplier = beamWidth * (1f + overload * 1.6f) * (1f + (0.15f + overload * 0.35f) * Mathf.Sin(Time.time * (9f + overload * 20f) + i * 1.7f));
                    lr.startColor = Color.Lerp(Color.white, hot, 0.5f + overload * 0.5f);
                    lr.endColor = hot;
                }
                bool reachedSeat = lit && !blocked;
                seat.SetLit(reachedSeat);
                if (reachedSeat) reached++;
            }
            ReachedCount = reached;
            if (prismGlow != null && !Shattered) prismGlow.enabled = lit;
            if (Shattered) return;
            bool solved = seats.Length > 0 && reached == seats.Length;
            if (solved != Solved)
            {
                Solved = solved;
                if (solved)
                {
                    Debug.Log("[RoundTable] Solved: all " + seats.Length + " beams reach their seats");
                    onSolved?.Invoke();
                }
                else onUnsolved?.Invoke();
            }
        }

        public void OnTorchBeamEnter(PuzzleTorch torch, RaycastHit hit) { torchLitUntil = Time.time + 0.25f; }
        public void OnTorchBeamStay(PuzzleTorch torch, RaycastHit hit) { torchLitUntil = Time.time + 0.25f; }
        public void OnTorchBeamExit(PuzzleTorch torch) { }
    }
}
