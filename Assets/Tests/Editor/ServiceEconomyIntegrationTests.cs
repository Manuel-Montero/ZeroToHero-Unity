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
        private object _player;
        private object _economy;
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
            _player = Activator.CreateInstance(_playerType);
            _now = DateTime.UtcNow.Ticks;
            RebuildEconomy();
        }

        [Test]
        public void BusinessPurchaseAndIncome_AreJournaledAndPersistTogether()
        {
            SetField(_player, "money", 1000);
            object business = Activator.CreateInstance(_businessServiceType, _player, _economy);
            Assert.That(Invoke<bool>(business, "BuyBusiness", "biz_coffee"), Is.True);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(0));
            AssertTransaction(0, "expense", -1000, 1000, 0, "Покупка бизнеса: Кофейный автомат");

            Assert.That(Invoke<bool>(business, "CollectIncome", "biz_coffee"), Is.True);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(150));
            AssertTransaction(1, "income", 150, 0, 150, "Доход от бизнеса: Кофейный автомат");
            Assert.That(Transactions.Count, Is.EqualTo(2));

            object loaded = SaveAndLoad(_player);
            Assert.That(Field<int>(loaded, "money"), Is.EqualTo(150));
            Assert.That(TransactionsOf(loaded).Count, Is.EqualTo(2));
            Assert.That(BusinessField<bool>(loaded, "isPurchased"), Is.True);
            Assert.That(BusinessField<int>(loaded, "collectCount"), Is.EqualTo(1));
            AssertTransaction(TransactionsOf(loaded), 0, "expense", -1000, 1000, 0);
            AssertTransaction(TransactionsOf(loaded), 1, "income", 150, 0, 150);
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
            businesses.Add(data);

            object business = Activator.CreateInstance(_businessServiceType, _player, _economy);

            Assert.That(Invoke<bool>(business, "CollectIncome", "biz_coffee"), Is.False);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(int.MaxValue));
            Assert.That((int)businessDataType.GetField("collectCount").GetValue(data), Is.EqualTo(0));
            Assert.That(Transactions, Is.Empty);
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
    }
}
