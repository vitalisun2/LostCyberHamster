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
                0.985f,
                1.4f,
                0.02f);

            Assert.IsFalse(result);
        }

        [Test]
        public void IsOverlapWithInset_ReportsContactPastForgiveness()
        {
            bool result = CollisionUtils.IsOverlapWithInset(
                0f,
                1f,
                0.975f,
                1.4f,
                0.02f);

            Assert.IsTrue(result);
        }
    }
}