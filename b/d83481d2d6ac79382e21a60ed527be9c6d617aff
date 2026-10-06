using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPCharacter
{
    public static class WalkFixes
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            Apply();
        }

        static void OnLoaded(Scene s, LoadSceneMode m) { Apply(); }

        public static void Apply()
        {
            if (Object.FindAnyObjectByType<FPCharacterMover>() == null) return;
            if (GameObject.Find("Walk fixes") != null) return;
            Collider approach = null, dais = null;
            foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                if (c.name == "Keep dais approach") approach = c;
                if (c.name == "Royal dais") dais = c;
            }
            if (approach == null || dais == null) return;
            var root = new GameObject("Walk fixes");
            Bounds a = approach.bounds;
            float floorY = a.min.y + 0.12f;
            if (Physics.Raycast(new Vector3(a.center.x, a.min.y + 0.6f, a.min.z - 0.6f), Vector3.down, out RaycastHit fh, 2f, ~0, QueryTriggerInteraction.Ignore)) floorY = fh.point.y;
            float topY = dais.bounds.max.y;
            Vector3 low = new Vector3(a.center.x, floorY, a.min.z - 0.1f);
            Vector3 high = new Vector3(a.center.x, topY, dais.bounds.min.z + 0.45f);
            Ramp(root.transform, "Dais ramp surface", low, high, a.size.x);
        }

        static void Ramp(Transform parent, string name, Vector3 low, Vector3 high, float width)
        {
            const float T = 0.12f;
            Vector3 dir = high - low;
            Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            Vector3 normal = rot * Vector3.up;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation((low + high) * 0.5f - normal * (T * 0.5f), rot);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(width, T, dir.magnitude + 0.3f);
        }
    }
}
