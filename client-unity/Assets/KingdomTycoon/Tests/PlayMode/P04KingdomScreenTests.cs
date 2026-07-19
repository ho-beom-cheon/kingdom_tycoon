using System.Collections;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Presentation.Kingdom;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class P04KingdomScreenTests
    {
        [UnitySetUp]
        public IEnumerator ResetPersistentAppRoot()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator DestroyPersistentAppRoot()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_BindsEightCanonicalFacilityViews()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom");
            KingdomScreenPresenter presenter = Object.FindAnyObjectByType<KingdomScreenPresenter>();
            Assert.That(presenter, Is.Not.Null);
            yield return new WaitUntil(() => presenter.IsBound);
            Assert.That(presenter.BoundFacilityCount, Is.EqualTo(8));
        }
    }
}
