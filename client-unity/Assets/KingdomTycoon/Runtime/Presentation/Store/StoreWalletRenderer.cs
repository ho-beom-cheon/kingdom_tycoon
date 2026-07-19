using TMPro;
using UnityEngine;

namespace KingdomTycoon.Presentation.Store
{
    public sealed class StoreWalletRenderer : MonoBehaviour
    {
        [SerializeField] private TMP_Text kingdomGold;
        [SerializeField] private TMP_Text personalGold;

        public void Configure(TMP_Text kingdom, TMP_Text personal)
        {
            kingdomGold = kingdom;
            personalGold = personal;
        }

        public void Render(long kingdom, long personal)
        {
            kingdomGold.text = $"왕국 금고  {kingdom:N0}";
            personalGold.text = $"개인 지갑  {personal:N0}";
        }
    }
}
