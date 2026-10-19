using UnityEngine;
using UnityEngine.SceneManagement;

namespace TakeUp.Prototype
{
    public sealed class MenuSceneLink : MonoBehaviour
    {
        [SerializeField] string sceneName;
        bool loading;

        public void OpenScene()
        {
            if (loading) return;
            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"Menu scene is not enabled in Build Settings: {sceneName}", this);
                return;
            }
            loading = true;
            Time.timeScale = 1;
            SceneManager.LoadSceneAsync(sceneName);
        }
    }
}
