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
        public void SchemaV2SaveWithoutClaimState_LoadsAsAlreadyPaidOrIdle()
        {
            File.WriteAllText(_savePath,
                "{\"schemaVersion\":2,\"playerData\":{\"money\":100,\"health\":100,\"mood\":100," +
                "\"day\":1,\"month\":1,\"workTimer\":{\"isActive\":false,\"endUtcTicks\":0," +
                "\"jobId\":\"job_courier\",\"salary\":100,\"durationSeconds\":3}," +
                "\"studyTimer\":{},\"currentJobId\":\"job_courier\",\"completedEducation\":[]," +
                "\"businesses\":[],\"activeDeposits\":[],\"transactions\":[{\"id\":\"prior-payment\"," +
                "\"type\":\"income\",\"source\":\"Зарплата: Курьер\",\"amount\":100," +
                "\"balanceBefore\":0,\"balanceAfter\":100,\"timestampUtcTicks\":638900000000000000}]," +
                "\"assets\":{\"ownedHouses\":[],\"ownedCars\":[],\"ownedClothing\":[]}}}");

            object result = Load();
            object data = Get<object>(result, "Data");
            object workTimer = _playerType.GetField("workTimer").GetValue(data);
            Type economyType = _playerType.Assembly.GetType("ZeroToHero.Core.EconomyService", true);
            object economy = Activator.CreateInstance(economyType, new object[] { data, null });
            Type jobType = _playerType.Assembly.GetType("ZeroToHero.Jobs.JobService", true);
            object job = Activator.CreateInstance(jobType, new object[] { data, economy, null });

            Assert.That(Get<bool>(result, "Success"), Is.True);
            Assert.That(Get<bool>(result, "Migrated"), Is.False);
            Assert.That(GetFieldValue<bool>(workTimer, "isActive"), Is.False);
            Assert.That(GetFieldValue<bool>(workTimer, "isReadyToClaim"), Is.False);
            Assert.That(InvokeResult<bool>(job, "ClaimSalary"), Is.False);
            Assert.That(GetFieldValue<int>(data, "money"), Is.EqualTo(100));
            Assert.That(((System.Collections.IList)_playerType.GetField("transactions").GetValue(data)).Count,
                Is.EqualTo(1));
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

        [Test]
        public void TestProfileWriteAndClear_DoNotTouchGameProfileOrBackup()
        {
            string gameDirectory = Path.Combine(_testDirectory, "game-profile");
            string testPath = Path.Combine(_testDirectory, "test-profile", "player_save.json");
            object gameService = CreateGameProfile(gameDirectory);
            AssertSaveSuccess(_serviceType.GetMethod("Save").Invoke(gameService, new[] { NewPlayer(111, 1) }));
            AssertSaveSuccess(_serviceType.GetMethod("Save").Invoke(gameService, new[] { NewPlayer(222, 2) }));

            string gamePath = Path.Combine(gameDirectory, "player_save.json");
            byte[] primaryBefore = File.ReadAllBytes(gamePath);
            byte[] backupBefore = File.ReadAllBytes(gamePath + ".bak");
            object testService = CreateTestProfile(testPath);

            Assert.That(Get<bool>(testService, "IsTestProfile"), Is.True);
            AssertSaveSuccess(_serviceType.GetMethod("Save").Invoke(testService, new[] { NewPlayer(999, 9) }));
            _serviceType.GetMethod("ClearSave").Invoke(testService, null);

            CollectionAssert.AreEqual(primaryBefore, File.ReadAllBytes(gamePath));
            CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(gamePath + ".bak"));
            Assert.That(File.Exists(testPath), Is.False);
            Assert.That(File.Exists(testPath + ".bak"), Is.False);
        }

        [Test]
        public void GameProfileFactory_LoadsExistingSaveFromConfiguredGameDirectory()
        {
            string gameDirectory = Path.Combine(_testDirectory, "game-profile");
            object writer = CreateGameProfile(gameDirectory);
            AssertSaveSuccess(_serviceType.GetMethod("Save").Invoke(writer, new[] { NewPlayer(321, 4) }));

            object gameService = CreateGameProfile(gameDirectory);
            object result = _serviceType.GetMethod("Load").Invoke(gameService, null);

            Assert.That(Get<bool>(result, "Success"), Is.True);
            Assert.That(GetFieldValue<int>(Get<object>(result, "Data"), "money"), Is.EqualTo(321));
            Assert.That(Get<bool>(gameService, "IsTestProfile"), Is.False);
        }

        [Test]
        public void TestProfileFactory_RejectsTheRealPersistentDataPath()
        {
            string realProfilePath = Path.Combine(Application.persistentDataPath, "player_save.json");
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => CreateTestProfile(realProfilePath));

            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void ReinitializingGameManagerWithTestProfile_KeepsUsingThatProfile()
        {
            string gameDirectory = Path.Combine(_testDirectory, "game-profile");
            string testPath = Path.Combine(_testDirectory, "test-profile", "player_save.json");
            object gameService = CreateGameProfile(gameDirectory);
            AssertSaveSuccess(_serviceType.GetMethod("Save").Invoke(gameService, new[] { NewPlayer(444, 4) }));
            AssertSaveSuccess(_serviceType.GetMethod("Save").Invoke(gameService, new[] { NewPlayer(555, 5) }));
            string gamePath = Path.Combine(gameDirectory, "player_save.json");
            byte[] primaryBefore = File.ReadAllBytes(gamePath);
            byte[] backupBefore = File.ReadAllBytes(gamePath + ".bak");

            GameObject holder = new GameObject("SaveIsolationTest_GameManager");
            holder.SetActive(false);
            try
            {
                Component manager = holder.AddComponent(
                    AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Assembly-CSharp")
                        .GetType("ZeroToHero.Core.GameManager", true));
                object testService = CreateTestProfile(testPath);
                manager.GetType().GetMethod("ConfigureSaveServiceForTesting").Invoke(manager, new[] { testService });
                manager.GetType().GetMethod("InitServices", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, null);

                Assert.That(Get<object>(manager, "SaveSystem"), Is.SameAs(testService));
                manager.GetType().GetMethod("SaveGame").Invoke(manager, null);
                manager.GetType().GetMethod("InitServices", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(manager, null);
                Assert.That(Get<object>(manager, "SaveSystem"), Is.SameAs(testService));
                manager.GetType().GetMethod("SaveGame").Invoke(manager, null);

                Assert.That(File.Exists(testPath), Is.True);
                object loaded = Get<object>(testService.GetType().GetMethod("Load").Invoke(testService, null), "Data");
                Assert.That(GetFieldValue<int>(loaded, "money"), Is.Zero);
                CollectionAssert.AreEqual(primaryBefore, File.ReadAllBytes(gamePath));
                CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(gamePath + ".bak"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }

        private object NewPlayer(int money, int day)
        {
            object player = Activator.CreateInstance(_playerType);
            _playerType.GetField("money").SetValue(player, money);
            _playerType.GetField("day").SetValue(player, day);
            return player;
        }

        private object NewService(string path) => Activator.CreateInstance(_serviceType, new object[] { path });
        private object CreateTestProfile(string path)
            => _serviceType.GetMethod("CreateTestProfile", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { path });
        private object CreateGameProfile(string directory)
            => _serviceType.GetMethod("CreateGameProfile", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { directory });
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

        private static T InvokeResult<T>(object target, string method)
            => (T)target.GetType().GetMethod(method).Invoke(target, null);

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
