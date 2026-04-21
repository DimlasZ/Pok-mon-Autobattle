using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HelperPanelUI : MonoBehaviour
{
    public GameObject[] pages;
    public TextMeshProUGUI pageIndicator;
    public Button prevButton;
    public Button nextButton;

    private int _currentPage;

    private void OnEnable()
    {
        ShowPage(0);
    }

    private void Start()
    {
        if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
        if (nextButton != null) nextButton.onClick.AddListener(NextPage);
        ShowPage(0);
    }

    void PrevPage()
    {
        ShowPage(Mathf.Max(0, _currentPage - 1));
    }

    void NextPage()
    {
        ShowPage(Mathf.Min(pages.Length - 1, _currentPage + 1));
    }

    void ShowPage(int index)
    {
        _currentPage = index;
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].SetActive(i == _currentPage);

        if (pageIndicator != null)
            pageIndicator.text = $"{_currentPage + 1} / {pages.Length}";

        if (prevButton != null) prevButton.interactable = _currentPage > 0;
        if (nextButton != null) nextButton.interactable = _currentPage < pages.Length - 1;
    }
}
