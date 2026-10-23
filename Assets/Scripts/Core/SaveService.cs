using System;
using System.IO;
using System.Text;
using UnityEngine;
using ZeroToHero.Player;

namespace ZeroToHero.Core
{
    public sealed class SaveResult
    {
        public bool Success { get; }
        public string Error { get; }

        internal SaveResult(bool success, string error = null)
        {
            Success = success;
            Error = error;
        }
    }

    public sealed class LoadResult
    {
        public bool Success { get; }
        public bool HasSave { get; }
        public bool RecoveredFromBackup { get; }
        public bool Migrated { get; }
        public PlayerData Data { get; }
        public string Error { get; }

        internal LoadResult(bool success, bool hasSave, PlayerData data, string error = null,
            bool recoveredFromBackup = false, bool migrated = false)
        {
            Success = success;
            HasSave = hasSave;
            Data = data;
            Error = error;
            RecoveredFromBackup = recoveredFromBackup;
            Migrated = migrated;
        }
    }

    [Serializable]
    internal sealed class SaveEnvelope
    {
        public int schemaVersion;
        public PlayerData playerData;
    }

    public class SaveService
    {
        public const int CurrentSchemaVersion = 2;

        private readonly string _saveFilePath;
        private readonly string _backupFilePath;
        private readonly Func<string, Exception> _testFailureProvider;
        public bool IsTestProfile { get; }

        public SaveService() : this(GetGameSavePath(Application.persistentDataPath), false, null)
        {
        }

        // Explicit-path callers are responsible for choosing the correct storage environment.
        public SaveService(string saveFilePath)
            : this(saveFilePath, false, null)
        {
        }

        public static SaveService CreateGameProfile(string persistentDataPath)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
                throw new ArgumentException("Папка игрового профиля не задана.", nameof(persistentDataPath));

            return new SaveService(GetGameSavePath(persistentDataPath), false, null);
        }

        public static SaveService CreateTestProfile(string saveFilePath)
        {
            if (string.IsNullOrWhiteSpace(saveFilePath))
                throw new ArgumentException("Путь тестового профиля не задан.", nameof(saveFilePath));

            string fullPath = Path.GetFullPath(saveFilePath);
            string tempRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Тестовое сохранение разрешено только в отдельной подпапке временного каталога.");

            string gamePath = Path.GetFullPath(GetGameSavePath(Application.persistentDataPath));
            if (string.Equals(fullPath, gamePath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fullPath, gamePath + ".bak", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Путь тестового сохранения совпадает с игровым профилем или его резервной копией.");

            return new SaveService(fullPath, true, null);
        }

        // Internal fault injection keeps filesystem failure tests deterministic without
        // exposing a test-only hook to gameplay code or changing the production path.
        internal SaveService(string saveFilePath, Func<string, Exception> testFailureProvider)
            : this(saveFilePath, false, testFailureProvider)
        {
        }

        private SaveService(string saveFilePath, bool isTestProfile, Func<string, Exception> testFailureProvider)
        {
            if (string.IsNullOrWhiteSpace(saveFilePath))
                throw new ArgumentException("Путь к файлу сохранения не задан.", nameof(saveFilePath));

            _saveFilePath = saveFilePath;
            _backupFilePath = saveFilePath + ".bak";
            _testFailureProvider = testFailureProvider;
            IsTestProfile = isTestProfile;
        }

        private static string GetGameSavePath(string persistentDataPath)
            => Path.Combine(persistentDataPath, "player_save.json");

        public SaveResult Save(PlayerData data)
        {
            if (data == null)
                return new SaveResult(false, "Нельзя сохранить пустые данные игрока.");

            string directory = Path.GetDirectoryName(_saveFilePath);
            string temporaryPath = _saveFilePath + ".tmp";

            try
            {
                if (string.IsNullOrEmpty(directory))
                    throw new IOException("Не удалось определить папку сохранения.");

                Directory.CreateDirectory(directory);
                Normalize(data);
                WriteAndFlush(temporaryPath, JsonUtility.ToJson(new SaveEnvelope
                {
                    schemaVersion = CurrentSchemaVersion,
                    playerData = data
                }, true));

                if (!TryReadSave(temporaryPath, out _, out string validationError))
                    throw new IOException("Проверка временного сохранения не пройдена: " + validationError);

                if (File.Exists(_saveFilePath))
                {
                    if (!TryReadSave(_saveFilePath, out _, out string currentSaveError))
                    {
                        PreserveCorruptFile(_saveFilePath);
                        throw new IOException("Текущий файл сохранения повреждён; исходный файл сохранён для диагностики. " + currentSaveError);
                    }

                    CreateValidatedBackup();
                }

                ReplaceFile(temporaryPath, _saveFilePath);
                Debug.Log($"[SaveService] Прогресс сохранен: {_saveFilePath}");
                return new SaveResult(true);
            }
            catch (Exception e)
            {
                TryDelete(temporaryPath);
                string error = $"Ошибка при сохранении: {e.Message}";
                Debug.LogError($"[SaveService] {error}");
                return new SaveResult(false, error);
            }
        }

        public LoadResult Load()
        {
            bool hasPrimary;
            bool hasBackup;
            try
            {
                string directory = Path.GetDirectoryName(_saveFilePath);
                if (string.IsNullOrEmpty(directory))
                    throw new IOException("Не удалось определить папку сохранения.");

                Directory.CreateDirectory(directory);
                if (Directory.Exists(_saveFilePath))
                    throw new IOException("Путь к сохранению занят папкой, чтение профиля невозможно.");

                hasPrimary = File.Exists(_saveFilePath);
                hasBackup = File.Exists(_backupFilePath);
            }
            catch (Exception e)
            {
                string error = "Не удалось проверить файлы сохранения: " + e.Message;
                Debug.LogError("[SaveService] " + error);
                return new LoadResult(false, true, null, error);
            }

            if (!hasPrimary && !hasBackup)
            {
                Debug.Log("[SaveService] Файл сохранения не найден. Создан новый профиль.");
                return new LoadResult(true, false, new PlayerData());
            }

            string primaryError = null;
            if (hasPrimary)
            {
                if (TryReadSave(_saveFilePath, out ParsedSave primary, out primaryError))
                {
                    if (primary.Migrated)
                    {
                        SaveResult migrationSave = Save(primary.Data);
                        if (!migrationSave.Success)
                            Debug.LogWarning("[SaveService] Старое сохранение загружено, но обновление его формата не удалось. " + migrationSave.Error);
                    }

                    return new LoadResult(true, true, primary.Data, migrated: primary.Migrated);
                }

                try
                {
                    PreserveCorruptFile(_saveFilePath);
                }
                catch (Exception e)
                {
                    primaryError += " Не удалось сохранить повреждённый файл для диагностики: " + e.Message;
                }
            }

            if (TryReadSave(_backupFilePath, out ParsedSave backup, out string backupError))
            {
                try
                {
                    RestoreBackup();
                    if (backup.Migrated)
                    {
                        SaveResult migrationSave = Save(backup.Data);
                        if (!migrationSave.Success)
                            Debug.LogWarning("[SaveService] Резервное старое сохранение восстановлено, но обновить формат не удалось. " + migrationSave.Error);
                    }

                    Debug.LogWarning("[SaveService] Основное сохранение повреждено; восстановлена резервная копия.");
                    return new LoadResult(true, true, backup.Data, recoveredFromBackup: true, migrated: backup.Migrated);
                }
                catch (Exception e)
                {
                    string error = "Резервная копия прочитана, но восстановить основной файл не удалось: " + e.Message;
                    Debug.LogError("[SaveService] " + error);
                    return new LoadResult(false, true, null, error);
                }
            }

            string combinedError = "Не удалось прочитать сохранение.";
            if (!string.IsNullOrEmpty(primaryError)) combinedError += " Основной файл: " + primaryError;
            if (!string.IsNullOrEmpty(backupError)) combinedError += " Резервная копия: " + backupError;
            Debug.LogError("[SaveService] " + combinedError);
            return new LoadResult(false, true, null, combinedError);
        }

        public void ClearSave()
        {
            if (File.Exists(_saveFilePath)) File.Delete(_saveFilePath);
            if (File.Exists(_backupFilePath)) File.Delete(_backupFilePath);
        }

        private void CreateValidatedBackup()
        {
            string temporaryBackupPath = _backupFilePath + ".tmp";
            try
            {
                ThrowIfInjectedFailure("backup.copy");
                File.Copy(_saveFilePath, temporaryBackupPath, true);
                if (!TryReadSave(temporaryBackupPath, out _, out string error))
                    throw new IOException("Проверка резервной копии не пройдена: " + error);

                ReplaceFile(temporaryBackupPath, _backupFilePath);
            }
            finally
            {
                TryDelete(temporaryBackupPath);
            }
        }

        private void RestoreBackup()
        {
            string temporaryPath = _saveFilePath + ".restore.tmp";
            try
            {
                File.Copy(_backupFilePath, temporaryPath, true);
                if (!TryReadSave(temporaryPath, out _, out string error))
                    throw new IOException("Проверка восстанавливаемой копии не пройдена: " + error);

                ReplaceFile(temporaryPath, _saveFilePath);
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }

        private static void WriteAndFlush(string path, string contents)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(contents);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        // Same-directory moves work on Android and desktop filesystems. If the second
        // move fails, restore the old file; the validated .bak remains an extra fallback.
        private void ReplaceFile(string sourcePath, string destinationPath)
        {
            if (!File.Exists(destinationPath))
            {
                File.Move(sourcePath, destinationPath);
                return;
            }

            string displacedPath = destinationPath + ".swap." + Guid.NewGuid().ToString("N");
            File.Move(destinationPath, displacedPath);
            try
            {
                if (string.Equals(destinationPath, _saveFilePath, StringComparison.Ordinal))
                    ThrowIfInjectedFailure("primary.install");
                File.Move(sourcePath, destinationPath);
            }
            catch
            {
                if (!File.Exists(destinationPath) && File.Exists(displacedPath))
                    File.Move(displacedPath, destinationPath);
                throw;
            }

            TryDelete(displacedPath);
        }

        private void ThrowIfInjectedFailure(string operation)
        {
            Exception exception = _testFailureProvider?.Invoke(operation);
            if (exception != null) throw exception;
        }

        private static void PreserveCorruptFile(string path)
        {
            if (!File.Exists(path)) return;

            string diagnosticPath = path + ".corrupt." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "." + Guid.NewGuid().ToString("N");
            File.Copy(path, diagnosticPath, false);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] Не удалось удалить временный файл {path}: {e.Message}");
            }
        }

        private static bool TryReadSave(string path, out ParsedSave parsed, out string error)
        {
            parsed = null;
            error = null;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8).Trim();
                if (json.Length < 2 || json[0] != '{' || json[json.Length - 1] != '}')
                    throw new InvalidDataException("Файл не содержит корректный JSON-объект.");

                if (json.IndexOf("\"playerData\"", StringComparison.Ordinal) >= 0)
                {
                    SaveEnvelope envelope = JsonUtility.FromJson<SaveEnvelope>(json);
                    if (envelope == null || envelope.playerData == null)
                        throw new InvalidDataException("В сохранении отсутствуют данные игрока.");
                    if (envelope.schemaVersion > CurrentSchemaVersion)
                        throw new InvalidDataException($"Версия сохранения {envelope.schemaVersion} новее поддерживаемой {CurrentSchemaVersion}.");
                    if (envelope.schemaVersion < 1)
                        throw new InvalidDataException("У обёрнутого сохранения отсутствует допустимая версия схемы.");

                    Normalize(envelope.playerData);
                    parsed = new ParsedSave(envelope.playerData, envelope.schemaVersion < CurrentSchemaVersion);
                    return true;
                }

                // Before schema versioning, the JSON root was PlayerData itself.
                if (json.IndexOf("\"money\"", StringComparison.Ordinal) < 0 ||
                    json.IndexOf("\"day\"", StringComparison.Ordinal) < 0)
                    throw new InvalidDataException("Не распознана старая схема сохранения.");

                PlayerData legacyData = JsonUtility.FromJson<PlayerData>(json);
                if (legacyData == null)
                    throw new InvalidDataException("Не удалось прочитать данные старого сохранения.");

                Normalize(legacyData);
                parsed = new ParsedSave(legacyData, true);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        private static void Normalize(PlayerData data)
        {
            if (data.completedEducation == null) data.completedEducation = new System.Collections.Generic.List<string>();
            if (data.businesses == null) data.businesses = new System.Collections.Generic.List<ZeroToHero.Business.BusinessData>();
            if (data.activeDeposits == null) data.activeDeposits = new System.Collections.Generic.List<ZeroToHero.Bank.DepositData>();
            if (data.transactions == null) data.transactions = new System.Collections.Generic.List<MoneyTransactionData>();
            if (data.assets == null) data.assets = new ZeroToHero.Assets.AssetData();
            if (data.assets.ownedHouses == null) data.assets.ownedHouses = new System.Collections.Generic.List<string>();
            if (data.assets.ownedCars == null) data.assets.ownedCars = new System.Collections.Generic.List<string>();
            if (data.assets.ownedClothing == null) data.assets.ownedClothing = new System.Collections.Generic.List<string>();
            if (data.workTimer == null) data.workTimer = new WorkTimerData();
            if (data.studyTimer == null) data.studyTimer = new StudyTimerData();
        }

        private sealed class ParsedSave
        {
            public PlayerData Data { get; }
            public bool Migrated { get; }

            public ParsedSave(PlayerData data, bool migrated)
            {
                Data = data;
                Migrated = migrated;
            }
        }
    }
}
