using System;
using System.IO;
using UnityEngine;
using ZeroToHero.Player;

namespace ZeroToHero.Core
{
    public class SaveService
    {
        private readonly string _saveFilePath;

        public SaveService()
        {
            _saveFilePath = Path.Combine(Application.persistentDataPath, "player_save.json");
        }

        public void Save(PlayerData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(_saveFilePath, json);
                Debug.Log($"[SaveService] Прогресс сохранен в: {_saveFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Ошибка при сохранении: {e.Message}");
            }
        }

        public PlayerData Load()
        {
            if (!File.Exists(_saveFilePath))
            {
                Debug.Log("[SaveService] Файл сохранения не найден. Создаем новый профиль.");
                return new PlayerData();
            }

            try
            {
                string json = File.ReadAllText(_saveFilePath);
                PlayerData data = JsonUtility.FromJson<PlayerData>(json);
                
                if (data == null)
                {
                    Debug.LogWarning("[SaveService] Файл сохранения пуст. Создаем новый профиль.");
                    return new PlayerData();
                }

                Debug.Log("[SaveService] Прогресс успешно загружен!");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Ошибка при загрузке: {e.Message}. Создаем новый профиль.");
                return new PlayerData();
            }
        }

        public void ClearSave()
        {
            if (File.Exists(_saveFilePath))
            {
                File.Delete(_saveFilePath);
                Debug.Log("[SaveService] Сохранение удалено.");
            }
        }
    }
}