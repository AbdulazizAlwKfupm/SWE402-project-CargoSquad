using UnityEngine;

namespace CargoSquad
{
    public readonly struct GameplayNoise
    {
        public Vector3 Position { get; }
        public float Radius { get; }

        public GameplayNoise(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }
}
