using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPCharacter
{
    public static class GhostHat
    {
        public const string NpcName = "Ghost_NPC (Void Passage)";
        public static float sinkIntoHead = 0.42f;
        public static float sizeScale = 1f;
        static readonly Color Khaki = new Color(0.74f, 0.62f, 0.4f);
        static readonly Color Band = new Color(0.32f, 0.2f, 0.11f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            TryAttach();
        }

        static void OnLoaded(Scene s, LoadSceneMode m) { TryAttach(); }

        static void TryAttach()
        {
            GameObject npc = GameObject.Find(NpcName);
            if (npc != null) Attach(npc);
        }

        public static GameObject Attach(GameObject npc)
        {
            Transform head = null, top = null;
            foreach (Transform t in npc.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Explorer hat") return t.gameObject;
                if (t.name == "Head") head = t;
                if (t.name == "HeadTop_End") top = t;
            }
            if (head == null) return null;
            Vector3 topPos = top != null ? top.position : head.position + Vector3.up * 0.2f;
            float headH = Mathf.Max(0.08f, Vector3.Distance(head.position, topPos));
            float w = headH * 0.6f * sizeScale;
            float r = w * 1.08f, crownH = w * 0.98f, bandH = w * 0.24f, brimR = w * 2f, droop = w * 0.14f;

            var hat = new GameObject("Explorer hat");
            hat.transform.position = topPos - Vector3.up * crownH * sinkIntoHead;
            Vector3 fwd = Vector3.ProjectOnPlane(npc.transform.forward, Vector3.up);
            hat.transform.rotation = Quaternion.LookRotation(fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward, Vector3.up) * Quaternion.Euler(-4f, 0f, 0f);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material khaki = Mat(lit, Khaki);
            Material band = Mat(lit, Band);
            Part(hat.transform, "Crown", Dome(r, crownH, 1.12f), khaki);
            Part(hat.transform, "Band", Ring(r * 1.015f, bandH, 1.12f), band);
            Part(hat.transform, "Brim", Brim(r * 0.96f, brimR, droop, 1.1f), khaki);
            hat.transform.SetParent(head, true);
            return hat;
        }

        static Material Mat(Shader s, Color c)
        {
            var m = new Material(s);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 0.12f);
            return m;
        }

        static void Part(Transform parent, string name, Mesh mesh, Material m)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
        }

        static Mesh Dome(float r, float h, float zStretch)
        {
            const int Seg = 32, Rings = 10;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i <= Rings; i++)
            {
                float a = i / (float)Rings * Mathf.PI * 0.5f;
                float rr = Mathf.Cos(a), y = Mathf.Sin(a);
                for (int j = 0; j <= Seg; j++)
                {
                    float b = j / (float)Seg * Mathf.PI * 2f;
                    Vector3 p = new Vector3(Mathf.Cos(b) * rr * r, y * h, Mathf.Sin(b) * rr * r * zStretch);
                    v.Add(p);
                    n.Add(new Vector3(p.x / (r * r), p.y / (h * h), p.z / (r * r * zStretch * zStretch)).normalized);
                }
            }
            for (int i = 0; i < Rings; i++)
                for (int j = 0; j < Seg; j++)
                {
                    int a0 = i * (Seg + 1) + j, a1 = a0 + 1, b0 = a0 + Seg + 1, b1 = b0 + 1;
                    tri.Add(a0); tri.Add(b0); tri.Add(a1);
                    tri.Add(a1); tri.Add(b0); tri.Add(b1);
                }
            return Build("Hat crown", v, n, tri);
        }

        static Mesh Ring(float r, float h, float zStretch)
        {
            const int Seg = 32;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tri = new List<int>();
            for (int j = 0; j <= Seg; j++)
            {
                float b = j / (float)Seg * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b) * zStretch);
                Vector3 p = new Vector3(d.x * r, 0f, d.z * r);
                v.Add(p); v.Add(p + Vector3.up * h);
                Vector3 nn = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b) / zStretch).normalized;
                n.Add(nn); n.Add(nn);
            }
            for (int j = 0; j < Seg; j++)
            {
                int a0 = j * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                tri.Add(a0); tri.Add(a1); tri.Add(b0);
                tri.Add(b0); tri.Add(a1); tri.Add(b1);
            }
            return Build("Hat band", v, n, tri);
        }

        static Mesh Brim(float inner, float outer, float droop, float zStretch)
        {
            const int Seg = 40, Steps = 4;
            const float T = 0.006f;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tri = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int start = v.Count;
                float off = side == 0 ? T * 0.5f : -T * 0.5f;
                for (int s = 0; s <= Steps; s++)
                {
                    float k = s / (float)Steps;
                    float rad = Mathf.Lerp(inner, outer, k);
                    float y = -droop * k * k + off;
                    for (int j = 0; j <= Seg; j++)
                    {
                        float b = j / (float)Seg * Mathf.PI * 2f;
                        v.Add(new Vector3(Mathf.Cos(b) * rad, y, Mathf.Sin(b) * rad * zStretch));
                        Vector3 slope = new Vector3(Mathf.Cos(b) * 2f * droop * k / (outer - inner), 1f, Mathf.Sin(b) * 2f * droop * k / (outer - inner)).normalized;
                        n.Add(side == 0 ? slope : -slope);
                    }
                }
                for (int s = 0; s < Steps; s++)
                    for (int j = 0; j < Seg; j++)
                    {
                        int a0 = start + s * (Seg + 1) + j, a1 = a0 + 1, b0 = a0 + Seg + 1, b1 = b0 + 1;
                        if (side == 0) { tri.Add(a0); tri.Add(b0); tri.Add(a1); tri.Add(a1); tri.Add(b0); tri.Add(b1); }
                        else { tri.Add(a0); tri.Add(a1); tri.Add(b0); tri.Add(a1); tri.Add(b1); tri.Add(b0); }
                    }
            }
            int rimTop = Steps * (Seg + 1), rimBottom = (Steps + 1) * (Seg + 1) + Steps * (Seg + 1);
            int baseIdx = v.Count;
            for (int j = 0; j <= Seg; j++)
            {
                Vector3 a = v[rimTop + j], b = v[rimBottom + j];
                Vector3 nn = new Vector3(a.x, 0f, a.z).normalized;
                v.Add(a); v.Add(b); n.Add(nn); n.Add(nn);
            }
            for (int j = 0; j < Seg; j++)
            {
                int a0 = baseIdx + j * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                tri.Add(a0); tri.Add(b0); tri.Add(a1);
                tri.Add(a1); tri.Add(b0); tri.Add(b1);
            }
            return Build("Hat brim", v, n, tri);
        }

        static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<int> tri)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
