using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core
{
    public class Game : MonoBehaviour
    {
        private static Game _instance;
        public static Game Instance => _instance;
        public static GameConfig Config;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var gameScene = SceneManager.CreateScene("Core", new CreateSceneParameters(LocalPhysicsMode.None));
            _instance = new GameObject("Core").AddComponent<Game>();
            SceneManager.MoveGameObjectToScene(_instance.gameObject, gameScene);
            Config = new GameConfig();
        }
    }
}
