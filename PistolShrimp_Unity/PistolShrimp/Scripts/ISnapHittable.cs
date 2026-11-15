using UnityEngine;

namespace PistolShrimp
{
    /// <summary>
    /// What a snap tells the thing it hit. This is the whole contract between the
    /// shrimp and the world: put ISnapHittable on anything that should react.
    /// </summary>
    public struct SnapHit
    {
        public Vector3 point;       // where the cavitation bubble collapsed
        public Vector3 normal;      // surface normal at the hit, or reverse travel direction in open water
        public Vector3 direction;   // travel direction of the water jet
        public float strength;      // 1 for a direct hit, falls off with distance for shockwave-only hits
        public bool direct;         // true if the jet touched this object, false if only the shockwave reached it
        public GameObject source;   // the shrimp that fired
    }

    public interface ISnapHittable
    {
        void OnSnapHit(SnapHit hit);
    }
}
