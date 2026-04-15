using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleScreen : MonoBehaviour
{
    [SerializeField] private ToturialScreen tutorialScreen;
    [SerializeField] private SettingScreen settingScreen;

    private void Awake()
    {
        if (tutorialScreen == null)
        {
            tutorialScreen = FindFirstObjectByType<ToturialScreen>(FindObjectsInactive.Include);
        }

        if (settingScreen == null)
        {
            settingScreen = FindFirstObjectByType<SettingScreen>(FindObjectsInactive.Include);
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene(1);
    }

    public void ShowTutorial()
    {
        if (tutorialScreen == null)
        {
            tutorialScreen = FindFirstObjectByType<ToturialScreen>(FindObjectsInactive.Include);
        }

        if (tutorialScreen == null)
        {
            Debug.LogWarning($"{nameof(TitleScreen)} could not find a {nameof(ToturialScreen)} instance in the scene.", this);
            return;
        }

        tutorialScreen.Show();
    }

    public void ShowSetting()
    {
        if (settingScreen == null)
        {
            settingScreen = FindFirstObjectByType<SettingScreen>(FindObjectsInactive.Include);
        }

        if (settingScreen == null)
        {
            Debug.LogWarning($"{nameof(TitleScreen)} could not find a {nameof(SettingScreen)} instance in the scene.", this);
            return;
        }

        settingScreen.Show();
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
