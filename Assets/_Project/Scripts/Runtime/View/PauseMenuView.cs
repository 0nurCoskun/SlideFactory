using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Oyun sırasında Pause butonuna basılınca açılan menüyü yönetir.
/// Resume, Level Select ve Main Menu butonlarının davranışını içerir.
/// Level'ın kendisini duraklatmak için GameManager.PauseLevel()/ResumeLevel()
/// metodlarını kullanır (Recipe Preview'in kullandığı aynı altyapı).
/// </summary>
public class PauseMenuView : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private GameManager gameManager;

    [Header("Panel")]
    [SerializeField] private GameObject pausePanel;

    [Header("Sahne")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Awake()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    /// <summary>Oyun ekranındaki Pause butonuna bağlanacak.</summary>
    public void OnPauseButtonPressed()
    {
        if (gameManager == null || !gameManager.HasBegun) return; // level henüz başlamadıysa (Recipe Preview açıksa) pause anlamsız

        if (pausePanel != null) pausePanel.SetActive(true);
        gameManager.PauseLevel();
    }

    /// <summary>Pause menüsündeki Resume butonuna bağlanacak.</summary>
    public void OnResumeButtonPressed()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        gameManager?.ResumeLevel();
    }

    /// <summary>Pause menüsündeki Level Select butonuna bağlanacak.</summary>
    public void OnLevelSelectButtonPressed()
    {
        LevelSession.OpenLevelSelectDirectly = true;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>Pause menüsündeki Main Menu butonuna bağlanacak.</summary>
    public void OnMainMenuButtonPressed()
    {
        LevelSession.OpenLevelSelectDirectly = false;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
