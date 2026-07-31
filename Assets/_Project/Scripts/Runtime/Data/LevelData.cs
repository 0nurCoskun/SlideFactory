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

    [Header("İlerleme / Kilit")]
    [Tooltip("Bu level'ın açılması için hangi level'ın tamamlanmış olması gerekiyor? " +
             "Boş bırakılırsa (ilk level için) her zaman açık olur.")]
    public LevelData requiredPreviousLevel;

    [Tooltip("Win panelindeki 'Sonraki Level' butonunun yükleyeceği level. " +
             "Son level ise boş bırak - buton otomatik gizlenir.")]
    public LevelData nextLevel;

    [Header("Deste")]
    [Tooltip("Bu level'da destede bulunacak ham kartlar.")]
    public List<CardData> initialDeck;

    [Header("Süre")]
    [Tooltip("Bölümün toplam süresi (saniye).")]
    public float levelDuration = 90f;

    [Header("Yıldız Eşikleri (kalan sürenin toplam süreye oranı)")]
    [Tooltip("Kalan süre oranı bu değerin ÜSTÜNDEYSE 3 yıldız verilir. Örn: 0.5 = süresinin yarısından fazlası kalmışsa.")]
    [Range(0f, 1f)] public float threeStarRemainingRatio = 0.5f;
    [Tooltip("Kalan süre oranı bu değerin ÜSTÜNDEYSE (ama 3 yıldız eşiğinin altındaysa) 2 yıldız verilir.")]
    [Range(0f, 1f)] public float twoStarRemainingRatio = 0.2f;

    [Header("İstasyon Karışması")]
    [Tooltip("TAM OLARAK 4 istasyon olmalı - bu level'da hangi istasyonlar kullanılacak.")]
    public StationData[] stationsForLevel;
    [Tooltip("İki karışma arasındaki minimum süre (saniye).")]
    public float minStationShuffleInterval = 3f;
    [Tooltip("İki karışma arasındaki maksimum süre (saniye).")]
    public float maxStationShuffleInterval = 4f;

    [Header("Tutorial")]
    [Tooltip("True ise bu level bir öğretici (tutorial). Süre bitince level KAYBEDİLMEZ (sayaç sıfırdan başlar), " +
             "kazanılınca LevelProgress'e (tamamlanma/yıldız) hiçbir şey yazılmaz ve normal Win/Lose panelleri " +
             "gösterilmez - TutorialFlowView akışı devralır.")]
    public bool isTutorial;
}