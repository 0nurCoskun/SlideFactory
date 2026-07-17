using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tek bir level'ın TÜM ayarlarını tutan veri kaynağı. Yeni bir level eklemek
/// artık sahne kopyalamak değil, bu asset'ten yeni bir tane oluşturmak demek.
/// </summary>
[CreateAssetMenu(fileName = "NewLevelData", menuName = "CardCraft/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Kimlik")]
    public string levelId;
    public string displayName;

    [Header("Deste")]
    [Tooltip("Bu level'da destede bulunacak ham kartlar.")]
    public List<CardData> initialDeck;

    [Header("Süre")]
    [Tooltip("Bölümün toplam süresi (saniye).")]
    public float levelDuration = 90f;

    [Header("İstasyon Karışması")]
    [Tooltip("TAM OLARAK 4 istasyon olmalı - bu level'da hangi istasyonlar kullanılacak.")]
    public StationData[] stationsForLevel;
    [Tooltip("İki karışma arasındaki minimum süre (saniye).")]
    public float minStationShuffleInterval = 3f;
    [Tooltip("İki karışma arasındaki maksimum süre (saniye).")]
    public float maxStationShuffleInterval = 4f;
}
