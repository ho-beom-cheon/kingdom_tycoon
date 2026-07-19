using System.Collections;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class BootstrapFlowTests
    {
        [UnityTest]
        public IEnumerator Bootstrap_LoadsKingdomAndKeepsAppRootAcrossScenes()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom");

            AppRoot root = AppRoot.Instance;
            Assert.That(root, Is.Not.Null);
            Assert.That(root.IsInitialized, Is.True);
            Assert.That(root.Services.Get<SceneFlowService>().IsInitialized, Is.True);
            Assert.That(root.CommonUiRoot, Is.Not.Null);
            Assert.That(root.CommonUiRoot.HasAllRequiredLayers(), Is.True);
            Assert.That(Object.FindFirstObjectByType<SampleKingdomScreen>(), Is.Not.Null);

            yield return root.Services.Get<SceneFlowService>().LoadSceneAsync("Region");

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Region"));
            Assert.That(AppRoot.Instance, Is.SameAs(root));
        }
    }
}
