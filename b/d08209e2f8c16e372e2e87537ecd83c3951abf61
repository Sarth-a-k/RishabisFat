using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class SceneDump
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_dump";
        static readonly Regex Interest = new Regex("Mirror|PuzzleTorch|Prism|prism|altar|moon|Moon|seal|Seal|rune|Rune|LUNAR|Ember gate|BeamTarget|Ghost_|Player|EMBER|Ember Crypt", RegexOptions.Compiled);

        static SceneDump()
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

        static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        [MenuItem("Tools/Interaction/Dump Scene Info")]
        static void Run()
        {
            var sb = new StringBuilder();
            try
            {
                Scene s = SceneManager.GetActiveScene();
                sb.AppendLine("SCENE " + s.path + " dirty=" + s.isDirty);
                foreach (GameObject root in s.GetRootGameObjects())
                {
                    sb.AppendLine("ROOT " + root.name + " active=" + root.activeSelf + " pos=" + root.transform.position.ToString("F2"));
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (!Interest.IsMatch(t.name)) continue;
                        if (t.name.Contains("supplied design")) continue;
                        string comps = string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
                        Renderer r = t.GetComponent<Renderer>();
                        string bounds = r != null ? " bounds=" + r.bounds.center.ToString("F2") + "/" + r.bounds.size.ToString("F2") : "";
                        string mats = r != null ? " mats=" + string.Join("|", r.sharedMaterials.Select(m => m != null ? m.name + "(" + m.shader.name + ")" : "null")) : "";
                        sb.AppendLine("  " + PathOf(t) + " active=" + t.gameObject.activeInHierarchy + " pos=" + t.position.ToString("F3") + " rot=" + t.eulerAngles.ToString("F1") + " scale=" + t.lossyScale.ToString("F2") + " [" + comps + "]" + bounds + mats);
                    }
                }
                foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (mb == null) continue;
                    string n = mb.GetType().Name;
                    if (n == "PuzzleTorch" || n == "MirrorPickup")
                        sb.AppendLine("COMP " + n + " on " + PathOf(mb.transform) + " json=" + EditorJsonUtility.ToJson(mb));
                }
            }
            catch (Exception e)
            {
                sb.AppendLine("FAILED " + e);
            }
            Directory.CreateDirectory("Backups");
            File.WriteAllText("Backups/scene_dump.txt", sb.ToString());
        }
    }
}
