using UnityEngine;

namespace EXFIL.Utils
{
    /// <summary>
    /// Helpers for the 3/4 top-down (dimetric) projection used by the whole game.
    /// World X/Z-like movement is done on the XY plane, Y is the "depth" axis and is
    /// added to the sorting order so sprites overlap correctly.
    /// </summary>
    public static class IsoUtils
    {
        public const float IsoYSquash = 0.57735f; // ~tan(30°) classic dimetric factor

        /// <summary>Screen/world input vector -> world movement vector with depth squashing.</summary>
        public static Vector2 ToIso(Vector2 input)
        {
            return new Vector2(input.x, input.y * IsoYSquash);
        }

        /// <summary>World movement vector -> flat input vector.</summary>
        public static Vector2 FromIso(Vector2 iso)
        {
            return new Vector2(iso.x, iso.y / IsoYSquash);
        }

        /// <summary>Converts a direction vector to one of the 8 facings.</summary>
        public static Core.Facing8 DirectionToFacing(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return Core.Facing8.S;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // -180..180, 0 = east
            if (angle < 0f) angle += 360f;

            // 8 sectors starting at East, going counter-clockwise
            int sector = Mathf.RoundToInt(angle / 45f) % 8;
            switch (sector)
            {
                case 0: return Core.Facing8.E;
                case 1: return Core.Facing8.NE;
                case 2: return Core.Facing8.N;
                case 3: return Core.Facing8.NW;
                case 4: return Core.Facing8.W;
                case 5: return Core.Facing8.SW;
                case 6: return Core.Facing8.S;
                default: return Core.Facing8.SE;
            }
        }

        /// <summary>Unit vector for a facing (in flat screen space, before iso squash).</summary>
        public static Vector2 FacingToVector(Core.Facing8 facing)
        {
            switch (facing)
            {
                case Core.Facing8.E: return new Vector2(1f, 0f);
                case Core.Facing8.NE: return new Vector2(0.7071f, 0.7071f);
                case Core.Facing8.N: return new Vector2(0f, 1f);
                case Core.Facing8.NW: return new Vector2(-0.7071f, 0.7071f);
                case Core.Facing8.W: return new Vector2(-1f, 0f);
                case Core.Facing8.SW: return new Vector2(-0.7071f, -0.7071f);
                case Core.Facing8.S: return new Vector2(0f, -1f);
                default: return new Vector2(0.7071f, -0.7071f);
            }
        }

        /// <summary>True when the facing looks away from the camera (back turned).</summary>
        public static bool IsBackFacing(Core.Facing8 facing)
        {
            return facing == Core.Facing8.N || facing == Core.Facing8.NE || facing == Core.Facing8.NW;
        }

        /// <summary>Y angle in degrees used to rotate held weapons / aim pivots.</summary>
        public static float FacingToAngle(Core.Facing8 facing)
        {
            Vector2 v = FacingToVector(facing);
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        /// <summary>Stable sorting order from world Y (higher Y = further away = drawn first).</summary>
        public static int SortingFromY(float worldY, int baseOrder = 0, float scale = 1000f)
        {
            return baseOrder - Mathf.RoundToInt(worldY * scale);
        }
    }
}
