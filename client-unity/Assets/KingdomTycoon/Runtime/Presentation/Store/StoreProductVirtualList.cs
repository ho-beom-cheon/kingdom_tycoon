using System;
using System.Collections.Generic;
using KingdomTycoon.Application.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Store
{
    public sealed class StoreProductVirtualList : MonoBehaviour
    {
        [SerializeField] private Button[] rows;
        [SerializeField] private TMP_Text[] labels;
        private IReadOnlyList<StoreProductDto> products = Array.Empty<StoreProductDto>();

        public event Action<int> Selected;
        public int PoolSize => rows?.Length ?? 0;

        public void Configure(Button[] buttons, TMP_Text[] texts)
        {
            rows = buttons;
            labels = texts;
        }

        public void Bind(IReadOnlyList<StoreProductDto> values, int selected)
        {
            products = values ?? Array.Empty<StoreProductDto>();
            for (int index = 0; index < rows.Length; index++)
            {
                int captured = index;
                rows[index].onClick.RemoveAllListeners();
                rows[index].onClick.AddListener(() => Selected?.Invoke(captured));
                bool visible = index < products.Count;
                rows[index].gameObject.SetActive(visible);
                if (!visible) continue;
                StoreProductDto item = products[index];
                labels[index].text = $"{KindIcon(item.Kind)}  {item.Name}\n<size=20><color=#B9B0A3>{item.QualityId ?? item.SourceType} · 재고 {item.Quantity}</color></size>\n<color=#F2D47A>{item.UnitPrice:N0} G</color>";
                rows[index].GetComponent<Image>().color = index == selected ? new Color32(91, 67, 42, 255) : new Color32(48, 46, 42, 245);
            }
        }

        private static string KindIcon(string kind) => kind switch { "POTION" => "물약", "EQUIPMENT" => "장비", _ => "재료" };
    }
}
