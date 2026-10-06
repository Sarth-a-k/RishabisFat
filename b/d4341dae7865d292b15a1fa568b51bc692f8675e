using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class CryptFixSetup
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_cryptfix";
        const string WorkScene = "Assets/Scenes/FourfoldCitadel_WithOurStuff.unity";
        const string Dir = "Assets/Interaction/Generated/LavaCrypt";
        const string RootName = "02 - THE EMBER CRYPT (lava)";
        const float HX = 23.25f, CEIL = 4.4f, WALL_T = 1f;

        static StringBuilder report;

        static CryptFixSetup()
        {
            EditorApplication.delayCall += Check;
        }

        static void Check()
        {
            if (!File.Exists(TriggerPath) || File.ReadAllText(TriggerPath).Trim() != "pending") return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += Check;
                return;
            }
            File.WriteAllText(TriggerPath, "done");
            Run();
        }

        static void Log(string s) { report.AppendLine(s); }

        static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        [MenuItem("Tools/Interaction/Fix Lava Crypt (gaps, lava, prism)")]
        internal static void Run()
        {
            report = new StringBuilder();
            try
            {
                EditorSceneManager.SaveOpenScenes();
                File.Copy(WorkScene, "Backups/FourfoldCitadel_WithOurStuff_before_cryptfix.unity", true);
                Scene s = EditorSceneManager.OpenScene(WorkScene, OpenSceneMode.Single);
                Transform root = null;
                foreach (GameObject g in s.GetRootGameObjects())
                {
                    root = Find(g.transform, RootName);
                    if (root != null) break;
                }
                if (root == null) throw new Exception("Lava crypt not found");

                Material wall = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Crypt_Wall.mat");
                foreach (string n in new[] { "Bulkhead West", "Bulkhead East" })
                {
                    Transform old = Find(root, n);
                    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
                Material bulkMat = wall;
                if (wall != null)
                {
                    string p = Dir + "/Crypt_Wall_Bulkhead.mat";
                    bulkMat = AssetDatabase.LoadAssetAtPath<Material>(p);
                    if (bulkMat == null) { bulkMat = new Material(wall); AssetDatabase.CreateAsset(bulkMat, p); }
                    bulkMat.CopyPropertiesFromMaterial(wall);
                    bulkMat.SetTextureScale("_BaseMap", new Vector2(5.25f, 3.55f));
                    EditorUtility.SetDirty(bulkMat);
                }
                foreach (int side in new[] { -1, 1 })
                {
                    GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    b.name = side < 0 ? "Bulkhead West" : "Bulkhead East";
                    b.transform.SetParent(root, false);
                    float top = 11.5f;
                    b.transform.localPosition = new Vector3(side * (HX + WALL_T * 0.5f), (CEIL + top) * 0.5f, 0f);
                    b.transform.localScale = new Vector3(WALL_T * 1.02f, top - CEIL, 10.5f);
                    b.GetComponent<Renderer>().sharedMaterial = bulkMat;
                    GameObjectUtility.SetStaticEditorFlags(b, StaticEditorFlags.BatchingStatic);
                }
                Log("bulkheads added");

                int flipped = 0;
                foreach (LavaFlow lf in root.GetComponentsInChildren<LavaFlow>(true))
                    if (lf.name == "Lava sheet") { lf.scrollSpeed = new Vector2(0f, -Mathf.Abs(lf.scrollSpeed.y)); EditorUtility.SetDirty(lf); flipped++; }
                Log("lavafalls reversed: " + flipped);

                RoundTablePuzzle puzzle = root.GetComponentInChildren<RoundTablePuzzle>(true);
                if (puzzle == null) throw new Exception("RoundTablePuzzle missing");
                Transform prism = puzzle.transform.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Prism" && t.GetComponent<PrismBeamSplitter>() != null);
                if (prism == null) prism = Find(puzzle.transform, "Prism");
                Transform crystal = prism != null ? Find(prism, "Crystal") : null;
                Transform center = Find(puzzle.transform, "TableCenter");
                PrismSocket socket = puzzle.GetComponent<PrismSocket>();
                if (socket == null) socket = puzzle.gameObject.AddComponent<PrismSocket>();
                socket.prismObject = prism != null ? prism.gameObject : null;
                socket.tableCenter = center != null ? center : puzzle.transform;
                socket.startPlaced = false;

                float tableTop = -999f;
                foreach (Renderer r in puzzle.GetComponentsInChildren<Renderer>(true))
                    if (r.name.Contains("RoundTable") || (r.transform.parent != null && r.transform.parent.name.Contains("Table")))
                        tableTop = Mathf.Max(tableTop, r.bounds.max.y);
                if (tableTop < -900f && crystal != null) tableTop = crystal.GetComponent<Renderer>().bounds.min.y - 0.1f;
                Transform oldZone = Find(puzzle.transform, "Prism socket zone");
                if (oldZone != null) UnityEngine.Object.DestroyImmediate(oldZone.gameObject);
                GameObject zone = new GameObject("Prism socket zone");
                zone.transform.SetParent(puzzle.transform, false);
                Vector3 cpos = socket.tableCenter.position;
                zone.transform.position = new Vector3(cpos.x, tableTop - 0.06f, cpos.z);
                zone.AddComponent<BoxCollider>().size = new Vector3(1.7f, 0.1f, 1.7f);
                Transform oldFlash = Find(puzzle.transform, "Prism place flash");
                if (oldFlash != null) UnityEngine.Object.DestroyImmediate(oldFlash.gameObject);
                GameObject fl = new GameObject("Prism place flash");
                fl.transform.SetParent(puzzle.transform, false);
                fl.transform.position = new Vector3(cpos.x, tableTop + 0.6f, cpos.z);
                Light flash = fl.AddComponent<Light>();
                flash.type = LightType.Point; flash.color = new Color(1f, 0.6f, 0.4f); flash.range = 6f; flash.intensity = 0f; flash.enabled = false; flash.shadows = LightShadows.None;
                socket.placeFlash = flash;
                EditorUtility.SetDirty(socket);
                Log("socket on table, table top " + tableTop.ToString("F2") + ", prism " + (prism != null));

                Transform oldPick = Find(root, "Prism pickup");
                if (oldPick != null) UnityEngine.Object.DestroyImmediate(oldPick.gameObject);
                if (crystal != null)
                {
                    GameObject pick = new GameObject("Prism pickup");
                    pick.transform.SetParent(root, false);
                    pick.transform.localPosition = new Vector3(15.5f, 1.55f, -11f);
                    GameObject vis = new GameObject("Prism visual");
                    vis.transform.SetParent(pick.transform, false);
                    vis.transform.localScale = crystal.lossyScale;
                    vis.AddComponent<MeshFilter>().sharedMesh = crystal.GetComponent<MeshFilter>().sharedMesh;
                    vis.AddComponent<MeshRenderer>().sharedMaterials = crystal.GetComponent<MeshRenderer>().sharedMaterials;
                    Bounds mb = crystal.GetComponent<MeshFilter>().sharedMesh.bounds;
                    vis.transform.localPosition = -Vector3.Scale(mb.center, crystal.lossyScale);
                    BoxCollider bc = pick.AddComponent<BoxCollider>();
                    bc.size = Vector3.Scale(mb.size, crystal.lossyScale) + Vector3.one * 0.15f;
                    GameObject gl = new GameObject("Prism pickup glow");
                    gl.transform.SetParent(pick.transform, false);
                    Light l = gl.AddComponent<Light>();
                    l.type = LightType.Point; l.color = new Color(1f, 0.85f, 0.7f); l.range = 3f; l.intensity = 1.6f; l.shadows = LightShadows.None;
                    pick.AddComponent<PrismPickup>();
                    Log("prism pickup at " + pick.transform.position.ToString("F2"));
                }
                else Log("crystal not found, no pickup made");

                EditorSceneManager.MarkSceneDirty(s);
                EditorSceneManager.SaveScene(s);
                AssetDatabase.SaveAssets();
                Snap(new Vector3(83f, 0.2f, 0f), new Vector3(95f, -0.2f, 0f), "Backups/fix_pass_w.png", 75f);
                Snap(new Vector3(141f, 0.0f, 0f), new Vector3(129f, -0.2f, 0f), "Backups/fix_pass_e.png", 75f);
                Vector3 pp = root.TransformPoint(new Vector3(15.5f, 1.55f, -11f));
                Snap(pp + new Vector3(-2.2f, 0.6f, 2.2f), pp, "Backups/fix_pickup.png", 55f);
                Log("DONE");
            }
            catch (Exception e) { Log("FAILED " + e); }
            File.WriteAllText("Backups/cryptfix_report.txt", report.ToString());
        }

        static void Snap(Vector3 camPos, Vector3 target, string file, float fov)
        {
            GameObject go = new GameObject("SnapCam");
            go.hideFlags = HideFlags.HideAndDontSave;
            Camera cam = go.AddComponent<Camera>();
            cam.transform.position = camPos;
            cam.transform.LookAt(target);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            GameObject lg = new GameObject("SnapLamp");
            lg.hideFlags = HideFlags.HideAndDontSave;
            lg.transform.position = camPos;
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.range = 25f; l.intensity = 3f;
            RenderTexture rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(file, tex.EncodeToPNG());
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(lg);
        }
    }
}
