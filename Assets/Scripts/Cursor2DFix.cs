using UnityEngine;

public sealed class Cursor2DFix : MonoBehaviour
{
    public bool showCursor = true;

    void OnEnable()
    {
        Apply();
    }

    void Update()
    {
        if (Cursor.lockState != CursorLockMode.None || Cursor.visible != showCursor) Apply();
    }

    void Apply()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = showCursor;
    }
}
