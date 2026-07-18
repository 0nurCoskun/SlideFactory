using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[Serializable] public class SwipeDirectionEvent : UnityEvent<SwipeDirection> { }
[Serializable] public class DragDeltaEvent : UnityEvent<Vector2> { }

/// <summary>
/// Ekrandaki dokunma/tıklama girdisini dinler, sürükleme mesafesine ve açısına göre
/// bir SwipeDirection hesaplar. Bu sınıf SADECE input algılamaktan sorumludur;
/// hangi kartın ne yapacağını bilmez (Single Responsibility).
///
/// GameManager bu sınıfa abone olur, kart görselini (CardView) hareket ettiren
/// katman ise OnDragDelta / OnDragStarted / OnDragCanceled event'lerini dinleyip
/// DOTween ile anlık sürükleme takibi ve "bırakınca merkeze dönme" animasyonunu yapar.
/// </summary>
public class SwipeInputManager : MonoBehaviour
{
    [Header("Ayarlar")]
    [Tooltip("Swipe'ın başlayabileceği alan. Boş bırakılırsa ekranın HER YERİNDEN swipe başlar. " +
             "Sadece kart üzerinden başlasın istiyorsan, buraya Card'ın RectTransform'unu ata.")]
    [SerializeField] private RectTransform swipeTargetArea;

    [Tooltip("Bir hamlenin geçerli sayılması için minimum piksel mesafesi.")]
    [SerializeField] private float minSwipeDistance = 80f;

    [Tooltip("0-1 arası. Yüksek değer, çapraz sürüklemelerin daha 'net' bir yöne (Up/Down/Left/Right) yorumlanmasını zorlar.")]
    [Range(0f, 1f)]
    [SerializeField] private float directionThreshold = 0.5f;

    [Header("Events")]
    public SwipeDirectionEvent OnSwipeDetected; // Geçerli bir hamle tamamlandığında (parmak kalktığında)
    public DragDeltaEvent OnDragDelta;          // Sürükleme sırasında her frame - görsel takip için
    public UnityEvent OnDragStarted;            // Parmak/mouse ekrana değdiğinde
    public UnityEvent OnDragCanceled;           // Eşik altında bırakıldığında (kart merkeze dönmeli)

    private Vector2 _startPos;
    private bool _isDragging;

    private void Update()
    {
        // Dokunmatik cihaz varsa ve aktif bir dokunuş varsa touch, yoksa mouse (editör/PC) kullan.
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            HandleTouchInput();
        }
        else if (Mouse.current != null)
        {
            HandleMouseInput();
        }
    }

    private void HandleMouseInput()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            BeginDrag(Mouse.current.position.ReadValue());
        }
        else if (Mouse.current.leftButton.isPressed && _isDragging)
        {
            UpdateDrag(Mouse.current.position.ReadValue());
        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame && _isDragging)
        {
            EndDrag(Mouse.current.position.ReadValue());
        }
    }

    private void HandleTouchInput()
    {
        TouchControl touch = Touchscreen.current.primaryTouch;
        UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();

        switch (phase)
        {
            case UnityEngine.InputSystem.TouchPhase.Began:
                BeginDrag(touch.position.ReadValue());
                break;
            case UnityEngine.InputSystem.TouchPhase.Moved:
            case UnityEngine.InputSystem.TouchPhase.Stationary:
                if (_isDragging) UpdateDrag(touch.position.ReadValue());
                break;
            case UnityEngine.InputSystem.TouchPhase.Ended:
            case UnityEngine.InputSystem.TouchPhase.Canceled:
                if (_isDragging) EndDrag(touch.position.ReadValue());
                break;
        }
    }

    private void BeginDrag(Vector2 screenPos)
    {
        // Parmak/mouse bir UI elemanının (buton, panel vb.) üzerindeyse swipe başlatma.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // Dokunma, belirlenen hedef alanın (genelde Card'ın kendisi) DIŞINDAYSA swipe başlatma.
        // Bu sayede oyuncu ekranın herhangi bir yerinden değil, sadece kartı tutarak kaydırabilir.
        if (swipeTargetArea != null && !RectTransformUtility.RectangleContainsScreenPoint(swipeTargetArea, screenPos, null))
        {
            return;
        }

        _startPos = screenPos;
        _isDragging = true;
        OnDragStarted?.Invoke();
    }

    private void UpdateDrag(Vector2 screenPos)
    {
        Vector2 delta = screenPos - _startPos;
        OnDragDelta?.Invoke(delta);
    }

    private void EndDrag(Vector2 screenPos)
    {
        _isDragging = false;
        Vector2 delta = screenPos - _startPos;

        if (delta.magnitude < minSwipeDistance)
        {
            OnDragCanceled?.Invoke();
            return;
        }

        SwipeDirection direction = CalculateDirection(delta);

        if (direction == SwipeDirection.None)
        {
            OnDragCanceled?.Invoke();
            return;
        }

        OnSwipeDetected?.Invoke(direction);
    }

    private SwipeDirection CalculateDirection(Vector2 delta)
    {
        Vector2 normalized = delta.normalized;

        // Yatay hareket dikeyden baskınsa Sağ/Sol, değilse Yukarı/Aşağı olarak değerlendir.
        if (Mathf.Abs(normalized.x) > Mathf.Abs(normalized.y))
        {
            if (Mathf.Abs(normalized.x) < directionThreshold) return SwipeDirection.None;
            return normalized.x > 0 ? SwipeDirection.Right : SwipeDirection.Left;
        }
        else
        {
            if (Mathf.Abs(normalized.y) < directionThreshold) return SwipeDirection.None;
            return normalized.y > 0 ? SwipeDirection.Up : SwipeDirection.Down;
        }
    }
}