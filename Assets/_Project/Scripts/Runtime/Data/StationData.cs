using UnityEngine;

/// <summary>
/// Bir üretim istasyonunu temsil eder (örn. "Demirci", "Atölye", "Boyahane", "Kesimhane").
/// CardData artık HANGİ YÖNE değil, HANGİ İSTASYONA atılması gerektiğini bilir.
/// Yön <-> İstasyon eşleşmesini StationAssignmentManager, oyun sırasında karıştırarak yönetir.
///
/// Bu ayrım sayesinde: "Demir Külçesi -> Atölye'ye gitmeli" kuralı hep sabit kalır,
/// ama Atölye'nin ekranda hangi yönde (Sağ/Sol/Yukarı/Aşağı) durduğu her birkaç
/// saniyede bir değişir. Oyuncu kuralı değil, ekrandaki güncel yerleşimi takip etmek zorunda kalır.
/// </summary>
[CreateAssetMenu(fileName = "NewStationData", menuName = "CardCraft/Station Data")]
public class StationData : ScriptableObject
{
    [Header("Kimlik Bilgileri")]
    public string stationId;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
}
