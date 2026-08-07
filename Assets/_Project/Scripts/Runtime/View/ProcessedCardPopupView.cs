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
/// Ara aşamaya geçen kartlar (OnCardProcessed) kendi ikon+ismini gösterir. Final ürün
/// (OnCardCompleted) için ise kart değil, sadece localize edilmiş "Tamamlandı!" mesajı
/// gösterilir - aynı görünüş/kayboluş animasyonuyla. "Çöp" sonucu (resultData yok) için
/// popup ATLANIR, gösterilecek bir şey yok.
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

    [Header("Final Ürün Mesajı")]
    [Tooltip("UI String Table'daki (UIStrings.json) anahtar - final ürün tamamlandığında gösterilir.")]
    [SerializeField] private string completedMessageKey = "ui_completed";
    [Tooltip("Bu mesaj, doğru swipe'ın yapıldığı istasyon etiketinin HER ZAMAN ÜSTÜNDE gösterilir (kart popup'larındaki yöne göre üst/alt ofsetinden farklı olarak, yön farketmeksizin sabit bu ofset kullanılır) - etiketin metniyle çakışıp okunmaz olmasın.")]
    [SerializeField] private Vector2 completedMessageOffset = new Vector2(0f, 90f);
    [Tooltip("Doğru istasyon feedback'indeki yeşille aynı renk (bkz. StationLabelsView.correctFlashColor).")]
    [SerializeField] private Color completedMessageColor = new Color(0.49f, 0.82f, 0.48f, 1f);

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
        if (resultData == null) return;
        if (!_anchorsByDirection.TryGetValue(_pendingDirection, out RectTransform anchor) || anchor == null) return;

        Vector2 spawnOffset = _spawnOffsetByDirection.TryGetValue(_pendingDirection, out Vector2 offset) ? offset : Vector2.zero;
        Vector2 spawnPos = GetLocalPointInPopupParent(anchor) + spawnOffset;

        ProcessedCardPopupItem popup = CreatePopup(spawnPos);
        if (popup != null) popup.Setup(resultData);
    }

    private void HandleCardCompleted(CardInstance instance)
    {
        if (!_anchorsByDirection.TryGetValue(_pendingDirection, out RectTransform anchor) || anchor == null) return;

        // Kart popup'larından farklı olarak yöne göre üst/alt seçmiyoruz - bu mesaj
        // hangi istasyon olursa olsun HER ZAMAN etiketin üstünde beliriyor.
        Vector2 spawnPos = GetLocalPointInPopupParent(anchor) + completedMessageOffset;

        ProcessedCardPopupItem popup = CreatePopup(spawnPos);
        if (popup != null) popup.SetupMessage(GameLocalization.GetUIString(completedMessageKey), completedMessageColor);
    }

    /// <summary>
    /// popupParent içinde verilen local noktada bir popup yaratır, görünüş/kayboluş
    /// animasyon zincirini kurar; içeriğini (kart mı, mesaj mı) çağıran taraf belirler.
    /// </summary>
    private ProcessedCardPopupItem CreatePopup(Vector2 spawnPos)
    {
        if (popupPrefab == null || popupParent == null) return null;

        ProcessedCardPopupItem popup = Instantiate(popupPrefab, popupParent);

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

        return popup;
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
