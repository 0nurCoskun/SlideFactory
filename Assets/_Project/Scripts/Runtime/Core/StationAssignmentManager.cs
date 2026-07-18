using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hangi istasyonun (Demirci, Atölye vb.) ekranın hangi yönünde (Sağ/Sol/Yukarı/Aşağı)
/// durduğunu yönetir ve bu eşleşmeyi belirli aralıklarla karıştırır.
///
/// Tek sorumluluğu budur - kart kurallarını (doğru/yanlış hamle) bilmez, sadece
/// "şu an Sağ'da hangi istasyon var" sorusuna cevap verir. GameManager bu cevabı
/// kartın recipe'siyle karşılaştırıp karar verir (Single Responsibility).
/// </summary>
public class StationAssignmentManager : MonoBehaviour
{
    [Header("İstasyonlar")]
    [Tooltip("TAM OLARAK 4 istasyon olmalı - her yön (Up/Down/Left/Right) için bir tane.")]
    [SerializeField] private StationData[] allStations;

    [Header("Karışma Zamanlaması")]
    [Tooltip("İki karışma arasındaki minimum süre (saniye).")]
    [SerializeField] private float minShuffleInterval = 3f;
    [Tooltip("İki karışma arasındaki maksimum süre (saniye). Min-Max arasında rastgele seçilir, tahmin edilebilir olmasın diye.")]
    [SerializeField] private float maxShuffleInterval = 4f;

    private readonly Dictionary<SwipeDirection, StationData> _currentAssignment = new Dictionary<SwipeDirection, StationData>();
    private static readonly SwipeDirection[] AllDirections = { SwipeDirection.Up, SwipeDirection.Down, SwipeDirection.Left, SwipeDirection.Right };

    private float _shuffleTimer;
    private float _nextShuffleInterval;
    private bool _isRunning;

    /// <summary>Eşleşme her karıştığında tetiklenir. UI (StationLabelsView) bunu dinleyip etiketleri günceller.</summary>
    public event Action<IReadOnlyDictionary<SwipeDirection, StationData>> OnStationsShuffled;

    private void Awake()
    {
        // Artık istasyonlar Configure() ile LevelData'dan geliyor,
        // bu yüzden burada Inspector'daki değeri kontrol etmiyoruz.
    }

    /// <summary>
    /// GameManager, seçilen LevelData'daki değerlerle bu sistemi çalıştırmadan
    /// önce yapılandırır. StartAssigning()'den ÖNCE çağrılmalıdır.
    /// </summary>
    public void Configure(StationData[] stations, float minInterval, float maxInterval)
    {
        allStations = stations;
        minShuffleInterval = minInterval;
        maxShuffleInterval = maxInterval;

        if (allStations == null || allStations.Length != AllDirections.Length)
        {
            Debug.LogError($"[StationAssignmentManager] Tam olarak {AllDirections.Length} istasyon atanmalı, " +
                            $"şu an {allStations?.Length ?? 0} tane var.");
        }
    }

    /// <summary>Bölüm başladığında GameManager tarafından çağrılır.</summary>
    public void StartAssigning()
    {
        _isRunning = true;
        ShuffleNow();
    }

    /// <summary>Bölüm bittiğinde (kazanıldı/kaybedildi) karışmayı durdurmak için.</summary>
    public void StopAssigning()
    {
        _isRunning = false;
    }

    /// <summary>
    /// Recipe önizleme paneli gibi bir sebeple oyunu geçici durdurmak için.
    /// StopAssigning()'den farkı: geri sayım durumu KORUNUR, panel kapanınca
    /// StartAssigning() gibi anında yeniden karıştırmaz, kaldığı yerden devam eder.
    /// </summary>
    public void PauseAssigning()
    {
        _isRunning = false;
    }

    /// <summary>Duraklatılan karışma geri sayımını kaldığı yerden devam ettirir.</summary>
    public void ResumeAssigning()
    {
        _isRunning = true;
    }

    private void Update()
    {
        if (!_isRunning) return;

        _shuffleTimer += Time.deltaTime;
        if (_shuffleTimer >= _nextShuffleInterval)
        {
            ShuffleNow();
        }
    }

    /// <summary>Şu an verilen yönde hangi istasyon var, GameManager bunu sorar.</summary>
    public StationData GetStationForDirection(SwipeDirection direction)
    {
        return _currentAssignment.TryGetValue(direction, out StationData station) ? station : null;
    }

    private void ShuffleNow()
    {
        _shuffleTimer = 0f;
        _nextShuffleInterval = UnityEngine.Random.Range(minShuffleInterval, maxShuffleInterval);

        List<StationData> shuffled = new List<StationData>(allStations);
        FisherYatesShuffle(shuffled);

        _currentAssignment.Clear();
        for (int i = 0; i < AllDirections.Length && i < shuffled.Count; i++)
        {
            _currentAssignment[AllDirections[i]] = shuffled[i];
        }

        OnStationsShuffled?.Invoke(_currentAssignment);
    }

    private static void FisherYatesShuffle(List<StationData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
        }
    }
}