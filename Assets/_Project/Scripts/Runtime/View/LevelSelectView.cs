using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level Select panelinin tamamını LevelCatalog'dan RUNTIME'DA kurar. Eskiden sayfalar
/// ve 10'ar buton sahneye elle diziliyordu; 200 level'da bu imkânsız olduğu için artık
/// sayfa ve buton prefab'larından üretiliyorlar.
///
/// İKİ ÖNEMLİ DAVRANIŞ:
///
/// 1) TEMBEL YÜKLEME - sayfa KUTULARININ hepsi bir kerede oluşturulur (ScrollRect'in
///    içerik genişliği ve sayfa sayısı ancak böyle doğru olur), ama BUTONLAR sadece
///    o an görünen sayfanın çevresindeki birkaç sayfada bulunur. Sayfa değiştikçe
///    butonlar bir havuz üzerinden taşınır - 200 level için 2000 UI objesi yerine
///    ekranda ~30 buton olur.
///
/// 2) KALDIĞI YERDEN AÇILMA - panel her açıldığında oyuncunun oynaması gereken sayfaya
///    ANINDA (animasyonsuz) konumlanır. Bkz. ResolveTargetPage.
///
/// Bu script LevelSelectPanel'in KENDİSİNDE durmalı: tüm iş OnEnable'da yapılıyor ve
/// panel her gösterildiğinde (Play butonu, pause menüsünden dönüş) yeniden hedeflenmesi
/// buna bağlı.
/// </summary>
public class LevelSelectView : MonoBehaviour
{
    [Header("Veri")]
    [SerializeField] private LevelCatalog catalog;

    [Header("Sahne Referansları")]
    [SerializeField] private PageScrollSnap pageScrollSnap;
    [SerializeField] private PageIndicatorManager pageIndicatorManager;
    [Tooltip("ScrollRect'in Content objesi - sayfalar bunun altına üretilir.")]
    [SerializeField] private RectTransform contentRoot;
    [Tooltip("Butona tıklanınca açılacak bilgi paneli. Üretilen her butona Bind ile aktarılır.")]
    [SerializeField] private LevelInfoPanelView levelInfoPanelView;

    [Header("Prefab'lar")]
    [SerializeField] private LevelPageView levelPagePrefab;
    [SerializeField] private LevelButton levelButtonPrefab;

    [Header("Tembel Yükleme")]
    [Tooltip("Aktif sayfanın kaç sayfa ötesine kadar butonlar HAZIR bulundurulsun. " +
             "1 = önceki + aktif + sonraki. Sürükleme sırasında komşu sayfa boş görünmesin diye en az 1 olmalı.")]
    [SerializeField, Min(1)] private int windowRadius = 1;

    [Tooltip("Butonların havuza geri verilmesi için gereken uzaklık. windowRadius'tan " +
             "büyük olmalı - ileri geri kaydırmada sürekli kurup yıkmayı önler.")]
    [SerializeField, Min(1)] private int keepRadius = 2;

    private readonly List<LevelPageView> _pages = new List<LevelPageView>();
    private readonly Stack<LevelButton> _buttonPool = new Stack<LevelButton>();
    private Transform _poolRoot;
    private bool _built;
    private bool _subscribed;

    private void OnEnable()
    {
        string missing = DescribeMissingReferences();
        if (missing != null)
        {
            Debug.LogError($"[LevelSelectView] Şu alanlar Inspector'da boş: {missing}. " +
                           "Level Select ekranı kurulamıyor.", this);
            return;
        }

        if (!_built) BuildPages();
        if (_pages.Count == 0) return;

        Subscribe();

        int targetPage = ResolveTargetPage();

        // Butonları ÖNCE bas, sonra konumlan: JumpToPageInstant OnPageChanged'i tetikliyor
        // ve zaten dolu bir sayfaya atlamak, bir kare boş sayfa görünmesini engelliyor.
        PopulateWindow(targetPage);
        pageScrollSnap.JumpToPageInstant(targetPage);
    }

    /// <summary>
    /// Eksik referansların İSİMLERİNİ döner (hepsi doluysa null). Toplu bir "bir şey eksik"
    /// mesajı, hangi alanı dolduracağını söylemediği için işe yaramıyordu.
    /// </summary>
    private string DescribeMissingReferences()
    {
        List<string> missing = new List<string>();

        if (catalog == null) missing.Add(nameof(catalog));
        if (pageScrollSnap == null) missing.Add(nameof(pageScrollSnap));
        if (contentRoot == null) missing.Add(nameof(contentRoot));
        if (levelPagePrefab == null) missing.Add(nameof(levelPagePrefab));
        if (levelButtonPrefab == null) missing.Add(nameof(levelButtonPrefab));

        return missing.Count == 0 ? null : string.Join(", ", missing);
    }

    private void OnDisable() => Unsubscribe();

    private void OnDestroy() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed || pageScrollSnap == null) return;
        _subscribed = true;
        pageScrollSnap.OnPageChanged += HandlePageChanged;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || pageScrollSnap == null) return;
        _subscribed = false;
        pageScrollSnap.OnPageChanged -= HandlePageChanged;
    }

    // ------------------------------------------------------------------
    // Kurulum
    // ------------------------------------------------------------------

    private void BuildPages()
    {
        _built = true;

        ClearContent();

        int pageCount = catalog.PageCount;
        for (int i = 0; i < pageCount; i++)
        {
            LevelPageView page = Instantiate(levelPagePrefab, contentRoot);
            page.name = $"LevelPage_{i + 1}";
            page.Configure(catalog.GetChapterForPage(i));
            _pages.Add(page);
        }

        // KRİTİK: ScrollRect "Elastic" modda ve içerik genişliği ContentSizeFitter'dan
        // geliyor. Layout tazelenmeden hedef sayfaya atlarsak, ScrollRect hâlâ ESKİ
        // (dar) içerik sınırlarını bildiği için hedefi "sınır dışı" sayıp içeriği
        // 1. sayfaya geri çeker. Bu satır olmadan "kaldığı sayfada açılma" çalışmaz.
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);

        pageScrollSnap.RefreshPages();
        if (pageIndicatorManager != null) pageIndicatorManager.Rebuild(pageScrollSnap.PageCount);

        // Komşu sayfa sürükleme sırasında görünür olduğu için pencere en az 1 olmalı;
        // havuza iade mesafesi de pencereden dar olamaz.
        windowRadius = Mathf.Max(1, windowRadius);
        keepRadius = Mathf.Max(windowRadius, keepRadius);
    }

    /// <summary>
    /// Content'te tasarım zamanından kalan (ya da önceki bir kurulumdan artan) sayfaları siler.
    /// Destroy kare sonunda gerçekleştiği için önce hiyerarşiden KOPARIYORUZ - aksi halde
    /// hemen ardından çağrılan PageScrollSnap.RefreshPages() ölmek üzere olan sayfaları da
    /// sayardı ve sayfa sayısı yanlış çıkardı.
    /// </summary>
    private void ClearContent()
    {
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            GameObject leftover = contentRoot.GetChild(i).gameObject;
            leftover.transform.SetParent(null, false);
            Destroy(leftover);
        }
    }

    // ------------------------------------------------------------------
    // Hedef sayfa
    // ------------------------------------------------------------------

    /// <summary>
    /// Panel açılınca hangi sayfanın gösterileceği.
    ///
    /// Kural: normalde oyuncunun SIRADA oynaması gereken level'ın (henüz tamamlanmamış
    /// ilk level - "sınır") sayfası gösterilir. Yani 112'yi bitirip menüye dönen oyuncu
    /// 113'ün sayfasında karşılanır.
    ///
    /// Tek istisna: oyuncu bilerek ÇOK GERİDEKİ bir level'ı tekrar oynadıysa (ör. sınır
    /// 113'ken 40'ı tekrar oynadıysa) onu 113'e fırlatmak sinir bozucu olur - o durumda
    /// en son oynadığı level'ın sayfasında kalır. "Çok geride" ölçüsü: sınırın bir
    /// öncesinden de geride olmak. Sınırın hemen öncesi zaten "yeni bitirdiği level"dir.
    /// </summary>
    private int ResolveTargetPage()
    {
        int frontierIndex = catalog.GetFrontierIndex();
        if (frontierIndex < 0) return 0;

        int targetIndex = frontierIndex;

        if (catalog.TryGetIndexById(LevelProgress.GetLastPlayedLevelId(), out int lastPlayedIndex))
        {
            if (lastPlayedIndex < frontierIndex - 1) targetIndex = lastPlayedIndex;
        }

        int page = catalog.GetPageIndexOf(catalog.GetLevelAtIndex(targetIndex));
        return Mathf.Clamp(page < 0 ? 0 : page, 0, _pages.Count - 1);
    }

    // ------------------------------------------------------------------
    // Tembel yükleme
    // ------------------------------------------------------------------

    private void HandlePageChanged(int page) => PopulateWindow(page);

    private void PopulateWindow(int centerPage)
    {
        for (int i = 0; i < _pages.Count; i++)
        {
            int distance = Mathf.Abs(i - centerPage);

            if (distance <= windowRadius)
            {
                _pages[i].Populate(catalog, i, RentButton, levelInfoPanelView);
            }
            else if (distance > keepRadius)
            {
                _pages[i].Clear(ReleaseButton);
            }
        }
    }

    private LevelButton RentButton(RectTransform parent)
    {
        while (_buttonPool.Count > 0)
        {
            LevelButton pooled = _buttonPool.Pop();
            if (pooled == null) continue; // sahne kapanırken yok edilmiş olabilir

            pooled.transform.SetParent(parent, false);
            pooled.gameObject.SetActive(true);
            return pooled;
        }

        // Havuz boş - doğrudan HEDEF ebeveynin altına üretiyoruz ki obje aktif doğsun
        // ve Awake/OnEnable'ı normal sırayla çalışsın.
        return Instantiate(levelButtonPrefab, parent);
    }

    private void ReleaseButton(LevelButton button)
    {
        if (button == null) return;

        EnsurePoolRoot();

        button.gameObject.SetActive(false);
        button.transform.SetParent(_poolRoot, false);
        _buttonPool.Push(button);
    }

    private void EnsurePoolRoot()
    {
        if (_poolRoot != null) return;

        GameObject holder = new GameObject("_LevelButtonPool", typeof(RectTransform));
        holder.transform.SetParent(transform, false);
        holder.SetActive(false);
        _poolRoot = holder.transform;
    }
}
