using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace KingdomTycoon.Services
{
    public sealed class LocalizationService : IAppService
    {
        private AsyncOperationHandle<LocalizationSettings> initializationOperation;

        public int InitializationOrder => 20;

        public bool IsInitializationComplete =>
            initializationOperation.IsValid() && initializationOperation.IsDone;

        public void Initialize(ServiceRegistry services)
        {
            initializationOperation = LocalizationSettings.InitializationOperation;
        }

        public void Shutdown()
        {
            initializationOperation = default;
        }
    }
}
