using UnityEngine;

/// <summary>
/// Bir kartın "o anki hali"ni temsil eden veri kaynağı.
/// Örn: "Ham Demir Cevheri" bir CardData'dır. Sağa atılınca "Demir Külçesi" (başka bir CardData) olur.
/// "Demir Külçesi" sola atılınca "Demir Kılıç" (isFinalProduct = true olan CardData) olur.
///
/// Bu tasarımda her üretim aşaması AYRI bir CardData asset'idir.
/// Bu sayede designer, Inspector üzerinden yeni tarifler eklerken kod yazmaz,
/// sadece yeni asset oluşturup outcomes dizisini bağlar.
/// </summary>
[CreateAssetMenu(fileName = "NewCardData", menuName = "CardCraft/Card Data")]
public class CardData : ScriptableObject
{
    [Header("Kimlik Bilgileri")]
    public string cardId;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Üretim Durumu")]
    [Tooltip("True ise bu kart zincirin son ürünüdür. Doğru yöne atılınca desteden tamamen silinir.")]
    public bool isFinalProduct = false;

    [Header("Yön Eşleşmeleri (Recipe)")]
    [Tooltip("Bu kart hangi yöne atılırsa neye dönüşür? Listede olmayan bir yön 'yanlış hamle' sayılır.")]
    public DirectionOutcome[] outcomes;

    /// <summary>
    /// Verilen yön için bir sonuç tanımlı mı diye bakar.
    /// </summary>
    /// <returns>Yön tanımlıysa true, tanımlı değilse false (GameManager bunu "yanlış hamle" olarak yorumlar).</returns>
    public bool TryGetOutcome(SwipeDirection direction, out CardData resultCard)
    {
        if (outcomes != null)
        {
            foreach (var outcome in outcomes)
            {
                if (outcome.direction == direction)
                {
                    resultCard = outcome.resultCard;
                    return true;
                }
            }
        }

        resultCard = null;
        return false;
    }
}

/// <summary>
/// Tek bir "yön -> sonuç" eşleşmesi.
/// resultCard null bırakılırsa, o yön kartı "çöpe" gönderir (imha eder) ama yine de geçerli bir hamledir.
/// </summary>
[System.Serializable]
public class DirectionOutcome
{
    public SwipeDirection direction;
    [Tooltip("Boş bırakılırsa bu yöne atılan kart imha edilir (çöp).")]
    public CardData resultCard;
}
