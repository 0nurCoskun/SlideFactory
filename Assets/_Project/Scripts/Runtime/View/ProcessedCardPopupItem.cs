using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// "ProcessedCardPopup" prefab'ının kendi görsel referanslarını taşıyan küçük bileşen.
/// ProcessedCardPopupView, bu prefab'ı Instantiate ettikten sonra Setup() çağırıp
/// hangi CardData'yı göstereceğini bildirir - kendisi hiçbir GameManager event'i dinlemez,
/// tamamen "dumb" bir görsel taşıyıcıdır.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
public class ProcessedCardPopupItem : MonoBehaviour
{
    [SerializeField] private Image iconArtImage;
    [SerializeField] private TMP_Text nameText;

    public RectTransform RectTransform { get; private set; }
    public CanvasGroup CanvasGroup { get; private set; }

    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
        CanvasGroup = GetComponent<CanvasGroup>();
    }

    public void Setup(CardData data)
    {
        if (data == null) return;

        if (iconArtImage != null)
        {
            bool hasIcon = data.icon != null;
            iconArtImage.sprite = data.icon;
            iconArtImage.gameObject.SetActive(hasIcon);
        }

        if (nameText != null)
        {
            nameText.text = GameLocalization.GetCardName(data);
            nameText.ForceMeshUpdate();
        }
    }
}
