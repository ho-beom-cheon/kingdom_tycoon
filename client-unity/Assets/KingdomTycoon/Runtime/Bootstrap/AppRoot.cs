using System.Collections;
using System.IO;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Save;
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
            if (saveSchema == null)
            {
                throw new System.InvalidOperationException("P03 save schema resource is missing.");
            }

            Services.Register(new SaveService(Application.persistentDataPath, saveSchema.text));
            Services.Register(new ContentCatalogService(Path.Combine(Application.streamingAssetsPath, "Content")));
            Services.Register(new SceneFlowService());
            Services.InitializeAll();
        }

        private IEnumerator Start()
        {
            if (!loadFirstSceneOnStart || SceneManager.GetActiveScene().name != "Bootstrap")
            {
                yield break;
            }

            AsyncOperation operation = Services.Get<SceneFlowService>().LoadSceneAsync(firstScene);
            yield return operation;
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
