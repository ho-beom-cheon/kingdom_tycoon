using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KingdomTycoon.Application.Profiles
{
    public enum ProfileLocateKind
    {
        NONE,
        ONE,
        AMBIGUOUS
    }

    public sealed class ProfileLocateResult
    {
        public ProfileLocateResult(ProfileLocateKind kind, string profileId, IReadOnlyList<string> validCandidates, IReadOnlyList<string> invalidEntries)
        {
            Kind = kind;
            ProfileId = profileId;
            ValidCandidates = validCandidates;
            InvalidEntries = invalidEntries;
        }

        public ProfileLocateKind Kind { get; }
        public string ProfileId { get; }
        public IReadOnlyList<string> ValidCandidates { get; }
        public IReadOnlyList<string> InvalidEntries { get; }
    }

    public interface IProfileLocator
    {
        Task<ProfileLocateResult> LocateAsync(CancellationToken cancellationToken);
    }
}
