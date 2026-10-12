using System;
using System.Collections.Generic;
using UnityEngine;
using ZeroToHero.Core;
using ZeroToHero.Player;

namespace ZeroToHero.Bank
{
    public class BankService
    {
        private PlayerData _player;
        private EconomyService _economy;

        public Dictionary<string, DepositConfig> Configs { get; private set; }
            = new Dictionary<string, DepositConfig>();

        public event Action OnBankUpdated;

        public BankService(PlayerData player, EconomyService economy)
        {
            _player = player;
            _economy = economy;

            InitDefaultConfigs();
        }

        private void InitDefaultConfigs()
        {
            var quickDeposit = new DepositConfig
            {
                id = "dep_quick",
                title = "Быстрый вклад (2 мин)",
                durationInSeconds = 120f,
                profitPercent = 0.10f
            };

            Configs.Add(quickDeposit.id, quickDeposit);
        }

        public DepositData GetDepositData(string depositId)
        {
            if (_player.activeDeposits == null)
            {
                _player.activeDeposits = new List<DepositData>();
            }

            var data = _player.activeDeposits.Find(d => d.id == depositId);
            if (data == null)
            {
                data = new DepositData { id = depositId, investedAmount = 0, timer = 0, isActive = false, isReadyToClaim = false };
                _player.activeDeposits.Add(data);
            }

            return data;
        }

        // Открытие вклада
        public bool OpenDeposit(string depositId, int amount)
        {
            if (!Configs.TryGetValue(depositId, out var config)) return false;
            var data = GetDepositData(depositId);

            if (data.isActive || data.isReadyToClaim) return false;
            if (amount <= 0) return false;

            if (!_economy.SpendMoney(amount, $"Вклад в банк: {config.title}"))
            {
                Debug.Log("[BankService] Недостаточно средств для вклада!");
                return false;
            }

            data.investedAmount = amount;
            data.timer = config.durationInSeconds;
            data.isActive = true;
            data.isReadyToClaim = false;

            Debug.Log($"[BankService] Открыт вклад '{config.title}' на сумму {amount} KZT");
            OnBankUpdated?.Invoke();
            return true;
        }

        // Обновление таймера
        public void UpdateTimer(float deltaTime)
        {
            if (_player.activeDeposits == null) return;

            bool changed = false;
            foreach (var data in _player.activeDeposits)
            {
                if (data.isActive && !data.isReadyToClaim)
                {
                    data.timer -= deltaTime;
                    if (data.timer <= 0f)
                    {
                        data.timer = 0f;
                        data.isActive = false;
                        data.isReadyToClaim = true;
                        changed = true;
                        Debug.Log($"[BankService] Вклад {data.id} готов к выплате!");
                    }
                }
            }

            if (changed)
            {
                OnBankUpdated?.Invoke();
            }
        }

        // Забрать деньги с процентом
        public bool ClaimDeposit(string depositId)
        {
            if (!Configs.TryGetValue(depositId, out var config)) return false;
            var data = GetDepositData(depositId);

            if (!data.isReadyToClaim) return false;

            int totalPayout = data.investedAmount + Mathf.RoundToInt(data.investedAmount * config.profitPercent);
            _economy.AddMoney(totalPayout, $"Выплата по вкладу: {config.title}");

            data.investedAmount = 0;
            data.isReadyToClaim = false;

            Debug.Log($"[BankService] Выплачено по вкладу: {totalPayout} KZT");
            OnBankUpdated?.Invoke();
            return true;
        }
    }
}