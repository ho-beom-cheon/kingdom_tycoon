using System;
using System.Threading;
using System.Threading.Tasks;
using KingdomTycoon.Infrastructure.Facilities;

namespace KingdomTycoon.Application.Facilities.Commands
{
    public abstract class FacilityCommandHandlerBase
    {
        protected FacilityCommandHandlerBase(FacilityGameService service) => Service = service ?? throw new ArgumentNullException(nameof(service));
        protected FacilityGameService Service { get; }
        protected static Task<FacilityOperationResult> Completed(FacilityOperationResult result, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    public sealed class StartFacilityBuildHandler : FacilityCommandHandlerBase
    {
        public StartFacilityBuildHandler(FacilityGameService service) : base(service) { }
        public Task<FacilityOperationResult> HandleAsync(StartFacilityBuildCommand command, CancellationToken cancellationToken) => Completed(Service.StartBuild(command), cancellationToken);
    }
    public sealed class StartFacilityUpgradeHandler : FacilityCommandHandlerBase
    {
        public StartFacilityUpgradeHandler(FacilityGameService service) : base(service) { }
        public Task<FacilityOperationResult> HandleAsync(StartFacilityUpgradeCommand command, CancellationToken cancellationToken) => Completed(Service.StartUpgrade(command), cancellationToken);
    }
    public sealed class ClaimFacilityJobHandler : FacilityCommandHandlerBase
    {
        public ClaimFacilityJobHandler(FacilityGameService service) : base(service) { }
        public Task<FacilityOperationResult> HandleAsync(ClaimFacilityJobCommand command, CancellationToken cancellationToken) => Completed(Service.Claim(command), cancellationToken);
    }
    public sealed class AssignManagementNpcHandler : FacilityCommandHandlerBase
    {
        public AssignManagementNpcHandler(FacilityGameService service) : base(service) { }
        public Task<FacilityOperationResult> HandleAsync(AssignManagementNpcCommand command, CancellationToken cancellationToken) => Completed(Service.Assign(command), cancellationToken);
    }
    public sealed class UnassignManagementNpcHandler : FacilityCommandHandlerBase
    {
        public UnassignManagementNpcHandler(FacilityGameService service) : base(service) { }
        public Task<FacilityOperationResult> HandleAsync(UnassignManagementNpcCommand command, CancellationToken cancellationToken) => Completed(Service.Unassign(command), cancellationToken);
    }
}
