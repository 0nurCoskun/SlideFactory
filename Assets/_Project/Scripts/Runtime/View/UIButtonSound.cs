using UnityEngine;

/// <summary>
/// Herhangi bir Button'a eklenip OnClick() event'ine bağlanacak, tekrar kullanılabilir
/// bir ses tetikleyicisi. Her butonda ayrı ayrı ses kodu yazmak yerine, bu script'i
/// her butona ekleyip Inspector'dan OnClick()'e PlayClickSound()'u bağlaman yeterli.
///
/// "Clip Override" boş bırakılırsa AudioManager'daki varsayılan buton sesi çalar.
/// Farklı bir buton (örn. "Restart" gibi özel bir ses istiyorsan) için buraya
/// özel bir klip atayabilirsin.
/// </summary>
public class UIButtonSound : MonoBehaviour
{
    [Header("Opsiyonel - Boş bırakılırsa AudioManager'daki varsayılan ses çalar")]
    [SerializeField] private AudioClip clipOverride;

    /// <summary>Butonun OnClick() listesine bağlanacak.</summary>
    public void PlayClickSound()
    {
        AudioManager.Instance?.PlayButtonClick(clipOverride);
    }
}
