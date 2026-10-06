using System.Collections.Generic;
using UnityEngine;

namespace FPCharacter
{
    public static class PromptBox
    {
        public static Color background = new Color(1f, 1f, 1f, 0.55f);
        public static Color border = new Color(0f, 0f, 0f, 0.22f);
        public static Color textColor = new Color(0.08f, 0.08f, 0.1f, 1f);
        public static Color shadowColor = new Color(0f, 0f, 0f, 0f);
        public static Color keyColor = new Color(0.1f, 0.1f, 0.12f, 1f);
        public static Color keyTextColor = new Color(1f, 1f, 1f, 1f);
        public static float cornerRadius = 3f;
        static readonly List<Rect> placed = new List<Rect>();
        static int placedFrame = -1;
        static EventType placedEvent;

        static GUIStyle label, keyLabel;
        static readonly List<string> parts = new List<string>();
        static readonly List<bool> isKey = new List<bool>();
        static readonly List<float> widths = new List<float>();
        static string lastText;

        public static void Draw(string text, float yFraction)
        {
            if (string.IsNullOrEmpty(text)) return;
            Setup();
            int fs = Mathf.Max(14, Screen.height / 38);
            label.fontSize = fs;
            keyLabel.fontSize = Mathf.Max(11, Mathf.RoundToInt(fs * 0.78f));
            if (text != lastText) Parse(text);

            float keyD = fs * 1.45f;
            float padX = fs * 0.6f, padY = fs * 0.32f;
            float total = 0f;
            widths.Clear();
            for (int i = 0; i < parts.Count; i++)
            {
                float w = isKey[i] ? Mathf.Max(keyD, keyLabel.CalcSize(new GUIContent(parts[i])).x + fs * 0.9f) : label.CalcSize(new GUIContent(parts[i])).x;
                widths.Add(w);
                total += w;
                if (i > 0) total += Gap(i, fs);
            }
            bool startsWithKey = parts.Count > 0 && isKey[0];
            bool endsWithKey = parts.Count > 0 && isKey[parts.Count - 1];
            float leftPad = startsWithKey ? padX * 0.8f : fs * 0.9f;
            float rightPad = endsWithKey ? padX * 0.8f : fs * 0.9f;
            float h = keyD + padY * 2f;
            float wBox = total + leftPad + rightPad;
            var box = new Rect((Screen.width - wBox) * 0.5f, Screen.height * yFraction - h * 0.5f, wBox, h);
            if (Time.frameCount != placedFrame || Event.current.type != placedEvent)
            {
                placed.Clear();
                placedFrame = Time.frameCount;
                placedEvent = Event.current.type;
            }
            for (int guard = 0; guard < 6; guard++)
            {
                bool hit = false;
                foreach (Rect r in placed) if (r.Overlaps(box)) { box.y = r.yMax + fs * 0.35f; hit = true; }
                if (!hit) break;
            }
            placed.Add(box);
            float a = textColor.a;
            float radius = cornerRadius * Mathf.Max(1f, Screen.height / 720f);

            GUI.DrawTexture(new Rect(box.x - 3f, box.y - 2f, box.width + 6f, box.height + 6f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.22f * background.a), 0f, radius + 3f);
            GUI.DrawTexture(box, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, background, 0f, radius);
            GUI.DrawTexture(box, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, border, Mathf.Max(1f, Screen.height / 720f), radius);

            float x = box.x + leftPad;
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) x += Gap(i, fs);
                float w = widths[i];
                if (isKey[i])
                {
                    var k = new Rect(x, box.y + padY, w, keyD);
                    GUI.DrawTexture(k, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(keyColor.r, keyColor.g, keyColor.b, keyColor.a * a), 0f, radius);
                    keyLabel.normal.textColor = new Color(keyTextColor.r, keyTextColor.g, keyTextColor.b, keyTextColor.a * a);
                    GUI.Label(k, parts[i], keyLabel);
                }
                else
                {
                    var r = new Rect(x, box.y, w + 2f, box.height);
                    float o = Mathf.Max(1f, Screen.height / 720f);
                    label.normal.textColor = shadowColor;
                    GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), parts[i], label);
                    label.normal.textColor = textColor;
                    GUI.Label(r, parts[i], label);
                }
                x += w;
            }
        }

        static float Gap(int i, int fs)
        {
            bool prevKey = isKey[i - 1], curKey = isKey[i];
            if (prevKey && !curKey) return parts[i] == "/" ? fs * 0.35f : fs * 0.5f;
            if (!prevKey && curKey) return parts[i - 1] == "/" ? fs * 0.35f : fs * 1.1f;
            return fs * 0.4f;
        }

        static void Parse(string text)
        {
            lastText = text;
            parts.Clear();
            isKey.Clear();
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('[', i);
                int close = open >= 0 ? text.IndexOf(']', open) : -1;
                if (open < 0 || close < 0)
                {
                    AddText(text.Substring(i));
                    break;
                }
                AddText(text.Substring(i, open - i));
                parts.Add(text.Substring(open + 1, close - open - 1).Trim());
                isKey.Add(true);
                i = close + 1;
            }
        }

        static void AddText(string t)
        {
            t = t.Trim();
            if (t.StartsWith("-")) t = t.Substring(1).Trim();
            if (t.EndsWith("-")) t = t.Substring(0, t.Length - 1).Trim();
            while (t.Contains("  ")) t = t.Replace("  ", " ");
            if (t.Length == 0) return;
            parts.Add(t);
            isKey.Add(false);
        }

        static void Setup()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label);
            label.alignment = TextAnchor.MiddleLeft;
            label.fontStyle = FontStyle.Normal;
            label.wordWrap = false;
            label.padding = new RectOffset(0, 0, 0, 0);
            label.margin = new RectOffset(0, 0, 0, 0);
            keyLabel = new GUIStyle(GUI.skin.label);
            keyLabel.alignment = TextAnchor.MiddleCenter;
            keyLabel.fontStyle = FontStyle.Bold;
            keyLabel.wordWrap = false;
            keyLabel.padding = new RectOffset(0, 0, 0, 0);
            keyLabel.margin = new RectOffset(0, 0, 0, 0);
        }
    }
}
