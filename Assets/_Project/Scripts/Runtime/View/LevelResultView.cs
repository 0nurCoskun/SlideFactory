using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using TMPro;

/// <summary>
/// GameManager'ın OnLevelWon / OnLevelFailed event'lerini dinler, ilgili paneli
/// açar ve Restart/Ana Menü butonlarının davranışını yönetir.
///
/// Sahnede tek bir GameObject'e eklenir (örn. "_LevelResultView"), iki panele
/// referans verir. Paneller varsayılan olarak KAPALI (inactive) durmalı.
/// </summary>
public class LevelResultView : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private GameManager gameManager;
    [Tooltip("Kazanılan puanı ve rekoru göstermek için. Boş bırakılırsa puan alanları " +
             "sessizce boş kalır, panel yine normal çalışır.")]
    [SerializeField] private ScoreManager scoreManager;

    [Header("Paneller (başlangıçta inactive olmalı)")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;

    [Tooltip("Oyun ekranındaki kalıcı Pause butonu. WinPanel/LosePanel bunun bir PARÇASI " +
             "DEĞİL (aynı Canvas'ta kardeş obje) - o yüzden panel açılışları bu butonu " +
             "otomatik kapatmıyor, burada elle yönetiyoruz. Level bittiyse oyuncunun pause " +
             "menüsünü açmasının hiçbir anlamı yok (GameManager zaten IsLevelEnded'i true " +
             "yapmış oluyor ama buton yine de tıklanabilir kalırdı).")]
    [SerializeField] private GameObject pauseButton;

    [Header("Next Level Butonu (WinPanel içinde)")]
    [Tooltip("Son level'da otomatik gizlenir (LevelData.nextLevel boşsa).")]
    [SerializeField] private GameObject nextLevelButton;

    [Header("Skor Tablosu Butonu (WinPanel içinde)")]
    [Tooltip("Play Games'in native skor tablosu ekranını açar. LevelData.leaderboardId " +
             "boşsa (tutorial gibi) otomatik gizlenir - skor zaten sadece kazanılınca " +
             "gönderildiği için bu buton yalnızca WinPanel'de anlamlı.")]
    [SerializeField] private GameObject leaderboardButton;

    [Header("Yıldız Görselleri (WinPanel içinde, soldan sağa 1-2-3 sırasıyla)")]
    [SerializeField] private UnityEngine.UI.Image[] starImages;
    [SerializeField] private Sprite filledStarSprite;
    [SerializeField] private Sprite emptyStarSprite;

    [Header("Giriş Animasyonu")]
    [SerializeField] private float popDuration = 0.4f;
    [SerializeField] private Ease popEase = Ease.OutBack;

    [Header("Yıldız Giriş Animasyonu (WinPanel açıldıktan sonra)")]
    [Tooltip("Panel'in kendi pop animasyonu bitince, yıldızlar patlamaya başlamadan önceki ek bekleme.")]
    [SerializeField] private float starStartDelay = 0.15f;
    [Tooltip("Her bir yıldızın kendi pop (0 -> 1 scale) süresi.")]
    [SerializeField] private float starPopDuration = 0.35f;
    [Tooltip("Yıldızlar arasındaki gecikme - art arda, tek tek patlasınlar diye.")]
    [SerializeField] private float starStagger = 0.15f;
    [SerializeField] private Ease starPopEase = Ease.OutBack;
    [Tooltip("Sadece KAZANILAN yıldızlar patladığında çalınır (boş yıldızlar sessiz belirir).")]
    [SerializeField] private AudioClip starPopSound;

    [Header("Skor (WinPanel)")]
    [SerializeField] private TMP_Text winScoreText;
    [Tooltip("Bu level'daki en yüksek puan. Zorunlu değil.")]
    [SerializeField] private TMP_Text winBestScoreText;
    [Tooltip("Sadece GERÇEKTEN yeni rekor kırıldığında, sayım animasyonu BİTTİKTEN sonra belirir. " +
             "Sahnede inactive bırakılabilir - Awake zaten kapatıyor.")]
    [SerializeField] private GameObject newRecordBadge;

    [Header("Skor (LosePanel)")]
    [Tooltip("Kaybedilen denemede de toplanan puan gösterilir (koşu boşa gitmiş gibi " +
             "hissettirmemek için) ama ASLA kaydedilmez.")]
    [SerializeField] private TMP_Text loseScoreText;
    [SerializeField] private TMP_Text loseBestScoreText;

    [Header("Reklamla Devam Et (LosePanel)")]
    [Tooltip("Rewarded reklam izlenince GameManager.ReviveWithExtraTime'a verilecek ekstra süre (saniye).")]
    [SerializeField] private float continueExtraSeconds = 15f;
    [Tooltip("LosePanel içindeki 'İzle ve Devam Et' butonu. Reklam hazır değilken, " +
             "reklamlar kaldırılmışken ya da bu denemede zaten kullanılmışken OTOMATİK gizlenir - " +
             "Inspector'da aktif bırakılabilir.")]
    [SerializeField] private GameObject continueWithAdButton;

    // Bir DENEME (= bir sahne ömrü, Restart/Next Level sahneyi baştan yüklüyor) başına
    // en fazla BİR reklamlı devam hakkı - yoksa oyuncu süresiz reklam izleyip level'ı
    // asla kaybetmezdi. SADECE Awake()'te sıfırlanır - HandleLevelFailed'da SIFIRLAMA,
    // ReviveWithExtraTime sonrası gelecek İKİNCİ bir süre bitişi bu bayrağı tekrar açıp
    // hakkı sınırsız tekrarlatır (bkz. Awake() içindeki not). Sahne başına statik
    // DEĞİL - ScoreManager'daki NOT ile aynı gerekçe.
    private bool _hasUsedContinueThisAttempt;

    [Header("Skor Sayım Animasyonu")]
    [Tooltip("Puanın 0'dan nihai değere akma süresi. Yıldızlar bittikten SONRA başlar.")]
    [SerializeField] private float scoreCountUpDuration = 0.9f;
    [SerializeField] private Ease scoreCountUpEase = Ease.OutCubic;
    [SerializeField] private AudioClip scoreCountUpSound;
    [SerializeField] private AudioClip newRecordSound;

    [Header("Sahne İsimleri")]
    [Tooltip("Restart'a basınca mevcut sahne yeniden yüklenir - bu alanı doldurmana gerek yok.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // Yıldız + skor sayımı AYNI zaman çizgisinde ilerlediği için tek bir sequence.
    // Sahne yeniden yüklenirken (Restart) öldürülmek zorunda - yoksa DOTween yok olmuş
    // transform'lar için "target is missing" uyarısı basıyor.
    private Sequence _resultSequence;

    private void Awake()
    {
        // Baştan emin ol - Inspector'da yanlışlıkla aktif bırakılmış olabilir.
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);

        // Rozet, sayım bitmeden görünmemeli - yoksa rekor kırılmadan "YENİ REKOR" yazar.
        if (newRecordBadge != null) newRecordBadge.SetActive(false);

        // BİR KEZ, sahne yüklendiğinde: "bir deneme" burada bir SAHNE ÖMRÜ demek
        // (Restart/Next Level sahneyi baştan yüklüyor). HandleLevelFailed İÇİNDE
        // sıfırlanırsa, ReviveWithExtraTime sonrası ikinci bir süre bitişi bu bayrağı
        // GERİ AÇAR ve oyuncu sınırsız reklam izleyip level'ı asla kaybetmez - tam da
        // _hasUsedContinueThisAttempt'in var oluş sebebini bozar.
        _hasUsedContinueThisAttempt = false;
    }

    private void OnEnable()
    {
        if (gameManager != null)
        {
            gameManager.OnLevelWon += HandleLevelWon;
            gameManager.OnLevelFailed += HandleLevelFailed;
        }

        // AdManager sahneler arası DontDestroyOnLoad singleton - MainMenu'den geçmeden
        // doğrudan Game sahnesi Editor'de açılırsa Instance null olabilir, o yüzden hepsi ?.
        if (AdManager.Instance != null)
            AdManager.Instance.OnRewardedAdReadyChanged += HandleRewardedAdReadyChanged;
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnLevelWon -= HandleLevelWon;
            gameManager.OnLevelFailed -= HandleLevelFailed;
        }

        if (AdManager.Instance != null)
            AdManager.Instance.OnRewardedAdReadyChanged -= HandleRewardedAdReadyChanged;

        _resultSequence?.Kill();
    }

    private void HandleLevelWon(int stars)
    {
        // Tutorial level'da Win paneli gösterilmez - TutorialFlowView kendi tamamlanma akışını yönetir.
        if (gameManager != null && gameManager.ActiveLevel != null && gameManager.ActiveLevel.isTutorial) return;

        if (pauseButton != null) pauseButton.SetActive(false);

        ShowPanel(winPanel);
        PlayWinTimeline(stars);

        bool hasNextLevel = gameManager != null && gameManager.ActiveLevel != null && gameManager.ActiveLevel.nextLevel != null;
        if (nextLevelButton != null) nextLevelButton.SetActive(hasNextLevel);

        bool hasLeaderboard = gameManager != null && gameManager.ActiveLevel != null
            && !string.IsNullOrEmpty(gameManager.ActiveLevel.leaderboardId);
        if (leaderboardButton != null) leaderboardButton.SetActive(hasLeaderboard);
    }

    /// <summary>
    /// Win panelinin tüm giriş koreografisi TEK bir zaman çizgisinde:
    /// panel pop -> yıldızlar tek tek patlar -> puan 0'dan sayılır -> (varsa) rekor rozeti.
    /// Hepsinin aynı sequence'te olması ZORUNLU, yoksa iki bağımsız animasyon
    /// birbiriyle yarışır ve sıra her seferinde farklı görünür.
    ///
    /// Yıldız sprite'ları (dolu/boş) hemen atanır ama scale 0'da gizli tutulur.
    /// </summary>
    private void PlayWinTimeline(int stars)
    {
        // Skor değerleri BURADA yerel değişkene alınıyor: GameManager.EndLevel,
        // OnLevelWon'dan ÖNCE FinalizeScore'u çağırdığı için bu değerler artık NİHAİ.
        // Ayrıca closure'ların içinden scoreManager'ı tekrar okumak, sahne yıkımı
        // sırasında null'a düşme riski taşır.
        int finalScore = scoreManager != null ? scoreManager.TotalScore : 0;
        int previousBest = scoreManager != null ? scoreManager.PreviousBestScore : 0;
        bool isNewRecord = scoreManager != null && scoreManager.IsNewBestScore;

        // Rekor zaten kaydedilmiş olabilir; gösterilecek değer ikisinin büyüğü.
        int bestToShow = Mathf.Max(previousBest, finalScore);

        // Sayım 0'dan başlayacak - panel açılır açılmaz bir önceki koşunun sayısı
        // tek kare için görünmesin diye metni HEMEN sıfırla.
        if (winScoreText != null)
            winScoreText.text = GameLocalization.GetUIString("ui_score_value", "0");

        if (winBestScoreText != null)
            winBestScoreText.text = GameLocalization.GetUIString("ui_best_score", bestToShow.ToString("N0"));

        _resultSequence?.Kill();
        _resultSequence = DOTween.Sequence();

        // --- Yıldızlar: mevcut mutlak zamanlı Insert şeması korunuyor ---
        float starsEndTime = popDuration + starStartDelay;

        if (starImages != null)
        {
            for (int i = 0; i < starImages.Length; i++)
            {
                UnityEngine.UI.Image starImage = starImages[i];
                if (starImage == null) continue;

                bool earned = i < stars;
                starImage.sprite = earned ? filledStarSprite : emptyStarSprite;

                Transform starTransform = starImage.transform;
                starTransform.DOKill();
                starTransform.localScale = Vector3.zero;

                float startTime = popDuration + starStartDelay + i * starStagger;
                _resultSequence.Insert(startTime, starTransform.DOScale(Vector3.one, starPopDuration).SetEase(starPopEase));

                if (earned)
                {
                    _resultSequence.InsertCallback(startTime, () => AudioManager.Instance?.PlaySFX(starPopSound));
                }

                // Kademelemenin GERÇEK bitişini takip et - sabit bir değer yazmak,
                // yıldız sayısı/süreleri Inspector'dan değişince yanlış olurdu.
                starsEndTime = Mathf.Max(starsEndTime, startTime + starPopDuration);
            }
        }

        // --- Puan sayımı: yıldızlar bittikten SONRA, aynı zaman çizgisinde ---
        if (winScoreText != null)
        {
            _resultSequence.InsertCallback(starsEndTime, () => AudioManager.Instance?.PlaySFX(scoreCountUpSound));
            _resultSequence.Insert(starsEndTime,
                DOVirtual.Int(0, finalScore, scoreCountUpDuration,
                        value => winScoreText.text = GameLocalization.GetUIString("ui_score_value", value.ToString("N0")))
                    .SetEase(scoreCountUpEase));
        }

        // --- Rekor rozeti: sayım bittikten sonra ---
        if (isNewRecord && newRecordBadge != null)
        {
            _resultSequence.InsertCallback(starsEndTime + scoreCountUpDuration, () =>
            {
                newRecordBadge.SetActive(true);
                newRecordBadge.transform.localScale = Vector3.zero;
                newRecordBadge.transform.DOScale(Vector3.one, popDuration).SetEase(popEase);
                AudioManager.Instance?.PlaySFX(newRecordSound);
            });
        }
    }

    private void HandleLevelFailed()
    {
        // Tutorial'da bu event zaten tetiklenmemeli (GameManager engelliyor) ama ekstra güvenlik katmanı.
        if (gameManager != null && gameManager.ActiveLevel != null && gameManager.ActiveLevel.isTutorial) return;

        if (pauseButton != null) pauseButton.SetActive(false);

        ShowPanel(losePanel);

        // Kayıpta puan GÖSTERİLİR ama ASLA kaydedilmez (bkz. ScoreProgress ve
        // GameManager.EndLevel'daki persist parametresi). PreviousBestScore,
        // FinalizeScore(persist:false) tarafından yine dolduruluyor - bu yüzden
        // mevcut rekoru hiçbir şey yazmadan gösterebiliyoruz.
        int score = scoreManager != null ? scoreManager.TotalScore : 0;
        int best = scoreManager != null ? scoreManager.PreviousBestScore : 0;

        if (loseScoreText != null)
            loseScoreText.text = GameLocalization.GetUIString("ui_score_value", score.ToString("N0"));

        if (loseBestScoreText != null)
            loseBestScoreText.text = GameLocalization.GetUIString("ui_best_score", best.ToString("N0"));

        UpdateContinueButtonVisibility();
    }

    private void HandleRewardedAdReadyChanged(bool ready)
    {
        // LosePanel açık DEĞİLKEN de tetiklenebilir (reklam arka planda yüklenir) -
        // UpdateContinueButtonVisibility zararsız, panel kapalıyken buton zaten görünmez.
        UpdateContinueButtonVisibility();
    }

    /// <summary>
    /// Butonu sadece GERÇEKTEN bir devam hakkı sunulabilecekken gösterir: bu denemede
    /// henüz kullanılmamış, reklam SDK'sı hazır ve "Reklamları Kaldır" satın alınmamış.
    /// Reklamları kaldıran oyuncu için reklamla devam mantıksız olurdu - onlara bir
    /// devam yolu sunmak istenirse bu AYRI bir mekanik (ör. jeton) olmalı, şimdilik yok.
    /// </summary>
    private void UpdateContinueButtonVisibility()
    {
        if (continueWithAdButton == null) return;

        bool canContinue = !_hasUsedContinueThisAttempt
            && AdManager.Instance != null
            && AdManager.Instance.IsRewardedAdReady
            && !AdManager.Instance.AdsRemoved;

        continueWithAdButton.SetActive(canContinue);
    }

    /// <summary>LosePanel içindeki "İzle ve Devam Et" butonuna bağlanacak.</summary>
    public void OnContinueWithAdButtonPressed()
    {
        if (_hasUsedContinueThisAttempt || gameManager == null || AdManager.Instance == null) return;

        // Reklam gösterilirken çift tıklamayı engelle - kapanınca sonucuna göre
        // ya tamamen gizli kalır (devam hakkı kullanıldı) ya da geri gelir (izlenmedi).
        if (continueWithAdButton != null) continueWithAdButton.SetActive(false);

        AdManager.Instance.ShowRewardedAd(rewardGranted =>
        {
            if (!rewardGranted)
            {
                UpdateContinueButtonVisibility();
                return;
            }

            _hasUsedContinueThisAttempt = true;

            if (losePanel != null)
            {
                losePanel.transform.DOKill();
                losePanel.SetActive(false);
            }

            // Oyun gerçekten devam ediyor artık - Pause butonu HandleLevelFailed'da
            // kapatılmıştı, geri açılmazsa oyuncu canlanan level'da hiç duraklatamaz.
            if (pauseButton != null) pauseButton.SetActive(true);

            gameManager.ReviveWithExtraTime(continueExtraSeconds);
        });
    }

    private void ShowPanel(GameObject panel)
    {
        if (panel == null) return;

        panel.SetActive(true);
        panel.transform.localScale = Vector3.zero;
        panel.transform.DOScale(Vector3.one, popDuration).SetEase(popEase);
    }

    /// <summary>Restart butonuna bağlanacak.</summary>
    public void OnRestartButtonPressed()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    /// <summary>Ana Menü butonuna bağlanacak (istersen kullan, zorunlu değil).</summary>
    public void OnMainMenuButtonPressed()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>WinPanel içindeki Next Level butonuna bağlanacak.</summary>
    public void OnNextLevelButtonPressed()
    {
        LevelData next = gameManager?.ActiveLevel?.nextLevel;
        if (next == null) return; // güvenlik - buton zaten gizli olmalıydı

        LevelSession.SelectedLevel = next;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>WinPanel içindeki Skor Tablosu butonuna bağlanacak.</summary>
    public void OnLeaderboardButtonPressed()
    {
        LeaderboardManager.Instance?.ShowLeaderboardUI(gameManager?.ActiveLevel);
    }
}