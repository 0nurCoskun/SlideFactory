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
    public event Action OnLevelWon;                                  // Süre bitmeden deste tamamlandı
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

    private void Start()
    {
        _levelEnded = false;

        _activeLevel = LevelSession.SelectedLevel != null ? LevelSession.SelectedLevel : fallbackLevelData;

        if (_activeLevel == null)
        {
            Debug.LogError("[GameManager] Hiçbir LevelData bulunamadı - ne LevelSession'da seçili bir level var, " +
                            "ne de Fallback Level Data Inspector'da atanmış. Oyun başlatılamıyor.");
            return;
        }

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

    private void BuildInitialDeck()
    {
        _deck.Clear();
        foreach (var data in _activeLevel.initialDeck)
        {
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
        if (_levelEnded || _currentCard == null || direction == SwipeDirection.None) return;

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
            OnLevelWon?.Invoke();
        else
            OnLevelFailed?.Invoke();
    }
}