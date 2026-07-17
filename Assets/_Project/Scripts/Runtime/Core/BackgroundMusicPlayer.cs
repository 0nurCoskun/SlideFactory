using UnityEngine;

/// <summary>
/// Bulunduğu sahne açıldığında belirlenen müziği çalmaya başlar. Her sahneye
/// (MainMenu, Game) bu script'ten birer tane ekleyip farklı klip atayarak,
/// sahneye göre farklı arka plan müziği çalmasını sağlarsın.
///
/// AudioManager zaten aynı klip çalıyorsa yeniden başlatmadığı için (bkz.
/// AudioManager.PlayMusic), Game sahnesi içinde Restart yapılsa bile müzik
/// gereksiz yere kesilip başa sarmaz.
/// </summary>
public class BackgroundMusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip musicClip;

    private void Start()
    {
        AudioManager.Instance?.PlayMusic(musicClip);
    }
}
