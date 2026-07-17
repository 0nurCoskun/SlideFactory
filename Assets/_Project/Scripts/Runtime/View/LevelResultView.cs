using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

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

    [Header("Paneller (başlangıçta inactive olmalı)")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;

    [Header("Giriş Animasyonu")]
    [SerializeField] private float popDuration = 0.4f;
    [SerializeField] private Ease popEase = Ease.OutBack;

    [Header("Sahne İsimleri")]
    [Tooltip("Restart'a basınca mevcut sahne yeniden yüklenir - bu alanı doldurmana gerek yok.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Awake()
    {
        // Baştan emin ol - Inspector'da yanlışlıkla aktif bırakılmış olabilir.
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (gameManager != null)
        {
            gameManager.OnLevelWon += HandleLevelWon;
            gameManager.OnLevelFailed += HandleLevelFailed;
        }
    }

    private void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnLevelWon -= HandleLevelWon;
            gameManager.OnLevelFailed -= HandleLevelFailed;
        }
    }

    private void HandleLevelWon()
    {
        ShowPanel(winPanel);
    }

    private void HandleLevelFailed()
    {
        ShowPanel(losePanel);
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
}