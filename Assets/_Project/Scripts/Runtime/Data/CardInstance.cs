using System;

/// <summary>
/// ÖNEMLİ: CardData bir ScriptableObject'tir, yani bir "asset"tir ve proje genelinde PAYLAŞILIR.
/// Eğer runtime'da doğrudan CardData'nın kendi state'ini değiştirirsek, tüm oyun oturumları
/// ve hatta editördeki asset bile bozulur.
///
/// Bu yüzden her fiziksel kart için, sahnede bir CardInstance (plain C# sınıfı, MonoBehaviour DEĞİL)
/// yaratıyoruz. CardInstance, hangi CardData'yı "şu an temsil ettiğini" ve state machine'ini tutar.
/// Kart işlendikçe (Ham Demir -> Külçe -> Kılıç) aynı CardInstance'ın Data referansı değişir,
/// asset'lerin kendisi hiç değişmez.
/// </summary>
public class CardInstance
{
    public CardData Data { get; private set; }
    public CardStateMachine StateMachine { get; private set; }

    /// <summary>Bu instance'ın state'i değiştiğinde tetiklenir (UI/animasyon dinleyebilir).</summary>
    public event Action<CardInstance> OnStateChanged;

    public CardInstance(CardData data)
    {
        Data = data;
        StateMachine = new CardStateMachine(this, new RawCardState());
        StateMachine.OnStateChanged += _ => OnStateChanged?.Invoke(this);
    }

    /// <summary>Kart işlendiğinde (örn. Demir Külçesi -> Demir Kılıç) çağrılır.</summary>
    public void SetData(CardData newData)
    {
        Data = newData;
    }

    public bool IsFinal => Data != null && Data.isFinalProduct;
}
