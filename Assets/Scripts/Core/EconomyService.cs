using UnityEngine;
using ZeroToHero.Player;

namespace ZeroToHero.Core
{
    public class EconomyService
    {
        private PlayerData _playerData;

        public EconomyService(PlayerData playerData)
        {
            _playerData = playerData;
        }

        public void AddMoney(int amount, string source)
        {
            if (amount <= 0) return;
            _playerData.money += amount;
            Debug.Log($"[Economy] Earned +{amount} KZT from {source}. Total: {_playerData.money}");
        }

        public bool SpendMoney(int amount, string category)
        {
            if (amount <= 0) return true;
            if (_playerData.money < amount)
            {
                Debug.LogWarning($"[Economy] Not enough money for {category}. Needed: {amount}, Current: {_playerData.money}");
                return false;
            }

            _playerData.money -= amount;
            Debug.Log($"[Economy] Spent -{amount} KZT on {category}. Remaining: {_playerData.money}");
            return true;
        }
    }
}