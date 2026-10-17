using UnityEngine;
using UnityEngine.SceneManagement;

// Szenenwechsel für die Buttons im Menü und den Zurück-Button.
// Die Szenennamen müssen exakt so in der Scene List der Build Profiles stehen.
public class SceneLoader : MonoBehaviour
{
    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void QuitApp()
    {
        Application.Quit();
    }
}
