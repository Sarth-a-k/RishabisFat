using UnityEngine;

/// <summary>
/// Example receiver: put on your prism (it needs a Collider). It glows while a torch beam hits it
/// and tells you which torch is shining on it. Replace the bodies with your puzzle logic
/// (e.g. split the light, fire a new beam, open a door...).
/// </summary>
public class PrismBeamReceiverExample : MonoBehaviour, ITorchBeamReceiver
{
    public Renderer glowRenderer;
    public Color glowColor = new Color(1f, 0.8f, 0.5f);
    public float glowIntensity = 2f;

    public bool IsLitByBeam { get; private set; }
    MaterialPropertyBlock _mpb;

    void Awake()
    {
        if (glowRenderer == null) glowRenderer = GetComponentInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    public void OnTorchBeamEnter(PuzzleTorch torch, RaycastHit hit)
    {
        IsLitByBeam = true;
        SetGlow(glowColor * glowIntensity);
        Debug.Log($"{name}: hit by the beam of {torch.name} at {hit.point}");
    }

    public void OnTorchBeamStay(PuzzleTorch torch, RaycastHit hit) { }

    public void OnTorchBeamExit(PuzzleTorch torch)
    {
        IsLitByBeam = false;
        SetGlow(Color.black);
    }

    void SetGlow(Color c)
    {
        if (glowRenderer == null) return;
        glowRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor("_EmissionColor", c);
        glowRenderer.SetPropertyBlock(_mpb);
    }
}
