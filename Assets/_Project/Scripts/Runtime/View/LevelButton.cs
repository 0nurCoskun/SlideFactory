using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Level Select ekranındaki HER BUTONA ayrı ayrı eklenir. Her buton kendi
/// LevelData'sını taşır, tıklanınca LevelSession'a yazıp Game sahnesini açar.
/// </summary>
public class LevelButton : MonoBehaviour
{
    [Header("Bu Butonun Temsil Ettiği Level")]
    [SerializeField] private LevelData levelData;

    [Header("Sahne")]
    [SerializeField] private string gameSceneName = "Game";

    /// <summary>Button'un OnClick()'ine bağlanacak.</summary>
    public void OnLevelButtonPressed()
    {
        if (levelData == null)
        {
            Debug.LogError($"[LevelButton] '{gameObject.name}' objesine bir LevelData atanmamış.");
            return;
        }

        LevelSession.SelectedLevel = levelData;
        SceneManager.LoadScene(gameSceneName);
    }
}
