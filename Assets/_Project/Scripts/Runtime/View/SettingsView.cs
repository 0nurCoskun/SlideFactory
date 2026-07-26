using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ayarlar panelindeki müzik/SFX ses slider'larını AudioManager'a bağlar.
/// Panel her açıldığında slider'lar, AudioManager'daki GÜNCEL (kayıtlı) değerlerle
/// senkronize edilir - böylece oyuncu daha önce ne ayarladıysa onu görür.
/// </summary>
public class SettingsView : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Slider'lar")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [Header("Text'ler")]
    [SerializeField] private TMP_Text musicVolumeText;
    [SerializeField] private TMP_Text sfxVolumeText;

    private void Awake()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    /// <summary>Ayarlar butonuna bağlanacak.</summary>
    public void OnSettingsButtonPressed()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
        SyncSlidersWithCurrentSettings();
    }

    /// <summary>Ayarlar panelindeki Kapat butonuna bağlanacak.</summary>
    public void OnCloseButtonPressed()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    /// <summary>Music Volume slider'ının OnValueChanged()'ine bağlanacak.</summary>
    public void OnMusicSliderChanged(float value)
    {
        AudioManager.Instance?.SetMusicVolume(value);
        if (musicVolumeText != null) musicVolumeText.text = Mathf.RoundToInt(AudioManager.Instance.MusicVolume * 100).ToString() + "%";
    }

    /// <summary>SFX Volume slider'ının OnValueChanged()'ine bağlanacak.</summary>
    public void OnSfxSliderChanged(float value)
    {
        AudioManager.Instance?.SetSfxVolume(value);
        if (sfxVolumeText != null) sfxVolumeText.text = Mathf.RoundToInt(AudioManager.Instance.SfxVolume * 100).ToString() + "%";
    }

    private void SyncSlidersWithCurrentSettings()
    {
        if (AudioManager.Instance == null) return;

        // SetValueWithoutNotify kullanıyoruz - yoksa slider'ın değerini elle
        // ayarlamak OnValueChanged'i tetikler ve AudioManager'a gereksiz bir
        // "değişiklik" bildirimi gider (zararsız ama gereksiz bir PlayerPrefs.Save() olur).
        if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.MusicVolume);
        if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.SfxVolume);
    }
}
