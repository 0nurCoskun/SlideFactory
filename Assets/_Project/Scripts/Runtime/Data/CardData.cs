using UnityEngine;

/// <summary>
/// Bir kartın "o anki hali"ni temsil eden veri kaynağı.
/// Örn: "Ham Demir Cevheri" bir CardData'dır. Dökümhane'ye atılınca "Demir Külçesi" (başka bir CardData) olur.
/// "Demir Külçesi" Atölye'ye atılınca "Demir Kılıç" (isFinalProduct = true olan CardData) olur.
///
/// ÖNEMLİ: Kart artık YÖNE değil, İSTASYONA göre işlenir. Çünkü istasyonların ekrandaki
/// yönü (Sağ/Sol/Yukarı/Aşağı) StationAssignmentManager tarafından sürekli karıştırılıyor.
/// Bu sayede "Demir Külçesi Atölye'ye gitmeli" kuralı hep sabit kalır, sadece Atölye'nin
/// o an hangi yönde durduğu değişir.
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
    [Tooltip("True ise bu kart zincirin son ürünüdür. Doğru istasyona atılınca desteden tamamen silinir.")]
    public bool isFinalProduct = false;

    [Header("Yanlış Hamle Cezası")]
    [Tooltip("Bu kart yanlış istasyona atılırsa, HANGİ karta sıfırlanacağını belirtir. " +
             "Eğer bu kart zaten zincirin başlangıcı (Ham hali) ise BOŞ bırak - " +
             "kendi kendine sıfırlanmaya çalışmaz, zaten en baştadır.")]
    public CardData rawStageVersion;

    [Header("İstasyon Eşleşmeleri (Recipe)")]
    [Tooltip("Bu kart hangi istasyona atılırsa neye dönüşür? Listede olmayan bir istasyon 'yanlış hamle' sayılır ve kart Ham haline sıfırlanır.")]
    public StationOutcome[] outcomes;

    /// <summary>
    /// Verilen istasyon için bir sonuç tanımlı mı diye bakar.
    /// </summary>
    /// <returns>İstasyon tanımlıysa true, tanımlı değilse false (GameManager bunu "yanlış hamle" olarak yorumlar).</returns>
    public bool TryGetOutcome(StationData station, out CardData resultCard)
    {
        if (outcomes != null && station != null)
        {
            foreach (var outcome in outcomes)
            {
                if (outcome.station == station)
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
/// Tek bir "istasyon -> sonuç" eşleşmesi.
/// resultCard null bırakılırsa, bu istasyon kartı "çöpe" gönderir (imha eder) ama yine de geçerli bir hamledir.
/// </summary>
[System.Serializable]
public class StationOutcome
{
    public StationData station;
    [Tooltip("Boş bırakılırsa bu istasyona atılan kart imha edilir (çöp).")]
    public CardData resultCard;
}