using UnityEngine;

namespace FPCharacter
{
    public sealed class MirrorPickup : MonoBehaviour, ITorchBeamReflector
    {
        public Vector3 localFront = Vector3.forward;
        public Bounds localBounds = new Bounds(new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.9f, 0.3f));
        public Vector3 localNormal = Vector3.zero;
        public Vector3 localGlassCenter = Vector3.zero;
        public bool keepBeamLevel = true;

        public bool IsCarried { get; private set; }

        Collider[] colliders;

        void Awake()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            if (localNormal == Vector3.zero) ComputeGlass();
        }

        void ComputeGlass()
        {
            Vector3 nSum = Vector3.zero;
            Vector3 cSum = Vector3.zero;
            int count = 0;
            foreach (MeshFilter mf in GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                Mesh mesh = mf.sharedMesh;
                if (mr == null || mesh == null || !mesh.isReadable) continue;
                Material[] mats = mr.sharedMaterials;
                Vector3[] verts = mesh.vertices;
                Vector3[] normals = mesh.normals;
                for (int s = 0; s < mesh.subMeshCount && s < mats.Length; s++)
                {
                    if (mats[s] == null || !mats[s].name.Contains("Glass")) continue;
                    foreach (int i in mesh.GetTriangles(s))
                    {
                        if (normals != null && normals.Length == verts.Length) nSum += mf.transform.TransformDirection(normals[i]);
                        cSum += mf.transform.TransformPoint(verts[i]);
                        count++;
                    }
                }
            }
            if (count > 0 && nSum.sqrMagnitude > 1e-8f)
            {
                localNormal = transform.InverseTransformDirection(nSum).normalized;
                localGlassCenter = transform.InverseTransformPoint(cSum / count);
            }
            else
            {
                localNormal = Quaternion.AngleAxis(-9f, Vector3.Cross(Vector3.up, FlatFront)) * FlatFront;
                localGlassCenter = localBounds.center;
            }
        }

        public Vector3 FlatFront
        {
            get
            {
                Vector3 f = localFront;
                f.y = 0f;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        public Vector3 GlassNormal => transform.TransformDirection(localNormal).normalized;

        public void SetCarried(bool carried)
        {
            IsCarried = carried;
            if (colliders == null) colliders = GetComponentsInChildren<Collider>(true);
            foreach (Collider c in colliders)
                if (c != null) c.enabled = !carried;
        }

        public Vector3 GripPoint(float side, float height01, float inset)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, FlatFront).normalized;
            Vector3 e = localBounds.extents;
            float half = Mathf.Abs(axis.x) * e.x + Mathf.Abs(axis.z) * e.z;
            Vector3 c = localBounds.center;
            Vector3 local = new Vector3(c.x, localBounds.min.y + localBounds.size.y * height01, c.z) + axis * (side * half * inset);
            return transform.TransformPoint(local);
        }

        public bool TryReflect(Vector3 origin, Vector3 direction, RaycastHit hit, out Vector3 point, out Vector3 reflected)
        {
            point = hit.point;
            reflected = direction;
            if (IsCarried) return false;
            Vector3 n = GlassNormal;
            if (Vector3.Dot(direction, n) >= 0f) return false;
            Plane plane = new Plane(n, transform.TransformPoint(localGlassCenter));
            if (plane.Raycast(new Ray(origin, direction), out float enter))
            {
                Vector3 p = origin + direction * enter;
                if ((p - hit.point).sqrMagnitude < 0.36f) point = p;
            }
            if (keepBeamLevel)
            {
                Vector3 fd = direction;
                fd.y = 0f;
                Vector3 fn = n;
                fn.y = 0f;
                if (fd.sqrMagnitude > 1e-6f && fn.sqrMagnitude > 1e-6f)
                {
                    reflected = Vector3.Reflect(fd.normalized, fn.normalized).normalized;
                    return true;
                }
            }
            reflected = Vector3.Reflect(direction, n).normalized;
            return true;
        }
    }
}
