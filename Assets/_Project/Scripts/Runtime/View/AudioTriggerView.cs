using UnityEngine;

/// <summary>
/// GameManager'ın oyun event'lerini dinler ve AudioManager üzerinden ilgili ses
/// efektini çalar. GameManager'ın kendisi ses çalmayı HİÇ bilmez (Dependency Inversion) -
/// CardView'in animasyon için event dinlemesiyle birebir aynı mantık.
///
/// Sahnede GameManager'ın bulunduğu herhangi bir objeye (veya ayrı bir objeye) eklenir.
/// </summary>
public class AudioTriggerView : MonoBehaviour
{
    [Header("Bağımlılık")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private SwipeInputManager swipeInputManager;

    [Header("Ses Klipleri")]
    [SerializeField] private AudioClip cardTouchClip;
    [SerializeField] private AudioClip cardSwipeClip;
    [SerializeField] private AudioClip correctSwipeClip;
    [SerializeField] private AudioClip wrongSwipeClip;
    [SerializeField] private AudioClip cardCompletedClip;
    [SerializeField] private AudioClip levelWonClip;
    [SerializeField] private AudioClip levelFailedClip;

    private void OnEnable()
    {
        if (swipeInputManager != null)
            swipeInputManager.OnDragStarted.AddListener(HandleCardTouched);
        swipeInputManager.OnSwipeDetected.AddListener(HandleCardProcessed);

        if (gameManager == null) return;

        gameManager.OnCardProcessed += HandleCardProcessed;
        gameManager.OnCardCompleted += HandleCardCompleted;
        gameManager.OnInvalidSwipe += HandleInvalidSwipe;
        gameManager.OnLevelWon += HandleLevelWon;
        gameManager.OnLevelFailed += HandleLevelFailed;
    }

    private void OnDisable()
    {
        if (swipeInputManager != null)
            swipeInputManager.OnDragStarted.RemoveListener(HandleCardTouched);
        swipeInputManager.OnSwipeDetected.RemoveListener(HandleCardProcessed);

        if (gameManager == null) return;

        gameManager.OnCardProcessed -= HandleCardProcessed;
        gameManager.OnCardCompleted -= HandleCardCompleted;
        gameManager.OnInvalidSwipe -= HandleInvalidSwipe;
        gameManager.OnLevelWon -= HandleLevelWon;
        gameManager.OnLevelFailed -= HandleLevelFailed;
    }

    private void HandleCardTouched()
    {
        AudioManager.Instance?.PlaySFX(cardTouchClip);
    }

    private void HandleCardProcessed(CardInstance card, CardData newData)
    {
        AudioManager.Instance?.PlaySFX(correctSwipeClip);
    }

    private void HandleCardProcessed(SwipeDirection direction)
    {
        AudioManager.Instance?.PlaySFX(cardSwipeClip);
    }

    private void HandleCardCompleted(CardInstance card)
    {
        AudioManager.Instance?.PlaySFX(cardCompletedClip);
    }

    private void HandleInvalidSwipe(SwipeDirection direction, StationData wrongStation)
    {
        AudioManager.Instance?.PlaySFX(wrongSwipeClip);
    }

    private void HandleLevelWon()
    {
        AudioManager.Instance?.PlaySFX(levelWonClip);
    }

    private void HandleLevelFailed()
    {
        AudioManager.Instance?.PlaySFX(levelFailedClip);
    }
}