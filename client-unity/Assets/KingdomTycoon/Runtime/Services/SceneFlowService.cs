using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KingdomTycoon.Services
{
    public sealed class SceneFlowService : IAppService
    {
        public int InitializationOrder => 100;

        public bool IsInitialized { get; private set; }

        public void Initialize(ServiceRegistry services)
        {
            IsInitialized = true;
        }

        public void Shutdown()
        {
            IsInitialized = false;
        }

        public AsyncOperation LoadSceneAsync(string sceneName)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Scene flow service is not initialized.");
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name is required.", nameof(sceneName));
            }

            return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
    }
}
