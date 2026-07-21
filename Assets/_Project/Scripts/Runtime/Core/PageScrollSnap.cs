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

    private void Awake()
    {
        _scrollRect = GetComponent<ScrollRect>();
        if (viewport == null) viewport = _scrollRect.viewport;
        if (content == null) content = _scrollRect.content;

        PageCount = content.childCount;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float pageWidth = viewport.rect.width;
        float currentX = -content.anchoredPosition.x;

        int nearestPage = Mathf.RoundToInt(currentX / pageWidth);

        // Hızlı bir flick yapıldıysa, tam ortalanmamış olsa bile yönüne göre
        // bir sonraki/önceki sayfaya geçmeye zorla (daha doğal bir his verir).
        if (Mathf.Abs(_scrollRect.velocity.x) > flickVelocityThreshold)
        {
            nearestPage += _scrollRect.velocity.x < 0 ? 1 : -1;
        }

        GoToPage(nearestPage);
    }

    /// <summary>Belirli bir sayfaya git (dot'a tıklanınca da çağırabilirsin).</summary>
    public void GoToPage(int pageIndex)
    {
        CurrentPage = Mathf.Clamp(pageIndex, 0, PageCount - 1);
        float targetX = -CurrentPage * viewport.rect.width;

        // KRİTİK SATIR: ScrollRect'in kendi Elastic/Inertia hareketini durduruyoruz,
        // yoksa bizim DOTween ile verdiğimiz hedefle çekişip kazanıyor ve content
        // Page 1'e geri fırlıyor.
        _scrollRect.velocity = Vector2.zero;

        content.DOKill();
        content.DOAnchorPosX(targetX, snapDuration).SetEase(Ease.OutCubic);

        OnPageChanged?.Invoke(CurrentPage);
    }
}
