using System;
using System.Collections.Generic;
using ZeroToHero.Player;

namespace ZeroToHero.Core
{
    public class EconomyService
    {
        public const int MaxTransactionHistory = 1000;

        private readonly PlayerData _player;
        private readonly Func<long> _utcTicksProvider;

        // Событие изменения баланса (передаем новый баланс и причину)
        public event Action<int, string> OnBalanceChanged;

        public EconomyService(PlayerData player, Func<long> utcTicksProvider = null)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _utcTicksProvider = utcTicksProvider ?? (() => DateTime.UtcNow.Ticks);
            if (_player.transactions == null)
                _player.transactions = new List<MoneyTransactionData>();
        }

        public bool AddMoney(int amount, string reason = "")
        {
            if (amount <= 0) return false;

            int balanceBefore = _player.money;
            long balanceAfterLong = (long)balanceBefore + amount;
            if (balanceBefore < 0 || balanceAfterLong > int.MaxValue) return false;

            RecordTransaction("income", reason, amount, balanceBefore, (int)balanceAfterLong);
            _player.money = (int)balanceAfterLong;
            OnBalanceChanged?.Invoke(_player.money, reason);
            return true;
        }

        public bool SpendMoney(int amount, string reason = "")
        {
            if (amount <= 0 || _player.money < amount || _player.money < 0) return false;

            int balanceBefore = _player.money;
            int balanceAfter = balanceBefore - amount;
            RecordTransaction("expense", reason, -amount, balanceBefore, balanceAfter);
            _player.money = balanceAfter;
            OnBalanceChanged?.Invoke(_player.money, reason);
            return true;
        }

        public bool HasEnoughMoney(int amount)
        {
            return amount >= 0 && _player.money >= amount;
        }

        private void RecordTransaction(string type, string source, int amount, int balanceBefore, int balanceAfter)
        {
            if (_player.transactions == null)
                _player.transactions = new List<MoneyTransactionData>();

            MoneyTransactionData transaction = new MoneyTransactionData
            {
                id = Guid.NewGuid().ToString("N"),
                type = type,
                source = source ?? string.Empty,
                amount = amount,
                balanceBefore = balanceBefore,
                balanceAfter = balanceAfter,
                timestampUtcTicks = _utcTicksProvider()
            };

            while (_player.transactions.Count >= MaxTransactionHistory)
                _player.transactions.RemoveAt(0);

            _player.transactions.Add(transaction);
        }
    }
}
