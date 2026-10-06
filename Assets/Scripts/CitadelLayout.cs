using UnityEngine;

namespace SunkenPrism
{
    /// <summary>Shared room footprints and the four passages of the eastbound expedition.</summary>
    public static class CitadelLayout
    {
        // Nexus, Sunken Prism, Ember Crypt, Violet Sanctum, Eclipse Keep.
        public static readonly Vector3[] Centers = {
            new Vector3(0, 0, 0), new Vector3(48.85f, 0, 0),
            new Vector3(112.15f, -2, 0), new Vector3(173.9f, 0, 0),
            new Vector3(237.15f, 2, 0)
        };
        public static readonly Vector2[] Sizes = {
            new Vector2(21.6f, 21.6f), new Vector2(48.1f, 36.4f),
            new Vector2(46.5f, 30), new Vector2(45, 45), new Vector2(49.5f, 48)
        };
        public static readonly float[] Scales = { .6f, .65f, .75f, .75f, .75f };
        public static readonly Vector3[] LegacyCenters = {
            new Vector3(0, 0, 0), new Vector3(-68, 0, 4),
            new Vector3(67, -2, 0), new Vector3(0, 0, -66), new Vector3(0, 2, 72)
        };

        public static Vector3 SpawnPosition { get { return new Vector3(0, .12f, 0); } }
        public const float SpawnYaw = 90f;
        public const float PassageWidth = 8f;

        /// <summary>Resize a legacy room footprint without changing floor or object heights.</summary>
        public static Vector3 Map(int room, Vector3 legacy)
        {
            Vector3 delta = legacy - LegacyCenters[room];
            return new Vector3(Centers[room].x + delta.x * Scales[room], legacy.y,
                Centers[room].z + delta.z * Scales[room]);
        }

        public static bool Contains(int room, Vector3 position)
        {
            Vector3 delta = position - Centers[room];
            float x = Mathf.Abs(delta.x), z = Mathf.Abs(delta.z);
            Vector2 half = Sizes[room] * .5f;
            if (x > half.x || z > half.y) return false;
            // Violet Sanctum keeps its octagonal footprint, including the clipped corners.
            return room != 3 || x + z <= half.x + half.y - 6.75f;
        }

        public static Vector3 PassageStart(int passage)
        {
            return Centers[passage] + Vector3.right * (Sizes[passage].x * .5f);
        }

        public static Vector3 PassageEnd(int passage)
        {
            int room = passage + 1;
            return Centers[room] - Vector3.right * (Sizes[room].x * .5f);
        }
    }
}
