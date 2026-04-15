using UnityEngine;

public class SettingScreen : MonoBehaviour
{
    [SerializeField] private GameObject screenRoot;

    private void Awake()
    {
        if (screenRoot == null)
        {
            screenRoot = gameObject;
        }
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
}
