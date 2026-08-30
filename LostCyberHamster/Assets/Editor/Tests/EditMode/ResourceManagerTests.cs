using GameManagement;
using NUnit.Framework;
using Vues.GameCore;

namespace Assets.Tests.EditMode
{
    public sealed class ResourceManagerTests
    {
        private PlayerData _previousPlayerData;

        [SetUp]
        public void SetUp()
        {
            ResourceManager.OnDisable();
            _previousPlayerData = GameDataManager.PlayerData;
            GameDataManager.PlayerData = new PlayerData();
        }

        [TearDown]
        public void TearDown()
        {
            ResourceManager.OnDisable();
            GameDataManager.PlayerData = _previousPlayerData;
        }

        [TestCase(ResourceType.Coins)]
        [TestCase(ResourceType.Crystals)]
        public void AddResource_UpdatesExpectedBalance(ResourceType resourceType)
        {
            ResourceManager.AddResource(resourceType, 7);

            Assert.AreEqual(7, ResourceManager.GetCurrentBalance(resourceType));
        }

        [Test]
        public void GetCurrentBalance_ReflectsDirectPlayerDataMutation()
        {
            GameDataManager.PlayerData.Money = 11;
            GameDataManager.PlayerData.Crystals = 4;

            Assert.AreEqual(11, ResourceManager.GetCurrentBalance(ResourceType.Coins));
            Assert.AreEqual(4, ResourceManager.GetCurrentBalance(ResourceType.Crystals));
        }

        [TestCase(ResourceType.Coins)]
        [TestCase(ResourceType.Crystals)]
        public void SetResourceBalance_ReplacesExpectedBalance(ResourceType resourceType)
        {
            ResourceManager.AddResource(resourceType, 3);

            ResourceManager.SetResourceBalance(resourceType, 8);

            Assert.AreEqual(8, ResourceManager.GetCurrentBalance(resourceType));
        }

        [TestCase(ResourceType.Coins)]
        [TestCase(ResourceType.Crystals)]
        public void SpendResource_ValidatesAmountAndAvailableBalance(ResourceType resourceType)
        {
            ResourceManager.AddResource(resourceType, 10);

            Assert.IsTrue(ResourceManager.CanSpendResource(resourceType, 6));
            Assert.IsTrue(ResourceManager.SpendResource(resourceType, 6));
            Assert.AreEqual(4, ResourceManager.GetCurrentBalance(resourceType));
            Assert.IsFalse(ResourceManager.SpendResource(resourceType, 5));
            Assert.AreEqual(4, ResourceManager.GetCurrentBalance(resourceType));
        }

        [Test]
        public void InvalidOperations_DoNotChangeBalances()
        {
            ResourceManager.AddResource(ResourceType.Coins, 5);

            ResourceManager.AddResource(ResourceType.Coins, 0);
            ResourceManager.AddResource(ResourceType.Crystals, -1);
            ResourceManager.SetResourceBalance(ResourceType.Advertisement, 10);

            Assert.IsFalse(ResourceManager.CanSpendResource(ResourceType.Coins, 0));
            Assert.IsFalse(ResourceManager.SpendResource(ResourceType.Coins, -1));
            Assert.IsFalse(ResourceManager.SpendResource(ResourceType.Advertisement, 1));
            Assert.AreEqual(5, ResourceManager.GetCurrentBalance(ResourceType.Coins));
            Assert.AreEqual(0, ResourceManager.GetCurrentBalance(ResourceType.Crystals));
            Assert.AreEqual(0, ResourceManager.GetCurrentBalance(ResourceType.Advertisement));
        }

        [TestCase(ResourceType.Coins)]
        [TestCase(ResourceType.Crystals)]
        public void AddResource_RejectsOverflowWithoutChangingBalance(ResourceType resourceType)
        {
            Assert.IsTrue(ResourceManager.SetResourceBalance(resourceType, int.MaxValue));

            Assert.IsFalse(ResourceManager.AddResource(resourceType, 1));

            Assert.AreEqual(int.MaxValue, ResourceManager.GetCurrentBalance(resourceType));
        }

        [Test]
        public void CollectionSubscriptions_AreIdempotentAndRemovable()
        {
            ResourceManager.OnEnable();
            ResourceManager.OnEnable();

            GameEventsManager.CoinCollected(3);
            GameEventsManager.CrystallCollected(2);

            Assert.AreEqual(3, GameDataManager.PlayerData.Money);
            Assert.AreEqual(2, GameDataManager.PlayerData.Crystals);

            ResourceManager.OnDisable();
            GameEventsManager.CoinCollected(3);
            GameEventsManager.CrystallCollected(2);

            Assert.AreEqual(3, GameDataManager.PlayerData.Money);
            Assert.AreEqual(2, GameDataManager.PlayerData.Crystals);
        }

        [TestCase(ResourceType.Coins)]
        [TestCase(ResourceType.Crystals)]
        public void SuccessfulOperations_RaiseBalanceChangedOnce(
            ResourceType resourceType)
        {
            var eventCount = 0;
            var reportedType = ResourceType.Advertisement;
            var reportedBalance = -1;
            void OnBalanceChanged(ResourceType type, int balance)
            {
                eventCount++;
                reportedType = type;
                reportedBalance = balance;
            }

            ResourceManager.BalanceChanged += OnBalanceChanged;
            try
            {
                Assert.IsTrue(ResourceManager.AddResource(resourceType, 5));
                Assert.AreEqual(1, eventCount);
                Assert.AreEqual(resourceType, reportedType);
                Assert.AreEqual(5, reportedBalance);

                Assert.IsTrue(ResourceManager.SetResourceBalance(resourceType, 8));
                Assert.AreEqual(2, eventCount);
                Assert.AreEqual(8, reportedBalance);

                Assert.IsTrue(ResourceManager.SpendResource(resourceType, 3));
                Assert.AreEqual(3, eventCount);
                Assert.AreEqual(5, reportedBalance);
            }
            finally
            {
                ResourceManager.BalanceChanged -= OnBalanceChanged;
            }
        }

        [Test]
        public void RejectedOperations_DoNotRaiseBalanceChanged()
        {
            var eventCount = 0;
            void OnBalanceChanged(ResourceType resourceType, int balance)
            {
                eventCount++;
            }

            ResourceManager.BalanceChanged += OnBalanceChanged;
            try
            {
                Assert.IsFalse(ResourceManager.AddResource(ResourceType.Coins, 0));
                Assert.IsFalse(ResourceManager.SetResourceBalance(ResourceType.Coins, -1));
                Assert.IsFalse(ResourceManager.SpendResource(ResourceType.Coins, 1));
                Assert.IsFalse(ResourceManager.AddResource(ResourceType.Advertisement, 1));
                Assert.AreEqual(0, eventCount);
            }
            finally
            {
                ResourceManager.BalanceChanged -= OnBalanceChanged;
            }
        }
    }
}
