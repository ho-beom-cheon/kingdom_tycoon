namespace KingdomTycoon.Services
{
    public interface IAppService
    {
        int InitializationOrder { get; }

        void Initialize(ServiceRegistry services);

        void Shutdown();
    }
}
