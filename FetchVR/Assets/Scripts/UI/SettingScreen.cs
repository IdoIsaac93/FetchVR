using UnityEngine;

public class SettingScreen : MonoBehaviour
{
    public const string MasterVolumePrefKey = "Audio.MasterVolume";
    public const float DefaultMasterVolume = 1f;

    [SerializeField] private GameObject screenRoot;
    [SerializeField] private BgmController bgmController;

    private void Awake()
    {
        if (screenRoot == null)
        {
            screenRoot = gameObject;
        }

        if (bgmController == null)
        {
            bgmController = FindFirstObjectByType<BgmController>(FindObjectsInactive.Include);
        }

        ApplySavedMasterVolume();
    }

    public void Show()
    {
        if (screenRoot != null)
        {
            screenRoot.SetActive(true);
        }
    }

    public void Hide()
    {
        if (screenRoot != null)
        {
            screenRoot.SetActive(false);
        }
    }

    public void Close()
    {
        Hide();
    }

    public void SetMasterVolume(float normalizedVolume)
    {
        float clampedVolume = Mathf.Clamp01(normalizedVolume);
        AudioListener.volume = clampedVolume;
        PlayerPrefs.SetFloat(MasterVolumePrefKey, clampedVolume);
        PlayerPrefs.Save();
    }

    public float GetMasterVolume()
    {
        return PlayerPrefs.GetFloat(MasterVolumePrefKey, DefaultMasterVolume);
    }

    public void SetBgmVolume(float normalizedVolume)
    {
        if (bgmController == null)
        {
            bgmController = FindFirstObjectByType<BgmController>(FindObjectsInactive.Include);
        }

        if (bgmController != null)
        {
            bgmController.SetBgmVolume(normalizedVolume);
        }
    }

    public float GetBgmVolume()
    {
        if (bgmController == null)
        {
            bgmController = FindFirstObjectByType<BgmController>(FindObjectsInactive.Include);
        }

        return bgmController != null
            ? bgmController.GetBgmVolume()
            : BgmController.DefaultBgmVolume;
    }

    private void ApplySavedMasterVolume()
    {
        AudioListener.volume = GetMasterVolume();
    }
}
