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
        AdManager.Instance?.NotifyLevelEnded();
    }

    private void HandleLevelFailed()
    {
        AdManager.Instance?.NotifyLevelEnded();
    }
}
