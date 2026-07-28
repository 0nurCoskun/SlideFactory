using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// Level başlarken (Recipe Preview paneli kapanınca) oynatılan "deste karılıyor"
/// animasyonu. Birkaç sahte kart görselini rastgele yönlerden merkeze toplayıp
/// bitince GameManager.BeginLevelPlay()'i çağırır - gerçek ilk kart, bu animasyon
/// bittikten SONRA kendi normal giriş animasyonuyla (CardView) belirir.
///
/// Bu script, GameManager.BeginLevelPlay()'i DOĞRUDAN değil, animasyon bittikten
/// sonra çağırır - RecipePreviewView artık BeginLevelPlay()'i bu script üzerinden tetikler.
/// </summary>
public class DeckShuffleView : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private GameManager gameManager;

    [Header("Hedef Pozisyon")]
    [Tooltip("Sahte kartların toplanacağı, gerçek Card'ın durduğu pozisyon.")]
    [SerializeField] private RectTransform targetPosition;
    [SerializeField] private GameObject shuffleCardPrefab;
    [SerializeField] private Transform shuffleCardsParent;

    [Header("Animasyon Ayarları")]
    [SerializeField] private int shuffleCardCount = 5;
    [SerializeField] private float perCardFlyDuration = 0.35f;
    [SerializeField] private float staggerDelay = 0.08f;
    [SerializeField] private float spawnDistance = 500f;
    [SerializeField] private float maxEntryRotation = 25f;

    [Header("Ses")]
    [SerializeField] private AudioClip cardShuffleSound;

    /// <summary>RecipePreviewView, level ilk kez başlarken bunu çağırır (BeginLevelPlay yerine).</summary>
    public void PlayShuffleThenBeginLevel()
    {
        AudioManager.Instance.PlaySFX(cardShuffleSound);

        List<RectTransform> spawnedCards = new List<RectTransform>();
        Sequence masterSequence = DOTween.Sequence();

        for (int i = 0; i < shuffleCardCount; i++)
        {
            GameObject fakeCard = Instantiate(shuffleCardPrefab, shuffleCardsParent);
            RectTransform rt = fakeCard.GetComponent<RectTransform>();
            spawnedCards.Add(rt);

            // Rastgele bir yönden başlasın - deste "dağılmış" gibi görünsün.
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 randomOffset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnDistance;

            Vector2 finalPos = targetPosition.anchoredPosition;
            rt.anchoredPosition = finalPos + randomOffset;
            rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-maxEntryRotation, maxEntryRotation));

            // Insert kullanıyoruz - her kart bir öncekinden staggerDelay kadar GEÇ başlıyor,
            // ama hepsi AYNI sequence içinde, toplam süre otomatik en son kartın bitişine göre ayarlanıyor.
            masterSequence.Insert(i * staggerDelay, rt.DOAnchorPos(finalPos, perCardFlyDuration).SetEase(Ease.OutQuad));
            masterSequence.Insert(i * staggerDelay, rt.DOLocalRotate(Vector3.zero, perCardFlyDuration).SetEase(Ease.OutQuad));
        }

        masterSequence.OnComplete(() =>
        {
            foreach (RectTransform rt in spawnedCards)
            {
                if (rt != null) Destroy(rt.gameObject);
            }

            gameManager.BeginLevelPlay();
        });
    }
}
