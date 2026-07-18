using System;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Application.Inventory;
using KingdomTycoon.Infrastructure.Inventory;
using UnityEngine;

namespace KingdomTycoon.Presentation.Inventory
{
    public sealed class InventoryScreenPresenter : MonoBehaviour
    {
        [SerializeField] private InventoryScreenView view;
        private InventoryGameService service;

        public void Configure(InventoryScreenView value) => view = value;

        private void Start()
        {
            if (view == null) view = GetComponentInChildren<InventoryScreenView>(true);
            view.Bind(() => view.ShowPolicy(true), () => view.ShowSale(true));
            if (AppRoot.Instance == null || !AppRoot.Instance.IsInitialized)
            { view.ShowState(InventoryUiState.Content); return; }
            try
            {
                service = AppRoot.Instance.Services.Get<InventoryGameService>();
                service.Changed += OnChanged;
                view.Render(service.GetSnapshot());
            }
            catch (Exception exception)
            { view.ShowState(InventoryUiState.Error, exception.Message); }
        }

        private void OnChanged(object sender, InventorySnapshotDto value) => view.Render(value);
        private void OnDestroy() { if (service != null) service.Changed -= OnChanged; }
    }
}
