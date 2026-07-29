using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyunun akışını yöneten merkezi sınıf.
/// Sorumluluğu: deste durumunu tutmak, SwipeInputManager'dan gelen yönü StationAssignmentManager
/// üzerinden bir istasyona çevirmek, bu istasyonu mevcut kartın "recipe"si ile karşılaştırmak ve
/// sonucu uygulamak. Ayrıca LevelTimerManager'ı dinleyip bölüm kazanma/kaybetme kararını verir.
/// Görsel/animasyon işini bilmez; bunun yerine event fırlatır (Dependency Inversion).
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] private SwipeInputManager swipeInputManager;
    [SerializeField] private StationAssignmentManager stationAssignmentManager;
    [SerializeField] private LevelTimerManager levelTimerManager;

    [Header("Level Verisi")]
    [Tooltip("Level Select ekranından gelen seçim varsa o kullanılır. " +
             "Bu sahneyi Editor'de doğrudan test ediyorsan (Level Select'ten geçmeden), " +
             "aşağıdaki Fallback Level Data kullanılır.")]
    [SerializeField] private LevelData fallbackLevelData;

    // FIFO kuyruk: işlenmiş kart, deste sonuna eklenir. İstersen Stack'e çevirip
    // "işlenen kart hemen tekrar önüne gelsin" davranışına kolayca geçebilirsin.
    private readonly Queue<CardInstance> _deck = new Queue<CardInstance>();
    private CardInstance _currentCard;
    private bool _levelEnded;

    public CardInstance CurrentCard => _currentCard;
    public int RemainingCardCount => _deck.Count + (_currentCard != null ? 1 : 0);

    // --- Dış dünyaya (UI, CardView, SFX) haber veren event'ler ---
    public event Action<CardInstance> OnCardChanged;               // Ekranda gösterilecek yeni kart geldi
    public event Action<CardInstance, CardData> OnCardProcessed;    // Kart bir üst aşamaya geçti (henüz final değil)
    public event Action<CardInstance> OnCardCompleted;              // Kart son ürüne dönüştü ve desteden düştü
    public event Action<SwipeDirection, StationData> OnInvalidSwipe; // Yanlış istasyona atıldı, kart Ham'a sıfırlandı
    public event Action OnDeckEmptied;                               // Deste bitti (henüz "level kazanıldı" ile karıştırma - süre de kontrol edilir)
    public event Action<int> OnLevelWon;                             // Süre bitmeden deste tamamlandı - kaç yıldız kazanıldığını da taşır
    public event Action OnLevelFailed;                               // Süre bitti ama deste hâlâ dolu

    /// <summary>
    /// Geçerli bir swipe algılandığında (sonucu ne olursa olsun: doğru/yanlış/final)
    /// hemen tetiklenir. CardView bu event'i dinleyerek "ekrandaki kartı hangi yöne
    /// fırlatacağını" bilir - kuralın kendisiyle (doğru mu yanlış mı) ilgilenmez.
    /// </summary>
    public event Action<SwipeDirection> OnSwipeResolved;

    private void OnEnable()
    {
        if (swipeInputManager != null)
            swipeInputManager.OnSwipeDetected.AddListener(HandleSwipe);

        if (levelTimerManager != null)
            levelTimerManager.OnTimeExpired += HandleTimeExpired;
    }

    private void OnDisable()
    {
        if (swipeInputManager != null)
            swipeInputManager.OnSwipeDetected.RemoveListener(HandleSwipe);

        if (levelTimerManager != null)
            levelTimerManager.OnTimeExpired -= HandleTimeExpired;
    }

    private LevelData _activeLevel;
    private bool _hasBegun;
    private bool _isPaused;

    /// <summary>RecipePreviewView gibi dışarıdaki sistemlerin okuyabilmesi için.</summary>
    public LevelData ActiveLevel => _activeLevel;
    public bool HasBegun => _hasBegun;

    private void Awake()
    {
        // ÖNEMLİ: Bu bilerek Awake'te, Start()'ta değil - çünkü RecipePreviewView
        // kendi Start()'ında ActiveLevel'ı okumak zorunda ve Awake'lerin TÜMÜ,
        // herhangi bir Start()'tan önce garantili olarak biter.
        _activeLevel = LevelSession.SelectedLevel != null ? LevelSession.SelectedLevel : fallbackLevelData;

        if (_activeLevel == null)
        {
            Debug.LogError("[GameManager] Hiçbir LevelData bulunamadı - ne LevelSession'da seçili bir level var, " +
                            "ne de Fallback Level Data Inspector'da atanmış. Oyun başlatılamıyor.");
            return;
        }

        // Level henüz FİİLEN başlamasa da (BeginLevelPlay çağrılmadan), süre değerini
        // erkenden bildiriyoruz - böylece Recipe Preview paneli açıkken TimerView
        // "00:00" değil, level'ın gerçek süresini gösterebiliyor. Sayaç bu noktada
        // AKMIYOR, sadece görüntülenecek başlangıç değeri hazırlanıyor.
        levelTimerManager?.Configure(_activeLevel.levelDuration);
    }

    private void Start()
    {
        _levelEnded = false;
        _hasBegun = false;
        _isPaused = false;

        // Deste/süre/istasyon karışması ARTIK BURADA BAŞLAMIYOR.
        // RecipePreviewView, oyuncu üretim zinciri ekranını kapattığında
        // BeginLevelPlay()'i çağırır - level ancak o zaman fiilen başlar.
    }

    /// <summary>
    /// RecipePreviewView, oyuncu ilk kez üretim zinciri ekranını kapattığında bunu çağırır.
    /// Level'ı fiilen başlatır (deste kurulur, süre ve istasyon karışması akmaya başlar).
    /// </summary>
    public void BeginLevelPlay()
    {
        if (_hasBegun || _activeLevel == null) return;
        _hasBegun = true;

        BuildInitialDeck();
        DrawNextCard();

        stationAssignmentManager?.Configure(
            _activeLevel.stationsForLevel,
            _activeLevel.minStationShuffleInterval,
            _activeLevel.maxStationShuffleInterval);
        stationAssignmentManager?.StartAssigning();

        levelTimerManager?.Configure(_activeLevel.levelDuration);
        levelTimerManager?.StartTimer();
    }

    /// <summary>
    /// Oyuncu oyun sırasında "?" butonuyla üretim zincirini tekrar açtığında çağrılır.
    /// Süreyi ve istasyon karışmasını DONDURUR (sıfırlamaz), swipe'ları da geçici olarak yok sayar.
    /// </summary>
    public void PauseLevel()
    {
        if (!_hasBegun || _levelEnded) return;
        _isPaused = true;
        stationAssignmentManager?.PauseAssigning();
        levelTimerManager?.PauseTimer();
    }

    /// <summary>Duraklatılan level'ı kaldığı yerden devam ettirir.</summary>
    public void ResumeLevel()
    {
        if (!_hasBegun || _levelEnded) return;
        _isPaused = false;
        stationAssignmentManager?.ResumeAssigning();
        levelTimerManager?.ResumeTimer();
    }

    private void BuildInitialDeck()
    {
        _deck.Clear();
        foreach (var data in _activeLevel.initialDeck)
        {
            if (data == null)
            {
                Debug.LogWarning("[GameManager] Initial Deck listesinde boş (None) bir slot bulundu, atlanıyor. " +
                                  "LevelData asset'indeki Initial Deck listesini kontrol et.");
                continue;
            }

            _deck.Enqueue(new CardInstance(data));
        }
    }

    private void DrawNextCard()
    {
        if (_deck.Count == 0)
        {
            _currentCard = null;
            OnDeckEmptied?.Invoke();
            EndLevel(won: true);
            return;
        }

        _currentCard = _deck.Dequeue();
        OnCardChanged?.Invoke(_currentCard);
    }

    /// <summary>
    /// SwipeInputManager'dan gelen yönü işler. Yön, StationAssignmentManager üzerinden
    /// önce bir istasyona çevrilir, kural karşılaştırması istasyon bazlı yapılır.
    /// </summary>
    private void HandleSwipe(SwipeDirection direction)
    {
        if (_levelEnded || _isPaused || !_hasBegun || _currentCard == null || direction == SwipeDirection.None) return;

        // Kural sonucu ne olursa olsun (doğru/yanlış/final) görsel katman
        // "kart bu yöne fırlatıldı" bilgisini burada alır.
        OnSwipeResolved?.Invoke(direction);

        StationData targetStation = stationAssignmentManager != null
            ? stationAssignmentManager.GetStationForDirection(direction)
            : null;

        bool hasOutcome = _currentCard.Data.TryGetOutcome(targetStation, out CardData resultData);

        if (!hasOutcome)
        {
            // Yanlış istasyon: kart artık sadece kuyruğa dönmüyor, Ham (Raw) haline SIFIRLANIYOR.
            CardInstance revertedInstance = _currentCard;
            CardData revertTarget = revertedInstance.Data.rawStageVersion != null
                ? revertedInstance.Data.rawStageVersion
                : revertedInstance.Data; // zaten Ham ise kendisi kalır

            revertedInstance.SetData(revertTarget);
            revertedInstance.StateMachine.ChangeState(new RawCardState());

            OnInvalidSwipe?.Invoke(direction, targetStation);
            _deck.Enqueue(revertedInstance);
            DrawNextCard();
            return;
        }

        if (resultData == null)
        {
            // Bu istasyon tanımlı ama sonuç kartı yok -> kart bilerek çöpe gönderiliyor.
            _currentCard = null;
            DrawNextCard();
            return;
        }

        CardInstance processedInstance = _currentCard;
        processedInstance.StateMachine.ChangeState(new ProcessingCardState());
        processedInstance.SetData(resultData);

        if (resultData.isFinalProduct)
        {
            processedInstance.StateMachine.ChangeState(new CompletedCardState());
            OnCardCompleted?.Invoke(processedInstance);
            _currentCard = null;
        }
        else
        {
            processedInstance.StateMachine.ChangeState(new RawCardState());
            OnCardProcessed?.Invoke(processedInstance, resultData);
            _deck.Enqueue(processedInstance);
        }

        DrawNextCard();
    }

    private void HandleTimeExpired()
    {
        if (_levelEnded) return; // deste zaten bitmiş, level zaten kazanılmışsa süre bitmesi önemsiz

        EndLevel(won: false);
    }

    private void EndLevel(bool won)
    {
        if (_levelEnded) return;

        _levelEnded = true;
        stationAssignmentManager?.StopAssigning();
        levelTimerManager?.StopTimer();

        if (won)
        {
            int stars = CalculateStars();
            LevelProgress.MarkLevelCompleted(_activeLevel);
            LevelProgress.SetStarsIfHigher(_activeLevel, stars);
            OnLevelWon?.Invoke(stars);
        }
        else
        {
            OnLevelFailed?.Invoke();
        }
    }

    /// <summary>
    /// Kalan sürenin toplam süreye oranına göre 1-3 arası yıldız hesaplar.
    /// Bu metod sadece won=true durumunda çağrıldığı için her zaman EN AZ 1 yıldız garantiler.
    /// </summary>
    private int CalculateStars()
    {
        if (levelTimerManager == null || _activeLevel == null || _activeLevel.levelDuration <= 0f)
            return 1;

        float remainingRatio = levelTimerManager.RemainingTime / _activeLevel.levelDuration;

        if (remainingRatio >= _activeLevel.threeStarRemainingRatio) return 3;
        if (remainingRatio >= _activeLevel.twoStarRemainingRatio) return 2;
        return 1;
    }
}