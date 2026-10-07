using System;
using UnityEngine;
using ZeroToHero.Core;
using ZeroToHero.Player;

namespace ZeroToHero.Player
{
    public class NeedsService
    {
        private PlayerData _player;
        private EconomyService _economy;

        public event Action OnNeedsChanged;

        public NeedsService(PlayerData player, EconomyService economy)
        {
            _player = player;
            _economy = economy;
        }

        // Потеря показателей при действиях
        public void ConsumeNeeds(int healthCost, int moodCost)
        {
            _player.health = Mathf.Clamp(_player.health - healthCost, 0, 100);
            _player.mood = Mathf.Clamp(_player.mood - moodCost, 0, 100);

            OnNeedsChanged?.Invoke();
        }

        // Восстановление Здоровья (за деньги)
        public bool Heal(int amount, int cost)
        {
            if (_player.health >= 100) return false;
            if (!_economy.SpendMoney(cost, "Восстановление здоровья")) return false;

            _player.health = Mathf.Clamp(_player.health + amount, 0, 100);
            OnNeedsChanged?.Invoke();
            return true;
        }

        // Восстановление Настроения (за деньги)
        public bool Rest(int amount, int cost)
        {
            if (_player.mood >= 100) return false;
            if (!_economy.SpendMoney(cost, "Отдых / Развлечения")) return false;

            _player.mood = Mathf.Clamp(_player.mood + amount, 0, 100);
            OnNeedsChanged?.Invoke();
            return true;
        }

        public bool CanWork()
        {
            return _player.health > 10 && _player.mood > 10;
        }
    }
}