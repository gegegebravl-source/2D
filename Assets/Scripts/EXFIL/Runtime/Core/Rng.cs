using System;
using UnityEngine;

namespace EXFIL.Core
{
    /// <summary>Seeded deterministic RNG (xorshift) so raids, loot and bot spawns are reproducible.</summary>
    public sealed class Rng
    {
        private uint _state;

        public Rng(int seed)
        {
            _state = seed == 0 ? 0x9E3779B9u : (uint)seed;
            if (_state == 0) _state = 0x9E3779B9u;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        public float NextFloat()
        {
            return NextUInt() / 4294967295f;
        }

        public float Range(float min, float max)
        {
            return min + NextFloat() * (max - min);
        }

        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextUInt() % (uint)(maxExclusive - min));
        }

        public bool Chance(float probability)
        {
            return NextFloat() < probability;
        }

        public T Pick<T>(System.Collections.Generic.IList<T> list)
        {
            if (list == null || list.Count == 0) return default(T);
            return list[Range(0, list.Count)];
        }

        public UnityEngine.Vector2 InsideUnitCircle()
        {
            float angle = NextFloat() * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(NextFloat());
            return new UnityEngine.Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }
    }
}
