using UnityEngine;
using UnityEngine.SceneManagement;

public class BgmController : MonoBehaviour
{
    public const string BgmVolumePrefKey = "Audio.BgmVolume";
    public const float DefaultBgmVolume = 0.75f;

    [Header("References")]
    [SerializeField] private AudioSource bgmAudioSource;

    [Header("BGM Clips")]
    [SerializeField] private AudioClip titleBgm;
    [SerializeField] private AudioClip gameplayBgm;

    [Header("Scene Routing")]
    [SerializeField] private string titleSceneName = "TitleScreen";
    [SerializeField] private string gameplaySceneName = "BasicScene";

    private static BgmController s_Instance;

    private void Awake()
    {
        if (s_Instance != null && s_Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_Instance = this;
        DontDestroyOnLoad(gameObject);

        if (bgmAudioSource == null)
        {
            bgmAudioSource = GetComponent<AudioSource>();
        }

        if (bgmAudioSource == null)
        {
            bgmAudioSource = gameObject.AddComponent<AudioSource>();
        }

        bgmAudioSource.playOnAwake = false;
        bgmAudioSource.loop = true;
        bgmAudioSource.spatialBlend = 0f;

        ApplySavedVolume();
        PlayForScene(SceneManager.GetActiveScene());
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void SetBgmVolume(float normalizedVolume)
    {
        float clampedVolume = Mathf.Clamp01(normalizedVolume);
        PlayerPrefs.SetFloat(BgmVolumePrefKey, clampedVolume);
        PlayerPrefs.Save();

        if (bgmAudioSource != null)
        {
            bgmAudioSource.volume = clampedVolume;
        }
    }

    public float GetBgmVolume()
    {
        return PlayerPrefs.GetFloat(BgmVolumePrefKey, DefaultBgmVolume);
    }

    public void RefreshCurrentSceneBgm()
    {
        PlayForScene(SceneManager.GetActiveScene());
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        PlayForScene(scene);
    }

    private void ApplySavedVolume()
    {
        if (bgmAudioSource != null)
        {
            bgmAudioSource.volume = GetBgmVolume();
        }
    }

    private void PlayForScene(Scene scene)
    {
        AudioClip targetClip = GetClipForScene(scene.name);
        if (bgmAudioSource == null || targetClip == null)
        {
            return;
        }

        if (bgmAudioSource.clip == targetClip && bgmAudioSource.isPlaying)
        {
            return;
        }

        bgmAudioSource.clip = targetClip;
        bgmAudioSource.volume = GetBgmVolume();
        bgmAudioSource.Play();
    }

    private AudioClip GetClipForScene(string sceneName)
    {
        if (string.Equals(sceneName, titleSceneName, System.StringComparison.Ordinal))
        {
            return titleBgm;
        }

        if (string.Equals(sceneName, gameplaySceneName, System.StringComparison.Ordinal))
        {
            return gameplayBgm;
        }

        return gameplayBgm != null ? gameplayBgm : titleBgm;
    }
}
