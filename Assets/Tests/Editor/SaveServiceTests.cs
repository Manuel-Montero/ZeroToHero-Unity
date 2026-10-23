using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZeroToHero.SaveTests
{
    // Reflection keeps this small Editor test assembly independent from the
    // project's predefined Assembly-CSharp assembly.
    public class SaveServiceTests
    {
        private string _testDirectory;
        private string _savePath;
        private Type _serviceType;
        private Type _playerType;

        [SetUp]
        public void SetUp()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "ZeroToHero_SaveTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
            _savePath = Path.Combine(_testDirectory, "player_save.json");

            Assembly gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .First(assembly => assembly.GetName().Name == "Assembly-CSharp");
            _serviceType = gameAssembly.GetType("ZeroToHero.Core.SaveService", true);
            _playerType = gameAssembly.GetType("ZeroToHero.Player.PlayerData", true);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDirectory)) Directory.Delete(_testDirectory, true);
        }

        [Test]
        public void NewSaveAndLoad_PreservesPlayerData()
        {
            object player = NewPlayer(1250, 7);
            AssertSaveSuccess(Save(player));

            object result = Load();
            Assert.That(Get<bool>(result, "Success"), Is.True);
            Assert.That(Get<bool>(result, "HasSave"), Is.True);
            Assert.That(Get<object>(result, "Data").GetType().GetField("money").GetValue(Get<object>(result, "Data")), Is.EqualTo(1250));
            Assert.That(File.ReadAllText(_savePath), Does.Contain("\"schemaVersion\": 2"));
        }

        [Test]
        public void LegacySave_IsLoadedAndMigratedToVersionedEnvelope()
        {
            File.WriteAllText(_savePath,
                "{\"money\":321,\"health\":70,\"mood\":80,\"day\":4,\"month\":2," +
                "\"currentJobId\":\"job_waiter\",\"completedEducation\":[\"edu_courses\"]," +
                "\"businesses\":[],\"activeDeposits\":[],\"assets\":{\"ownedHouses\":[\"house_hostel\"]," +
                "\"ownedCars\":[],\"ownedClothing\":[]}}");

            object result = Load();
            Assert.That(Get<bool>(result, "Success"), Is.True);
            Assert.That(Get<bool>(result, "Migrated"), Is.True);
            Assert.That(Get<object>(result, "Data").GetType().GetField("money").GetValue(Get<object>(result, "Data")), Is.EqualTo(321));
            Assert.That(File.ReadAllText(_savePath), Does.Contain("\"schemaVersion\": 2"));
        }

        [Test]
        public void CorruptPrimary_IsPreservedAndRecoveredFromBackup()
        {
            AssertSaveSuccess(Save(NewPlayer(100, 1)));
            AssertSaveSuccess(Save(NewPlayer(200, 2)));
            File.WriteAllText(_savePath, "not valid json");

            object result = Load();
            object data = Get<object>(result, "Data");
            Assert.That(Get<bool>(result, "Success"), Is.True);
            Assert.That(Get<bool>(result, "RecoveredFromBackup"), Is.True);
            Assert.That(data.GetType().GetField("money").GetValue(data), Is.EqualTo(100));
            Assert.That(File.ReadAllText(_savePath), Does.Contain("\"schemaVersion\": 2"));
            Assert.That(Directory.GetFiles(_testDirectory, "player_save.json.corrupt.*").Length, Is.EqualTo(1));
        }

        [Test]
        public void PreviousEnvelopeVersion_IsMigratedWithEmptyTransactionHistory()
        {
            File.WriteAllText(_savePath,
                "{\"schemaVersion\":1,\"playerData\":{\"money\":77,\"day\":3,\"month\":1," +
                "\"completedEducation\":[],\"businesses\":[],\"activeDeposits\":[],\"assets\":{}}}");

            object result = Load();
            object data = Get<object>(result, "Data");
            var transactions = (System.Collections.IList)_playerType.GetField("transactions").GetValue(data);
            Assert.That(Get<bool>(result, "Success"), Is.True);
            Assert.That(Get<bool>(result, "Migrated"), Is.True);
            Assert.That(transactions, Is.Empty);
            Assert.That(File.ReadAllText(_savePath), Does.Contain("\"schemaVersion\": 2"));
        }

        [Test]
        public void CorruptPrimaryWithoutBackup_ReturnsErrorAndDoesNotCreateBlankProfile()
        {
            File.WriteAllText(_savePath, "{broken");
            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveService\] Не удалось прочитать сохранение\."));

            object result = Load();
            Assert.That(Get<bool>(result, "Success"), Is.False);
            Assert.That(Get<bool>(result, "HasSave"), Is.True);
            Assert.That(Get<object>(result, "Data"), Is.Null);
            Assert.That(Get<string>(result, "Error"), Is.Not.Empty);
            Assert.That(File.ReadAllText(_savePath), Is.EqualTo("{broken"));
            Assert.That(Directory.GetFiles(_testDirectory, "player_save.json.corrupt.*").Length, Is.EqualTo(1));
        }

        [Test]
        public void WriteFailure_IsReportedAsFailure()
        {
            string blockedPath = Path.Combine(_testDirectory, "a_directory_not_a_file");
            Directory.CreateDirectory(blockedPath);
            object service = NewService(blockedPath);
            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveService\] Ошибка при сохранении:"));

            object result = _serviceType.GetMethod("Save").Invoke(service, new[] { NewPlayer(50, 1) });
            Assert.That(Get<bool>(result, "Success"), Is.False);
            Assert.That(Get<string>(result, "Error"), Is.Not.Empty);
        }

        [Test]
        public void BackupCreationFailure_LeavesPreviousSaveLoadable()
        {
            AssertSaveSuccess(Save(NewPlayer(101, 3)));
            object service = NewServiceWithFailure(_savePath,
                operation => operation == "backup.copy" ? new IOException("injected backup-copy failure") : null);
            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveService\] Ошибка при сохранении:"));

            object saveResult = _serviceType.GetMethod("Save").Invoke(service, new[] { NewPlayer(202, 4) });

            Assert.That(Get<bool>(saveResult, "Success"), Is.False);
            object loaded = Load();
            Assert.That(Get<bool>(loaded, "Success"), Is.True);
            Assert.That(GetFieldValue<int>(Get<object>(loaded, "Data"), "money"), Is.EqualTo(101));
            Assert.That(File.ReadAllText(_savePath), Does.Contain("101"));
        }

        [Test]
        public void PrimaryReplacementFailure_RollsBackAndAllowsRetryAndReload()
        {
            AssertSaveSuccess(Save(NewPlayer(101, 3)));
            bool failOnce = true;
            object service = NewServiceWithFailure(_savePath, operation =>
            {
                if (operation == "primary.install" && failOnce)
                {
                    failOnce = false;
                    return new IOException("injected primary-install failure");
                }

                return null;
            });
            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveService\] Ошибка при сохранении:"));

            object failedSave = _serviceType.GetMethod("Save").Invoke(service, new[] { NewPlayer(202, 4) });

            Assert.That(Get<bool>(failedSave, "Success"), Is.False);
            object afterFailure = Load();
            Assert.That(Get<bool>(afterFailure, "Success"), Is.True);
            Assert.That(GetFieldValue<int>(Get<object>(afterFailure, "Data"), "money"), Is.EqualTo(101));

            AssertSaveSuccess(_serviceType.GetMethod("Save").Invoke(service, new[] { NewPlayer(202, 4) }));
            object afterRetry = Load();
            Assert.That(Get<bool>(afterRetry, "Success"), Is.True);
            Assert.That(GetFieldValue<int>(Get<object>(afterRetry, "Data"), "money"), Is.EqualTo(202));

            File.WriteAllText(_savePath, "damaged after successful retry");
            object recovered = Load();
            Assert.That(Get<bool>(recovered, "Success"), Is.True);
            Assert.That(Get<bool>(recovered, "RecoveredFromBackup"), Is.True);
            Assert.That(GetFieldValue<int>(Get<object>(recovered, "Data"), "money"), Is.EqualTo(101));
        }

        [Test]
        public void CorruptPrimaryAndBackup_ReturnErrorWithoutResettingProfile()
        {
            const string damagedPrimary = "broken primary contents";
            const string damagedBackup = "broken backup contents";
            File.WriteAllText(_savePath, damagedPrimary);
            File.WriteAllText(_savePath + ".bak", damagedBackup);
            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveService\] Не удалось прочитать сохранение\."));

            object result = Load();

            Assert.That(Get<bool>(result, "Success"), Is.False);
            Assert.That(Get<bool>(result, "HasSave"), Is.True);
            Assert.That(Get<object>(result, "Data"), Is.Null);
            Assert.That(Get<string>(result, "Error"), Is.Not.Empty);
            Assert.That(File.ReadAllText(_savePath), Is.EqualTo(damagedPrimary));
            Assert.That(File.ReadAllText(_savePath + ".bak"), Is.EqualTo(damagedBackup));
        }

        private object NewPlayer(int money, int day)
        {
            object player = Activator.CreateInstance(_playerType);
            _playerType.GetField("money").SetValue(player, money);
            _playerType.GetField("day").SetValue(player, day);
            return player;
        }

        private object NewService(string path) => Activator.CreateInstance(_serviceType, new object[] { path });
        private object NewServiceWithFailure(string path, Func<string, Exception> failureProvider)
            => Activator.CreateInstance(_serviceType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { path, failureProvider }, null);
        private object Save(object player) => _serviceType.GetMethod("Save").Invoke(NewService(_savePath), new[] { player });
        private object Load() => _serviceType.GetMethod("Load").Invoke(NewService(_savePath), null);

        private static T Get<T>(object instance, string propertyName)
            => (T)instance.GetType().GetProperty(propertyName).GetValue(instance);

        private static T GetFieldValue<T>(object instance, string fieldName)
            => (T)instance.GetType().GetField(fieldName).GetValue(instance);

        private static void AssertSaveSuccess(object result)
            => Assert.That(Get<bool>(result, "Success"), Is.True, Get<string>(result, "Error"));
    }

    public sealed class SaveTestsRunReporter : ICallbacks
    {
        public static SaveTestsRunReporter Latest { get; private set; }
        public bool Finished { get; private set; }
        public int PassedCount { get; private set; }
        public int FailedCount { get; private set; }
        public string Failures { get; private set; } = string.Empty;

        public SaveTestsRunReporter()
        {
            Latest = this;
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            Finished = false;
            PassedCount = 0;
            FailedCount = 0;
            Failures = string.Empty;
        }

        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren) return;
            if (result.ResultState == "Passed") PassedCount++;
            else
            {
                FailedCount++;
                Failures += result.FullName + " :: " + result.Message + "\n";
            }
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            Finished = true;
            Debug.Log($"[SaveTests] Finished: {PassedCount} passed, {FailedCount} failed. {Failures}");
        }
    }
}
