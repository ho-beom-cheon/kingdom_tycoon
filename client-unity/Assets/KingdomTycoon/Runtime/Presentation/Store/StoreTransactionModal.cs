using System;
using KingdomTycoon.Application.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Store
{
    public sealed class StoreTransactionModal : MonoBehaviour
    {
        [SerializeField] private TMP_Text body;
        [SerializeField] private Button confirm;
        [SerializeField] private Button cancel;

        public void Configure(TMP_Text value, Button yes, Button no)
        {
            body = value;
            confirm = yes;
            cancel = no;
        }

        public void Bind(Action accepted, Action rejected)
        {
            confirm.onClick.RemoveAllListeners();
            cancel.onClick.RemoveAllListeners();
            confirm.onClick.AddListener(() => accepted());
            cancel.onClick.AddListener(() => rejected());
        }

        public void Show(StoreProductDto product)
        {
            body.text = $"{product.Name}\n\n수량  1\n결제  <color=#F2D47A>{product.UnitPrice:N0} 개인 골드</color>\n\n구매 후 왕국 금고로 이전됩니다.";
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
