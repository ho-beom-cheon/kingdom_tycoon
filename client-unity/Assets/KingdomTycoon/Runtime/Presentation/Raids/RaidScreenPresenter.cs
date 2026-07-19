using System;
using System.Collections.Generic;
using KingdomTycoon.Application.Raids;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Domain.Raids;
using KingdomTycoon.Infrastructure.Raids;
using UnityEngine;

namespace KingdomTycoon.Presentation.Raids
{
    public sealed class RaidScreenPresenter : MonoBehaviour
    {
        [SerializeField] private RaidScreenView view; private RaidGameService service;
        public void Configure(RaidScreenView value) => view = value;
        public void Open()
        {
            gameObject.SetActive(true); view.SetVisible(true);
            try { service ??= AppRoot.Instance?.Services.Get<RaidGameService>() ?? throw new InvalidOperationException("P14_APP_ROOT_MISSING"); Bind(); view.Render(service.GetOverview()); }
            catch (Exception error) { view.ShowError(Code(error)); }
        }
        private void Bind() { view.CloseRequested -= Close; view.CloseRequested += Close; view.DeployRequested -= Deploy; view.DeployRequested += Deploy; }
        private void Close() { view.SetVisible(false); gameObject.SetActive(false); }
        private void Deploy(RaidSummaryDto raid, IReadOnlyList<string> party, string part, bool accepted)
        {
            try { RaidOperationResult result = service.Resolve(service.CreateResolveCommand(raid.Id, raid.Difficulty, party, part, accepted)); view.Render(service.GetOverview()); view.RenderResult(result); }
            catch (Exception error) { view.ShowError(Code(error)); }
        }
        private static string Code(Exception error) => error is RaidDomainException domain ? domain.Code : error.GetBaseException().Message;
    }
}
