using UnityEngine;

/// <summary>
/// Bir RectTransform'u cihazın "güvenli alanına" (Screen.safeArea) sığdırır.
/// Çentikli (notch) telefonlarda, alt gesture bar'ı olan cihazlarda veya
/// kavisli ekran kenarlarında UI/kart elemanlarının kesilmesini engeller.
///
/// KULLANIM: Bu script'i, içine tüm oyun UI'ını (kart alanı, skor, butonlar vb.)
/// koyacağın bir "SafeAreaContainer" adında boş bir RectTransform'a ekle.
/// Canvas'ın kendisine EKLEME - Canvas'ın altındaki ilk çocuğa ekle.
///
/// Hierarchy örneği:
/// Canvas
///   └── SafeAreaContainer (RectTransform, Stretch-Stretch anchor) <-- SafeArea.cs BURAYA
///         └── CardDisplayArea
///         └── TopUI (skor, ilerleme vb.)
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    [Tooltip("Editor'de Game View boyutunu değiştirdiğinde her frame yeniden hesapla. " +
             "Build'de bu gerekmez ama test sırasında faydalıdır.")]
    [SerializeField] private bool recalculateContinuously = false;

    private RectTransform _rectTransform;
    private Rect _lastSafeArea = new Rect(0f, 0f, 0f, 0f);
    private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        if (recalculateContinuously)
        {
            ApplySafeArea();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        // Ekran döndüğünde veya çözünürlük değiştiğinde Unity bunu otomatik çağırır.
        if (_rectTransform != null)
        {
            ApplySafeArea();
        }
    }

    private void ApplySafeArea()
    {
        // Bazı cihazlarda/Editor'ün ilk frame'inde Screen.width/height 0 veya
        // tutarsız gelebilir. Bu durumda bölme işlemi NaN üretip anchor'ları
        // bozar ve kart/UI ekranın tamamen dışına savrulur. Böyle bir durumda
        // bu frame'i atla, bir sonraki çağrıda tekrar dene.
        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect safeArea = Screen.safeArea;

        // Değişiklik yoksa gereksiz hesaplama yapma.
        if (safeArea == _lastSafeArea && Screen.orientation == _lastOrientation)
            return;

        _lastSafeArea = safeArea;
        _lastOrientation = Screen.orientation;

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        // Piksel değerlerini 0-1 aralığına (anchor formatına) normalize et.
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        // Güvenlik: bölme hatalarında (Screen.width/height 0 gelirse) anchor bozulmasın.
        anchorMin.x = Mathf.Clamp01(anchorMin.x);
        anchorMin.y = Mathf.Clamp01(anchorMin.y);
        anchorMax.x = Mathf.Clamp01(anchorMax.x);
        anchorMax.y = Mathf.Clamp01(anchorMax.y);

        _rectTransform.anchorMin = anchorMin;
        _rectTransform.anchorMax = anchorMax;
    }
}