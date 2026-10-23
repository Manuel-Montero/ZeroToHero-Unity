using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ZeroToHero.SaveTests
{
    public class TimeAndTimerTests
    {
        private Type _playerType;
        private Type _economyType;
        private Type _assetsType;
        private Type _timeType;
        private Type _jobType;
        private Type _educationType;
        private Type _bankType;
        private object _player;
        private object _economy;
        private object _time;
        private object _assets;
        private long _now;

        [SetUp]
        public void SetUp()
        {
            Assembly game = AppDomain.CurrentDomain.GetAssemblies()
                .First(assembly => assembly.GetName().Name == "Assembly-CSharp");
            _playerType = game.GetType("ZeroToHero.Player.PlayerData", true);
            _economyType = game.GetType("ZeroToHero.Core.EconomyService", true);
            _assetsType = game.GetType("ZeroToHero.Assets.AssetService", true);
            _timeType = game.GetType("ZeroToHero.Core.TimeManager", true);
            _jobType = game.GetType("ZeroToHero.Jobs.JobService", true);
            _educationType = game.GetType("ZeroToHero.Education.EducationService", true);
            _bankType = game.GetType("ZeroToHero.Bank.BankService", true);

            _player = Activator.CreateInstance(_playerType);
            _economy = Activator.CreateInstance(_economyType, new object[] { _player, null });
            _assets = Activator.CreateInstance(_assetsType, _player, _economy);
            _now = DateTime.UtcNow.Ticks;
            Func<long> clock = () => _now;
            _time = Activator.CreateInstance(_timeType, _player, _economy, _assets, clock);
        }

        [Test]
        public void Calendar_AdvancesAfterTenForegroundSecondsAndKeepsRemainder()
        {
            Invoke(_time, "UpdateTimer", 9.5f);
            Assert.That(Field<int>(_player, "day"), Is.EqualTo(1));
            Invoke(_time, "UpdateTimer", 1f);
            Assert.That(Field<int>(_player, "day"), Is.EqualTo(2));
            Assert.That(Field<float>(_player, "dayElapsedSeconds"), Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void WorkDeadline_ResumesAfterLoadAndPaysOnlyOnce()
        {
            SetField(_player, "money", 0);
            object job = Activator.CreateInstance(_jobType, _player, _economy, _time);
            Invoke(job, "StartWork");
            long end = NestedField<long>(_player, "workTimer", "endUtcTicks");

            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ZeroToHero_TimerTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = System.IO.Path.Combine(directory, "save.json");
            try
            {
                Type saveType = _playerType.Assembly.GetType("ZeroToHero.Core.SaveService", true);
                object save = Activator.CreateInstance(saveType, path);
                object saveResult = saveType.GetMethod("Save").Invoke(save, new[] { _player });
                Assert.That((bool)saveResult.GetType().GetProperty("Success").GetValue(saveResult), Is.True);
                object loadResult = saveType.GetMethod("Load").Invoke(save, null);
                _player = loadResult.GetType().GetProperty("Data").GetValue(loadResult);
                _economy = Activator.CreateInstance(_economyType, new object[] { _player, null });
                _assets = Activator.CreateInstance(_assetsType, _player, _economy);
                Func<long> loadedClock = () => _now;
                _time = Activator.CreateInstance(_timeType, _player, _economy, _assets, loadedClock);
                job = Activator.CreateInstance(_jobType, _player, _economy, _time);
                Assert.That(NestedField<long>(_player, "workTimer", "endUtcTicks"), Is.EqualTo(end));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }

            _now = end + TimeSpan.TicksPerSecond;
            Invoke(job, "UpdateTimer", 0f);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(100));
            Assert.That(NestedField<bool>(_player, "workTimer", "isActive"), Is.False);
            Invoke(job, "UpdateTimer", 0f);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(100));
        }

        [Test]
        public void WorkCompletion_RemainsCompletedAfterSavingAndLoadingAgain()
        {
            SetField(_player, "money", 0);
            object job = Activator.CreateInstance(_jobType, _player, _economy, _time);
            Invoke(job, "StartWork");
            long end = NestedField<long>(_player, "workTimer", "endUtcTicks");
            string directory = NewTemporaryDirectory("WorkCycle");
            string path = System.IO.Path.Combine(directory, "save.json");
            Type saveType = _playerType.Assembly.GetType("ZeroToHero.Core.SaveService", true);
            object save = Activator.CreateInstance(saveType, path);
            try
            {
                AssertSaveSucceeded(saveType.GetMethod("Save").Invoke(save, new[] { _player }));
                _now = end - TimeSpan.TicksPerSecond * 3 / 2;
                _player = GetProperty<object>(saveType.GetMethod("Load").Invoke(save, null), "Data");
                RebuildServices();
                Assert.That(InvokeResult<float>(_time, "GetRemainingSeconds", end), Is.EqualTo(1.5f).Within(0.001f));

                _now = end;
                job = Activator.CreateInstance(_jobType, _player, _economy, _time);
                Invoke(job, "UpdateTimer", 0f);
                Assert.That(Field<int>(_player, "money"), Is.EqualTo(100));
                Assert.That(NestedField<bool>(_player, "workTimer", "isActive"), Is.False);
                Assert.That(Transactions.Count, Is.EqualTo(1));
                AssertTransaction(Transactions[0], "income", 100, 0, 100, "Зарплата: Курьер");
                AssertSaveSucceeded(saveType.GetMethod("Save").Invoke(save, new[] { _player }));

                _player = GetProperty<object>(saveType.GetMethod("Load").Invoke(save, null), "Data");
                RebuildServices();
                job = Activator.CreateInstance(_jobType, _player, _economy, _time);
                Invoke(job, "UpdateTimer", 0f);

                Assert.That(Field<int>(_player, "money"), Is.EqualTo(100));
                Assert.That(Transactions.Count, Is.EqualTo(1));
                AssertTransaction(Transactions[0], "income", 100, 0, 100, "Зарплата: Курьер");
                Assert.That(NestedField<bool>(_player, "workTimer", "isActive"), Is.False);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void WorkSalaryOverflow_DoesNotCompleteActionOrAddTransaction()
        {
            SetField(_player, "money", int.MaxValue);
            object job = Activator.CreateInstance(_jobType, _player, _economy, _time);
            Invoke(job, "StartWork");
            long end = NestedField<long>(_player, "workTimer", "endUtcTicks");

            _now = end;
            Invoke(job, "UpdateTimer", 0f);

            Assert.That(Field<int>(_player, "money"), Is.EqualTo(int.MaxValue));
            Assert.That(NestedField<bool>(_player, "workTimer", "isActive"), Is.True);
            Assert.That(Transactions, Is.Empty);
        }

        [Test]
        public void StudyCycle_RestoresDeadlineAndCompletionAcrossReloads()
        {
            SetField(_player, "money", 2000);
            object study = Activator.CreateInstance(_educationType, _player, _economy, _time);
            Assert.That(InvokeResult<bool>(study, "StartStudy", "edu_courses"), Is.True);
            long end = NestedField<long>(_player, "studyTimer", "endUtcTicks");
            string directory = NewTemporaryDirectory("StudyCycle");
            string path = System.IO.Path.Combine(directory, "save.json");
            Type saveType = _playerType.Assembly.GetType("ZeroToHero.Core.SaveService", true);
            object save = Activator.CreateInstance(saveType, path);
            try
            {
                AssertSaveSucceeded(saveType.GetMethod("Save").Invoke(save, new[] { _player }));
                _now += 2 * TimeSpan.TicksPerSecond;
                _player = GetProperty<object>(saveType.GetMethod("Load").Invoke(save, null), "Data");
                RebuildServices();
                Assert.That(InvokeResult<float>(_time, "GetRemainingSeconds", end), Is.EqualTo(3f).Within(0.001f));

                _now = end;
                study = Activator.CreateInstance(_educationType, _player, _economy, _time);
                Invoke(study, "UpdateTimer", 0f);
                Assert.That(NestedField<bool>(_player, "studyTimer", "isActive"), Is.False);
                Assert.That(((System.Collections.IList)_playerType.GetField("completedEducation").GetValue(_player)).Count, Is.EqualTo(1));
                Assert.That(Transactions.Count, Is.EqualTo(1)); // Tuition only; study completion pays nothing.
                AssertTransaction(Transactions[0], "expense", -500, 2000, 1500, "Education: Курсы курьера / сервис");
                AssertSaveSucceeded(saveType.GetMethod("Save").Invoke(save, new[] { _player }));

                _player = GetProperty<object>(saveType.GetMethod("Load").Invoke(save, null), "Data");
                RebuildServices();
                study = Activator.CreateInstance(_educationType, _player, _economy, _time);
                Invoke(study, "UpdateTimer", 0f);
                Assert.That(((System.Collections.IList)_playerType.GetField("completedEducation").GetValue(_player)).Count, Is.EqualTo(1));
                Assert.That(Transactions.Count, Is.EqualTo(1));
                AssertTransaction(Transactions[0], "expense", -500, 2000, 1500, "Education: Курсы курьера / сервис");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void DepositCycle_RestoresActiveReadyAndClaimedStatesWithoutDuplicatePayout()
        {
            SetField(_player, "money", 1000);
            object bank = Activator.CreateInstance(_bankType, _player, _economy, _time);
            Assert.That(InvokeResult<bool>(bank, "OpenDeposit", "dep_quick", 100), Is.True);
            long end = NestedField<long>(_player, "activeDeposits", 0, "endUtcTicks");
            string directory = NewTemporaryDirectory("DepositCycle");
            string path = System.IO.Path.Combine(directory, "save.json");
            Type saveType = _playerType.Assembly.GetType("ZeroToHero.Core.SaveService", true);
            object save = Activator.CreateInstance(saveType, path);
            try
            {
                AssertSaveSucceeded(saveType.GetMethod("Save").Invoke(save, new[] { _player }));
                _now += 30 * TimeSpan.TicksPerSecond;
                _player = GetProperty<object>(saveType.GetMethod("Load").Invoke(save, null), "Data");
                RebuildServices();
                Assert.That(InvokeResult<float>(_time, "GetRemainingSeconds", end), Is.EqualTo(90f).Within(0.001f));

                bank = Activator.CreateInstance(_bankType, _player, _economy, _time);
                _now = end;
                Invoke(bank, "UpdateTimer", 0f);
                Assert.That(NestedField<bool>(_player, "activeDeposits", 0, "isReadyToClaim"), Is.True);
                AssertSaveSucceeded(saveType.GetMethod("Save").Invoke(save, new[] { _player }));

                _player = GetProperty<object>(saveType.GetMethod("Load").Invoke(save, null), "Data");
                RebuildServices();
                bank = Activator.CreateInstance(_bankType, _player, _economy, _time);
                Assert.That(NestedField<bool>(_player, "activeDeposits", 0, "isReadyToClaim"), Is.True);
                Assert.That(InvokeResult<bool>(bank, "ClaimDeposit", "dep_quick"), Is.True);
                Assert.That(Field<int>(_player, "money"), Is.EqualTo(1010));
                Assert.That(Transactions.Count, Is.EqualTo(2));
                AssertTransaction(Transactions[0], "expense", -100, 1000, 900, "Вклад в банк: Быстрый вклад (2 мин)");
                AssertTransaction(Transactions[1], "income", 110, 900, 1010, "Выплата по вкладу: Быстрый вклад (2 мин)");
                AssertSaveSucceeded(saveType.GetMethod("Save").Invoke(save, new[] { _player }));

                _player = GetProperty<object>(saveType.GetMethod("Load").Invoke(save, null), "Data");
                RebuildServices();
                bank = Activator.CreateInstance(_bankType, _player, _economy, _time);
                Assert.That(InvokeResult<bool>(bank, "ClaimDeposit", "dep_quick"), Is.False);
                Assert.That(Field<int>(_player, "money"), Is.EqualTo(1010));
                Assert.That(Transactions.Count, Is.EqualTo(2));
                AssertTransaction(Transactions[0], "expense", -100, 1000, 900, "Вклад в банк: Быстрый вклад (2 мин)");
                AssertTransaction(Transactions[1], "income", 110, 900, 1010, "Выплата по вкладу: Быстрый вклад (2 мин)");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void DepositPayoutOverflow_LeavesDepositClaimableWithoutCredit()
        {
            SetField(_player, "money", 0);
            Type depositType = _playerType.Assembly.GetType("ZeroToHero.Bank.DepositData", true);
            object deposit = Activator.CreateInstance(depositType);
            depositType.GetField("id").SetValue(deposit, "dep_quick");
            depositType.GetField("investedAmount").SetValue(deposit, int.MaxValue);
            depositType.GetField("isReadyToClaim").SetValue(deposit, true);
            ((System.Collections.IList)_playerType.GetField("activeDeposits").GetValue(_player)).Add(deposit);
            object bank = Activator.CreateInstance(_bankType, _player, _economy, _time);

            Assert.That(InvokeResult<bool>(bank, "ClaimDeposit", "dep_quick"), Is.False);
            Assert.That(NestedField<bool>(_player, "activeDeposits", 0, "isReadyToClaim"), Is.True);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(0));
            Assert.That(Transactions, Is.Empty);
        }

        [Test]
        public void Calendar_RestoresSavedRemainderAndAdvancesMonthBoundaryOnce()
        {
            SetField(_player, "day", 30);
            SetField(_player, "month", 4);
            SetField(_player, "dayElapsedSeconds", 9.25f);
            string directory = NewTemporaryDirectory("CalendarCycle");
            string path = System.IO.Path.Combine(directory, "save.json");
            Type saveType = _playerType.Assembly.GetType("ZeroToHero.Core.SaveService", true);
            object save = Activator.CreateInstance(saveType, path);
            try
            {
                AssertSaveSucceeded(saveType.GetMethod("Save").Invoke(save, new[] { _player }));
                _player = GetProperty<object>(saveType.GetMethod("Load").Invoke(save, null), "Data");
                RebuildServices();
                Invoke(_time, "UpdateTimer", 0.74f);
                Assert.That(Field<int>(_player, "day"), Is.EqualTo(30));
                Assert.That(Field<int>(_player, "month"), Is.EqualTo(4));

                Invoke(_time, "UpdateTimer", 0.02f);
                Assert.That(Field<int>(_player, "day"), Is.EqualTo(1));
                Assert.That(Field<int>(_player, "month"), Is.EqualTo(5));
                Assert.That(Field<float>(_player, "dayElapsedSeconds"), Is.EqualTo(0.01f).Within(0.001f));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void StudyDeadline_CompletesOnceAfterRealTimeElapses()
        {
            SetField(_player, "money", 1000);
            object study = Activator.CreateInstance(_educationType, _player, _economy, _time);
            Assert.That(InvokeResult<bool>(study, "StartStudy", "edu_courses"), Is.True);
            long end = NestedField<long>(_player, "studyTimer", "endUtcTicks");
            _now = end;
            Invoke(study, "UpdateTimer", 0f);
            var completed = (System.Collections.IList)_playerType.GetField("completedEducation").GetValue(_player);
            Assert.That(completed.Contains("edu_courses"), Is.True);
            Assert.That(NestedField<bool>(_player, "studyTimer", "isActive"), Is.False);
            Invoke(study, "UpdateTimer", 0f);
            Assert.That(completed.Cast<object>().Count(value => (string)value == "edu_courses"), Is.EqualTo(1));
        }

        [Test]
        public void DepositDeadline_ContinuesOfflineAndCanBeClaimedOnlyOnce()
        {
            SetField(_player, "money", 1000);
            object bank = Activator.CreateInstance(_bankType, _player, _economy, _time);
            Assert.That(InvokeResult<bool>(bank, "OpenDeposit", "dep_quick", 100), Is.True);
            long end = NestedField<long>(_player, "activeDeposits", 0, "endUtcTicks");
            _now = end;
            Invoke(bank, "UpdateTimer", 0f);
            Assert.That(NestedField<bool>(_player, "activeDeposits", 0, "isReadyToClaim"), Is.True);
            Assert.That(InvokeResult<bool>(bank, "ClaimDeposit", "dep_quick"), Is.True);
            int balanceAfterClaim = Field<int>(_player, "money");
            Assert.That(InvokeResult<bool>(bank, "ClaimDeposit", "dep_quick"), Is.False);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(balanceAfterClaim));
        }

        [Test]
        public void LegacyActiveDeposit_UsesPersistedRemainingSecondsFromMigrationTime()
        {
            Type depositType = _playerType.Assembly.GetType("ZeroToHero.Bank.DepositData", true);
            object deposit = Activator.CreateInstance(depositType);
            depositType.GetField("id").SetValue(deposit, "dep_quick");
            depositType.GetField("timer").SetValue(deposit, 60f);
            depositType.GetField("isActive").SetValue(deposit, true);
            ((System.Collections.IList)_playerType.GetField("activeDeposits").GetValue(_player)).Add(deposit);

            object bank = Activator.CreateInstance(_bankType, _player, _economy, _time);

            Assert.That((bool)_bankType.GetProperty("MigratedLegacyDeadlines").GetValue(bank), Is.True);
            Assert.That(NestedField<long>(_player, "activeDeposits", 0, "endUtcTicks"),
                Is.EqualTo(_now + 60 * TimeSpan.TicksPerSecond));
        }

        [Test]
        public void MonthlyExpenses_AreCappedAtAvailableBalance()
        {
            SetField(_player, "money", 220);
            Invoke(_assets, "BuyAsset", "house_hostel");
            SetField(_player, "day", 30);
            Invoke(_time, "UpdateTimer", 10f);
            Assert.That(Field<int>(_player, "money"), Is.EqualTo(0));
        }

        private static object Invoke(object target, string method, params object[] args)
            => target.GetType().GetMethod(method).Invoke(target, args);

        private static T InvokeResult<T>(object target, string method, params object[] args)
            => (T)Invoke(target, method, args);

        private static T Field<T>(object target, string field)
            => (T)target.GetType().GetField(field).GetValue(target);

        private static void SetField(object target, string field, object value)
            => target.GetType().GetField(field).SetValue(target, value);

        private static T NestedField<T>(object target, string parentField, string childField)
            => (T)target.GetType().GetField(parentField).GetValue(target).GetType().GetField(childField)
                .GetValue(target.GetType().GetField(parentField).GetValue(target));

        private static T NestedField<T>(object target, string listField, int index, string childField)
        {
            var list = (System.Collections.IList)target.GetType().GetField(listField).GetValue(target);
            return (T)list[index].GetType().GetField(childField).GetValue(list[index]);
        }

        private System.Collections.IList Transactions
            => (System.Collections.IList)_playerType.GetField("transactions").GetValue(_player);

        private static string NewTemporaryDirectory(string testName)
        {
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "ZeroToHero_" + testName + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static T GetProperty<T>(object instance, string property)
            => (T)instance.GetType().GetProperty(property).GetValue(instance);

        private static void AssertSaveSucceeded(object result)
        {
            Assert.That(GetProperty<bool>(result, "Success"), Is.True,
                (string)result.GetType().GetProperty("Error").GetValue(result));
        }

        private static void AssertTransaction(object transaction, string type, int amount,
            int before, int after, string source)
        {
            Type transactionType = transaction.GetType();
            Assert.That(transactionType.GetField("type").GetValue(transaction), Is.EqualTo(type));
            Assert.That(transactionType.GetField("amount").GetValue(transaction), Is.EqualTo(amount));
            Assert.That(transactionType.GetField("balanceBefore").GetValue(transaction), Is.EqualTo(before));
            Assert.That(transactionType.GetField("balanceAfter").GetValue(transaction), Is.EqualTo(after));
            Assert.That(transactionType.GetField("source").GetValue(transaction), Is.EqualTo(source));
            Assert.That(transactionType.GetField("id").GetValue(transaction), Is.Not.Empty);
            Assert.That((long)transactionType.GetField("timestampUtcTicks").GetValue(transaction), Is.GreaterThan(0));
        }

        private static void AssertTransaction(System.Collections.IList transactions, int index,
            string type, int amount, int before, int after, string source)
            => AssertTransaction(transactions[index], type, amount, before, after, source);

        private void RebuildServices()
        {
            _economy = Activator.CreateInstance(_economyType, new object[] { _player, new Func<long>(() => _now) });
            _assets = Activator.CreateInstance(_assetsType, _player, _economy);
            _time = Activator.CreateInstance(_timeType, _player, _economy, _assets, new Func<long>(() => _now));
        }
    }
}
