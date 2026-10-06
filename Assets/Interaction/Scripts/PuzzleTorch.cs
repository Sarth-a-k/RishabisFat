using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Puzzle torch: starts UNLIT. Press E nearby (or call Ignite()) to light it.
/// The moment it lights, a light beam with travelling "light waves" shoots out in ONE direction
/// (by default toward the centre of the map). Anything the beam hits that implements
/// ITorchBeamReceiver (e.g. your prism) is told when the beam enters / stays / leaves it.
///
/// CHANGE THE BEAM DIRECTION
///   In the Inspector:  Beam Aim = TowardMapCenter / TowardTarget / CustomDirection
///   From code:         torch.SetBeamDirection(worldDirection);
///                      torch.AimAt(worldPoint);   torch.AimAt(someTransform);
///                      torch.AimAtMapCenter(newCenter);
///                      torch.RotateBeam(yawDegrees, pitchDegrees);
///   Or subclass and override ComputeBeamDirection() for fully custom logic.
/// </summary>
public class PuzzleTorch : MonoBehaviour
{
    public enum BeamAim { TowardMapCenter, TowardTarget, CustomDirection }

    // ------------------------------------------------------------------ parts
    [Header("Parts (auto-found by name if left empty)")]
    [Tooltip("Flame base / beam origin. Default: child named 'FlameSocket'.")]
    public Transform flameSocket;
    [Tooltip("Flame meshes that flicker. Default: children named 'Flame_Outer' and 'Flame_Inner'.")]
    public Transform[] flames;
    [Tooltip("Glowing coals in the cup. Default: child named 'Embers'.")]
    public GameObject embers;

    // ------------------------------------------------------------------ lighting the torch
    [Header("Lighting the torch")]
    public bool startLit = false;
    public bool readInteractKey = true;
    public KeyCode interactKey = KeyCode.E;
    [Tooltip("Player must be this close (metres) to light it with the key.")]
    public float interactRadius = 2.5f;
    [Tooltip("Player transform. If empty, the object tagged 'Player' is used.")]
    public Transform player;
    public string playerTag = "Player";
    [Tooltip("A collider with this tag entering the torch's trigger collider lights it (e.g. the player's own torch). Leave empty to disable.")]
    public string igniteByTag = "Fire";
    [Tooltip("Allow pressing the key again to put the torch out.")]
    public bool allowExtinguish = false;

    // ------------------------------------------------------------------ fire look
    [Header("Fire light & flicker")]
    public Color fireColor = new Color(1f, 0.55f, 0.2f);
    public float fireIntensity = 2.2f;
    public float fireRange = 7f;
    [Range(0f, 0.5f)] public float flickerAmount = 0.12f;
    public float flickerSpeed = 3.5f;
    [Tooltip("Seconds for the flames to grow when lit.")]
    public float igniteDuration = 0.35f;

    // ------------------------------------------------------------------ beam
    [Header("Beam direction")]
    public BeamAim beamAim = BeamAim.TowardMapCenter;
    [Tooltip("World position of the map centre (used by TowardMapCenter).")]
    public Vector3 mapCenter = Vector3.zero;
    [Tooltip("Optional: use this object's position as the map centre instead.")]
    public Transform mapCenterTransform;
    [Tooltip("Used by TowardTarget, e.g. a prism.")]
    public Transform target;
    [Tooltip("World-space direction used by CustomDirection.")]
    public Vector3 customDirection = Vector3.forward;
    [Tooltip("Keep the beam horizontal (at flame height). Turn off to allow aiming up/down.")]
    public bool keepBeamLevel = true;

    [Header("Beam look")]
    public float beamLength = 30f;
    [Tooltip("Speed (m/s) of the first wave front when the torch lights. 0 = the beam appears instantly.")]
    public float emitSpeed = 45f;
    public float beamWidth = 0.06f;
    public Color beamColor = new Color(1f, 0.78f, 0.45f, 0.9f);
    [Tooltip("Optional material for the beam. Leave empty to use an unlit default.")]
    public Material beamMaterial;
    [Tooltip("Travelling light waves along the beam.")]
    public float waveSpeed = 9f;
    public float waveSpacing = 1.6f;
    [Range(0f, 4f)] public float waveStrength = 1.6f;
    public float waveSize = 0.35f;
    [Tooltip("Also add a narrow spot light along the beam so it lights what it points at.")]
    public bool beamSpotLight = true;
    public float beamSpotAngle = 14f;
    public float beamSpotIntensity = 3f;
    [Tooltip("Layers the beam can hit (stops at the first hit).")]
    public LayerMask beamHitLayers = ~0;
    [Tooltip("How many times the beam may bounce off mirrors.")]
    public int maxBounces = 4;

    [Header("Events")]
    public UnityEvent onLit;
    public UnityEvent onExtinguished;
    [Tooltip("Called when the beam starts hitting a new object.")]
    public UnityEvent<GameObject> onBeamHit;

    // ------------------------------------------------------------------ state
    public bool IsLit { get; private set; }
    public Vector3 BeamDirection { get; private set; } = Vector3.forward;
    public Vector3 BeamOrigin => flameSocket != null ? flameSocket.position : transform.position + Vector3.up * 2f;
    public GameObject CurrentHit => _hitObject;

    const int BeamPoints = 40;
    const int WidthKeys = 32;
    const int MaxPathPoints = 160;
    readonly Vector3[] _path = new Vector3[MaxPathPoints];
    readonly List<Vector3> _corners = new List<Vector3>(8);
    readonly List<Light> _bounceLights = new List<Light>();
    Light _fireLight, _spotLight;
    LineRenderer _beam;
    AnimationCurve _widthCurve;
    readonly Vector3[] _points = new Vector3[BeamPoints];
    Vector3[] _flameBaseScale;
    Quaternion[] _flameBaseRot;
    float _litTime = -1f, _front, _seed;
    GameObject _hitObject;
    ITorchBeamReceiver _hitReceiver;
    readonly RaycastHit[] _hits = new RaycastHit[16];
    readonly List<Collider> _ownColliders = new List<Collider>();

    // ================================================================== setup
    void Awake()
    {
        _seed = Random.value * 100f;
        if (flameSocket == null) flameSocket = FindDeep(transform, "FlameSocket");
        if (flames == null || flames.Length == 0)
        {
            var list = new List<Transform>();
            var o = FindDeep(transform, "Flame_Outer"); if (o) list.Add(o);
            var i = FindDeep(transform, "Flame_Inner"); if (i) list.Add(i);
            flames = list.ToArray();
        }
        if (embers == null) { var e = FindDeep(transform, "Embers"); if (e) embers = e.gameObject; }

        _flameBaseScale = new Vector3[flames.Length];
        _flameBaseRot = new Quaternion[flames.Length];
        for (int k = 0; k < flames.Length; k++)
        {
            _flameBaseScale[k] = flames[k].localScale;
            _flameBaseRot[k] = flames[k].localRotation;
        }
        GetComponentsInChildren(true, _ownColliders);

        // fire point light (no shadows: cheap)
        var lgo = new GameObject("FireLight");
        lgo.transform.SetParent(flameSocket != null ? flameSocket : transform, false);
        lgo.transform.localPosition = Vector3.up * 0.2f;
        _fireLight = lgo.AddComponent<Light>();
        _fireLight.type = LightType.Point;
        _fireLight.color = fireColor;
        _fireLight.range = fireRange;
        _fireLight.shadows = LightShadows.None;

        // beam
        var bgo = new GameObject("LightBeam");
        bgo.transform.SetParent(transform, false);
        _beam = bgo.AddComponent<LineRenderer>();
        _beam.useWorldSpace = true;
        _beam.positionCount = BeamPoints;
        _beam.numCapVertices = 2;
        _beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _beam.receiveShadows = false;
        _beam.material = beamMaterial != null ? beamMaterial : DefaultBeamMaterial();
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(beamColor, 0.15f), new GradientColorKey(beamColor, 1f) },
            new[] { new GradientAlphaKey(beamColor.a, 0f), new GradientAlphaKey(beamColor.a, 0.8f), new GradientAlphaKey(0f, 1f) });
        _beam.colorGradient = grad;
        var keys = new Keyframe[WidthKeys];
        for (int k = 0; k < WidthKeys; k++) keys[k] = new Keyframe(k / (WidthKeys - 1f), 1f);
        _widthCurve = new AnimationCurve(keys);
        _beam.widthMultiplier = beamWidth;

        if (beamSpotLight)
        {
            var sgo = new GameObject("BeamSpotLight");
            sgo.transform.SetParent(transform, false);
            _spotLight = sgo.AddComponent<Light>();
            _spotLight.type = LightType.Spot;
            _spotLight.color = beamColor;
            _spotLight.spotAngle = beamSpotAngle;
            _spotLight.range = beamLength;
            _spotLight.intensity = beamSpotIntensity;
            _spotLight.shadows = LightShadows.None;
        }
        SetVisualsLit(false, instant: true);
    }

    void Start()
    {
        if (player == null && !string.IsNullOrEmpty(playerTag))
        {
            try { var p = GameObject.FindWithTag(playerTag); if (p) player = p.transform; }
            catch (UnityException) { /* tag not defined in this project */ }
        }
        if (startLit) Ignite();
    }

    // ================================================================== public API
    /// <summary>Light the torch (does nothing if already lit). The beam fires immediately.</summary>
    public void Ignite()
    {
        if (IsLit) return;
        IsLit = true;
        _litTime = Time.time;
        _front = emitSpeed <= 0f ? beamLength : 0f;
        UpdateBeamDirection();
        SetVisualsLit(true, instant: false);
        onLit?.Invoke();
    }

    /// <summary>Put the torch out.</summary>
    public void Extinguish()
    {
        if (!IsLit) return;
        IsLit = false;
        SetVisualsLit(false, instant: true);
        ClearHit();
        onExtinguished?.Invoke();
    }

    public void Toggle() { if (IsLit) Extinguish(); else Ignite(); }

    /// <summary>Point the beam along a world-space direction (switches Beam Aim to CustomDirection).</summary>
    public void SetBeamDirection(Vector3 worldDirection)
    {
        if (worldDirection.sqrMagnitude < 1e-6f) return;
        customDirection = worldDirection.normalized;
        beamAim = BeamAim.CustomDirection;
        UpdateBeamDirection();
    }

    /// <summary>Point the beam at a world position.</summary>
    public void AimAt(Vector3 worldPoint) => SetBeamDirection(worldPoint - BeamOrigin);

    /// <summary>Keep the beam pointed at a moving object (e.g. a prism) every frame.</summary>
    public void AimAt(Transform t)
    {
        target = t;
        beamAim = BeamAim.TowardTarget;
        UpdateBeamDirection();
    }

    /// <summary>Aim toward the map centre (optionally giving a new centre).</summary>
    public void AimAtMapCenter(Vector3? newCenter = null)
    {
        if (newCenter.HasValue) mapCenter = newCenter.Value;
        beamAim = BeamAim.TowardMapCenter;
        UpdateBeamDirection();
    }

    /// <summary>Turn the current beam by yaw (around up) and pitch (up/down) in degrees.</summary>
    public void RotateBeam(float yawDegrees, float pitchDegrees = 0f)
    {
        Vector3 d = Quaternion.AngleAxis(yawDegrees, Vector3.up) * BeamDirection;
        Vector3 side = Vector3.Cross(Vector3.up, d);
        if (side.sqrMagnitude > 1e-6f) d = Quaternion.AngleAxis(-pitchDegrees, side.normalized) * d;
        SetBeamDirection(d);
    }

    /// <summary>Override this in a subclass for completely custom aiming.</summary>
    protected virtual Vector3 ComputeBeamDirection()
    {
        Vector3 origin = BeamOrigin;
        Vector3 d;
        switch (beamAim)
        {
            case BeamAim.TowardTarget:
                d = target != null ? target.position - origin : BeamDirection;
                break;
            case BeamAim.CustomDirection:
                d = customDirection;
                break;
            default:
                Vector3 c = mapCenterTransform != null ? mapCenterTransform.position : mapCenter;
                d = c - origin;
                break;
        }
        if (keepBeamLevel) d.y = 0f;
        if (d.sqrMagnitude < 1e-6f) d = transform.forward;   // torch standing exactly on the centre
        return d.normalized;
    }

    // ================================================================== per frame
    void Update()
    {
        HandleInput();
        if (!IsLit) return;

        float t = Time.time;
        float age = t - _litTime;

        // flames: grow in, then flicker (Perlin noise -> smooth, cheap, no allocations)
        float grow = igniteDuration > 0f ? Mathf.SmoothStep(0f, 1f, age / igniteDuration) : 1f;
        for (int k = 0; k < flames.Length; k++)
        {
            float n1 = Mathf.PerlinNoise(_seed + k * 7.1f, t * flickerSpeed) - 0.5f;
            float n2 = Mathf.PerlinNoise(_seed + k * 3.3f + 50f, t * flickerSpeed * 1.3f) - 0.5f;
            Vector3 s = _flameBaseScale[k];
            flames[k].localScale = new Vector3(s.x * (1f + n2 * flickerAmount), s.y * (1f + n1 * flickerAmount * 2f), s.z * (1f - n2 * flickerAmount)) * grow;
            flames[k].localRotation = _flameBaseRot[k] * Quaternion.Euler(n2 * 8f * flickerAmount * 4f, n1 * 25f, n1 * 6f * flickerAmount * 4f);
        }
        _fireLight.intensity = fireIntensity * grow * (1f + (Mathf.PerlinNoise(_seed, t * flickerSpeed * 2f) - 0.5f) * flickerAmount * 2.5f);

        // beam
        if (beamAim == BeamAim.TowardTarget || beamAim == BeamAim.TowardMapCenter) UpdateBeamDirection();
        _front = emitSpeed <= 0f ? beamLength : Mathf.Min(beamLength, _front + emitSpeed * Time.deltaTime);
        UpdateBeam(age);
    }

    void HandleInput()
    {
        if (!readInteractKey) return;
        if (!KeyPressed()) return;
        if (player != null && (player.position - transform.position).sqrMagnitude > interactRadius * interactRadius) return;
        if (!IsLit) Ignite();
        else if (allowExtinguish) Extinguish();
    }

    bool KeyPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var kb = Keyboard.current;
        if (kb == null) return false;
        if (System.Enum.TryParse(interactKey.ToString(), out Key key)) return kb[key].wasPressedThisFrame;
        return kb.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(interactKey);
#endif
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsLit && !string.IsNullOrEmpty(igniteByTag) && other.tag == igniteByTag) Ignite();
    }

    void UpdateBeamDirection()
    {
        BeamDirection = ComputeBeamDirection();
    }

    void UpdateBeam(float age)
    {
        Vector3 o = BeamOrigin + Vector3.up * 0.15f;
        Vector3 d = BeamDirection;
        float remaining = _front;
        float total = 0f;
        int bounces = 0;
        Collider skip = null;
        GameObject hitObj = null;
        RaycastHit endHit = default(RaycastHit);
        _corners.Clear();
        _corners.Add(o);
        while (remaining > 0.001f)
        {
            RaycastHit hit;
            if (!CastBeam(o, d, remaining, skip, out hit))
            {
                _corners.Add(o + d * remaining);
                total += remaining;
                break;
            }
            ITorchBeamReflector reflector = hit.collider.GetComponentInParent<ITorchBeamReflector>();
            Vector3 p, nd;
            if (reflector != null && bounces < maxBounces && reflector.TryReflect(o, d, hit, out p, out nd))
            {
                float seg = Vector3.Distance(o, p);
                _corners.Add(p);
                total += seg;
                remaining -= seg;
                SetBounceLight(bounces, p, true);
                bounces++;
                o = p;
                d = nd;
                skip = hit.collider;
                continue;
            }
            _corners.Add(hit.point);
            total += hit.distance;
            hitObj = hit.collider.gameObject;
            endHit = hit;
            break;
        }
        for (int k = bounces; k < _bounceLights.Count; k++) SetBounceLight(k, Vector3.zero, false);
        HandleHit(hitObj, endHit);
        BuildPath(total);

        float length = total;
        float firstWave = Mathf.Clamp01(1f - age * 0.8f) * 2.2f;
        for (int k = 0; k < WidthKeys; k++)
        {
            float u = k / (WidthKeys - 1f);
            float dist = u * length;
            float phase = (dist - age * waveSpeed) / Mathf.Max(0.01f, waveSpacing);
            float wave = Mathf.Pow(Mathf.Abs(Mathf.Cos(phase * Mathf.PI)), 1f / Mathf.Max(0.05f, waveSize) * 0.35f + 1f);
            float frontPulse = firstWave * Mathf.Exp(-Mathf.Pow((dist - _front) / 0.6f, 2f));
            float w = 1f + waveStrength * wave * 0.5f + frontPulse;
            _widthCurve.MoveKey(k, new Keyframe(u, w));
        }
        _beam.widthCurve = _widthCurve;
        _beam.widthMultiplier = beamWidth * Mathf.Clamp01(age / 0.08f);

        if (_spotLight != null && _corners.Count >= 2)
        {
            Vector3 s0 = _corners[0];
            Vector3 sd = _corners[1] - s0;
            _spotLight.transform.position = s0;
            if (sd.sqrMagnitude > 1e-6f) _spotLight.transform.rotation = Quaternion.LookRotation(sd.normalized, Vector3.up);
            _spotLight.range = Mathf.Max(0.1f, sd.magnitude + 0.5f);
        }
    }

    bool CastBeam(Vector3 origin, Vector3 dir, float distance, Collider skip, out RaycastHit best)
    {
        best = default(RaycastHit);
        int n = Physics.RaycastNonAlloc(origin, dir, _hits, distance, beamHitLayers, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        bool found = false;
        for (int k = 0; k < n; k++)
        {
            Collider c = _hits[k].collider;
            if (c == skip || _ownColliders.Contains(c)) continue;
            if (_hits[k].distance < bestDist)
            {
                bestDist = _hits[k].distance;
                best = _hits[k];
                found = true;
            }
        }
        return found;
    }

    void BuildPath(float total)
    {
        int count = 0;
        float spacing = Mathf.Max(0.2f, total / Mathf.Max(1, MaxPathPoints - _corners.Count - 1));
        for (int c = 0; c < _corners.Count - 1 && count < MaxPathPoints - 1; c++)
        {
            Vector3 a = _corners[c], b = _corners[c + 1];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / spacing));
            for (int s = 0; s < steps && count < MaxPathPoints - 1; s++) _path[count++] = Vector3.Lerp(a, b, s / (float)steps);
        }
        _path[count++] = _corners[_corners.Count - 1];
        if (count < 2) _path[count++] = _path[0];
        _beam.positionCount = count;
        for (int k = 0; k < count; k++) _beam.SetPosition(k, _path[k]);
    }

    void SetBounceLight(int index, Vector3 position, bool on)
    {
        while (on && _bounceLights.Count <= index)
        {
            var go = new GameObject("BeamBounceLight");
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = beamColor;
            l.range = 1.6f;
            l.intensity = 1.6f;
            l.shadows = LightShadows.None;
            _bounceLights.Add(l);
        }
        if (index >= _bounceLights.Count) return;
        _bounceLights[index].enabled = on;
        if (on) _bounceLights[index].transform.position = position;
    }

    void HandleHit(GameObject hitObj, RaycastHit hit)
    {
        if (hitObj != _hitObject)
        {
            ClearHit();
            _hitObject = hitObj;
            if (hitObj != null)
            {
                _hitReceiver = hitObj.GetComponentInParent<ITorchBeamReceiver>();
                _hitReceiver?.OnTorchBeamEnter(this, hit);
                onBeamHit?.Invoke(hitObj);
            }
        }
        else if (_hitReceiver != null)
        {
            _hitReceiver.OnTorchBeamStay(this, hit);
        }
    }

    void ClearHit()
    {
        _hitReceiver?.OnTorchBeamExit(this);
        _hitReceiver = null;
        _hitObject = null;
    }

    void SetVisualsLit(bool lit, bool instant)
    {
        foreach (var f in flames) if (f) f.gameObject.SetActive(lit);
        if (embers) embers.SetActive(lit);
        if (_fireLight) { _fireLight.enabled = lit; if (instant) _fireLight.intensity = lit ? fireIntensity : 0f; }
        if (_beam) _beam.enabled = lit;
        if (_spotLight) _spotLight.enabled = lit;
        if (!lit) foreach (var bl in _bounceLights) if (bl) bl.enabled = false;
        if (lit && instant)
            for (int k = 0; k < flames.Length; k++) flames[k].localScale = _flameBaseScale[k];
    }

    static Material DefaultBeamMaterial()
    {
        // "Sprites/Default" is unlit, uses vertex colour and exists in Built-in, URP and HDRP projects.
        var sh = Shader.Find("Sprites/Default");
        return new Material(sh != null ? sh : Shader.Find("Unlit/Color"));
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindDeep(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    // ================================================================== editor helper
    void OnDrawGizmosSelected()
    {
        // shows where the beam will go, even before the torch is lit
        if (flameSocket == null) flameSocket = FindDeep(transform, "FlameSocket");
        Vector3 o = BeamOrigin + Vector3.up * 0.15f;
        Vector3 d = Application.isPlaying ? BeamDirection : ComputeBeamDirection();
        Gizmos.color = new Color(1f, 0.7f, 0.3f);
        Gizmos.DrawLine(o, o + d * Mathf.Min(beamLength, 10f));
        Gizmos.DrawWireSphere(o + d * Mathf.Min(beamLength, 10f), 0.15f);
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}

/// <summary>Implement on your prism (or anything) to react to a torch beam.</summary>
public interface ITorchBeamReceiver
{
    void OnTorchBeamEnter(PuzzleTorch torch, RaycastHit hit);
    void OnTorchBeamStay(PuzzleTorch torch, RaycastHit hit);
    void OnTorchBeamExit(PuzzleTorch torch);
}

public interface ITorchBeamReflector
{
    bool TryReflect(Vector3 origin, Vector3 direction, RaycastHit hit, out Vector3 point, out Vector3 reflected);
}
