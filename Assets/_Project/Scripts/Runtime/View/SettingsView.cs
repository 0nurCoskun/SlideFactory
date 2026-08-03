using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ayarlar panelindeki müzik/SFX ses slider'larını AudioManager'a bağlar.
/// Panelin AÇILIP KAPANMASI artık MainMenuController tarafından (geçiş animasyonuyla
/// birlikte) yönetiliyor - bu script sadece slider senkronizasyonundan sorumlu.
/// Panel her açıldığında MainMenuController, RefreshSliders()'ı çağırarak slider'ları
/// AudioManager'daki GÜNCEL (kayıtlı) değerlerle senkronize eder.
/// </summary>
public class SettingsView : MonoBehaviour
{
    [Header("Slider'lar")]
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [Header("Text'ler")]
    [SerializeField] private TMP_Text musicVolumeText;
    [SerializeField] private TMP_Text sfxVolumeText;

    [Header("Dil Seçimi")]
    [Tooltip("Seçili dilde vurgulanacak (ör. rengi/ölçeği değişecek) İngilizce butonu üzerindeki işaret objesi.")]
    [SerializeField] private GameObject englishSelectedMark;
    [Tooltip("Seçili dilde vurgulanacak Türkçe butonu üzerindeki işaret objesi.")]
    [SerializeField] private GameObject turkishSelectedMark;

    /// <summary>MainMenuController, Settings paneli açılırken bunu çağırır.</summary>
    public void RefreshSliders()
    {
        SyncSlidersWithCurrentSettings();
        RefreshLanguageSelector();
    }

    /// <summary>Dil satırındaki "English" butonunun OnClick()'ine bağlanacak.</summary>
    public void OnEnglishButtonPressed()
    {
        LocalizationManager.Instance?.SetLanguage(LocalizationManager.Language.English);
        RefreshLanguageSelector();
    }

    /// <summary>Dil satırındaki "Türkçe" butonunun OnClick()'ine bağlanacak.</summary>
    public void OnTurkishButtonPressed()
    {
        LocalizationManager.Instance?.SetLanguage(LocalizationManager.Language.Turkish);
        RefreshLanguageSelector();
    }

    private void RefreshLanguageSelector()
    {
        if (LocalizationManager.Instance == null) return;

        bool isTurkish = LocalizationManager.Instance.CurrentLanguage == LocalizationManager.Language.Turkish;

        if (englishSelectedMark != null) englishSelectedMark.SetActive(!isTurkish);
        if (turkishSelectedMark != null) turkishSelectedMark.SetActive(isTurkish);
    }

    /// <summary>Music Volume slider'ının OnValueChanged()'ine bağlanacak.</summary>
    public void OnMusicSliderChanged(float value)
    {
        AudioManager.Instance?.SetMusicVolume(value);
        UpdateMusicText();
    }

    /// <summary>SFX Volume slider'ının OnValueChanged()'ine bağlanacak.</summary>
    public void OnSfxSliderChanged(float value)
    {
        AudioManager.Instance?.SetSfxVolume(value);
        UpdateSfxText();
    }

    private void SyncSlidersWithCurrentSettings()
    {
        if (AudioManager.Instance == null) return;

        // SetValueWithoutNotify kullanıyoruz - yoksa slider'ın değerini elle
        // ayarlamak OnValueChanged'i tetikler ve AudioManager'a gereksiz bir
        // "değişiklik" bildirimi gider (zararsız ama gereksiz bir PlayerPrefs.Save() olur).
        // Bu yüzden yüzde text'lerini de burada AYRICA güncellememiz gerekiyor -
        // OnValueChanged tetiklenmediği için text'ler kendiliğinden güncellenmiyordu.
        if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.MusicVolume);
        if (sfxVolumeSlider != null) sfxVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.SfxVolume);

        UpdateMusicText();
        UpdateSfxText();
    }

    private void UpdateMusicText()
    {
        if (musicVolumeText == null || AudioManager.Instance == null) return;
        musicVolumeText.text = Mathf.RoundToInt(AudioManager.Instance.MusicVolume * 100) + "%";
    }

    private void UpdateSfxText()
    {
        if (sfxVolumeText == null || AudioManager.Instance == null) return;
        sfxVolumeText.text = Mathf.RoundToInt(AudioManager.Instance.SfxVolume * 100) + "%";
    }
}
