using UnityEngine;
using UnityEngine.UI;
using FetchVR.Dog;

public class SettingScreen : MonoBehaviour
{
    public const string MasterVolumePrefKey = "Audio.MasterVolume";
    public const float DefaultMasterVolume = 1f;

    [SerializeField] private GameObject screenRoot;
    [SerializeField] private BgmController bgmController;
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

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
        RefreshUi();
    }

    private void OnEnable()
    {
        RefreshUi();
    }

    public void Show()
    {
        if (screenRoot != null)
        {
            screenRoot.SetActive(true);
        }

        RefreshUi();
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

    public void SetSfxVolume(float normalizedVolume)
    {
        float clampedVolume = Mathf.Clamp01(normalizedVolume);
        PlayerPrefs.SetFloat(DogAudioController.SfxVolumePrefKey, clampedVolume);
        PlayerPrefs.Save();

        DogAudioController[] dogAudioControllers = FindObjectsByType<DogAudioController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < dogAudioControllers.Length; i++)
        {
            if (dogAudioControllers[i] != null)
            {
                dogAudioControllers[i].SetSfxVolume(clampedVolume);
            }
        }
    }

    public float GetSfxVolume()
    {
        return PlayerPrefs.GetFloat(DogAudioController.SfxVolumePrefKey, DogAudioController.DefaultSfxVolume);
    }

    private void ApplySavedMasterVolume()
    {
        AudioListener.volume = GetMasterVolume();
    }

    public void RefreshUi()
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(GetMasterVolume());
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.SetValueWithoutNotify(GetBgmVolume());
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(GetSfxVolume());
        }
    }
}
