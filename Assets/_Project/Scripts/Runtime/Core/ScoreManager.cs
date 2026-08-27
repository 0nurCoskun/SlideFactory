using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Combo/puan sistemi. GameManager'ın swipe event'lerini dinleyip puan biriktirir,
/// art arda gelen doğru swipe'larda ÇARPANI yükseltir, oyuncu oyalanırsa çarpanı
/// zamanla söndürür (decay). Level sonunda kalan süreyi bonusa çevirir ve rekoru
/// ScoreProgress üzerinden kaydeder.
///
/// GameManager gibi oynanış kurallarına karışmaz: hangi swipe'ın doğru olduğunu
/// BİLMEZ, sadece "doğru oldu / yanlış oldu" event'lerini duyar. Görsel tarafı da
/// bilmez - ScoreHudView ve LevelResultView buradaki event'leri dinler.
///
/// ÇİFT YÖNLÜ REFERANS UYARISI: Bu sınıf gameManager'ı tutar (event dinlemek için),
/// GameManager da scoreManager'ı tutar (level sonunda süre bonusunu OnLevelWon'dan
/// ÖNCE uygulamak için). Projedeki diğer manager'lar tek yönlüyken buranın çift
/// yönlü olması ZORUNLU: eğer süre bonusunu OnLevelWon'u dinleyerek eklersek bonus,
/// CalculateStars() ve LevelResultView'ın skoru okumasından SONRA eklenmiş olur.
///
/// Sahnede GameManager/LevelTimerManager ile aynı (HER ZAMAN AKTİF) objeye eklenmeli -
/// objesi kapalı başlarsa swipe'ları sessizce kaçırır.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private GameManager gameManager;

    [Header("Temel Puanlar")]
    [Tooltip("Bir doğru swipe'ın temel puanı. Gerçek puan = bu değer x o anki çarpan.")]
    [SerializeField] private int basePointsPerCorrectSwipe = 100;
    [Tooltip("Bir kart son ürüne dönüştüğünde verilen ek puan. Çarpanla ÇARPILMAZ - " +
             "par skorunun deterministik kalması için bilerek sabit tutuldu.")]
    [SerializeField] private int pointsPerCompletedCard = 250;
    [Tooltip("Yanlış swipe'ta düşülen temel ceza. Gerçek ceza = bu değer x o anki çarpan " +
             "(yüksek çarpanda hata yapmak daha pahalı olsun diye). Toplam puan asla 0'ın altına inmez.")]
    [SerializeField] private int wrongSwipePenalty = 50;
    [Tooltip("Level sonunda kalan her saniye için verilen bonus. Par skoruna DAHİL DEĞİLDİR.")]
    [SerializeField] private int pointsPerSecondRemaining = 20;

    [Header("Çarpan (Combo)")]
    [Tooltip("Her doğru swipe'ta çarpanın artacağı miktar.")]
    [SerializeField] private float multiplierStep = 0.25f;
    [Tooltip("Çarpanın çıkabileceği en yüksek değer.")]
    [SerializeField] private float maxMultiplier = 4f;
    [Tooltip("Yanlış swipe'ta çarpanın düşeceği miktar. 1.0 = bir kademe geri, " +
             "SIFIRLANMAZ (taban her zaman 1.0).")]
    [SerializeField] private float wrongSwipeMultiplierDrop = 1f;

    [Header("Çarpan Sönümü (Decay)")]
    [Tooltip("Son doğru swipe'tan sonra çarpanın düşmeye başlaması için geçmesi gereken " +
             "boş süre (saniye). Kartları art arda oynamayı ödüllendiren asıl mekanik bu.")]
    [SerializeField] private float decayIdleDelay = 2f;
    [Tooltip("Sönüm başladıktan sonra çarpanın saniyede ne kadar düşeceği.")]
    [SerializeField] private float decayRatePerSecond = 0.5f;

    [Header("Seri (Streak)")]
    [Tooltip("Kaç doğru swipe'ta bir 'kilometre taşı' event'i tetiklenecek (ses/efekt için). 0 = kapalı.")]
    [SerializeField] private int comboMilestoneInterval = 5;

    [Header("Çiftlik (Farm) Koruması")]
    [Tooltip("TEMEL PUAN kazandıran doğru swipe sayısı, par swipe sayısının bu KATINI aşamaz.\n\n" +
             "NEDEN GEREKLİ: Yanlış swipe atılan kart Ham'a sıfırlanıp desteye geri döner, " +
             "yani oyuncu aynı zinciri tekrar oynayıp tekrar puan alabilir. Bu sınır olmasa " +
             "BİLEREK yanlış swipe atmak, doğru oynamaktan daha kârlı olurdu ve her level " +
             "bedava 3 yıldıza dönerdi.\n\n" +
             "Sınırın üstündeki swipe'lar 0 temel puan verir ama çarpanı ve seriyi YİNE " +
             "ilerletir - oyuncu oyunun bozulduğunu hissetmesin. 1.5 = dürüst hataları affeder.")]
    [SerializeField] private float scoringSwipeAllowance = 1.5f;

    [Header("Tutorial")]
    [Tooltip("Tutorial'da süre bitince sayaç BAŞA DÖNÜYOR (GameManager.HandleTimeExpired), " +
             "yani kalan süre orada bir 'hız' ölçüsü değil. Açık bırak: oyalanan oyuncu " +
             "yanlışlıkla maksimum süre bonusu almasın.")]
    [SerializeField] private bool skipTimeBonusOnTutorial = true;

    [Header("Debug")]
    [Tooltip("Level başında hesaplanan par skorunu Console'a yazar - eşikleri ayarlarken işe yarar.")]
    [SerializeField] private bool logParScoreOnStart = true;

    // --- Çalışma zamanı durumu (static YOK: Restart/Next Level sahneyi yeniden
    //     yüklüyor ve her seferinde sıfırdan bir instance bekleniyor) ---
    private float _timeSinceLastCorrectSwipe;
    private int _correctSwipeCount;
    private int _parScore;
    private int _parCorrectSwipes;
    private bool _parComputed;

    public int TotalScore { get; private set; }
    public float CurrentMultiplier { get; private set; }
    public int CurrentStreak { get; private set; }
    public int BestStreak { get; private set; }

    /// <summary>Bu level'ın "kusursuz oynanış" temel puanı. BİR KEZ hesaplanır, sonra önbellekten döner.</summary>
    public int ParScore { get { EnsureParComputed(); return _parScore; } }

    /// <summary>Kusursuz bir koşuda gereken doğru swipe sayısı. Çiftlik koruması bunu baz alır.</summary>
    public int ParCorrectSwipes { get { EnsureParComputed(); return _parCorrectSwipes; } }

    /// <summary>
    /// BU koşudan ÖNCEKİ rekor. FinalizeScore, rekoru kaydetmeden ÖNCE burayı doldurur -
    /// yoksa sonuç paneli "yeni rekor" karşılaştırmasını yapamazdı (üzerine yazılmış
    /// değeri okurdu ve rozet asla görünmezdi).
    /// </summary>
    public int PreviousBestScore { get; private set; }

    /// <summary>Bu koşuda gerçekten yeni bir rekor kırıldı mı? Sadece KAZANILAN, tutorial olmayan level'da true olabilir.</summary>
    public bool IsNewBestScore { get; private set; }

    // --- Event'ler (HUD ve sonuç paneli bunları dinler) ---
    public event Action<int> OnScoreChanged;                 // yeni toplam puan
    public event Action<float> OnMultiplierChanged;          // yeni çarpan
    public event Action<int, int, float> OnPointsAwarded;    // (uygulanan değişim, yeni toplam, çarpan)
    public event Action<int> OnComboMilestone;               // seri kilometre taşına ulaşıldı

    private void Awake()
    {
        // ÖNEMLİ: float alanı varsayılan 0 gelir - burada 1'e çekilmezse
        // her puan 0 ile çarpılır ve sistem hiç puan üretmez.
        CurrentMultiplier = 1f;
    }

    private void Start()
    {
        // Awake'te DEĞİL: GameManager._activeLevel kendi Awake'inde atanıyor ve iki
        // MonoBehaviour arasındaki Awake sırası GARANTİ DEĞİL. Tüm Awake'ler herhangi
        // bir Start'tan önce bittiği için burada ActiveLevel kesin hazır (aynı gerekçe
        // GameManager.Awake içindeki yorumda da yazıyor).
        EnsureParComputed();

        if (logParScoreOnStart)
        {
            Debug.Log($"[ScoreManager] Par skoru: {_parScore} puan / {_parCorrectSwipes} doğru swipe. " +
                       $"Çiftlik koruması sınırı: {GetScoringSwipeLimit()} swipe.");
        }
    }

    private void OnEnable()
    {
        if (gameManager != null)
        {
            gameManager.OnValidSwipe += HandleValidSwipe;
            gameManager.OnInvalidSwipe += HandleInvalidSwipe;
            gameManager.OnCardCompleted += HandleCardCompleted;
        }

        _timeSinceLastCorrectSwipe = 0f;
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnValidSwipe -= HandleValidSwipe;
            gameManager.OnInvalidSwipe -= HandleInvalidSwipe;
            gameManager.OnCardCompleted -= HandleCardCompleted;
        }
    }

    private void Update()
    {
        // SwipeHintArrowView.IsHintAllowed ile aynı kapı. HasBegun kontrolü ŞART, fazlalık
        // değil: Recipe Preview paneli İLK kez açıkken IsPaused hâlâ false (PauseLevel,
        // !_hasBegun olduğu için erken return ediyor) - o sırada çarpan sönmemeli.
        if (gameManager == null || !gameManager.HasBegun || gameManager.IsPaused || gameManager.IsLevelEnded)
        {
            // Sayacı DONDURMAKLA yetinme, SIFIRLA: yoksa 30 saniye pause menüsünde
            // kalan oyuncunun çarpanı, oyuna döndüğü ilk karede dibe vurur.
            _timeSinceLastCorrectSwipe = 0f;
            return;
        }

        if (CurrentMultiplier <= 1f) return;

        // Time.timeScale bu projede hiç kullanılmıyor (bkz. SwipeHintArrowView),
        // bu yüzden deltaTime yeterli - unscaledDeltaTime'a gerek yok.
        _timeSinceLastCorrectSwipe += Time.deltaTime;
        if (_timeSinceLastCorrectSwipe < decayIdleDelay) return;

        float decayed = Mathf.Max(1f, CurrentMultiplier - decayRatePerSecond * Time.deltaTime);
        SetMultiplier(decayed);

        // Çarpan tabana döndüyse seriyi de sıfırla: HUD'da "7 Combo" yazarken
        // çarpanın x1.00 olması oyuncuya hata gibi görünür.
        if (Mathf.Approximately(decayed, 1f)) CurrentStreak = 0;
    }

    /// <summary>
    /// Doğru istasyona atılan HER swipe (çöp/normal/final farketmez). GameManager.OnValidSwipe,
    /// OnCardProcessed/OnCardCompleted'dan ÖNCE ve tüm doğru hamleleri kapsayacak şekilde
    /// tetiklendiği için seri sayacı buna bağlanmak zorunda.
    /// </summary>
    private void HandleValidSwipe(SwipeDirection direction, StationData station)
    {
        _correctSwipeCount++;

        // Çiftlik koruması: sınırın üstündeki swipe'lar temel puan vermez.
        // Par bilinmiyorsa (0) kilitleme yapma - yoksa hatalı yapılandırılmış bir
        // level'da oyuncu hiç puan alamaz.
        int limit = GetScoringSwipeLimit();
        if (limit <= 0 || _correctSwipeCount <= limit)
        {
            AddPoints(Mathf.RoundToInt(basePointsPerCorrectSwipe * CurrentMultiplier));
        }

        // SIRA ÖNEMLİ: önce o anki çarpanla puan verilir, SONRA çarpan artar.
        // ComputeParScore da birebir aynı sırayı taklit ediyor.
        SetMultiplier(Mathf.Min(CurrentMultiplier + multiplierStep, maxMultiplier));
        _timeSinceLastCorrectSwipe = 0f;

        CurrentStreak++;
        if (CurrentStreak > BestStreak) BestStreak = CurrentStreak;

        if (comboMilestoneInterval > 0 && CurrentStreak % comboMilestoneInterval == 0)
        {
            OnComboMilestone?.Invoke(CurrentStreak);
        }
    }

    private void HandleInvalidSwipe(SwipeDirection direction, StationData station)
    {
        AddPoints(-Mathf.RoundToInt(wrongSwipePenalty * CurrentMultiplier));

        // SIFIRLAMA değil, bir kademe DÜŞÜRME (taban 1.0).
        SetMultiplier(Mathf.Max(1f, CurrentMultiplier - wrongSwipeMultiplierDrop));

        CurrentStreak = 0;
        _timeSinceLastCorrectSwipe = 0f;
    }

    private void HandleCardCompleted(CardInstance instance)
    {
        // Sabit bonus - çarpanla çarpılmıyor (bkz. pointsPerCompletedCard tooltip'i).
        AddPoints(pointsPerCompletedCard);
    }

    /// <summary>
    /// Level bitişinde GameManager tarafından, OnLevelWon/OnLevelFailed'dan ÖNCE çağrılır.
    /// Süre bonusunu ekler ve rekoru işler. Tek giriş noktası olması bilinçli: süre bonusu,
    /// tutorial istisnası ve kalıcı kayıt kararı hep burada, birbirinden sapamayacak şekilde.
    ///
    /// persist=false (kayıp ya da tutorial) ise PlayerPrefs'e HİÇBİR ŞEY yazılmaz;
    /// PreviousBestScore yine doldurulur, böylece Lose paneli mevcut rekoru gösterebilir.
    /// </summary>
    public void FinalizeScore(LevelData level, float remainingSeconds, bool persist)
    {
        bool isTutorial = level != null && level.isTutorial;

        if (!(isTutorial && skipTimeBonusOnTutorial))
        {
            ApplyEndOfLevelTimeBonus(remainingSeconds);
        }

        PreviousBestScore = ScoreProgress.GetBestScore(level);
        IsNewBestScore = persist && !isTutorial && ScoreProgress.SetBestScoreIfHigher(level, TotalScore);

        // Play Games skor tablosuna gönderim - ScoreProgress'ten AYRI, best-effort bir katman.
        // Sunucu zaten mevcut rekordan düşük skorları kendisi eliyor, o yüzden IsNewBestScore
        // şartı aranmadan her persist edilen (kaybedilmeyen, tutorial olmayan) koşuda gönderilir.
        if (persist && !isTutorial)
        {
            LeaderboardManager.Instance?.SubmitScore(level, TotalScore);
        }
    }

    /// <summary>Kalan süreyi puana çevirir. Negatif süreye karşı korumalı.</summary>
    public void ApplyEndOfLevelTimeBonus(float remainingSeconds)
    {
        if (pointsPerSecondRemaining <= 0) return;
        AddPoints(Mathf.RoundToInt(Mathf.Max(0f, remainingSeconds) * pointsPerSecondRemaining));
    }

    /// <summary>Temel puan kazandıran swipe üst sınırı. Par bilinmiyorsa 0 (= sınır yok).</summary>
    private int GetScoringSwipeLimit()
    {
        int parSwipes = ParCorrectSwipes;
        if (parSwipes <= 0) return 0;
        return Mathf.RoundToInt(parSwipes * scoringSwipeAllowance);
    }

    private void AddPoints(int delta)
    {
        int previous = TotalScore;
        TotalScore = Mathf.Max(0, TotalScore + delta);

        // Kırpma SONRASI gerçek değişim: puan 20'de iken -50 ceza gelirse HUD'da
        // "-50" göstermek yalan olur, gerçekte sadece 20 puan kaybedildi.
        int applied = TotalScore - previous;

        OnPointsAwarded?.Invoke(applied, TotalScore, CurrentMultiplier);
        OnScoreChanged?.Invoke(TotalScore);
    }

    private void SetMultiplier(float value)
    {
        // Sadece GERÇEKTEN değiştiyse haber ver - sönüm sırasında her karede event
        // fırlatmak HUD'daki vurgu animasyonunu titretir.
        if (Mathf.Approximately(CurrentMultiplier, value)) return;

        CurrentMultiplier = value;
        OnMultiplierChanged?.Invoke(CurrentMultiplier);
    }

    private void EnsureParComputed()
    {
        if (_parComputed) return;
        _parComputed = true;

        LevelData level = gameManager != null ? gameManager.ActiveLevel : null;
        _parScore = ComputeParScore(level, out _parCorrectSwipes);
    }

    /// <summary>
    /// Bir level'ın par skoru: kusursuz bir koşunun (hiç hata yok, çarpan hiç düşmüyor)
    /// toplayacağı puan. Süre bonusu DAHİL DEĞİLDİR - o yüzden gerçek skor par'ın
    /// üstüne çıkabilir ve yıldız eşikleri 0-2 aralığında.
    /// </summary>
    public int ComputeParScore(LevelData level)
    {
        return ComputeParScore(level, out _);
    }

    private int ComputeParScore(LevelData level, out int totalCorrectSwipes)
    {
        totalCorrectSwipes = 0;
        if (level == null || level.initialDeck == null) return 0;

        int completedCardCount = 0;

        // Deste TEKRARLI girdiler içerebilir ve her girdi ayrı bir CardInstance olur -
        // GameManager.BuildInitialDeck ile birebir aynı şekilde, null'ları atlayarak
        // dolaş. (RecipePreviewView zincirleri GÖSTERMEK için HashSet ile tekilleştiriyor;
        // o mantığı buraya kopyalamak par'ı yanlış hesaplardı.)
        foreach (CardData entry in level.initialDeck)
        {
            if (entry == null) continue;

            totalCorrectSwipes += CountCorrectSwipesForEntry(entry, out bool endsOnFinalProduct);
            if (endsOnFinalProduct) completedCardCount++;
        }

        // ÖNEMLİ: kapalı formül (base * (1 + i*step)) KULLANILMIYOR. Çalışma zamanı
        // çarpanı tek tek "+= step" ile biriktirdiği için float hatası oluşuyor
        // (12 adım sonra 4.0 yerine 3.9999997 gibi). Aynı sırayla biriktirmezsek par,
        // ULAŞILAMAZ bir hedefe dönüşür ve 3 yıldız imkânsızlaşır.
        int total = 0;
        float multiplier = 1f;
        for (int i = 0; i < totalCorrectSwipes; i++)
        {
            total += Mathf.RoundToInt(basePointsPerCorrectSwipe * multiplier);
            multiplier = Mathf.Min(multiplier + multiplierStep, maxMultiplier);
        }

        total += completedCardCount * pointsPerCompletedCard;
        return total;
    }

    /// <summary>
    /// Tek bir deste girdisi için "kaç DOĞRU swipe gerekir" sorusunun cevabı.
    ///
    /// DİKKAT: BuildChain'in adım sayısı doğrudan kullanılamaz. ProductionChainUtility,
    /// resultCard'ı boş (çöp) olan outcome'ları ATLADIĞI için zincirin SONUNDAKİ kartı
    /// temizleyen son hamleyi hiç saymaz - oysa oyuncu o kartı da bir kez doğru istasyona
    /// atmak ZORUNDA. Bu +1 eklenmezse par eksik hesaplanır, skor/par oranı yapay olarak
    /// şişer ve 3 yıldız bedavaya gelir.
    /// </summary>
    private static int CountCorrectSwipesForEntry(CardData raw, out bool endsOnFinalProduct)
    {
        endsOnFinalProduct = false;
        if (raw == null) return 0;

        List<ProductionChainUtility.ChainStep> chain = ProductionChainUtility.BuildChain(raw);
        if (chain == null || chain.Count == 0) return 0;

        if (chain.Count >= 20)
        {
            Debug.LogWarning($"[ScoreManager] '{raw.name}' zinciri 20 adımda kesildi " +
                              "(ProductionChainUtility güvenlik sınırı) - döngüsel ya da hatalı " +
                              "yapılandırılmış bir zincir olabilir. Par skoru güvenilir değil.");
        }

        // Zinciri İLERLETEN hamle sayısı. StationToNext'in null olmasına BAKARAK saymak
        // yanlış olurdu: TryGetNextStep yalnızca resultCard'a bakıyor, outcome.station'ı
        // hiç kontrol etmiyor - yani istasyonu boş ama sonucu dolu bir outcome
        // "ilerliyor" ama StationToNext null geliyor.
        int swipes = chain.Count - 1;

        CardData last = chain[chain.Count - 1].Card;
        if (last == null) return swipes;

        if (last.isFinalProduct)
        {
            // Dönüşümle final ürüne varıldıysa kart O ANDA desteden düşer
            // (GameManager: _currentCard = null) - fazladan swipe YOK.
            // Ama chain.Count == 1 ise final ürün DOĞRUDAN desteye konmuş demektir:
            // hiç dönüşüm olmaz, OnCardCompleted ASLA tetiklenmez (yani tamamlama
            // bonusu da yok) ama oyuncu o kartı bir kez atmak zorundadır.
            endsOnFinalProduct = chain.Count >= 2;

            if (chain.Count == 1 && HasAnyOutcome(last)) swipes += 1;
        }
        else if (HasAnyOutcome(last))
        {
            // Zincirin sonu final değil -> tek çıkışı çöp outcome'u. Bir swipe daha gerekir.
            swipes += 1;
        }
        else
        {
            Debug.LogWarning($"[ScoreManager] '{last.name}' kartının hiç outcome'u yok - " +
                              "bu kart desteden asla düşmez, level bitirilemez. " +
                              "CardData asset'ini kontrol et.");
        }

        return swipes;
    }

    /// <summary>
    /// Kartın oynanabilir (tanımlı istasyonu olan) en az bir outcome'u var mı?
    /// StationOutcome bir CLASS olduğu için dizide null slot olabilir - bu yüzden
    /// tek tek kontrol ediliyor (aynı koruma ProductionChainUtility'de de var).
    /// </summary>
    private static bool HasAnyOutcome(CardData card)
    {
        if (card == null || card.outcomes == null) return false;

        foreach (StationOutcome outcome in card.outcomes)
        {
            if (outcome != null && outcome.station != null) return true;
        }

        return false;
    }

    // NOT: Çöpe yönlendirme (valid station + resultCard == null) bir istismar DEĞİL,
    // kendi kendini dengeliyor - "düzeltmeye" çalışma. Çöp swipe'ı OnValidSwipe
    // tetiklediği için puan verir, ama BuildChain çöp outcome'larını atlayıp UZUN yolu
    // varsaydığı için par yüksek kalır. Yani çöp-hızlı-koşu, daha yüksek bir par'a
    // karşı daha AZ puan toplar.
}
