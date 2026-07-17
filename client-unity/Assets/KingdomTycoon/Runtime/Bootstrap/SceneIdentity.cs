using UnityEngine;

namespace KingdomTycoon.Bootstrap
{
    public sealed class SceneIdentity : MonoBehaviour
    {
        [SerializeField] private string sceneId;

        public string SceneId => sceneId;

        public void SetSceneId(string value)
        {
            sceneId = value;
        }
    }
}
