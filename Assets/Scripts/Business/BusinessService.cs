using System;
using System.Collections.Generic;
using UnityEngine;
using ZeroToHero.Core;
using ZeroToHero.Player;

namespace ZeroToHero.Business
{
    public class BusinessService
    {
        private PlayerData _player;
        private EconomyService _economy;

        public Dictionary<string, BusinessConfig> Configs { get; private set; } 
            = new Dictionary<string, BusinessConfig>();

        public event Action OnBusinessUpdated;

        public BusinessService(PlayerData player, EconomyService economy)
        {
            _player = player;
            _economy = economy;

            InitDefaultConfigs();
        }

        private void InitDefaultConfigs()
        {
            var coffee = new BusinessConfig
            {
                id = "biz_coffee",
                title = "Кофейный автомат",
                buyCost = 1000,
                baseIncome = 150,
                managerCost = 2500
            };

            Configs.Add(coffee.id, coffee);
        }

        public BusinessData GetBusinessData(string bizId)
        {
            if (_player.businesses == null)
            {
                _player.businesses = new List<BusinessData>();
            }

            var data = _player.businesses.Find(b => b.id == bizId);
            if (data == null)
            {
                data = new BusinessData { id = bizId, isPurchased = false, stars = 0, collectCount = 0, hasManager = false };
                _player.businesses.Add(data);
            }

            return data;
        }

        // Покупка бизнеса
        public bool BuyBusiness(string bizId)
        {
            if (!Configs.TryGetValue(bizId, out var config)) return false;
            var data = GetBusinessData(bizId);

            if (data.isPurchased) return false;

            if (!_economy.SpendMoney(config.buyCost, $"Покупка бизнеса: {config.title}"))
            {
                Debug.Log("[BusinessService] Недостаточно денег для покупки!");
                return false;
            }

            data.isPurchased = true;
            Debug.Log($"[BusinessService] Куплен бизнес: {config.title}");
            OnBusinessUpdated?.Invoke();
            return true;
        }

        // Сбор дохода
        public bool CollectIncome(string bizId)
        {
            if (!Configs.TryGetValue(bizId, out var config)) return false;
            var data = GetBusinessData(bizId);

            if (!data.isPurchased) return false;

            // Расчёт дохода с учётом звёзд (каждая звезда даёт +25% к доходу)
            int finalIncome = config.baseIncome + (config.baseIncome * data.stars / 4);
            _economy.AddMoney(finalIncome, $"Доход от бизнеса: {config.title}");

            data.collectCount++;

            // Логика прокачки звёзд по GDD (5, 10 и 50 сборов)
            if (data.stars == 0 && data.collectCount >= 5) data.stars = 1;
            else if (data.stars == 1 && data.collectCount >= 10) data.stars = 2;
            else if (data.stars == 2 && data.collectCount >= 50) data.stars = 3;

            OnBusinessUpdated?.Invoke();
            return true;
        }
    }
}