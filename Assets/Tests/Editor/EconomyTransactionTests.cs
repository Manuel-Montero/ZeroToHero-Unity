using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ZeroToHero.SaveTests
{
    public class EconomyTransactionTests
    {
        private Type _playerType;
        private Type _economyType;
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
            _player = Activator.CreateInstance(_playerType);
            _now = DateTime.UtcNow.Ticks;
            Func<long> clock = () => _now;
            _economy = Activator.CreateInstance(_economyType, new[] { _player, (object)clock });
        }

        [Test]
        public void IncomeAndExpense_RecordSignedAmountsAndBalanceSnapshots()
        {
            Assert.That(Invoke<bool>(_economy, "AddMoney", 100, "work salary"), Is.True);
            Assert.That(Invoke<bool>(_economy, "SpendMoney", 35, "education"), Is.True);

            IList transactions = Transactions;
            Assert.That(transactions.Count, Is.EqualTo(2));
            AssertTransaction(transactions[0], "income", 100, 0, 100, "work salary");
            AssertTransaction(transactions[1], "expense", -35, 100, 65, "education");
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(65));
        }

        [Test]
        public void RejectedOperationsAndOverflow_DoNotChangeBalanceOrAddTransactions()
        {
            SetField(_player, "money", 80);
            Assert.That(Invoke<bool>(_economy, "SpendMoney", 100, "too expensive"), Is.False);
            Assert.That(Invoke<bool>(_economy, "SpendMoney", 0, "zero"), Is.False);
            Assert.That(Invoke<bool>(_economy, "SpendMoney", -1, "invalid"), Is.False);
            Assert.That(Invoke<bool>(_economy, "AddMoney", 0, "zero"), Is.False);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(80));
            Assert.That(Transactions, Is.Empty);

            SetField(_player, "money", int.MaxValue);
            Assert.That(Invoke<bool>(_economy, "AddMoney", 1, "overflow"), Is.False);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(int.MaxValue));
            Assert.That(Transactions, Is.Empty);
        }

        [Test]
        public void Transactions_AreOrderedAndHaveUniqueIds()
        {
            Invoke<bool>(_economy, "AddMoney", 10, "first");
            _now += TimeSpan.TicksPerSecond;
            Invoke<bool>(_economy, "AddMoney", 20, "second");
            Invoke<bool>(_economy, "SpendMoney", 5, "third");

            IList transactions = Transactions;
            string[] ids = transactions.Cast<object>().Select(item => Field<string>(item, "id")).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            Assert.That(Field<string>(transactions[0], "source"), Is.EqualTo("first"));
            Assert.That(Field<string>(transactions[1], "source"), Is.EqualTo("second"));
            Assert.That(Field<string>(transactions[2], "source"), Is.EqualTo("third"));
            Assert.That(Field<long>(transactions[0], "timestampUtcTicks"), Is.LessThan(Field<long>(transactions[1], "timestampUtcTicks")));
        }

        [Test]
        public void TransactionHistory_IsBoundedToTheLatestThousandEntries()
        {
            for (int i = 0; i < 1002; i++)
                Assert.That(Invoke<bool>(_economy, "AddMoney", 1, "repeat"), Is.True);

            Assert.That(Transactions.Count, Is.EqualTo(1000));
            Assert.That(Field<int>(Transactions[999], "balanceAfter"), Is.EqualTo(1002));
        }

        [Test]
        public void SaveRecovery_RestoresBalanceAndMatchingJournalSnapshot()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ZeroToHero_EconomyTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "player_save.json");
            try
            {
                Type saveType = _playerType.Assembly.GetType("ZeroToHero.Core.SaveService", true);
                object save = Activator.CreateInstance(saveType, new object[] { path });
                Invoke<bool>(_economy, "AddMoney", 100, "initial income");
                AssertSaveSucceeded(saveType.GetMethod("Save").Invoke(save, new[] { _player }));

                Invoke<bool>(_economy, "SpendMoney", 40, "purchase");
                AssertSaveSucceeded(saveType.GetMethod("Save").Invoke(save, new[] { _player }));
                File.WriteAllText(path, "damaged");

                object load = saveType.GetMethod("Load").Invoke(save, null);
                object recovered = load.GetType().GetProperty("Data").GetValue(load);
                IList recoveredTransactions = (IList)_playerType.GetField("transactions").GetValue(recovered);
                Assert.That((bool)load.GetType().GetProperty("Success").GetValue(load), Is.True);
                Assert.That((bool)load.GetType().GetProperty("RecoveredFromBackup").GetValue(load), Is.True);
                Assert.That(Field<int>(recovered, "money"), Is.EqualTo(100));
                Assert.That(recoveredTransactions.Count, Is.EqualTo(1));
                Assert.That(Field<int>(recoveredTransactions[0], "amount"), Is.EqualTo(100));
                Assert.That(Field<int>(recoveredTransactions[0], "balanceAfter"), Is.EqualTo(100));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private IList Transactions => (IList)_playerType.GetField("transactions").GetValue(_player);

        private static T Invoke<T>(object target, string method, params object[] arguments)
            => (T)target.GetType().GetMethod(method).Invoke(target, arguments);

        private static T Field<T>(object target, string field)
            => (T)target.GetType().GetField(field).GetValue(target);

        private static void SetField(object target, string field, object value)
            => target.GetType().GetField(field).SetValue(target, value);

        private static void AssertTransaction(object item, string type, int amount,
            int before, int after, string source)
        {
            Assert.That(Field<string>(item, "type"), Is.EqualTo(type));
            Assert.That(Field<int>(item, "amount"), Is.EqualTo(amount));
            Assert.That(Field<int>(item, "balanceBefore"), Is.EqualTo(before));
            Assert.That(Field<int>(item, "balanceAfter"), Is.EqualTo(after));
            Assert.That(Field<string>(item, "source"), Is.EqualTo(source));
            Assert.That(Field<string>(item, "id"), Is.Not.Empty);
            Assert.That(Field<long>(item, "timestampUtcTicks"), Is.GreaterThan(0));
        }

        private static void AssertSaveSucceeded(object result)
            => Assert.That((bool)result.GetType().GetProperty("Success").GetValue(result), Is.True);
    }
}
