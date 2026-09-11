using Assets.Scripts.Common;
using NUnit.Framework;

namespace Assets.Tests.EditMode
{
    public class CollisionUtilsOverlapTests
    {
        [Test]
        public void IsOverlapWithInset_IgnoresEdgeContactInsideForgiveness()
        {
            bool result = CollisionUtils.IsOverlapWithInset(
                0f,
                1f,
                0.96f,
                1.4f,
                0.05f);

            Assert.IsFalse(result);
        }

        [Test]
        public void IsOverlapWithInset_ReportsContactPastForgiveness()
        {
            bool result = CollisionUtils.IsOverlapWithInset(
                0f,
                1f,
                0.94f,
                1.4f,
                0.05f);

            Assert.IsTrue(result);
        }
    }
}