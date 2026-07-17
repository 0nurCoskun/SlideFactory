using UnityEngine;

/// <summary>
/// Ana menüdeki panel geçişlerini (MainMenuPanel <-> LevelSelectPanel) ve
/// Quit butonunu yönetir. Artık doğrudan sahne değiştirmiyor - sadece
/// hangi panelin göründüğünü kontrol ediyor. Gerçek sahne geçişi (Game'e gitmek)
/// LevelButton.cs üzerinden, oyuncu bir level seçtiğinde olur.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Paneller")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject levelSelectPanel;

    private void Awake()
    {
        ShowMainMenuPanel();
    }

    /// <summary>Play butonuna bağlanacak - Level Select ekranını açar.</summary>
    public void OnPlayButtonPressed()
    {
        ShowLevelSelectPanel();
    }

    /// <summary>Level Select ekranındaki "Geri" butonuna bağlanacak (varsa).</summary>
    public void OnBackButtonPressed()
    {
        ShowMainMenuPanel();
    }

    /// <summary>Quit butonuna bağlanacak. Editor'de test ederken Play modundan çıkar.</summary>
    public void OnQuitButtonPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowMainMenuPanel()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
    }

    private void ShowLevelSelectPanel()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(true);
    }
}