using System;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class DecorationTile
    {
        public string name;
        public int xPos;
        public int yPos;

        /// <summary>
        /// Z-rotation in degrees.
        /// </summary>
        public float rotation;

        /// <summary>
        /// Scale along X axis. Defaults to 1.
        /// </summary>
        public float scaleX = 1f;

        /// <summary>
        /// Scale along Y axis. Defaults to 1.
        /// </summary>
        public float scaleY = 1f;
    }
}
