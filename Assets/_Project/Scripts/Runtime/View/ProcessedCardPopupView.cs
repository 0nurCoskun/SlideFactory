using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Doğru swipe sonrası, kartın dönüştüğü yeni hâlini (ikon + isim) o istasyonun
/// etiketinin yanında kısaca gösterip sonra desteye (merkez kart konumuna) uçurarak
/// yok eden "tatmin" animasyonu. Oyun kuralını bilmez, sadece GameManager event'lerini
/// dinler (View katmanı sözleşmesi).
///
/// GameManager.HandleSwipe() sırasında OnValidSwipe(direction, station) her zaman
/// OnCardProcessed/OnCardCompleted'dan HEMEN ÖNCE, aynı frame'de fırlatılır - bu yüzden
/// yönü OnValidSwipe'da yakalayıp, hemen ardından gelen sonuç event'inde kullanıyoruz.
/// "Çöp" sonucunda (resultData yok) gösterilecek bir kart olmadığı için popup atlanır.
/// </summary>
public class ProcessedCardPopupView : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private GameManager gameManager;

    [Header("Prefab / Konteyner")]
    [SerializeField] private ProcessedCardPopupItem popupPrefab;
    [SerializeField] private Transform popupParent;
    [Tooltip("Kartın uçarak kayboldğu 'deste' noktası - gerçek Card'ın durduğu RectTransform ile aynısı.")]
    [SerializeField] private RectTransform deckPosition;

    [Header("Yön Başına Ortaya Çıkış Noktası (istasyon etiketinin arka planı)")]
    [SerializeField] private RectTransform upAnchor;
    [SerializeField] private RectTransform downAnchor;
    [SerializeField] private RectTransform leftAnchor;
    [SerializeField] private RectTransform rightAnchor;

    [Header("Yön Başına Ofset (etiketin üstü/altı - hangisinde ekranda yer varsa)")]
    [SerializeField] private Vector2 upSpawnOffset = new Vector2(0f, -150f);
    [SerializeField] private Vector2 downSpawnOffset = new Vector2(0f, 150f);
    [SerializeField] private Vector2 leftSpawnOffset = new Vector2(0f, 150f);
    [SerializeField] private Vector2 rightSpawnOffset = new Vector2(0f, 150f);

    [Header("Zamanlama")]
    [Tooltip("Gerçek kartın fırlatma animasyonu istasyona ulaşana kadar geçen süre (CardView.flingDuration ile eşleşsin).")]
    [SerializeField] private float appearDelay = 0.3f;
    [SerializeField] private float appearDuration = 0.25f;
    [SerializeField] private Ease appearEase = Ease.OutBack;
    [SerializeField] private float holdDuration = 0.5f;
    [SerializeField] private float flyDuration = 0.45f;
    [SerializeField] private Ease flyEase = Ease.InOutQuad;

    private Dictionary<SwipeDirection, RectTransform> _anchorsByDirection;
    private Dictionary<SwipeDirection, Vector2> _spawnOffsetByDirection;
    private SwipeDirection _pendingDirection = SwipeDirection.None;

    private void Awake()
    {
        _anchorsByDirection = new Dictionary<SwipeDirection, RectTransform>
        {
            { SwipeDirection.Up, upAnchor },
            { SwipeDirection.Down, downAnchor },
            { SwipeDirection.Left, leftAnchor },
            { SwipeDirection.Right, rightAnchor },
        };

        _spawnOffsetByDirection = new Dictionary<SwipeDirection, Vector2>
        {
            { SwipeDirection.Up, upSpawnOffset },
            { SwipeDirection.Down, downSpawnOffset },
            { SwipeDirection.Left, leftSpawnOffset },
            { SwipeDirection.Right, rightSpawnOffset },
        };
    }

    private void OnEnable()
    {
        if (gameManager == null) return;

        gameManager.OnValidSwipe += HandleValidSwipe;
        gameManager.OnCardProcessed += HandleCardProcessed;
        gameManager.OnCardCompleted += HandleCardCompleted;
    }

    private void OnDisable()
    {
        if (gameManager == null) return;

        gameManager.OnValidSwipe -= HandleValidSwipe;
        gameManager.OnCardProcessed -= HandleCardProcessed;
        gameManager.OnCardCompleted -= HandleCardCompleted;
    }

    private void HandleValidSwipe(SwipeDirection direction, StationData station)
    {
        _pendingDirection = direction;
    }

    private void HandleCardProcessed(CardInstance instance, CardData resultData)
    {
        SpawnPopup(_pendingDirection, resultData);
    }

    private void HandleCardCompleted(CardInstance instance)
    {
        // GameManager, OnCardCompleted'ı fırlatmadan önce instance.SetData(resultData) çağırmış
        // oluyor - yani instance.Data burada zaten "final ürün" kartının kendisi.
        SpawnPopup(_pendingDirection, instance != null ? instance.Data : null);
    }

    private void SpawnPopup(SwipeDirection direction, CardData cardData)
    {
        if (popupPrefab == null || popupParent == null || deckPosition == null) return;
        if (cardData == null) return;
        if (!_anchorsByDirection.TryGetValue(direction, out RectTransform anchor) || anchor == null) return;

        Vector2 spawnOffset = _spawnOffsetByDirection.TryGetValue(direction, out Vector2 offset) ? offset : Vector2.zero;
        Vector2 spawnPos = GetLocalPositionRelativeTo(anchor, popupParent) + spawnOffset;
        Vector2 deckPos = GetLocalPositionRelativeTo(deckPosition, popupParent);

        ProcessedCardPopupItem popup = Instantiate(popupPrefab, popupParent);
        popup.Setup(cardData);

        RectTransform rect = popup.RectTransform;
        CanvasGroup canvasGroup = popup.CanvasGroup;

        rect.anchoredPosition = spawnPos;
        rect.localScale = Vector3.zero;
        canvasGroup.alpha = 0f;

        Sequence sequence = DOTween.Sequence();
        sequence.AppendInterval(appearDelay);
        sequence.Append(rect.DOScale(Vector3.one, appearDuration).SetEase(appearEase));
        sequence.Join(canvasGroup.DOFade(1f, appearDuration));
        sequence.AppendInterval(holdDuration);
        sequence.Append(rect.DOAnchorPos(deckPos, flyDuration).SetEase(flyEase));
        sequence.Join(rect.DOScale(Vector3.zero, flyDuration).SetEase(flyEase));
        sequence.Join(canvasGroup.DOFade(0f, flyDuration * 0.6f).SetDelay(flyDuration * 0.4f));
        sequence.OnComplete(() =>
        {
            if (popup != null) Destroy(popup.gameObject);
        });
    }

    /// <summary>
    /// "source" RectTransform'unun pivot noktasını, "relativeTo" transform'unun
    /// LOKAL uzayına çevirir. İki RectTransform farklı ebeveynlerin altında olsa bile
    /// (örn. istasyon etiketi StationLabels altında, popup konteyneri SafeAreaContainer
    /// altında) doğru sonucu verir - basit anchoredPosition toplamasına güvenmez.
    /// </summary>
    private static Vector2 GetLocalPositionRelativeTo(RectTransform source, Transform relativeTo)
    {
        Vector3 localPos = relativeTo.InverseTransformPoint(source.position);
        return new Vector2(localPos.x, localPos.y);
    }
}
