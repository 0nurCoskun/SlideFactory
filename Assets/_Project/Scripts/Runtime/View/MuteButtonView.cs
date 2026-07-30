using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Müzik açık/kapalı butonunu yönetir. Basınca AudioManager üzerinden müziği
/// mute/unmute eder ve butonun görselini (sprite) o duruma göre günceller.
/// AudioManager mute durumunu PlayerPrefs ile kalıcı tuttuğu için, oyun yeniden
/// açıldığında da buton doğru sprite ile (kapalıysa "off" ikonuyla) başlar.
/// </summary>
public class MuteButtonView : MonoBehaviour
{
    [Header("Görsel")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private Sprite mutedSprite;   // "kapalı" (off) ikonu
    [SerializeField] private Sprite unmutedSprite; // "açık" (on) ikonu

    private void Start()
    {
        // OnEnable yerine bilerek Start() kullanılıyor: AudioManager'ın Awake()'i
        // (PlayerPrefs'ten mute durumunu okuyan yer) sahne hiyerarşisindeki sıraya göre
        // bu OnEnable'dan SONRA çalışabiliyordu - bu durumda AudioManager.Instance hâlâ
        // null olduğu için sprite hiç güncellenmiyordu. Start() sahnedeki TÜM Awake()'ler
        // bittikten sonra çalıştığı için bu sıralama sorunu ortadan kalkıyor.
        RefreshSprite();
    }

    private void OnEnable()
    {
        RefreshSprite();
    }

    /// <summary>Mute butonunun OnClick()'ine bağlanacak.</summary>
    public void OnMuteButtonPressed()
    {
        AudioManager.Instance?.ToggleMusicMute();
        RefreshSprite();
    }

    private void RefreshSprite()
    {
        if (buttonImage == null || AudioManager.Instance == null) return;

        buttonImage.sprite = AudioManager.Instance.IsMusicMuted ? mutedSprite : unmutedSprite;
    }
}
