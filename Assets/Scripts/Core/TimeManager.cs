using System;
using UnityEngine;
using ZeroToHero.Assets;
using ZeroToHero.Player;

namespace ZeroToHero.Core
{
    public class TimeManager
    {
        public const float SecondsPerGameDay = 10f;

        private readonly PlayerData _player;
        private readonly EconomyService _economy;
        private readonly AssetService _assets;
        private readonly Func<long> _utcTicksProvider;

        public event Action OnDayPassed;
        public event Action OnMonthPassed;

        public TimeManager(PlayerData player, EconomyService economy, AssetService assets,
            Func<long> utcTicksProvider = null)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _utcTicksProvider = utcTicksProvider ?? (() => DateTime.UtcNow.Ticks);
            _player.dayElapsedSeconds = Mathf.Clamp(_player.dayElapsedSeconds, 0f, SecondsPerGameDay);
        }

        public long UtcNowTicks => _utcTicksProvider();

        public long CreateDeadlineTicks(float durationSeconds)
        {
            long durationTicks = (long)(Math.Max(0d, durationSeconds) * TimeSpan.TicksPerSecond);
            return UtcNowTicks + durationTicks;
        }

        public float GetRemainingSeconds(long endUtcTicks)
        {
            return Mathf.Max(0f, (float)((endUtcTicks - UtcNowTicks) / (double)TimeSpan.TicksPerSecond));
        }

        public void UpdateTimer(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime <= 0f) return;

            _player.dayElapsedSeconds += unscaledDeltaTime;
            while (_player.dayElapsedSeconds >= SecondsPerGameDay)
            {
                _player.dayElapsedSeconds -= SecondsPerGameDay;
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

        private void ProcessMonthlyExpenses()
        {
            int totalExpenses = 0;
            if (_assets.HasAsset("house_hostel")) totalExpenses += 50;
            if (_assets.HasAsset("house_apartment")) totalExpenses += 300;
            if (_assets.HasAsset("car_sedan")) totalExpenses += 100;

            int amountToSpend = Math.Min(totalExpenses, Math.Max(0, _player.money));
            if (amountToSpend > 0)
            {
                _economy.SpendMoney(amountToSpend, $"Ежемесячные расходы (Месяц {_player.month - 1})");
            }

            if (amountToSpend < totalExpenses)
                Debug.Log($"[TimeManager] Денег не хватило на ежемесячные расходы: списано {amountToSpend} из {totalExpenses} KZT; баланс не уходит ниже нуля.");
            else if (amountToSpend > 0)
                Debug.Log($"[TimeManager] Списано {amountToSpend} KZT за ежемесячное обслуживание.");
        }
    }
}
