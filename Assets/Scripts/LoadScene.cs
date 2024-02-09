using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadScene : MonoBehaviour
{
    public void Load(string sceneName)
    {
        PlayerPrefs.SetString("sceneToLoad", sceneName);
        SceneManager.LoadScene("Loading");
    }
}