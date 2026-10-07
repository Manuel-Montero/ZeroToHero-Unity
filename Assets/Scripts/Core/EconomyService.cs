using System;
using ZeroToHero.Player;

namespace ZeroToHero.Core
{
    public class EconomyService
    {
        private PlayerData _player;

        // Событие изменения баланса (передаем новый баланс и причину)
        public event Action<int, string> OnBalanceChanged;

        public EconomyService(PlayerData player)
        {
            _player = player;
        }

        public bool AddMoney(int amount, string reason = "")
        {
            if (amount <= 0) return false;

            _player.money += amount;
            OnBalanceChanged?.Invoke(_player.money, reason);
            return true;
        }

        public bool SpendMoney(int amount, string reason = "")
        {
            if (amount <= 0 || _player.money < amount) return false;

            _player.money -= amount;
            OnBalanceChanged?.Invoke(_player.money, reason);
            return true;
        }

        public bool HasEnoughMoney(int amount)
        {
            return _player.money >= amount;
        }
    }
}