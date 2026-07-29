using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonTextOffset : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform textTransform;
    [SerializeField] private Vector2 offset = new Vector2(0, -6f); // Y: -6 piksel aşağı kaydırır

    private Vector2 originalPosition;

    void Awake()
    {
        if (textTransform == null)
            textTransform = GetComponentInChildren<TMPro.TMP_Text>()?.rectTransform;

        if (textTransform != null)
            originalPosition = textTransform.anchoredPosition;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        LevelButton levelButton = GetComponent<LevelButton>();
        if (levelButton != null && LevelProgress.IsLevelUnlocked(levelButton.LevelData) == false)
            return;
        if (textTransform != null)
            textTransform.anchoredPosition = originalPosition + offset;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        LevelButton levelButton = GetComponent<LevelButton>();
        if (levelButton != null && LevelProgress.IsLevelUnlocked(levelButton.LevelData) == false)
            return;
        if (textTransform != null)
            textTransform.anchoredPosition = originalPosition;
    }
}