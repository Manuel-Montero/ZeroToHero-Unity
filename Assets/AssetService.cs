using System;
using System.Collections.Generic;
using UnityEngine;
using ZeroToHero.Core;
using ZeroToHero.Player;

namespace ZeroToHero.Assets
{
    public class AssetService
    {
        private PlayerData _player;
        private EconomyService _economy;

        public Dictionary<string, AssetConfig> Configs { get; private set; } 
            = new Dictionary<string, AssetConfig>();

        public event Action OnAssetsUpdated;

        public AssetService(PlayerData player, EconomyService economy)
        {
            _player = player;
            _economy = economy;

            InitDefaultConfigs();
        }

        private void InitDefaultConfigs()
        {
            // Недвижимость
            var hostel = new AssetConfig { id = "house_hostel", title = "Койко-место", type = AssetType.House, price = 200 };
            var apartment = new AssetConfig { id = "house_apartment", title = "1-комн. Квартира", type = AssetType.House, price = 5000 };

            // Транспорт
            var bicycle = new AssetConfig { id = "car_bicycle", title = "Велосипед", type = AssetType.Car, price = 150 };
            var sedan = new AssetConfig { id = "car_sedan", title = "Подержанный седан", type = AssetType.Car, price = 3000 };

            // Одежда
            var casual = new AssetConfig { id = "wear_casual", title = "Повседневная одежда", type = AssetType.Clothing, price = 100 };
            var suit = new AssetConfig { id = "wear_suit", title = "Деловой костюм", type = AssetType.Clothing, price = 1200 };

            Configs.Add(hostel.id, hostel);
            Configs.Add(apartment.id, apartment);
            Configs.Add(bicycle.id, bicycle);
            Configs.Add(sedan.id, sedan);
            Configs.Add(casual.id, casual);
            Configs.Add(suit.id, suit);
        }

        public bool HasAsset(string assetId)
        {
            if (_player.assets == null) _player.assets = new AssetData();
            
            return _player.assets.ownedHouses.Contains(assetId) || 
                   _player.assets.ownedCars.Contains(assetId) ||
                   _player.assets.ownedClothing.Contains(assetId);
        }

        public bool BuyAsset(string assetId)
        {
            if (!Configs.TryGetValue(assetId, out var config)) return false;
            if (HasAsset(assetId)) return false;

            if (!_economy.SpendMoney(config.price, $"Покупка имущества: {config.title}"))
            {
                Debug.Log("[AssetService] Недостаточно средств для покупки!");
                return false;
            }

            if (config.type == AssetType.House)
            {
                _player.assets.ownedHouses.Add(assetId);
            }
            else if (config.type == AssetType.Car)
            {
                _player.assets.ownedCars.Add(assetId);
            }
            else if (config.type == AssetType.Clothing)
            {
                _player.assets.ownedClothing.Add(assetId);
            }

            Debug.Log($"[AssetService] Куплено имущество: {config.title}");
            OnAssetsUpdated?.Invoke();
            return true;
        }
    }
}