using UnityEngine;

namespace Assets.Scripts.System.Models
{
    public class PreloadedObstacle
    {
        public GameObject PreloadedPrefab { get; set; }

        public Sprite PreloadedSprite { get; set; }

        public Vector3 Position { get; set; }

        public string Tag { get; set; }

        public string SortingLayer { get; set; }
    }
}
