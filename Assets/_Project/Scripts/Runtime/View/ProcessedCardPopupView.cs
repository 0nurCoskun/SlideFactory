using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Doğru swipe sonrası, kartın dönüştüğü yeni hâlini (ikon + isim) o istasyonun
/// etiketinin yanında (üstünde/altında) kısaca gösterip sonra AYNI YERDE
/// küçülüp/solarak kaybolan "tatmin" animasyonu. Oyun kuralını bilmez, sadece
/// GameManager event'lerini dinler (View katmanı sözleşmesi).
///
/// GameManager.HandleSwipe() sırasında OnValidSwipe(direction, station) her zaman
/// OnCardProcessed'dan HEMEN ÖNCE, aynı frame'de fırlatılır - bu yüzden yönü
/// OnValidSwipe'da yakalayıp, hemen ardından gelen OnCardProcessed'da kullanıyoruz.
///
/// SADECE ara aşamaya geçen kartlar (OnCardProcessed) popup gösterir. Final ürün
/// (OnCardCompleted) ve "çöp" sonucu (resultData yok) için popup ATLANIR - final ürün
/// zaten desteden tamamen ayrılıyor, "desteye dönüyor" hissi vermek yanlış olurdu.
/// </summary>
public class ProcessedCardPopupView : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private GameManager gameManager;

    [Header("Prefab / Konteyner")]
    [SerializeField] private ProcessedCardPopupItem popupPrefab;
    [SerializeField] private RectTransform popupParent;

    [Header("Yön Başına Ortaya Çıkış Noktası (istasyon etiketinin arka planı)")]
    [SerializeField] private RectTransform upAnchor;
    [SerializeField] private RectTransform downAnchor;
    [SerializeField] private RectTransform leftAnchor;
    [SerializeField] private RectTransform rightAnchor;

    [Header("Yön Başına Ofset (etiketin üstü/altı - hangisinde ekranda yer varsa)")]
    [SerializeField] private Vector2 upSpawnOffset = new Vector2(0f, -90f);
    [SerializeField] private Vector2 downSpawnOffset = new Vector2(0f, 90f);
    [SerializeField] private Vector2 leftSpawnOffset = new Vector2(0f, 90f);
    [SerializeField] private Vector2 rightSpawnOffset = new Vector2(0f, 90f);

    [Header("Zamanlama")]
    [Tooltip("Gerçek kartın fırlatma animasyonu istasyona ulaşana kadar geçen süre (CardView.flingDuration ile eşleşsin).")]
    [SerializeField] private float appearDelay = 0.3f;
    [SerializeField] private float appearDuration = 0.25f;
    [SerializeField] private Ease appearEase = Ease.OutBack;
    [SerializeField] private float holdDuration = 0.5f;
    [SerializeField] private float disappearDuration = 0.25f;
    [SerializeField] private Ease disappearEase = Ease.InBack;

    private Canvas _canvas;
    private Dictionary<SwipeDirection, RectTransform> _anchorsByDirection;
    private Dictionary<SwipeDirection, Vector2> _spawnOffsetByDirection;
    private SwipeDirection _pendingDirection = SwipeDirection.None;

    private void Awake()
    {
        _canvas = popupParent != null ? popupParent.GetComponentInParent<Canvas>() : null;

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
    }

    private void OnDisable()
    {
        if (gameManager == null) return;

        gameManager.OnValidSwipe -= HandleValidSwipe;
        gameManager.OnCardProcessed -= HandleCardProcessed;
    }

    private void HandleValidSwipe(SwipeDirection direction, StationData station)
    {
        _pendingDirection = direction;
    }

    private void HandleCardProcessed(CardInstance instance, CardData resultData)
    {
        SpawnPopup(_pendingDirection, resultData);
    }

    private void SpawnPopup(SwipeDirection direction, CardData cardData)
    {
        if (popupPrefab == null || popupParent == null) return;
        if (cardData == null) return;
        if (!_anchorsByDirection.TryGetValue(direction, out RectTransform anchor) || anchor == null) return;

        Vector2 spawnOffset = _spawnOffsetByDirection.TryGetValue(direction, out Vector2 offset) ? offset : Vector2.zero;
        Vector2 spawnPos = GetLocalPointInPopupParent(anchor) + spawnOffset;

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
        // Aynı yerde küçülüp solarak kaybolsun - istasyon etiketinin yanından
        // hiçbir yere uçmuyor, tamamen orada belirip orada kayboluyor.
        sequence.Append(rect.DOScale(Vector3.zero, disappearDuration).SetEase(disappearEase));
        sequence.Join(canvasGroup.DOFade(0f, disappearDuration));
        sequence.OnComplete(() =>
        {
            if (popup != null) Destroy(popup.gameObject);
        });
    }

    /// <summary>
    /// "source" RectTransform'unun ekrandaki GERÇEK (render edilen) konumunu bulup,
    /// popupParent içindeki karşılık gelen local noktaya çevirir. İki RectTransform'un
    /// farklı, hatta aralarında RectTransform OLMAYAN bir ebeveyn (örn. StationLabels'ın
    /// düz Transform olması) bulunsa da doğru sonucu verir - Unity'nin kendi UI hit-test
    /// sisteminin kullandığı WorldToScreenPoint/ScreenPointToLocalPointInRectangle
    /// çiftini kullanır, ham anchoredPosition toplamasına/InverseTransformPoint'e güvenmez.
    /// </summary>
    private Vector2 GetLocalPointInPopupParent(RectTransform source)
    {
        Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera
            : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, source.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(popupParent, screenPoint, cam, out Vector2 localPoint);
        return localPoint;
    }
}
