using UnityEngine;

/// <summary>
/// Tüm ses çalma işlemlerinin tek merkezi. SFX ve müzik için ayrı AudioSource'lar tutar.
/// AppBootstrap gibi singleton + DontDestroyOnLoad - sahne değişse bile hayatta kalır,
/// bu yüzden Level Select'te başlayan müzik Game sahnesine geçince kesilmez (istersen).
///
/// Sahnede tek bir GameObject'e (örn. "_AudioManager") eklenir, SADECE ilk açılan sahnede.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Source'lar")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [Header("Ses Klipleri")]
    [Tooltip("Tüm butonlar için varsayılan tıklama sesi. Bir buton farklı bir ses istiyorsa UIButtonSound üzerinden override edilebilir.")]
    [SerializeField] private AudioClip defaultButtonClickClip;

    [Header("Varsayılan Ses Seviyeleri")]
    [Range(0f, 1f)][SerializeField] private float sfxVolume = 1f;
    [Range(0f, 1f)][SerializeField] private float musicVolume = 0.6f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (sfxSource != null) sfxSource.volume = sfxVolume;
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
            musicSource.loop = true;
        }
    }

    /// <summary>Tek seferlik bir ses efekti çalar (doğru/yanlış hamle, tamamlama vb.).</summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    /// <summary>
    /// Butonlar için tıklama sesi çalar. clipOverride verilmezse (null),
    /// varsayılan buton sesini (defaultButtonClickClip) çalar.
    /// </summary>
    public void PlayButtonClick(AudioClip clipOverride = null)
    {
        PlaySFX(clipOverride != null ? clipOverride : defaultButtonClickClip);
    }

    /// <summary>Arka plan müziğini değiştirir. Aynı klip zaten çalıyorsa yeniden başlatmaz.</summary>
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null) sfxSource.volume = sfxVolume;
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null) musicSource.volume = musicVolume;
    }
}