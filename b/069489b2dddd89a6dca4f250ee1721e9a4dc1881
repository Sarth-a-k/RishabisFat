using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class SocketDump
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_socketdump";

        static SocketDump()
        {
            EditorApplication.delayCall += Check;
        }

        static void Check()
        {
            if (!File.Exists(TriggerPath) || File.ReadAllText(TriggerPath).Trim() != "pending") return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Check; return; }
            File.WriteAllText(TriggerPath, "done");
            var sb = new StringBuilder();
            foreach (MirrorSocket s in Object.FindObjectsByType<MirrorSocket>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sb.AppendLine("SOCKET " + s.name + " pos " + s.transform.position + " parent " + (s.transform.parent ? s.transform.parent.name : "-"));
                foreach (Transform t in s.GetComponentsInChildren<Transform>(true))
                {
                    Renderer r = t.GetComponent<Renderer>();
                    Collider c = t.GetComponent<Collider>();
                    sb.AppendLine("  " + t.name + (r ? " R:" + r.GetType().Name + " " + r.bounds.center + " " + r.bounds.size : "") + (c ? " C:" + c.GetType().Name : ""));
                }
                if (s.transform.parent != null)
                    foreach (Transform t in s.transform.parent)
                    {
                        Renderer r = t.GetComponent<Renderer>();
                        if (r != null && (r.bounds.center - s.transform.position).magnitude < 2f) sb.AppendLine("  sibling " + t.name + " " + r.bounds.center + " " + r.bounds.size);
                    }
            }
            File.WriteAllText("Backups/socketdump.txt", sb.ToString());
        }
    }
}
