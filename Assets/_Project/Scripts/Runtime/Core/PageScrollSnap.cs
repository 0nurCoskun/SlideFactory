using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// Yatay bir ScrollRect'i "sayfa sayfa" kaydırmaya zorlar. Standart ScrollRect'in
/// Elastic hareketi, bizim istediğimiz hedef pozisyonla ÇEKİŞİP kazanabiliyor -
/// bu yüzden bırakma anında ScrollRect'in kendi velocity/inertia'sını SIFIRLAYIP
/// hedefe DOTween ile biz taşıyoruz.
///
/// KURULUM: Bu script'i ScrollRect'in KENDİSİNİN olduğu objeye ekle (Content'e değil).
/// Content'in altında her sayfa aynı genişlikte (Viewport genişliği kadar), yan yana
/// bir Horizontal Layout Group ile dizili olmalı, Spacing: 0.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class PageScrollSnap : MonoBehaviour, IEndDragHandler
{
    [Header("Referanslar (boş bırakılırsa ScrollRect'ten otomatik alınır)")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;

    [Header("Snap Ayarları")]
    [SerializeField] private float snapDuration = 0.3f;
    [Tooltip("Bu hızın üzerinde bir 'flick' yapılırsa, tam ortalanmamış olsa bile bir sonraki/önceki sayfaya geçilir.")]
    [SerializeField] private float flickVelocityThreshold = 300f;

    /// <summary>Sayfa değiştiğinde tetiklenir - PageIndicatorManager bunu dinleyip dot'u güncelleyebilir.</summary>
    public event Action<int> OnPageChanged;

    public int CurrentPage { get; private set; }
    public int PageCount { get; private set; }

    private ScrollRect _scrollRect;
    private bool _initialized;

    private void Awake() => EnsureInitialized();

    /// <summary>
    /// Referansları kurar. Awake yerine HER public giriş noktasından çağrılıyor çünkü
    /// bu script'in bulunduğu panel (LevelSelectPanel) sahne açılışında MainMenuController
    /// tarafından SetActive(false) yapılıyor: Unity, Awake'i henüz çalışmamış bir objeyi
    /// deaktive ederse Awake'i TEKRAR AKTİF EDİLENE KADAR HİÇ çalıştırmaz. O aralıkta
    /// dışarıdan RefreshPages/GoToPage çağrılırsa _scrollRect null olurdu.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        _scrollRect = GetComponent<ScrollRect>();
        if (viewport == null) viewport = _scrollRect.viewport;
        if (content == null) content = _scrollRect.content;

        RefreshPages();
    }

    /// <summary>
    /// Sayfa sayısını Content'in çocuk sayısından YENİDEN okur. Sayfalar artık runtime'da
    /// (LevelSelectView tarafından) üretildiği için Awake anındaki sayı geçerli değil -
    /// sayfalar oluşturulduktan sonra bu çağrılmalı.
    /// </summary>
    public void RefreshPages()
    {
        _initialized = true;
        if (_scrollRect == null) _scrollRect = GetComponent<ScrollRect>();
        if (viewport == null) viewport = _scrollRect.viewport;
        if (content == null) content = _scrollRect.content;

        PageCount = content.childCount;
        CurrentPage = ClampPage(CurrentPage);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        EnsureInitialized();
        if (PageCount == 0) return;

        float pageWidth = viewport.rect.width;
        float currentX = -content.anchoredPosition.x;

        int nearestPage = Mathf.RoundToInt(currentX / pageWidth);

        // Hızlı bir flick yapıldıysa, tam ortalanmamış olsa bile yönüne göre
        // bir sonraki/önceki sayfaya geçmeye zorla (daha doğal bir his verir).
        if (Mathf.Abs(_scrollRect.velocity.x) > flickVelocityThreshold)
        {
            nearestPage += _scrollRect.velocity.x < 0 ? 1 : -1;
        }

        // Tek bir sürüklemede en fazla BİR sayfa ilerlenebilir. 200 level'da content
        // 20 sayfa uzunluğunda; sert bir flick'te aradaki sayfalar hiç görünmeden
        // atlanabilir ve tembel yüklenen (henüz butonları basılmamış) bir sayfaya
        // düşülebilirdi.
        nearestPage = Mathf.Clamp(nearestPage, CurrentPage - 1, CurrentPage + 1);

        GoToPage(nearestPage);
    }

    /// <summary>Belirli bir sayfaya git (dot'a tıklanınca da çağırabilirsin).</summary>
    public void GoToPage(int pageIndex)
    {
        EnsureInitialized();
        if (PageCount == 0) return;

        CurrentPage = ClampPage(pageIndex);

        // KRİTİK SATIR: ScrollRect'in kendi Elastic/Inertia hareketini durduruyoruz,
        // yoksa bizim DOTween ile verdiğimiz hedefle çekişip kazanıyor ve content
        // Page 1'e geri fırlıyor.
        _scrollRect.StopMovement();

        content.DOKill();
        content.DOAnchorPosX(TargetX(CurrentPage), snapDuration).SetEase(Ease.OutCubic);

        OnPageChanged?.Invoke(CurrentPage);
    }

    /// <summary>
    /// Animasyonsuz, ANINDA bir sayfaya konumlan. Level Select paneli açılırken
    /// oyuncunun kaldığı sayfaya "kayarak" değil, zaten oradaymış gibi açılmak için
    /// kullanılır (panelin kendisi zaten kayarak geliyor, ikinci bir kayma karmaşa yaratır).
    /// </summary>
    public void JumpToPageInstant(int pageIndex)
    {
        EnsureInitialized();
        if (PageCount == 0) return;

        CurrentPage = ClampPage(pageIndex);

        content.DOKill();
        _scrollRect.StopMovement();
        content.anchoredPosition = new Vector2(TargetX(CurrentPage), content.anchoredPosition.y);

        OnPageChanged?.Invoke(CurrentPage);
    }

    private float TargetX(int pageIndex) => -pageIndex * viewport.rect.width;

    /// <summary>PageCount 0 iken Mathf.Clamp(x, 0, -1) sonucu -1 döneceği için ayrıca korunuyor.</summary>
    private int ClampPage(int pageIndex) => Mathf.Clamp(pageIndex, 0, Mathf.Max(0, PageCount - 1));
}
