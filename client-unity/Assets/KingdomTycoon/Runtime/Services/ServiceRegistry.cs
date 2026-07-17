using System;
using System.Collections.Generic;

namespace KingdomTycoon.Services
{
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, IAppService> services = new();
        private readonly List<IAppService> initializedServices = new();

        public bool IsInitialized { get; private set; }

        public int Count => services.Count;

        public void Register<TService>(TService service)
            where TService : class, IAppService
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            if (IsInitialized)
            {
                throw new InvalidOperationException("Services cannot be registered after initialization.");
            }

            Type serviceType = typeof(TService);
            if (!services.TryAdd(serviceType, service))
            {
                throw new InvalidOperationException($"Service {serviceType.FullName} is already registered.");
            }
        }

        public TService Get<TService>()
            where TService : class, IAppService
        {
            if (!services.TryGetValue(typeof(TService), out IAppService service))
            {
                throw new KeyNotFoundException($"Service {typeof(TService).FullName} is not registered.");
            }

            return (TService)service;
        }

        public void InitializeAll()
        {
            if (IsInitialized)
            {
                return;
            }

            var orderedServices = new List<IAppService>(services.Values);
            orderedServices.Sort((left, right) => left.InitializationOrder.CompareTo(right.InitializationOrder));

            try
            {
                foreach (IAppService service in orderedServices)
                {
                    service.Initialize(this);
                    initializedServices.Add(service);
                }

                IsInitialized = true;
            }
            catch
            {
                ShutdownInitializedServices();
                throw;
            }
        }

        public void ShutdownAll()
        {
            ShutdownInitializedServices();
            IsInitialized = false;
        }

        private void ShutdownInitializedServices()
        {
            for (int index = initializedServices.Count - 1; index >= 0; index--)
            {
                initializedServices[index].Shutdown();
            }

            initializedServices.Clear();
        }
    }
}
