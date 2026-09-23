using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public sealed class ResetConfirmation : MonoBehaviour
{
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        if (yesButton != null) yesButton.onClick.AddListener(Confirm);
        if (noButton != null) noButton.onClick.AddListener(Close);
    }

    private void OnDisable()
    {
        if (yesButton != null) yesButton.onClick.RemoveListener(Confirm);
        if (noButton != null) noButton.onClick.RemoveListener(Close);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        SetCanvasGroupVisible(true);
    }

    private void Confirm()
    {
        PilotProgressManager.Instance?.ResetProgress();
        ScoreManager.Instance?.ResetProgress();
        Analytics.Log("progress_reset");
        Close();
    }

    private void Close()
    {
        SetCanvasGroupVisible(false);
        gameObject.SetActive(false);
    }

    private void SetCanvasGroupVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }
}
