using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Level Select'teki sayfa göstergesi. AZ sayfa varken klasik davranır (her sayfa için
/// bir nokta), ÇOK sayfa varken (200 level = 20 sayfa) 20 nokta ekrana sığmayacağı için
/// KAYAN BİR PENCEREYE geçer: sabit sayıda nokta gösterilir, aktif nokta pencerenin
/// içinde hareket eder, pencerenin ötesinde sayfa kaldığını belli etmek için kenardaki
/// noktalar küçültülür. Ayrıca "12 / 20" şeklinde bir sayaç ve opsiyonel ileri/geri
/// butonları eşlik eder.
/// </summary>
public class PageIndicatorManager : MonoBehaviour
{
    [Header("Elements")]
    public ScrollRect scrollRect;
    public Transform indicatorHolder;
    public GameObject dotPrefab; // Küçük nokta görselinin prefab'ı

    [Header("Snap Entegrasyonu (opsiyonel ama önerilir)")]
    [Tooltip("Atanırsa, snap animasyonu bittiğinde dot'lar KESİN doğru sayfayı gösterir. " +
             "Atanmazsa sadece ScrollRect'in canlı sürükleme pozisyonuna güvenilir, " +
             "DOTween ile yapılan snap hareketleri dot'u güncellemeyebilir.")]
    public PageScrollSnap pageScrollSnap;

    [Header("Settings")]
    [Tooltip("pageScrollSnap atanmışsa VE Rebuild() çağrılmışsa bu değer YOK SAYILIR, " +
             "sayfa sayısı oradan otomatik alınır.")]
    public int totalPages = 5;
    public Color activeColor = Color.white;
    public Color inactiveColor = new Color(1f, 1f, 1f, 0.3f); // Şeffaf beyaz

    [Header("Çok Sayfa Modu")]
    [Tooltip("Ekranda aynı anda gösterilecek EN FAZLA nokta sayısı. Sayfa sayısı bunu " +
             "aşarsa noktalar kayan bir pencereye dönüşür ve sayaç metni görünür hale gelir.")]
    [Min(1)] public int maxDots = 7;

    [Tooltip("Pencerenin dışında daha fazla sayfa kaldığını belli etmek için kenardaki " +
             "noktanın küçültülme oranı.")]
    [Range(0.1f, 1f)] public float edgeDotScale = 0.6f;

    [Tooltip("Opsiyonel - \"12 / 20\" sayacı. Sadece sayfa sayısı maxDots'u aşınca gösterilir. " +
             "Sadece rakam ve bölü işareti içerdiği için çeviri gerektirmez.")]
    public TMP_Text pageCounterText;

    [Tooltip("Opsiyonel - bir önceki/sonraki sayfaya atlayan butonlar. 20 sayfada tek tek " +
             "kaydırmak yerine adım adım ilerlemeyi mümkün kılar.")]
    public Button prevPageButton;
    public Button nextPageButton;

    private readonly List<Image> dotImages = new List<Image>();
    private bool _subscribed;

    void Start()
    {
        Rebuild(pageScrollSnap != null ? pageScrollSnap.PageCount : totalPages);
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    /// <summary>
    /// Sayfa sayısı değiştiğinde (sayfalar runtime'da üretildiğinde) LevelSelectView
    /// tarafından çağrılır. Start()'a güvenilemez: bu component her zaman aktif bir kök
    /// objede duruyor ama sayfaları üreten LevelSelectView panel aktif olunca çalışıyor,
    /// yani Start() çalıştığında sayfa sayısı henüz 0 olabilir.
    ///
    /// İdempotent'tir - tekrar tekrar çağrılabilir, abonelikler çiftlenmez.
    /// </summary>
    public void Rebuild(int pageCount)
    {
        totalPages = Mathf.Max(0, pageCount);

        CreateIndicators();
        Subscribe();
        WireArrowButtons();

        UpdateIndicatorsFromScroll();
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;

        // Canlı sürükleme sırasında anlık önizleme için (gerçek ScrollRect hareketi).
        // NOT: Anonim delegate yerine ADI OLAN bir metot kullanılıyor - anonim olsaydı
        // RemoveListener ile geri alınamaz, Rebuild her çağrıldığında bir kopya daha
        // eklenirdi.
        if (scrollRect != null) scrollRect.onValueChanged.AddListener(HandleScrollValueChanged);

        // Snap animasyonu KESİN bittiğinde doğru sayfayı garanti etmek için.
        if (pageScrollSnap != null) pageScrollSnap.OnPageChanged += SetActivePage;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        _subscribed = false;

        if (scrollRect != null) scrollRect.onValueChanged.RemoveListener(HandleScrollValueChanged);
        if (pageScrollSnap != null) pageScrollSnap.OnPageChanged -= SetActivePage;
    }

    private void WireArrowButtons()
    {
        if (prevPageButton != null)
        {
            prevPageButton.onClick.RemoveListener(GoToPreviousPage);
            prevPageButton.onClick.AddListener(GoToPreviousPage);
        }

        if (nextPageButton != null)
        {
            nextPageButton.onClick.RemoveListener(GoToNextPage);
            nextPageButton.onClick.AddListener(GoToNextPage);
        }
    }

    private void GoToPreviousPage()
    {
        if (pageScrollSnap != null) pageScrollSnap.GoToPage(pageScrollSnap.CurrentPage - 1);
    }

    private void GoToNextPage()
    {
        if (pageScrollSnap != null) pageScrollSnap.GoToPage(pageScrollSnap.CurrentPage + 1);
    }

    void CreateIndicators()
    {
        dotImages.Clear();
        if (dotPrefab == null || indicatorHolder == null) return;

        // Eski noktaları temizle. Destroy FRAME SONUNDA gerçekleştiği için önce
        // hiyerarşiden KOPARIYORUZ - aksi halde aynı karede tekrar Rebuild edilirse
        // ölmek üzere olan noktalar HorizontalLayoutGroup'ta yer kaplamaya devam eder.
        for (int i = indicatorHolder.childCount - 1; i >= 0; i--)
        {
            GameObject old = indicatorHolder.GetChild(i).gameObject;
            old.transform.SetParent(null, false);
            Destroy(old);
        }

        // Çok sayfa varken sabit sayıda nokta üretilir; hangi sayfaların temsil
        // edildiği SetActivePage'de kayan pencereyle belirlenir.
        int dotCount = Mathf.Min(totalPages, Mathf.Max(1, maxDots));

        for (int i = 0; i < dotCount; i++)
        {
            GameObject newDot = Instantiate(dotPrefab, indicatorHolder);
            Image dotImg = newDot.GetComponent<Image>();
            dotImages.Add(dotImg);
        }

        // Sayaç sadece pencereli moda geçildiğinde anlamlı - az sayfada ekranı kirletmesin.
        if (pageCounterText != null) pageCounterText.gameObject.SetActive(IsWindowed);
    }

    private bool IsWindowed => totalPages > Mathf.Max(1, maxDots);

    private void HandleScrollValueChanged(Vector2 _) => UpdateIndicatorsFromScroll();

    /// <summary>ScrollRect'in canlı (sürükleme sırasındaki) normalize pozisyonuna göre önizleme günceller.</summary>
    void UpdateIndicatorsFromScroll()
    {
        if (dotImages.Count == 0 || scrollRect == null) return;

        // Tek sayfa varken (totalPages - 1) == 0 olur; bölme/çarpma anlamsızlaşır.
        if (totalPages <= 1)
        {
            SetActivePage(0);
            return;
        }

        float currentPos = Mathf.Clamp01(scrollRect.horizontalNormalizedPosition);
        int currentPage = Mathf.RoundToInt(currentPos * (totalPages - 1));

        SetActivePage(currentPage);
    }

    /// <summary>
    /// Belirli bir sayfayı KESİN olarak aktif gösterir. PageScrollSnap.OnPageChanged
    /// event'inden çağrılır - snap animasyonu bittiğinde doğru sonucu garantiler.
    /// </summary>
    public void SetActivePage(int currentPage)
    {
        if (dotImages.Count == 0) return;

        currentPage = Mathf.Clamp(currentPage, 0, Mathf.Max(0, totalPages - 1));

        // Pencerenin ilk noktasının hangi sayfayı temsil ettiği. Aktif sayfa mümkün
        // olduğunca ortada tutulur, listenin başında/sonunda pencere kenara yaslanır.
        int windowStart = Mathf.Clamp(currentPage - dotImages.Count / 2, 0, Mathf.Max(0, totalPages - dotImages.Count));

        for (int i = 0; i < dotImages.Count; i++)
        {
            Image dot = dotImages[i];
            if (dot == null) continue;

            int representedPage = windowStart + i;
            dot.color = (representedPage == currentPage) ? activeColor : inactiveColor;

            // Pencerenin ötesinde hâlâ sayfa varsa o taraftaki uç noktayı küçült -
            // "devamı var" hissini veren standart pager davranışı.
            bool moreBefore = i == 0 && windowStart > 0;
            bool moreAfter = i == dotImages.Count - 1 && windowStart + dotImages.Count < totalPages;
            dot.transform.localScale = (moreBefore || moreAfter) ? Vector3.one * edgeDotScale : Vector3.one;
        }

        if (pageCounterText != null && IsWindowed)
        {
            pageCounterText.text = $"{currentPage + 1} / {totalPages}";
        }

        if (prevPageButton != null) prevPageButton.interactable = currentPage > 0;
        if (nextPageButton != null) nextPageButton.interactable = currentPage < totalPages - 1;
    }
}
