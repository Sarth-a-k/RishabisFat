using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FPCharacter.EditorTools
{
    [InitializeOnLoad]
    static class RampProbe
    {
        const string TriggerPath = "Assets/Interaction/Editor/.run_rampprobe";
        const string ReportPath = "Backups/rampprobe_report.txt";

        static RampProbe()
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

        [MenuItem("Tools/Interaction/Probe Walkability (Back Again)")]
        static void Run()
        {
            var log = new StringBuilder();
            Physics.SyncTransforms();
            float step = 0.3f, slope = 45f;
            CharacterController cc = Object.FindAnyObjectByType<CharacterController>();
            if (cc != null) { step = cc.stepOffset; slope = cc.slopeLimit; log.AppendLine("player step " + step + " slope " + slope + " radius " + cc.radius + " height " + cc.height); }
            var flagged = new Dictionary<string, int>();
            var samples = new List<string>();
            for (float z = -20f; z <= 20.01f; z += 1f)
            {
                float prevY = float.NaN;
                string prevName = "";
                for (float x = 196f; x <= 264f; x += 0.2f)
                {
                    if (!Physics.Raycast(new Vector3(x, 5.5f, z), Vector3.down, out RaycastHit h, 10f, ~0, QueryTriggerInteraction.Ignore)) { prevY = float.NaN; continue; }
                    float y = h.point.y;
                    float ang = Vector3.Angle(h.normal, Vector3.up);
                    string n = Path(h.collider.transform);
                    if (!float.IsNaN(prevY))
                    {
                        float dy = y - prevY;
                        if (Mathf.Abs(dy) > step && Mathf.Abs(dy) < 1.2f)
                        {
                            string key = (dy > 0 ? n : prevName) + " | " + h.collider.GetType().Name;
                            flagged[key] = flagged.TryGetValue(key, out int c) ? c + 1 : 1;
                            if (samples.Count < 60) samples.Add("step " + dy.ToString("F2") + " at x " + x.ToString("F1") + " z " + z.ToString("F0") + " y " + y.ToString("F2") + " -> " + n);
                        }
                        else if (ang > slope && ang < 85f && Mathf.Abs(dy) > 0.01f)
                        {
                            string key = n + " | steep " + Mathf.RoundToInt(ang) + " deg";
                            flagged[key] = flagged.TryGetValue(key, out int c) ? c + 1 : 1;
                            if (samples.Count < 60) samples.Add("steep " + ang.ToString("F0") + " at x " + x.ToString("F1") + " z " + z.ToString("F0") + " y " + y.ToString("F2") + " -> " + n);
                        }
                    }
                    prevY = y;
                    prevName = n;
                }
            }
            log.AppendLine("== blockers (count) ==");
            var list = new List<KeyValuePair<string, int>>(flagged);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (var kv in list) log.AppendLine(kv.Value + "  " + kv.Key);
            log.AppendLine("== samples ==");
            foreach (string s in samples) log.AppendLine(s);
            log.AppendLine("== dais pieces ==");
            foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                string nm = c.name;
                if (!(nm.Contains("dais") || nm.Contains("Dais") || nm.Contains("stair") || nm.Contains("Stair") || nm.Contains("ramp") || nm.Contains("Ramp") || nm.Contains("step") || nm.Contains("Step"))) continue;
                if (c.bounds.center.x < 196f) continue;
                log.AppendLine(Path(c.transform) + " " + c.GetType().Name + " bounds min " + c.bounds.min.ToString("F2") + " max " + c.bounds.max.ToString("F2") + " rot " + c.transform.eulerAngles.ToString("F0"));
            }
            for (float zz = -12f; zz <= 12.01f; zz += 4f)
            {
                var pr = new StringBuilder("profile z=" + zz + ": ");
                for (float x = 226f; x <= 262f; x += 0.5f)
                    if (Physics.Raycast(new Vector3(x, 5.5f, zz), Vector3.down, out RaycastHit h2, 10f, ~0, QueryTriggerInteraction.Ignore)) pr.Append(x.ToString("F1") + ":" + h2.point.y.ToString("F2") + " ");
                log.AppendLine(pr.ToString());
            }
            log.AppendLine("== height profile z=0 ==");
            var prof = new StringBuilder();
            for (float x = 196f; x <= 264f; x += 1f)
                if (Physics.Raycast(new Vector3(x, 8f, 0f), Vector3.down, out RaycastHit h, 14f, ~0, QueryTriggerInteraction.Ignore)) prof.Append(x.ToString("F0") + ":" + h.point.y.ToString("F2") + " ");
            log.AppendLine(prof.ToString());
            log.AppendLine("DONE");
            File.WriteAllText(ReportPath, log.ToString());
        }

        static string Path(Transform t)
        {
            string p = t.name;
            int guard = 0;
            while (t.parent != null && guard++ < 3) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
