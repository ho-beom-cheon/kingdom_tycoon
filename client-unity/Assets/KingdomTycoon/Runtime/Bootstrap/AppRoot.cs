using System.Collections;
using System;
using System.Threading;
using System.Threading.Tasks;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Content;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Combat;
using KingdomTycoon.Infrastructure.Economy;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Inventory;
using KingdomTycoon.Infrastructure.Mercenaries;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Infrastructure.Production;
using KingdomTycoon.Infrastructure.EquipmentGrowth;
using KingdomTycoon.Infrastructure.Progression;
using KingdomTycoon.Services;
using KingdomTycoon.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace KingdomTycoon.Bootstrap
{
    public sealed class AppRoot : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string firstScene = "Kingdom";
        [SerializeField] private bool loadFirstSceneOnStart = true;

        public static AppRoot Instance { get; private set; }

        public ServiceRegistry Services { get; private set; }

        public bool IsInitialized => Services != null && Services.IsInitialized;

        public CommonUiRoot CommonUiRoot => GetComponentInChildren<CommonUiRoot>(true);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Services = new ServiceRegistry();
            if (inputActions != null)
            {
                Services.Register(new InputService(inputActions));
            }

            Services.Register(new LocalizationService());
            TextAsset saveSchema = Resources.Load<TextAsset>("Contracts/save.schema");
            TextAsset p07SaveSchema = Resources.Load<TextAsset>("Contracts/save.content.5.schema");
            TextAsset p08SaveSchema = Resources.Load<TextAsset>("Contracts/save.content.6.schema");
            TextAsset p09SaveSchema = Resources.Load<TextAsset>("Contracts/save.content.7.schema");
            TextAsset p10SaveSchema = Resources.Load<TextAsset>("Contracts/save.content.8.schema");
            TextAsset p11SaveSchema = Resources.Load<TextAsset>("Contracts/save.content.9.schema");
            if (saveSchema == null)
            {
                throw new System.InvalidOperationException("P03 save schema resource is missing.");
            }

            TextAsset p04Template = Resources.Load<TextAsset>("Contracts/p04-new-game.template");
            TextAsset newGameTemplate = Resources.Load<TextAsset>("Contracts/p11-new-game.template");
            if (newGameTemplate == null)
            {
                throw new InvalidOperationException("P11 new-game template resource is missing.");
            }
            if (p04Template == null) throw new InvalidOperationException("P04 migration template resource is missing.");
            TextAsset p05MigrationGolden = Resources.Load<TextAsset>("Contracts/p05-migration-after.golden");
            if (p05MigrationGolden == null) throw new InvalidOperationException("P05 migration golden resource is missing.");

            if (p07SaveSchema == null) throw new InvalidOperationException("P07 save schema resource is missing.");
            if (p08SaveSchema == null) throw new InvalidOperationException("P08 save schema resource is missing.");
            if (p09SaveSchema == null) throw new InvalidOperationException("P09 save schema resource is missing.");
            if (p10SaveSchema == null) throw new InvalidOperationException("P10 save schema resource is missing.");
            if (p11SaveSchema == null) throw new InvalidOperationException("P11 save schema resource is missing.");
            Services.Register(new SaveService(UnityEngine.Application.persistentDataPath, saveSchema.text, p07SaveSchema.text, p08SaveSchema.text, p09SaveSchema.text, p10SaveSchema.text, p11SaveSchema.text));
            IStreamingAssetReader contentReader = UnityEngine.Application.platform == RuntimePlatform.Android
                ? new AndroidStreamingAssetReader(UnityEngine.Application.streamingAssetsPath)
                : new LocalStreamingAssetReader(UnityEngine.Application.streamingAssetsPath);
            Services.Register(new ContentCatalogService(contentReader, new CompileTimeActiveContentVersionProvider()));
            Services.Register(new FacilityGameService(new SystemTrustedUtcClock(), newGameTemplate.text, p04Template.text));
            Services.Register(new MercenaryRosterService(new SystemTrustedUtcClock(), p05MigrationGolden.text));
            Services.Register(new InventoryGameService(new SystemTrustedUtcClock()));
            Services.Register(new EconomyGameService(new SystemTrustedUtcClock()));
            Services.Register(new CombatGameService(new SystemTrustedUtcClock()));
            Services.Register(new ProductionGameService(new SystemTrustedUtcClock()));
            Services.Register(new EquipmentGrowthGameService(new SystemTrustedUtcClock()));
            Services.Register(new ProgressionGameService(new SystemTrustedUtcClock()));
            Services.Register(new SceneFlowService());
            Services.InitializeAll();
        }

        private IEnumerator Start()
        {
            Task bootstrap = BootstrapGameAsync();
            while (!bootstrap.IsCompleted)
            {
                yield return null;
            }
            if (bootstrap.IsFaulted)
            {
                throw bootstrap.Exception?.GetBaseException() ?? new InvalidOperationException("P04 bootstrap failed.");
            }

            if (!loadFirstSceneOnStart || SceneManager.GetActiveScene().name != "Bootstrap")
            {
                yield break;
            }

            AsyncOperation operation = Services.Get<SceneFlowService>().LoadSceneAsync(firstScene);
            yield return operation;
        }

        private async Task BootstrapGameAsync()
        {
            await Services.Get<ContentCatalogService>().LoadActiveAsync(CancellationToken.None);
            await Services.Get<FacilityGameService>().BootstrapAsync(CancellationToken.None);
            Services.Get<MercenaryRosterService>().Bootstrap();
            Services.Get<InventoryGameService>().Bootstrap();
            Services.Get<EconomyGameService>().Bootstrap();
            Services.Get<ProgressionGameService>().Bootstrap();
            Services.Get<CombatGameService>().Bootstrap();
            Services.Get<ProductionGameService>().Bootstrap();
            Services.Get<EquipmentGrowthGameService>().Bootstrap();
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            Services?.ShutdownAll();
            Services = null;
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }
    }
}
