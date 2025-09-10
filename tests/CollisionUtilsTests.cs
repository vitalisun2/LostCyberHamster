using NUnit.Framework;

namespace CollisionUtilsTests
{
    // Минимальные заглушки вместо Unity-классов
    internal class Transform
    {
        public float positionX;
    }

    internal class Obstacle
    {
        public float Left;
        public float Right;
    }

    internal static class CollisionUtils
    {
        public static bool IsHamsterCenterInsideObstacleAtShift(
            Transform hamster,
            float shift,
            Obstacle obstacle,
            float rightTol = 0f)
        {
            float centerX = hamster.positionX + shift;
            float leftBorder = obstacle.Left;
            float rightBorder = obstacle.Right + rightTol;
            return centerX >= leftBorder && centerX <= rightBorder;
        }
    }

    public class Tests
    {
        [Test]
        public void CenterWithinRightTolerance_IsInside()
        {
            var hamster = new Transform { positionX = 0f };
            var obstacle = new Obstacle { Left = -1f, Right = 0f };
            bool inside = CollisionUtils.IsHamsterCenterInsideObstacleAtShift(
                hamster,
                0.9f,
                obstacle,
                0.2f);
            Assert.IsTrue(inside);
        }

        [Test]
        public void CenterBeyondRightTolerance_IsOutside()
        {
            var hamster = new Transform { positionX = 0f };
            var obstacle = new Obstacle { Left = -1f, Right = 0f };
            bool inside = CollisionUtils.IsHamsterCenterInsideObstacleAtShift(
                hamster,
                1.3f,
                obstacle,
                0.2f);
            Assert.IsFalse(inside);
        }
    }
}

