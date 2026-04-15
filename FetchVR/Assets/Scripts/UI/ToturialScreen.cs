using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ToturialScreen : MonoBehaviour
{
    [Serializable]
    public class TutorialPage
    {
        [TextArea(3, 12)]
        public string content;
    }

    [Header("References")]
    [SerializeField] private TMP_Text contentText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private GameObject screenRoot;

    [Header("Pages")]
    [SerializeField] private List<TutorialPage> pages = new List<TutorialPage>();
    [SerializeField] private int startingPageIndex;

    [Header("State")]
    [SerializeField] private int currentPageIndex;

    public int CurrentPageIndex => currentPageIndex;
    public int PageCount => pages.Count;

    private void Awake()
    {
        if (screenRoot == null)
        {
            screenRoot = gameObject;
        }

        currentPageIndex = GetClampedPageIndex(startingPageIndex);
        Refresh();
    }

    private void OnEnable()
    {
        currentPageIndex = GetClampedPageIndex(currentPageIndex);
        Refresh();
    }

    public void Show()
    {
        if (screenRoot != null)
        {
            screenRoot.SetActive(true);
        }

        currentPageIndex = GetClampedPageIndex(currentPageIndex);
        Refresh();
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

    public void NextPage()
    {
        if (pages.Count == 0)
        {
            return;
        }

        currentPageIndex = Mathf.Min(currentPageIndex + 1, pages.Count - 1);
        Refresh();
    }

    public void PreviousPage()
    {
        if (pages.Count == 0)
        {
            return;
        }

        currentPageIndex = Mathf.Max(currentPageIndex - 1, 0);
        Refresh();
    }

    public void GoToPage(int pageIndex)
    {
        currentPageIndex = GetClampedPageIndex(pageIndex);
        Refresh();
    }

    private int GetClampedPageIndex(int pageIndex)
    {
        if (pages.Count == 0)
        {
            return 0;
        }

        return Mathf.Clamp(pageIndex, 0, pages.Count - 1);
    }

    private void Refresh()
    {
        if (contentText != null)
        {
            contentText.text = GetCurrentPageContent();
        }

        if (previousButton != null)
        {
            previousButton.interactable = pages.Count > 0 && currentPageIndex > 0;
        }

        if (nextButton != null)
        {
            nextButton.interactable = pages.Count > 0 && currentPageIndex < pages.Count - 1;
        }

        if (closeButton != null)
        {
            closeButton.interactable = true;
        }
    }

    private string GetCurrentPageContent()
    {
        if (pages.Count == 0)
        {
            return string.Empty;
        }

        TutorialPage page = pages[currentPageIndex];
        return page != null ? page.content : string.Empty;
    }
}
