using UnityEngine;

/// <summary>
/// GameManager'ın level bitiş event'lerini dinleyip AdManager üzerinden interstitial
/// gösterimini tetikler. GameManager'ın kendisi reklamların VARLIĞINI bile bilmez -
/// AudioTriggerView'in ses çalma için event dinlemesiyle birebir aynı mantık
/// (Dependency Inversion, bkz. CLAUDE.md "GameManager never touches visuals directly").
///
/// Sahnede GameManager'ın bulunduğu objeye (veya AudioTriggerView ile aynı objeye) eklenir.
/// Tutorial level'larda da bu event'ler ateşlenir - AdManager kendi tarafında tutorial'a
/// özel bir istisna YAPMIYOR henüz; tutorial'da interstitial istenmiyorsa bu kontrol
/// buraya (gameManager.ActiveLevel.isTutorial) eklenmeli.
/// </summary>
public class AdTriggerView : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private GameManager gameManager;

    // GameManager.ReviveWithExtraTime bir kaybı geri aldığında AYNI deneme ikinci kez
    // OnLevelWon/OnLevelFailed fırlatabilir (kayıp -> reklamla devam -> kazanma/tekrar
    // kayıp). Bu bayrak olmazsa NotifyLevelEnded() tek bir level denemesi için İKİ KEZ
    // çağrılır ve interstitial sıklık sayacı (AdManager.levelsBetweenInterstitials)
    // olması gerekenin iki katı hızda dolar. Sahne başına bir kez sıfırlanır (bu obje
    // her Restart/Next Level'da yeniden yaratılıyor) - LevelResultView'daki
    // _hasUsedContinueThisAttempt ile birebir aynı "bir deneme = bir sahne ömrü" gerekçesi.
    private bool _hasNotifiedThisAttempt;

    private void OnEnable()
    {
        if (gameManager == null) return;

        gameManager.OnLevelWon += HandleLevelEnded;
        gameManager.OnLevelFailed += HandleLevelFailed;
    }

    private void OnDisable()
    {
        if (gameManager == null) return;

        gameManager.OnLevelWon -= HandleLevelEnded;
        gameManager.OnLevelFailed -= HandleLevelFailed;
    }

    private void HandleLevelEnded(int stars)
    {
        NotifyOnce();
    }

    private void HandleLevelFailed()
    {
        NotifyOnce();
    }

    private void NotifyOnce()
    {
        if (_hasNotifiedThisAttempt) return;
        _hasNotifiedThisAttempt = true;

        AdManager.Instance?.NotifyLevelEnded();
    }
}
