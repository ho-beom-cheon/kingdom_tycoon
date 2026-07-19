using System;
using System.IO;
using KingdomTycoon.Bootstrap;
using NUnit.Framework;

namespace KingdomTycoon.Tests.PlayMode
{
    [SetUpFixture]
    public sealed class PlayModeSaveIsolation
    {
        private string root;

        [OneTimeSetUp]
        public void CreateIsolatedSaveRoot()
        {
            root = Path.Combine(Path.GetTempPath(), "kingdom-tycoon-playmode-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            AppRoot.TestPersistentDataPath = root;
        }

        [OneTimeTearDown]
        public void RemoveIsolatedSaveRoot()
        {
            AppRoot.TestPersistentDataPath = null;
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
