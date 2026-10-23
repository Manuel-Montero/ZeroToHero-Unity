using System;
using System.Collections.Generic;
using UnityEngine;
using ZeroToHero.Core;
using ZeroToHero.Player;

namespace ZeroToHero.Business
{
    public class BusinessService
    {
        public const int MaxAccumulatedIncome = 1500;
        public const float IncomeCycleSeconds = 10f;

        private PlayerData _player;
        private EconomyService _economy;
        private TimeManager _time;

        public Dictionary<string, BusinessConfig> Configs { get; private set; } 
            = new Dictionary<string, BusinessConfig>();

        public event Action OnBusinessUpdated;

        public BusinessService(PlayerData player, EconomyService economy, TimeManager time = null)
        {
            _player = player;
            _economy = economy;
            _time = time;

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
            data.accumulatedIncome = Math.Max(0, Math.Min(MaxAccumulatedIncome, data.accumulatedIncome));
            data.nextIncomeUtcTicks = _time != null
                ? _time.CreateDeadlineTicks(IncomeCycleSeconds)
                : 0;
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

            bool accrualChanged = AccrueIncome(data, config);
            int income = data.accumulatedIncome;
            if (income <= 0)
            {
                if (accrualChanged) OnBusinessUpdated?.Invoke();
                return false;
            }

            if (!_economy.AddMoney(income, $"Доход от бизнеса: {config.title}"))
            {
                if (accrualChanged) OnBusinessUpdated?.Invoke();
                return false;
            }

            data.accumulatedIncome = 0;
            data.nextIncomeUtcTicks = _time != null
                ? _time.CreateDeadlineTicks(IncomeCycleSeconds)
                : 0;

            data.collectCount++;

            // Логика прокачки звёзд по GDD (5, 10 и 50 сборов)
            if (data.stars == 0 && data.collectCount >= 5) data.stars = 1;
            else if (data.stars == 1 && data.collectCount >= 10) data.stars = 2;
            else if (data.stars == 2 && data.collectCount >= 50) data.stars = 3;

            OnBusinessUpdated?.Invoke();
            return true;
        }

        public void UpdateTimer(float unscaledDeltaTime)
        {
            if (_time == null || _player.businesses == null) return;

            bool changed = false;
            foreach (BusinessData data in _player.businesses)
            {
                if (data == null || !data.isPurchased || !Configs.TryGetValue(data.id, out var config))
                    continue;
                changed |= AccrueIncome(data, config);
            }

            if (changed) OnBusinessUpdated?.Invoke();
        }

        public float GetRemainingCycleSeconds(string bizId)
        {
            if (_time == null) return 0f;
            BusinessData data = GetBusinessData(bizId);
            if (!data.isPurchased || data.accumulatedIncome >= MaxAccumulatedIncome ||
                data.nextIncomeUtcTicks <= 0)
                return 0f;
            return _time.GetRemainingSeconds(data.nextIncomeUtcTicks);
        }

        private bool AccrueIncome(BusinessData data, BusinessConfig config)
        {
            if (_time == null)
                return false;

            bool changed = false;
            if (data.accumulatedIncome < 0)
            {
                data.accumulatedIncome = 0;
                changed = true;
            }
            else if (data.accumulatedIncome > MaxAccumulatedIncome)
            {
                data.accumulatedIncome = MaxAccumulatedIncome;
                changed = true;
            }

            if (data.accumulatedIncome >= MaxAccumulatedIncome)
            {
                if (data.nextIncomeUtcTicks != 0)
                {
                    data.nextIncomeUtcTicks = 0;
                    changed = true;
                }
                return changed;
            }

            if (data.nextIncomeUtcTicks <= 0)
            {
                data.nextIncomeUtcTicks = _time.CreateDeadlineTicks(IncomeCycleSeconds);
                return true;
            }

            long now = _time.UtcNowTicks;
            if (now < data.nextIncomeUtcTicks) return changed;

            long cycleTicks = (long)(IncomeCycleSeconds * TimeSpan.TicksPerSecond);
            long elapsedAfterFirstCycle = now - data.nextIncomeUtcTicks;
            long completedCycles = elapsedAfterFirstCycle / cycleTicks + 1;
            long capacity = MaxAccumulatedIncome - Math.Max(0, data.accumulatedIncome);
            long possibleIncome = completedCycles * Math.Max(0, config.baseIncome);
            int addedIncome = (int)Math.Min(capacity, possibleIncome);
            data.accumulatedIncome = Math.Max(0, data.accumulatedIncome) + addedIncome;

            if (data.accumulatedIncome >= MaxAccumulatedIncome)
            {
                data.accumulatedIncome = MaxAccumulatedIncome;
                data.nextIncomeUtcTicks = 0;
            }
            else
            {
                data.nextIncomeUtcTicks += completedCycles * cycleTicks;
            }

            return true;
        }
    }
}
