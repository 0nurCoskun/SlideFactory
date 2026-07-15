using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyunun akışını yöneten merkezi sınıf.
/// Sorumluluğu: deste durumunu tutmak, SwipeInputManager'dan gelen yönü mevcut kartın
/// "recipe"si ile karşılaştırmak ve sonucu uygulamak. Görsel/animasyon işini bilmez;
/// bunun yerine event fırlatır, CardView/UI katmanı bu event'leri dinleyip DOTween ile
/// animasyonu oynatır (Dependency Inversion - GameManager somut bir View'a bağımlı değil).
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] private SwipeInputManager swipeInputManager;

    [Header("Başlangıç Destesi")]
    [Tooltip("Oyunun başında destede bulunacak ham kartlar (Inspector'dan sürükle-bırak).")]
    [SerializeField] private List<CardData> initialDeckData;

    // FIFO kuyruk: işlenmiş kart, deste sonuna eklenir. İstersen Stack'e çevirip
    // "işlenen kart hemen tekrar önüne gelsin" davranışına kolayca geçebilirsin.
    private readonly Queue<CardInstance> _deck = new Queue<CardInstance>();
    private CardInstance _currentCard;

    public CardInstance CurrentCard => _currentCard;
    public int RemainingCardCount => _deck.Count + (_currentCard != null ? 1 : 0);

    // --- Dış dünyaya (UI, CardView, SFX) haber veren event'ler ---
    public event Action<CardInstance> OnCardChanged;               // Ekranda gösterilecek yeni kart geldi
    public event Action<CardInstance, CardData> OnCardProcessed;    // Kart bir üst aşamaya geçti (henüz final değil)
    public event Action<CardInstance> OnCardCompleted;              // Kart son ürüne dönüştü ve desteden düştü
    public event Action<SwipeDirection> OnInvalidSwipe;             // Yanlış yöne atıldı
    public event Action OnDeckEmptied;                               // Deste bitti, oyun kazanıldı

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
    }

    private void OnDisable()
    {
        if (swipeInputManager != null)
            swipeInputManager.OnSwipeDetected.RemoveListener(HandleSwipe);
    }

    private void Start()
    {
        BuildInitialDeck();
        DrawNextCard();
    }

    private void BuildInitialDeck()
    {
        _deck.Clear();
        foreach (var data in initialDeckData)
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
            return;
        }

        _currentCard = _deck.Dequeue();
        OnCardChanged?.Invoke(_currentCard);
    }

    /// <summary>
    /// SwipeInputManager'dan gelen yönü işler. Oyunun tüm "kural" mantığı burada yaşar.
    /// </summary>
    private void HandleSwipe(SwipeDirection direction)
    {
        if (_currentCard == null || direction == SwipeDirection.None) return;

        // Kural sonucu ne olursa olsun (doğru/yanlış/final) görsel katman
        // "kart bu yöne fırlatıldı" bilgisini burada alır.
        OnSwipeResolved?.Invoke(direction);

        bool hasOutcome = _currentCard.Data.TryGetOutcome(direction, out CardData resultData);

        if (!hasOutcome)
        {
            // Yanlış yön: kart kaybolmaz, deste sonuna geri döner.
            OnInvalidSwipe?.Invoke(direction);
            _deck.Enqueue(_currentCard);
            DrawNextCard();
            return;
        }

        if (resultData == null)
        {
            // Bu yön tanımlı ama sonuç kartı yok -> kart bilerek çöpe gönderiliyor.
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
}