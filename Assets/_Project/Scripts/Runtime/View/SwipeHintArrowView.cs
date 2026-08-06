using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// "Yardımcı ok" ipucu. Oyuncu hesitationDelay kadar SWIPE YAPMAZSA, mevcut kartın
/// gitmesi gereken istasyonun O ANKİ yönündeki ok yumuşakça belirir ve kendi yönünde
/// nazikçe nabız atar. İstasyon-yön eşleşmesi 3-4 saniyede bir karıştığı için,
/// GÖRÜNÜR bir ok OnStationsShuffled'da yeniden nişanlanır.
///
/// Sayacı SIFIRLAYAN tek şey gerçek bir hamledir (swipe / yeni kart). Sürükleme
/// sayacı ne durdurur ne sıfırlar ve ok, parmak ekrandayken de belirebilir -
/// BİLEREK böyle: kartı eline alıp kararsızca oynatan oyuncu, ipucuna en çok
/// ihtiyacı olan oyuncudur. Sürüklerken sayacı dondurmak tam da yardım edilmesi
/// gerekeni cezalandırırdı.
///
/// Gecikme bilinçli: oyuncuya önce kendi bulma şansı veriliyor, ipucu ancak gerçekten
/// takıldığında devreye giriyor.
///
/// Sahne kurulumu: 4 ok objesi HER ZAMAN AKTİF kalır - görünürlük yalnızca alpha ile
/// yönetilir. SetActive KULLANMA: sonsuz döngülü nabız tween'inin hedefi geçerli kalmalı.
///
/// RAYCAST UYARISI: SwipeInputManager.BeginDrag, parmak bir UI elemanının üzerindeyse
/// (EventSystem.IsPointerOverGameObject) swipe'ı HİÇ başlatmıyor. Oklar tam kartın
/// kenarında durduğu için raycast'leri açık kalırsa oyuncunun hamlesini sessizce bloke
/// ederler. Inspector'da unutulsa bile Awake'te blocksRaycasts = false zorlanıyor.
/// </summary>
public class SwipeHintArrowView : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private StationAssignmentManager stationAssignmentManager;
    [SerializeField] private SwipeInputManager swipeInputManager;

    [Header("Yön Okları (UI)")]
    [Tooltip("Her yön için o yönü gösteren okun CanvasGroup'u. Objeler hep aktif kalır, alpha 0'dan başlar.")]
    [SerializeField] private CanvasGroup upArrow;
    [SerializeField] private CanvasGroup downArrow;
    [SerializeField] private CanvasGroup leftArrow;
    [SerializeField] private CanvasGroup rightArrow;

    [Header("Zamanlama")]
    [Tooltip("Oyuncu kaç saniye hiç swipe yapmazsa ok belirsin? " +
             "0.7'nin ALTINA indirme: yanlış swipe sonrası kartın desteye dönüş animasyonu " +
             "~0.65 sn sürüyor ve GameManager o sırada zaten SONRAKİ karta geçmiş oluyor - " +
             "ok, ekranda hâlâ eski kart uçarken yeni kartın yönünü gösterir.")]
    [SerializeField] private float hesitationDelay = 1.2f;

    [Header("Belirme / Kaybolma")]
    [SerializeField] private float fadeDuration = 0.25f;

    [Header("Nabız (Pulse) Animasyonu")]
    [Tooltip("Okun kendi yönünde ileri-geri gidip geldiği mesafe (piksel).")]
    [SerializeField] private float pulseDistance = 18f;
    [Tooltip("Tek yönlü hareketin süresi - gidiş+dönüş bunun iki katı sürer.")]
    [SerializeField] private float pulseDuration = 0.55f;
    [SerializeField] private Ease pulseEase = Ease.InOutSine;

    /// <summary>Tek bir yön okunun çalışma zamanı verisi.</summary>
    private struct ArrowSlot
    {
        public CanvasGroup Group;
        public RectTransform Rect;
        public Vector2 RestPos; // nabız tween'i buradan başlar, gizlenirken buraya döner
    }

    private readonly Dictionary<SwipeDirection, ArrowSlot> _slots = new Dictionary<SwipeDirection, ArrowSlot>();

    private float _idleTimer;
    private SwipeDirection _visibleDirection = SwipeDirection.None;

    private Tween _fadeTween;
    private Tween _pulseTween;

    private void Awake()
    {
        RegisterSlot(SwipeDirection.Up, upArrow);
        RegisterSlot(SwipeDirection.Down, downArrow);
        RegisterSlot(SwipeDirection.Left, leftArrow);
        RegisterSlot(SwipeDirection.Right, rightArrow);
    }

    /// <summary>
    /// Bir ok alanını kaydeder: RectTransform'unu ve dinlenme pozisyonunu önbelleğe alır,
    /// görünmez yapar ve raycast'ini kapatır.
    /// DİKKAT: RestPos burada BİR KEZ yakalanıyor - ok konteynerine ASLA LayoutGroup /
    /// ContentSizeFitter ekleme, yoksa bu değer bayatlar ve ok kayar.
    /// </summary>
    private void RegisterSlot(SwipeDirection direction, CanvasGroup group)
    {
        if (group == null)
        {
            Debug.LogWarning($"[SwipeHintArrowView] {direction} yönü için ok atanmamış - o yönde ipucu gösterilemeyecek.");
            return;
        }

        RectTransform rect = group.transform as RectTransform;
        if (rect == null)
        {
            Debug.LogError($"[SwipeHintArrowView] {direction} okunun RectTransform'u yok - Canvas altında olduğundan emin ol.");
            return;
        }

        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        _slots[direction] = new ArrowSlot
        {
            Group = group,
            Rect = rect,
            RestPos = rect.anchoredPosition
        };
    }

    private void OnEnable()
    {
        // GameManager ve StationAssignmentManager düz C# event kullanıyor -> '+='
        if (gameManager != null)
            gameManager.OnCardChanged += HandleCardChanged;

        if (stationAssignmentManager != null)
            stationAssignmentManager.OnStationsShuffled += HandleStationsShuffled;

        // SwipeInputManager UnityEvent kullanıyor -> '+=' DEĞİL, AddListener.
        // Sadece OnSwipeDetected dinleniyor: OnDragStarted/OnDragCanceled bilerek
        // dinlenmiyor, çünkü sürükleme ipucu sayacını etkilememeli.
        if (swipeInputManager != null)
            swipeInputManager.OnSwipeDetected.AddListener(HandleSwipeDetected);
    }

    private void OnDisable()
    {
        if (gameManager != null)
            gameManager.OnCardChanged -= HandleCardChanged;

        if (stationAssignmentManager != null)
            stationAssignmentManager.OnStationsShuffled -= HandleStationsShuffled;

        if (swipeInputManager != null)
            swipeInputManager.OnSwipeDetected.RemoveListener(HandleSwipeDetected);

        // Proje genelinde Time.timeScale KULLANILMIYOR: tween'ler pause'ta bile akar.
        // Nabız tween'i SetLoops(-1) olduğu için DOTween onu asla kendiliğinden
        // öldürmez - sahne değişiminde "target is missing" uyarısı vermesin diye
        // burada elle öldürülmesi ZORUNLU.
        HideArrowInstantly();

        _idleTimer = 0f;
    }

    private void Update()
    {
        if (!IsHintAllowed())
        {
            // Duraklandı / level bitti / kart yok: oku kapat ve tereddüt sayacını sıfırla.
            FadeOutArrow();
            _idleTimer = 0f;
            return;
        }

        // Sürükleme BİLEREK sayacı etkilemiyor: parmak ekrandayken de sayaç işler ve
        // süre dolduğunda ok belirir. Kartı eline alıp kararsızca oynatan oyuncu,
        // ipucuna en çok ihtiyacı olandır.
        _idleTimer += Time.deltaTime;

        if (_visibleDirection != SwipeDirection.None) return;
        if (_idleTimer < hesitationDelay) return;

        // Hedef BURADA çözülüyor, kart geldiğinde DEĞİL: BeginLevelPlay() önce
        // DrawNextCard() (OnCardChanged), SONRA StartAssigning() çağırıyor - yani
        // ilk kartın geldiği anda istasyon->yön eşleşmesi HENÜZ BOŞ.
        // Çözülemezse (final ürün / hepsi çöp / istasyon bu level'da yok) sessizce
        // hiçbir şey yapılmıyor; koşullar değişirse sonraki frame'de tekrar denenir.
        TryShowArrow();
    }

    /// <summary>İpucu şu an gösterilebilir mi? Tek kapı - tüm oynanış koşulları burada.</summary>
    private bool IsHintAllowed()
    {
        return gameManager != null
               && gameManager.HasBegun
               && !gameManager.IsPaused
               && !gameManager.IsLevelEnded
               && gameManager.CurrentCard != null
               && gameManager.CurrentCard.Data != null;
    }

    /// <summary>
    /// Mevcut kart -> gitmesi gereken istasyon -> o istasyonun ŞU ANKİ yönü.
    /// Zincirde ilerleten bir outcome yoksa ya da istasyon o an bir yöne
    /// atanmamışsa SwipeDirection.None döner.
    /// </summary>
    private SwipeDirection ResolveTargetDirection()
    {
        if (gameManager == null || gameManager.CurrentCard == null || stationAssignmentManager == null)
            return SwipeDirection.None;

        StationData neededStation = ProductionChainUtility.GetNextStation(gameManager.CurrentCard.Data);
        if (neededStation == null) return SwipeDirection.None;

        return stationAssignmentManager.GetDirectionForStation(neededStation);
    }

    private void TryShowArrow()
    {
        SwipeDirection direction = ResolveTargetDirection();
        if (direction == SwipeDirection.None) return;

        ShowArrow(direction);
    }

    private void ShowArrow(SwipeDirection direction)
    {
        if (!_slots.TryGetValue(direction, out ArrowSlot slot)) return;

        // Önceki ok (varsa) ANINDA kapanır: aynı anda iki ok görünmesin.
        // Yeniden nişanlamada çapraz geçiş yerine bilerek sert geçiş yapılıyor -
        // karışma anında etiketler de zaten punch animasyonu oynuyor, ikisi birden
        // yumuşak geçse değişim fark edilmiyor.
        HideArrowInstantly();

        _visibleDirection = direction;
        slot.Rect.anchoredPosition = slot.RestPos;

        _fadeTween = slot.Group.DOFade(1f, fadeDuration).SetEase(Ease.OutQuad);

        _pulseTween = slot.Rect
            .DOAnchorPos(slot.RestPos + DirectionToVector(direction) * pulseDistance, pulseDuration)
            .SetEase(pulseEase)
            .SetLoops(-1, LoopType.Yoyo);
    }

    /// <summary>Oku yumuşakça kapatır (duraklama, level sonu, hedefin kaybolması).</summary>
    private void FadeOutArrow()
    {
        if (_visibleDirection == SwipeDirection.None) return;

        if (!_slots.TryGetValue(_visibleDirection, out ArrowSlot slot))
        {
            HideArrowInstantly();
            return;
        }

        // Nabız hemen dursun ve ok dinlenme noktasına dönsün; sadece alpha yumuşak kapansın.
        _pulseTween?.Kill();
        _pulseTween = null;
        slot.Rect.anchoredPosition = slot.RestPos;

        _fadeTween?.Kill();
        _fadeTween = slot.Group.DOFade(0f, fadeDuration).SetEase(Ease.InQuad);

        _visibleDirection = SwipeDirection.None;
    }

    /// <summary>
    /// Tween beklemeden TÜM okları anında görünmez yapar ve dinlenme pozisyonlarına
    /// döndürür (sürükleme başlangıcı, yeni kart, OnDisable).
    /// </summary>
    private void HideArrowInstantly()
    {
        _fadeTween?.Kill();
        _fadeTween = null;
        _pulseTween?.Kill();
        _pulseTween = null;

        foreach (KeyValuePair<SwipeDirection, ArrowSlot> pair in _slots)
        {
            pair.Value.Group.alpha = 0f;
            pair.Value.Rect.anchoredPosition = pair.Value.RestPos;
        }

        _visibleDirection = SwipeDirection.None;
    }

    // --- GameManager / StationAssignmentManager event'leri ---

    private void HandleCardChanged(CardInstance card)
    {
        // Yeni kart = yeni hedef. Eski okun yönü artık yanlış olabilir: anında kapat,
        // tereddüt sayacını sıfırdan başlat.
        _idleTimer = 0f;
        HideArrowInstantly();
    }

    private void HandleStationsShuffled(IReadOnlyDictionary<SwipeDirection, StationData> assignment)
    {
        // Gelen sözlük, StationAssignmentManager'ın CANLI _currentAssignment örneğidir
        // (her karışmada temizlenip yeniden dolduruluyor). Bilerek saklanmıyor;
        // hedef her seferinde GetDirectionForStation ile yeniden soruluyor.
        if (_visibleDirection == SwipeDirection.None) return;
        if (!IsHintAllowed()) return;

        SwipeDirection newDirection = ResolveTargetDirection();

        if (newDirection == _visibleDirection) return; // aynı yönde kaldı - nabzı bozma

        if (newDirection == SwipeDirection.None)
        {
            FadeOutArrow();
            return;
        }

        ShowArrow(newDirection);
    }

    // --- SwipeInputManager event'leri ---

    private void HandleSwipeDetected(SwipeDirection direction)
    {
        // Sayacı sıfırlayan TEK şey: gerçek bir hamle. Sürükleyip vazgeçmek
        // (OnDragCanceled) bilerek dinlenmiyor - kararsız sürükleme sayacı
        // sıfırlasaydı, oyuncu kartı oynattıkça ipucu hiç gelmezdi.
        // GameManager.OnSwipeResolved'a bağlanmıyor çünkü GameManager, level
        // duraklamışsa/bittiyse HandleSwipe'tan erken çıkıp o event'i hiç fırlatmıyor.
        _idleTimer = 0f;
        HideArrowInstantly();
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
