using System;
using UnityEngine;
using ZeroToHero.Assets;
using ZeroToHero.Player;

namespace ZeroToHero.Core
{
    public class TimeManager
    {
        private PlayerData _player;
        private EconomyService _economy;
        private AssetService _assets;

        private float _dayTimer = 0f;
        private const float SECONDS_PER_DAY = 10f; // 10 секунд реального времени = 1 день

        public event Action OnDayPassed;
        public event Action OnMonthPassed;

        public TimeManager(PlayerData player, EconomyService economy, AssetService assets)
        {
            _player = player;
            _economy = economy;
            _assets = assets;
        }

        public void UpdateTimer(float deltaTime)
        {
            _dayTimer += deltaTime;
            if (_dayTimer >= SECONDS_PER_DAY)
            {
                _dayTimer -= SECONDS_PER_DAY;
                AdvanceDay();
            }
        }

        private void AdvanceDay()
        {
            _player.day++;

            if (_player.day > 30)
            {
                _player.day = 1;
                _player.month++;
                ProcessMonthlyExpenses();
                OnMonthPassed?.Invoke();
            }

            OnDayPassed?.Invoke();
        }

        // Ежемесячные списания за жилье и авто
        private void ProcessMonthlyExpenses()
        {
            int totalExpenses = 0;

            if (_assets.HasAsset("house_hostel")) totalExpenses += 50;     // Аренда койко-места
            if (_assets.HasAsset("house_apartment")) totalExpenses += 300; // Аренда квартиры
            if (_assets.HasAsset("car_sedan")) totalExpenses += 100;      // Обслуживание авто

            if (totalExpenses > 0)
            {
                _economy.SpendMoney(totalExpenses, $"Ежемесячные расходы (Месяц {_player.month - 1})");
                Debug.Log($"[TimeManager] Списано {totalExpenses} KZT за ежемесячное обслуживание.");
            }
        }
    }
}