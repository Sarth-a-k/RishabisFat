using UnityEngine;

namespace FPCharacter
{
    public static class ShapeColors
    {
        public static Color For(string shape)
        {
            switch (Key(shape))
            {
                case "oval": return new Color(1f, 0.72f, 0.28f, 1f);
                case "rectangle": return new Color(0.35f, 0.8f, 1f, 1f);
                case "circle": return new Color(0.82f, 0.45f, 1f, 1f);
                default: return new Color(1f, 0.85f, 0.6f, 1f);
            }
        }

        public static string Name(string shape)
        {
            switch (Key(shape))
            {
                case "oval": return "gold";
                case "rectangle": return "blue";
                case "circle": return "violet";
                default: return "glowing";
            }
        }

        static string Key(string shape)
        {
            return string.IsNullOrEmpty(shape) ? "" : shape.Trim().ToLowerInvariant();
        }
    }
}
