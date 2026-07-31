using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro; // TextMeshPro kullanmıyorsan bu satırı silip Text alanını UnityEngine.UI.Text yapabilirsin.

/// <summary>
/// Ekrandaki tek bir kartın GÖRSEL temsilcisi. Oyun kuralını bilmez, sadece
/// GameManager'ın fırlattığı event'lere göre DOTween animasyonu oynatır ve
/// SwipeInputManager'ın sürükleme verisine göre kartı parmağın altında tutar.
///
/// Bu script'i Canvas altındaki "Card" GameObject'ine ekle (Image + TMP_Text çocukları olan).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CardView : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SwipeInputManager swipeInputManager;

    [Header("Görsel Referanslar")]
    [SerializeField] private RectTransform cardRectTransform;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;

    [Header("Sürükleme Ayarları")]
    [Tooltip("Sürüklerken kartın parmağın hareketine göre ne kadar eğileceği.")]
    [SerializeField] private float dragRotationMultiplier = 0.05f;

    [Header("Fırlatma (Fling) Ayarları")]
    [SerializeField] private float flingDistance = 1400f;
    [SerializeField] private float flingDuration = 0.35f;
    [SerializeField] private Ease flingEase = Ease.InQuad;

    [Header("Geri Dönme (Snap Back) Ayarları")]
    [SerializeField] private float snapBackDuration = 0.3f;
    [SerializeField] private Ease snapBackEase = Ease.OutBack;

    [Header("Giriş (Entrance) Ayarları")]
    [SerializeField] private float entranceDuration = 0.35f;
    [SerializeField] private Ease entranceEase = Ease.OutBack;

    [Header("Yanlış Swipe - Desteye Geri Dönüş Ayarları")]
    [Tooltip("Kart yanlış istasyona fırlatıldığında, istasyondan desteye geri dönerken oynayan animasyonun süresi.")]
    [SerializeField] private float wrongSwipeReturnDuration = 0.3f;
    [SerializeField] private Ease wrongSwipeReturnEase = Ease.InOutQuad;
    [Tooltip("Geri dönüş sırasında kartın küçülerek desteye 'yutuluyormuş' hissi vermesi için hedef ölçek.")]
    [SerializeField] private float wrongSwipeReturnScale = 0.6f;

    private Vector2 _centerAnchoredPos;
    private bool _centerPosCaptured;
    private bool _isAnimatingExit;
    private bool _isInvalidSwipe;
    private CardInstance _pendingNextCard;
    private bool _hasPendingCard;
    private Sequence _activeSequence;

    private void Awake()
    {
        if (cardRectTransform == null)
            cardRectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// Merkez pozisyonunu Awake/Start sırasına GÜVENMEDEN, ilk gerçekten
    /// ihtiyaç duyulduğu anda yakalar. Bu sayede GameManager'ın veya
    /// SafeArea'nın hangi sırada çalıştığı önemli olmaktan çıkar -
    /// bu metod her zaman "her şey kurulduktan sonraki" ilk çağrıda tetiklenir.
    /// </summary>
    private void EnsureCenterPositionCaptured()
    {
        if (_centerPosCaptured) return;

        _centerAnchoredPos = cardRectTransform.anchoredPosition;
        _centerPosCaptured = true;
    }

    private void OnEnable()
    {
        if (swipeInputManager != null)
        {
            swipeInputManager.OnDragDelta.AddListener(HandleDragDelta);
            swipeInputManager.OnDragCanceled.AddListener(HandleDragCanceled);
        }

        if (gameManager != null)
        {
            gameManager.OnSwipeResolved += HandleSwipeResolved;
            gameManager.OnInvalidSwipe += HandleInvalidSwipe;
            gameManager.OnCardChanged += HandleCardChanged;
            gameManager.OnDeckEmptied += HandleDeckEmptied;
        }
    }

    private void OnDisable()
    {
        if (swipeInputManager != null)
        {
            swipeInputManager.OnDragDelta.RemoveListener(HandleDragDelta);
            swipeInputManager.OnDragCanceled.RemoveListener(HandleDragCanceled);
        }

        if (gameManager != null)
        {
            gameManager.OnSwipeResolved -= HandleSwipeResolved;
            gameManager.OnInvalidSwipe -= HandleInvalidSwipe;
            gameManager.OnCardChanged -= HandleCardChanged;
            gameManager.OnDeckEmptied -= HandleDeckEmptied;
        }

        _activeSequence?.Kill();
    }

    // --- Sürükleme sırasında canlı takip (DOTween kullanılmaz, direkt transform) ---

    private void HandleDragDelta(Vector2 delta)
    {
        if (_isAnimatingExit) return; // animasyon oynarken parmak takibini engelle

        EnsureCenterPositionCaptured();

        cardRectTransform.anchoredPosition = _centerAnchoredPos + delta;

        float rotationZ = -delta.x * dragRotationMultiplier;
        cardRectTransform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
    }

    private void HandleDragCanceled()
    {
        if (_isAnimatingExit) return;

        EnsureCenterPositionCaptured();

        _activeSequence?.Kill();
        _activeSequence = DOTween.Sequence();
        _activeSequence.Append(cardRectTransform.DOAnchorPos(_centerAnchoredPos, snapBackDuration).SetEase(snapBackEase));
        _activeSequence.Join(cardRectTransform.DOLocalRotate(Vector3.zero, snapBackDuration).SetEase(snapBackEase));
    }

    // --- GameManager event'leri ---

    private void HandleSwipeResolved(SwipeDirection direction)
    {
        _isAnimatingExit = true;
        _isInvalidSwipe = false;

        EnsureCenterPositionCaptured();

        Vector2 exitOffset = DirectionToVector(direction) * flingDistance;
        float exitRotation = Mathf.Sign(exitOffset.x) * 25f;

        _activeSequence?.Kill();
        _activeSequence = DOTween.Sequence();
        _activeSequence.Append(cardRectTransform.DOAnchorPos(_centerAnchoredPos + exitOffset, flingDuration).SetEase(flingEase));
        _activeSequence.Join(cardRectTransform.DOLocalRotate(new Vector3(0f, 0f, exitRotation), flingDuration).SetEase(flingEase));
        _activeSequence.OnComplete(HandleExitAnimationComplete);
    }

    /// <summary>
    /// GameManager, OnSwipeResolved'dan hemen sonra (aynı frame'de, DrawNextCard çağrılmadan önce)
    /// yanlış istasyona atıldığını bildiriyorsa bu event tetiklenir. Fırlatma animasyonu bittiğinde
    /// kartın yeni bir kart olarak değil, istasyondan desteye geri dönen aynı kart gibi
    /// davranması için bayrağı burada işaretliyoruz.
    /// </summary>
    private void HandleInvalidSwipe(SwipeDirection direction, StationData station)
    {
        _isInvalidSwipe = true;
    }

    private void HandleExitAnimationComplete()
    {
        if (_isInvalidSwipe)
        {
            PlayReturnToDeckAnimation();
            return;
        }

        _isAnimatingExit = false;

        if (_hasPendingCard)
        {
            ShowCard(_pendingNextCard);
            _hasPendingCard = false;
        }
        else
        {
            // Deck boşsa (OnDeckEmptied zaten HandleDeckEmptied'de ayrıca ele alınıyor)
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Yanlış swipe sonrası kart istasyona gitti; şimdi merkeze (deste konumuna) küçülerek
    /// geri dönüyor - oyuncuya "bu hamle yanlıştı, kart desteye geri döndü" hissini verir.
    /// </summary>
    private void PlayReturnToDeckAnimation()
    {
        _activeSequence?.Kill();
        _activeSequence = DOTween.Sequence();
        _activeSequence.Append(cardRectTransform.DOAnchorPos(_centerAnchoredPos, wrongSwipeReturnDuration).SetEase(wrongSwipeReturnEase));
        _activeSequence.Join(cardRectTransform.DOLocalRotate(Vector3.zero, wrongSwipeReturnDuration).SetEase(wrongSwipeReturnEase));
        _activeSequence.Join(cardRectTransform.DOScale(Vector3.one * wrongSwipeReturnScale, wrongSwipeReturnDuration).SetEase(wrongSwipeReturnEase));
        _activeSequence.OnComplete(HandleReturnAnimationComplete);
    }

    private void HandleReturnAnimationComplete()
    {
        _isAnimatingExit = false;
        _isInvalidSwipe = false;

        if (_hasPendingCard)
        {
            ShowCard(_pendingNextCard);
            _hasPendingCard = false;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void HandleCardChanged(CardInstance newCard)
    {
        if (_isAnimatingExit)
        {
            // Fırlatma animasyonu hâlâ sürüyor, yeni kartı animasyon bitince göstereceğiz.
            _pendingNextCard = newCard;
            _hasPendingCard = true;
        }
        else
        {
            // Animasyon yok demek ki bu ilk kart (oyunun başlangıcı) - direkt göster.
            ShowCard(newCard);
        }
    }

    private void HandleDeckEmptied()
    {
        _hasPendingCard = false;
        // İsteğe bağlı: burada bir "Tebrikler / Üretim Tamamlandı" panelini açabilirsin.
    }

    // --- Görsel güncelleme ---

    private void ShowCard(CardInstance card)
    {
        if (card == null || card.Data == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        EnsureCenterPositionCaptured();

        cardRectTransform.anchoredPosition = _centerAnchoredPos;
        cardRectTransform.localRotation = Quaternion.identity;
        cardRectTransform.localScale = Vector3.zero;


        if (nameText != null)
        {
            nameText.text = card.Data.displayName;
            // Obje bu frame'de inaktiften aktife geçtiyse TMP bazen mesh'i
            // hemen yeniden çizmiyor (text doğru ama görsel güncellenmiyor).
            // Bu satır o ilk kare gecikmesini zorla düzeltir.
            nameText.ForceMeshUpdate();
        }

        _activeSequence?.Kill();
        _activeSequence = DOTween.Sequence();
        _activeSequence.Append(cardRectTransform.DOScale(Vector3.one, entranceDuration).SetEase(entranceEase));
    }

    private static Vector2 DirectionToVector(SwipeDirection direction)
    {
        switch (direction)
        {
            case SwipeDirection.Up: return Vector2.up;
            case SwipeDirection.Down: return Vector2.down;
            case SwipeDirection.Left: return Vector2.left;
            case SwipeDirection.Right: return Vector2.right;
            default: return Vector2.zero;
        }
    }
}