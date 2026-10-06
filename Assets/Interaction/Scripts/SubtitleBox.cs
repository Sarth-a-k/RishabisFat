using UnityEngine;

namespace FPCharacter
{
    public static class SubtitleBox
    {
        public const string Player = "Julian";
        public static Color panel = new Color(0.03f, 0.045f, 0.04f, 0.72f);
        public static Color rim = new Color(1f, 1f, 1f, 0.1f);
        public static Color textColor = new Color(1f, 1f, 1f, 1f);
        public static Color nameColor = new Color(0.86f, 0.86f, 0.82f, 1f);
        public static float yCenter = 0.8f;

        static GUIStyle body, nameStyle;
        static readonly System.Collections.Generic.List<PromptBox.Candidate> candidates = new System.Collections.Generic.List<PromptBox.Candidate>();
        static int candidateFrame = -1, winnerFrame = -1, drawnFrame = -1;
        static string winner;
        static Texture2D soft;

        public static void Draw(string text, float alpha = 1f)
        {
            Draw(Player, text, alpha);
        }

        public static void Draw(string speaker, string text, float alpha)
        {
            if (string.IsNullOrEmpty(text) || alpha <= 0f || PromptBox.Hidden) return;
            if (!PromptBox.Claim(text, 0, ref candidateFrame, candidates, ref winnerFrame, ref winner, ref drawnFrame)) return;
            text = Clean(text);
            Setup();
            int fs = Mathf.Max(15, Screen.height / 30);
            body.fontSize = fs;
            nameStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(fs * 0.68f));
            float maxW = Mathf.Min(Screen.width * 0.86f, Screen.height * 1.05f);
            float padX = fs * 2.2f, padY = fs * 0.85f;
            Vector2 one = body.CalcSize(new GUIContent(text));
            float textW = Mathf.Min(one.x + 2f, maxW - padX * 2f);
            float textH = body.CalcHeight(new GUIContent(text), textW);
            float w = Mathf.Max(textW + padX * 2f, Screen.height * 0.52f);
            float h = textH + padY * 2f;
            var box = new Rect((Screen.width - w) * 0.5f, Screen.height * yCenter - h * 0.5f, w, h);

            Color old = GUI.color;
            float radius = Mathf.Min(h * 0.5f, fs * 1.6f);
            GUI.DrawTexture(new Rect(box.x - fs * 0.5f, box.y - fs * 0.4f, box.width + fs, box.height + fs * 0.8f), soft, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.28f * alpha), 0f, radius + fs * 0.5f);
            GUI.DrawTexture(box, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(panel.r, panel.g, panel.b, panel.a * alpha), 0f, radius);
            GUI.DrawTexture(box, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(rim.r, rim.g, rim.b, rim.a * alpha), Mathf.Max(1f, Screen.height / 720f), radius);

            if (!string.IsNullOrEmpty(speaker))
            {
                var nc = new GUIContent(speaker);
                Vector2 ns = nameStyle.CalcSize(nc);
                var nr = new Rect(box.x + radius * 0.55f, box.y - ns.y * 0.62f, ns.x + 4f, ns.y);
                Shadowed(nr, nc, nameStyle, new Color(nameColor.r, nameColor.g, nameColor.b, nameColor.a * alpha), alpha);
            }
            var tr = new Rect(box.x + (box.width - textW) * 0.5f, box.y + padY, textW, textH);
            Shadowed(tr, new GUIContent(text), body, new Color(textColor.r, textColor.g, textColor.b, textColor.a * alpha), alpha);
            GUI.color = old;
        }

        public static float Fade(float shownAt, float until, float fade = 0.3f)
        {
            float now = Time.time;
            return Mathf.Clamp01(Mathf.Min((now - shownAt) / fade, (until - now) / fade));
        }

        static void Shadowed(Rect r, GUIContent c, GUIStyle s, Color col, float alpha)
        {
            float o = Mathf.Max(1f, Screen.height / 540f);
            s.normal.textColor = new Color(0f, 0f, 0f, 0.7f * alpha);
            GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), c, s);
            s.normal.textColor = col;
            GUI.Label(r, c, s);
        }

        static string Clean(string t)
        {
            t = t.Trim();
            if (t.Length >= 2 && t[0] == '"' && t[t.Length - 1] == '"') t = t.Substring(1, t.Length - 2);
            return t;
        }

        static void Setup()
        {
            if (body == null)
            {
                body = new GUIStyle(GUI.skin.label);
                body.alignment = TextAnchor.UpperLeft;
                body.fontStyle = FontStyle.Italic;
                body.wordWrap = true;
                body.richText = false;
                body.padding = new RectOffset(0, 0, 0, 0);
                body.margin = new RectOffset(0, 0, 0, 0);
                nameStyle = new GUIStyle(GUI.skin.label);
                nameStyle.alignment = TextAnchor.MiddleLeft;
                nameStyle.fontStyle = FontStyle.Normal;
                nameStyle.wordWrap = false;
                nameStyle.padding = new RectOffset(0, 0, 0, 0);
            }
            if (soft == null)
            {
                const int N = 64;
                soft = new Texture2D(N, N, TextureFormat.RGBA32, false);
                soft.wrapMode = TextureWrapMode.Clamp;
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = Mathf.Abs(x - (N - 1) * 0.5f) / (N * 0.5f), dy = Mathf.Abs(y - (N - 1) * 0.5f) / (N * 0.5f);
                        float e = 1f - Mathf.Clamp01((Mathf.Max(dx, dy) - 0.55f) / 0.45f);
                        px[y * N + x] = new Color(1f, 1f, 1f, e * e);
                    }
                soft.SetPixels32(px);
                soft.Apply();
                soft.hideFlags = HideFlags.DontSave;
            }
        }
    }
}
