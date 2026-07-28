using UnityEngine;
using DG.Tweening; // DOTween kütüphanesini dahil ediyoruz

public class ParallaxCard : MonoBehaviour
{
    [Header("Hareket Sınırları")]
    public float startX = -12f; // Ekranın sol dışı
    public float endX = 12f;    // Ekranın sağ dışı

    [Header("Parallax Ayarları")]
    public float minScale = 0.5f;  // En küçük boyut (Uzaktaki kart)
    public float maxScale = 1.5f;  // En büyük boyut (Yakındaki kart)

    [Header("Dönüş Ayarları")]
    [Tooltip("Dönüş süresi ne kadar düşükse kart o kadar hızlı fırıldak gibi döner.")]
    public float minRotationDuration = 2f; // En hızlı dönüş süresi
    public float maxRotationDuration = 8f; // En yavaş dönüş süresi
    public bool randomDirection = false;    // Bazıları sağa, bazıları sola dönsün mü?

    [Tooltip("Süre ne kadar düşükse, kart o kadar hızlı gider.")]
    public float minDuration = 2f; // Büyük kartlar için (Hızlı)
    public float maxDuration = 7f; // Küçük kartlar için (Yavaş)

    void Start()
    {
        // 1. Rastgele bir boyut belirle ve uygula
        float randomScale = Random.Range(minScale, maxScale);
        transform.localScale = Vector3.one * randomScale;

        // 2. Boyuta göre hızı (süreyi) hesapla
        // t değeri 0 (minScale) ile 1 (maxScale) arasında bir oran verir.
        float t = Mathf.InverseLerp(minScale, maxScale, randomScale);

        // Bu oranı kullanarak süreyi ters orantılı olarak buluyoruz:
        // Kart büyükse (t=1) süre minDuration olur. Kart küçükse (t=0) süre maxDuration olur.
        float loopDuration = Mathf.Lerp(maxDuration, minDuration, t);

        // 3. Görsel derinliği (Z ekseni veya Sorting Order) ayarla
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // Büyük kartlar önde (yüksek order), küçükler arkada (düşük order) kalsın
            sr.sortingOrder = Mathf.RoundToInt(t * 100);
        }

        //Dönüş Animasyonunu Başlat
        StartRotation();
        // 4. Hareketi Başlat
        StartMovement(loopDuration);
    }

    void StartRotation()
    {
        // Kendi etrafında dönmesi için rastgele bir süre (hız) seç
        float rotDuration = Random.Range(minRotationDuration, maxRotationDuration);

        // Z ekseninde -360 derece dönüş açısı belirle
        float angle = -360f;

        // Eğer rastgele yön seçiliyse %50 ihtimalle ters yöne (360) dönsün
        if (randomDirection && Random.value > 0.5f)
        {
            angle = 360f;
        }

        // DOTween ile sonsuz dönüş animasyonu
        transform.DORotate(new Vector3(0, 0, angle), rotDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart); // Bittiğinde sıfırlanıp tekrar döner
    }

    void StartMovement(float duration)
    {
        // Kartın sahnedeki mevcut konumundan sağ sınıra (endX) olan mesafesini hesapla.
        // Bu sayede kartları sahneye dağınık yerleştirirsen, hepsi aniden sola ışınlanmaz;
        // kaldıkları yerden sağa doğru doğalca gitmeye başlarlar.
        float totalDistance = endX - startX;
        float distanceLeft = endX - transform.position.x;
        float initialDuration = (distanceLeft / totalDistance) * duration;

        // Önce bulunduğu yerden sağa gitsin
        transform.DOMoveX(endX, initialDuration).SetEase(Ease.Linear).OnComplete(() =>
        {
            // İlk gidiş bittiğinde, kartı sol başa al...
            transform.position = new Vector3(startX, transform.position.y, transform.position.z);

            // ...ve sonsuz döngüyü başlat!
            // LoopType.Restart: Sona ulaştığında aniden başa döner ve tekrar başlar.
            transform.DOMoveX(endX, duration)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart);
        });
    }

    void OnDestroy()
    {
        // Obje silindiğinde (veya oyun durduğunda) bu objenin transform'una bağlı tüm tweenleri öldür.
        transform.DOKill();
    }
}