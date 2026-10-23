using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ZeroToHero.SaveTests
{
    public class ServiceEconomyIntegrationTests
    {
        private Type _playerType;
        private Type _economyType;
        private Type _assetServiceType;
        private Type _needsServiceType;
        private Type _businessServiceType;
        private Type _timeType;
        private object _player;
        private object _economy;
        private object _assets;
        private object _time;
        private long _now;

        [SetUp]
        public void SetUp()
        {
            Assembly game = AppDomain.CurrentDomain.GetAssemblies()
                .First(assembly => assembly.GetName().Name == "Assembly-CSharp");
            _playerType = game.GetType("ZeroToHero.Player.PlayerData", true);
            _economyType = game.GetType("ZeroToHero.Core.EconomyService", true);
            _assetServiceType = game.GetType("ZeroToHero.Assets.AssetService", true);
            _needsServiceType = game.GetType("ZeroToHero.Player.NeedsService", true);
            _businessServiceType = game.GetType("ZeroToHero.Business.BusinessService", true);
            _timeType = game.GetType("ZeroToHero.Core.TimeManager", true);
            _player = Activator.CreateInstance(_playerType);
            _now = DateTime.UtcNow.Ticks;
            RebuildEconomy();
            RebuildTime();
        }

        [Test]
        public void BusinessPurchaseAndIncome_AreJournaledAndPersistTogether()
        {
            SetField(_player, "money", 1000);
            object business = CreateBusinessService();
            Assert.That(Invoke<bool>(business, "BuyBusiness", "biz_coffee"), Is.True);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(0));
            AssertTransaction(0, "expense", -1000, 1000, 0, "Покупка бизнеса: Кофейный автомат");

            AdvanceBusinessTime(10);
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.EqualTo(150));
            Assert.That(Field<int>(_player, "money"), Is.Zero);
            Assert.That(Invoke<bool>(business, "CollectIncome", "biz_coffee"), Is.True);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(150));
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.Zero);
            AssertTransaction(1, "income", 150, 0, 150, "Доход от бизнеса: Кофейный автомат");
            Assert.That(Transactions.Count, Is.EqualTo(2));

            object loaded = SaveAndLoad(_player);
            Assert.That(Field<int>(loaded, "money"), Is.EqualTo(150));
            Assert.That(TransactionsOf(loaded).Count, Is.EqualTo(2));
            Assert.That(BusinessField<bool>(loaded, "isPurchased"), Is.True);
            Assert.That(BusinessField<int>(loaded, "collectCount"), Is.EqualTo(1));
            Assert.That(BusinessField<int>(loaded, "accumulatedIncome"), Is.Zero);
            Assert.That(BusinessField<long>(loaded, "nextIncomeUtcTicks"), Is.GreaterThan(_now));
            AssertTransaction(TransactionsOf(loaded), 0, "expense", -1000, 1000, 0);
            AssertTransaction(TransactionsOf(loaded), 1, "income", 150, 0, 150);

            _player = loaded;
            RebuildEconomy();
            RebuildTime();
            business = CreateBusinessService();
            Assert.That(Invoke<bool>(business, "CollectIncome", "biz_coffee"), Is.False);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(150));
            Assert.That(Transactions.Count, Is.EqualTo(2));
        }

        [Test]
        public void AssetPurchaseAndNeedsSpending_AreJournaledAndPersistTogether()
        {
            SetField(_player, "money", 1000);
            SetField(_player, "health", 40);
            SetField(_player, "mood", 30);
            object assets = Activator.CreateInstance(_assetServiceType, _player, _economy);
            object needs = Activator.CreateInstance(_needsServiceType, _player, _economy);

            Assert.That(Invoke<bool>(assets, "BuyAsset", "house_hostel"), Is.True);
            AssertTransaction(0, "expense", -200, 1000, 800, "Покупка имущества: Койко-место");
            Assert.That(Invoke<bool>(needs, "Heal", 20, 100), Is.True);
            AssertTransaction(1, "expense", -100, 800, 700, "Восстановление здоровья");
            Assert.That(Invoke<bool>(needs, "Rest", 20, 50), Is.True);
            AssertTransaction(2, "expense", -50, 700, 650, "Отдых / Развлечения");
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(650));
            Assert.That(Transactions.Count, Is.EqualTo(3));

            object loaded = SaveAndLoad(_player);
            Assert.That(Field<int>(loaded, "money"), Is.EqualTo(650));
            Assert.That(TransactionsOf(loaded).Count, Is.EqualTo(3));
            AssertTransaction(TransactionsOf(loaded), 0, "expense", -200, 1000, 800);
            AssertTransaction(TransactionsOf(loaded), 1, "expense", -100, 800, 700);
            AssertTransaction(TransactionsOf(loaded), 2, "expense", -50, 700, 650);
        }

        [Test]
        public void RejectedAssetAndNeedsOperations_DoNotClaimSuccessOrRecordSpending()
        {
            SetField(_player, "money", 25);
            SetField(_player, "health", 50);
            SetField(_player, "mood", 50);
            object assets = Activator.CreateInstance(_assetServiceType, _player, _economy);
            object needs = Activator.CreateInstance(_needsServiceType, _player, _economy);

            Assert.That(Invoke<bool>(assets, "BuyAsset", "house_hostel"), Is.False);
            Assert.That(Invoke<bool>(needs, "Heal", 20, 100), Is.False);
            Assert.That(Invoke<bool>(needs, "Rest", 20, 50), Is.False);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(25));
            Assert.That(Field<int>(_player, "health"), Is.EqualTo(50));
            Assert.That(Field<int>(_player, "mood"), Is.EqualTo(50));
            Assert.That(Transactions, Is.Empty);
        }

        [Test]
        public void BusinessIncomeOverflow_DoesNotCountCollectionOrAddTransaction()
        {
            SetField(_player, "money", int.MaxValue);
            IList businesses = (IList)_playerType.GetField("businesses").GetValue(_player);
            Type businessDataType = _playerType.Assembly.GetType("ZeroToHero.Business.BusinessData", true);
            object data = Activator.CreateInstance(businessDataType);
            businessDataType.GetField("id").SetValue(data, "biz_coffee");
            businessDataType.GetField("isPurchased").SetValue(data, true);
            businessDataType.GetField("accumulatedIncome").SetValue(data, 150);
            businesses.Add(data);

            object business = CreateBusinessService();

            Assert.That(Invoke<bool>(business, "CollectIncome", "biz_coffee"), Is.False);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(int.MaxValue));
            Assert.That((int)businessDataType.GetField("collectCount").GetValue(data), Is.EqualTo(0));
            Assert.That(Transactions, Is.Empty);
        }

        [Test]
        public void BusinessDoesNotAccrueBeforePurchase_AndPurchaseIsChargedOnlyOnce()
        {
            SetField(_player, "money", 2000);
            object business = CreateBusinessService();
            InvokeRaw(business, "GetBusinessData", "biz_coffee");
            AdvanceBusinessTime(100);
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.Zero);

            Assert.That(Invoke<bool>(business, "BuyBusiness", "biz_coffee"), Is.True);
            Assert.That(Invoke<bool>(business, "BuyBusiness", "biz_coffee"), Is.False);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(1000));
            Assert.That(Transactions.Count, Is.EqualTo(1));
            AssertTransaction(0, "expense", -1000, 2000, 1000, "Покупка бизнеса: Кофейный автомат");
        }

        [Test]
        public void BusinessPurchaseWithInsufficientBalance_IsRejectedWithoutTransaction()
        {
            SetField(_player, "money", 999);
            object business = CreateBusinessService();

            Assert.That(Invoke<bool>(business, "BuyBusiness", "biz_coffee"), Is.False);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(999));
            Assert.That(Transactions, Is.Empty);
            Assert.That(BusinessField<bool>(_player, "isPurchased"), Is.False);
        }

        [Test]
        public void BusinessAccruesOnlyCompleteTenSecondCycles_AndProcessesMultipleCycles()
        {
            SetField(_player, "money", 1000);
            object business = CreateBusinessService();
            Invoke<bool>(business, "BuyBusiness", "biz_coffee");
            AdvanceBusinessTime(9);
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.Zero);
            Assert.That(Invoke<bool>(business, "CollectIncome", "biz_coffee"), Is.False);
            AdvanceBusinessTime(1);
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.EqualTo(150));
            AdvanceBusinessTime(20);
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.EqualTo(450));
        }

        [Test]
        public void BusinessAccrualStopsAtCap_AndAddsOnlyAvailableCapacity()
        {
            SetField(_player, "money", 1000);
            object business = CreateBusinessService();
            Invoke<bool>(business, "BuyBusiness", "biz_coffee");
            AdvanceBusinessTime(1000);
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.EqualTo(1500));
            Assert.That(BusinessField<long>(_player, "nextIncomeUtcTicks"), Is.Zero);
            AdvanceBusinessTime(500);
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.EqualTo(1500));

            SetBusinessField(_player, "accumulatedIncome", 1450);
            SetBusinessField(_player, "nextIncomeUtcTicks", _now + TimeSpan.FromSeconds(10).Ticks);
            AdvanceBusinessTime(10);
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.EqualTo(1500));
        }

        [Test]
        public void BusinessCollectionTransfersAccumulationOnce_AndRestartsCycle()
        {
            SetField(_player, "money", 2000);
            object business = CreateBusinessService();
            Invoke<bool>(business, "BuyBusiness", "biz_coffee");
            AdvanceBusinessTime(20);
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.EqualTo(300));
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(1000));

            Assert.That(Invoke<bool>(business, "CollectIncome", "biz_coffee"), Is.True);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(1300));
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.Zero);
            AssertTransaction(1, "income", 300, 1000, 1300, "Доход от бизнеса: Кофейный автомат");
            Assert.That(Invoke<bool>(business, "CollectIncome", "biz_coffee"), Is.False);
            Assert.That(Transactions.Count, Is.EqualTo(2));

            AdvanceBusinessTime(9);
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.Zero);
            AdvanceBusinessTime(1);
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.EqualTo(150));
        }

        [Test]
        public void BusinessAccumulationAndDeadlineSurviveReload_OfflineCyclesAreNotRepeated()
        {
            SetField(_player, "money", 1000);
            object business = CreateBusinessService();
            Invoke<bool>(business, "BuyBusiness", "biz_coffee");
            AdvanceBusinessTime(4);
            _player = SaveAndLoad(_player);
            RebuildEconomy();
            RebuildTime();
            business = CreateBusinessService();
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.Zero);
            Assert.That(Invoke<float>(business, "GetRemainingCycleSeconds", "biz_coffee"), Is.EqualTo(6).Within(0.01));

            AdvanceClockOnly(31);
            InvokeRaw(business, "UpdateTimer", 0f);
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.EqualTo(450));
            long processedDeadline = BusinessField<long>(_player, "nextIncomeUtcTicks");
            InvokeRaw(business, "UpdateTimer", 0f);
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.EqualTo(450));
            Assert.That(BusinessField<long>(_player, "nextIncomeUtcTicks"), Is.EqualTo(processedDeadline));

            _player = SaveAndLoad(_player);
            RebuildEconomy();
            RebuildTime();
            business = CreateBusinessService();
            InvokeRaw(business, "UpdateTimer", 0f);
            Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.EqualTo(450));
        }

        [Test]
        public void SchemaV2BusinessWithoutAccrualFields_LoadsExistingProgress()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ZeroToHero_BusinessLegacy_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "save.json");
                File.WriteAllText(path,
                    "{\"schemaVersion\":2,\"playerData\":{\"money\":123,\"day\":7,\"month\":2," +
                    "\"businesses\":[{\"id\":\"biz_coffee\",\"isPurchased\":true,\"stars\":1," +
                    "\"collectCount\":5,\"hasManager\":false}],\"assets\":{}}}");
                Type saveType = _playerType.Assembly.GetType("ZeroToHero.Core.SaveService", true);
                object save = Activator.CreateInstance(saveType, new object[] { path });
                object result = saveType.GetMethod("Load").Invoke(save, null);
                object loaded = Property<object>(result, "Data");

                Assert.That(Property<bool>(result, "Success"), Is.True);
                Assert.That(Field<int>(loaded, "money"), Is.EqualTo(123));
                Assert.That(Field<int>(loaded, "day"), Is.EqualTo(7));
                Assert.That(BusinessField<bool>(loaded, "isPurchased"), Is.True);
                Assert.That(BusinessField<int>(loaded, "collectCount"), Is.EqualTo(5));
                Assert.That(BusinessField<int>(loaded, "accumulatedIncome"), Is.Zero);
                Assert.That(BusinessField<long>(loaded, "nextIncomeUtcTicks"), Is.Zero);

                _player = loaded;
                RebuildEconomy();
                RebuildTime();
                object business = CreateBusinessService();
                InvokeRaw(business, "UpdateTimer", 0f);
                Assert.That(BusinessField<int>(_player, "accumulatedIncome"), Is.Zero);
                Assert.That(BusinessField<long>(_player, "nextIncomeUtcTicks"),
                    Is.EqualTo(_now + TimeSpan.FromSeconds(10).Ticks));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private object SaveAndLoad(object player)
        {
            string directory = Path.Combine(Path.GetTempPath(), "ZeroToHero_ServiceEconomy_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                Type saveType = _playerType.Assembly.GetType("ZeroToHero.Core.SaveService", true);
                object save = Activator.CreateInstance(saveType, new object[] { Path.Combine(directory, "save.json") });
                object saveResult = saveType.GetMethod("Save").Invoke(save, new[] { player });
                Assert.That(Property<bool>(saveResult, "Success"), Is.True, Property<string>(saveResult, "Error"));
                object loadResult = saveType.GetMethod("Load").Invoke(save, null);
                Assert.That(Property<bool>(loadResult, "Success"), Is.True, Property<string>(loadResult, "Error"));
                return Property<object>(loadResult, "Data");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private T BusinessField<T>(object player, string field)
        {
            IList businesses = (IList)_playerType.GetField("businesses").GetValue(player);
            return (T)businesses[0].GetType().GetField(field).GetValue(businesses[0]);
        }

        private void AssertTransaction(int index, string type, int amount, int before, int after, string source)
        {
            AssertTransaction(Transactions, index, type, amount, before, after);
            object item = Transactions[index];
            Assert.That(Field<string>(item, "source"), Is.EqualTo(source));
            Assert.That(Field<string>(item, "id"), Is.Not.Empty);
            Assert.That(Field<long>(item, "timestampUtcTicks"), Is.GreaterThan(0));
        }

        private static void AssertTransaction(IList transactions, int index, string type,
            int amount, int before, int after)
        {
            object item = transactions[index];
            Assert.That(Field<string>(item, "type"), Is.EqualTo(type));
            Assert.That(Field<int>(item, "amount"), Is.EqualTo(amount));
            Assert.That(Field<int>(item, "balanceBefore"), Is.EqualTo(before));
            Assert.That(Field<int>(item, "balanceAfter"), Is.EqualTo(after));
            Assert.That(Field<string>(item, "id"), Is.Not.Empty);
            Assert.That(Field<long>(item, "timestampUtcTicks"), Is.GreaterThan(0));
        }

        private static object InvokeRaw(object target, string method, params object[] arguments)
            => target.GetType().GetMethod(method).Invoke(target, arguments);

        private static T Invoke<T>(object target, string method, params object[] arguments)
            => (T)InvokeRaw(target, method, arguments);

        private static T Field<T>(object target, string name)
            => (T)target.GetType().GetField(name).GetValue(target);

        private static void SetField(object target, string name, object value)
            => target.GetType().GetField(name).SetValue(target, value);

        private static T Property<T>(object target, string name)
            => (T)target.GetType().GetProperty(name).GetValue(target);

        private IList Transactions => (IList)_playerType.GetField("transactions").GetValue(_player);

        private static IList TransactionsOf(object player)
            => (IList)player.GetType().GetField("transactions").GetValue(player);

        private void RebuildEconomy()
        {
            _economy = Activator.CreateInstance(_economyType,
                new object[] { _player, new Func<long>(() => _now) });
        }

        private void RebuildTime()
        {
            _assets = Activator.CreateInstance(_assetServiceType, _player, _economy);
            _time = Activator.CreateInstance(_timeType,
                _player, _economy, _assets, new Func<long>(() => _now));
        }

        private object CreateBusinessService()
            => Activator.CreateInstance(_businessServiceType, _player, _economy, _time);

        private void AdvanceBusinessTime(float seconds)
        {
            AdvanceClockOnly(seconds);
            InvokeRaw(CreateBusinessService(), "UpdateTimer", 0f);
        }

        private void AdvanceClockOnly(float seconds)
        {
            _now += TimeSpan.FromSeconds(seconds).Ticks;
        }

        private static void SetBusinessField(object player, string field, object value)
        {
            IList businesses = (IList)player.GetType().GetField("businesses").GetValue(player);
            businesses[0].GetType().GetField(field).SetValue(businesses[0], value);
        }
    }
}
