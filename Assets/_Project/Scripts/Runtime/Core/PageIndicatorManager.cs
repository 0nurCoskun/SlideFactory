using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

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
    [Tooltip("pageScrollSnap atanmışsa bu değer YOK SAYILIR, sayfa sayısı oradan otomatik alınır.")]
    public int totalPages = 5;
    public Color activeColor = Color.white;
    public Color inactiveColor = new Color(1f, 1f, 1f, 0.3f); // Şeffaf beyaz

    private List<Image> dotImages = new List<Image>();

    void Start()
    {
        // pageScrollSnap atanmışsa, sayfa sayısını ORADAN al - Inspector'daki
        // totalPages ile PageScrollSnap'in gerçek sayfa sayısı farklı olursa
        // (örn. biri güncellenip diğeri unutulursa) tutarsızlık yaşanmasın.
        if (pageScrollSnap != null)
        {
            totalPages = pageScrollSnap.PageCount;
        }

        CreateIndicators();

        // Canlı sürükleme sırasında anlık önizleme için (gerçek ScrollRect hareketi).
        scrollRect.onValueChanged.AddListener(delegate { UpdateIndicatorsFromScroll(); });

        // Snap animasyonu KESİN bittiğinde doğru sayfayı garanti etmek için.
        if (pageScrollSnap != null)
        {
            pageScrollSnap.OnPageChanged += SetActivePage;
        }

        UpdateIndicatorsFromScroll();
    }

    private void OnDestroy()
    {
        if (pageScrollSnap != null)
        {
            pageScrollSnap.OnPageChanged -= SetActivePage;
        }
    }

    void CreateIndicators()
    {
        // Önce eski noktaları temizle (varsa)
        foreach (Transform child in indicatorHolder) Destroy(child.gameObject);
        dotImages.Clear();

        // Sayfa sayısı kadar nokta oluştur
        for (int i = 0; i < totalPages; i++)
        {
            GameObject newDot = Instantiate(dotPrefab, indicatorHolder);
            Image dotImg = newDot.GetComponent<Image>();
            dotImages.Add(dotImg);
        }
    }

    /// <summary>ScrollRect'in canlı (sürükleme sırasındaki) normalize pozisyonuna göre önizleme günceller.</summary>
    void UpdateIndicatorsFromScroll()
    {
        if (dotImages.Count == 0) return;

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

        for (int i = 0; i < dotImages.Count; i++)
        {
            dotImages[i].color = (i == currentPage) ? activeColor : inactiveColor;
        }
    }
}