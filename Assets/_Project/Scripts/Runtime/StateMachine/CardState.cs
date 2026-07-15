using System;

/// <summary>
/// Bir kartın yaşam döngüsündeki durum tipleri.
/// UI ve animasyon katmanları bu tipe göre karar verir (örn. Processing sırasında input kilitle).
/// </summary>
public enum CardStateType
{
    Raw,        // Deste üzerinde bekliyor, oynanabilir
    Processing, // Fırlatıldı, animasyon/işlem sürüyor, input kabul edilmez
    Completed   // Son ürüne dönüştü, desteden düşecek
}

/// <summary>
/// State pattern arayüzü. İleride her state'e özel davranış eklemek istersen
/// (örn. Processing durumunda otomatik bir timer başlatmak) bu arayüz sayesinde
/// GameManager'ı bozmadan genişletebilirsin (Open/Closed prensibi).
/// </summary>
public interface ICardState
{
    CardStateType StateType { get; }
    void Enter(CardInstance card);
    void Exit(CardInstance card);
}

/// <summary>
/// Her CardInstance kendi CardStateMachine'ine sahiptir.
/// Böylece 10 kart aynı anda farklı state'lerde olabilir (biri Processing, biri Raw).
/// </summary>
public class CardStateMachine
{
    public ICardState CurrentState { get; private set; }
    public event Action<CardStateType> OnStateChanged;

    private readonly CardInstance _owner;

    public CardStateMachine(CardInstance owner, ICardState initialState)
    {
        _owner = owner;
        CurrentState = initialState;
        CurrentState.Enter(_owner);
    }

    public void ChangeState(ICardState newState)
    {
        CurrentState?.Exit(_owner);
        CurrentState = newState;
        CurrentState.Enter(_owner);
        OnStateChanged?.Invoke(CurrentState.StateType);
    }
}

public class RawCardState : ICardState
{
    public CardStateType StateType => CardStateType.Raw;
    public void Enter(CardInstance card) { /* örn: kartı tekrar sürüklenebilir yap */ }
    public void Exit(CardInstance card) { }
}

public class ProcessingCardState : ICardState
{
    public CardStateType StateType => CardStateType.Processing;
    public void Enter(CardInstance card) { /* örn: DOTween animasyonunu tetikle, input'u kilitle */ }
    public void Exit(CardInstance card) { }
}

public class CompletedCardState : ICardState
{
    public CardStateType StateType => CardStateType.Completed;
    public void Enter(CardInstance card) { /* örn: "başarılı üretim" efektini tetikle */ }
    public void Exit(CardInstance card) { }
}
