using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// GEÇİCİ test aracı - Crashlytics ve Remote Config'i gerçek cihazda hızlıca doğrulamak
/// için eklendi. TEST BİTİNCE BU DOSYAYI VE Game.unity'deki component referansını SİL.
///
/// Herhangi bir UI'ya dokunmadan çalışır: ekranın SOL ÜST köşesine (200x200px) 3 kez
/// hızlı (1.5 sn içinde) dokunursan Remote Config'teki "test_key" değerini Console'a
/// loglar. SAĞ ÜST köşeye aynı şekilde 3 kez dokunursan KASITLI bir test exception'ı
/// fırlatır - Crashlytics.ReportUncaughtExceptionsAsFatal varsayılan olarak true
/// olduğu için bu, fatal bir crash raporu olarak Firebase Console'a düşer.
///
/// Sadece Debug.isDebugBuild true iken aktif - yanlışlıkla bir release build'de
/// kalırsa oyuncunun eline geçmesin diye.
/// </summary>
public class FirebaseDebugTestHarness : MonoBehaviour
{
    private const float TapWindowSeconds = 1.5f;
    private const float CornerSize = 200f;

    private readonly List<float> _topLeftTapTimes = new();
    private readonly List<float> _topRightTapTimes = new();

    private void Update()
    {
        if (!Debug.isDebugBuild) return;
        if (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame) return;

        Vector2 pos = Pointer.current.position.ReadValue();
        bool isTopLeft = pos.x <= CornerSize && pos.y >= Screen.height - CornerSize;
        bool isTopRight = pos.x >= Screen.width - CornerSize && pos.y >= Screen.height - CornerSize;

        if (isTopLeft) RegisterTap(_topLeftTapTimes, OnRemoteConfigTestTriggered);
        else if (isTopRight) RegisterTap(_topRightTapTimes, OnCrashTestTriggered);
    }

    private void RegisterTap(List<float> tapTimes, Action onTripleTap)
    {
        float now = Time.unscaledTime;
        tapTimes.Add(now);
        tapTimes.RemoveAll(t => now - t > TapWindowSeconds);

        if (tapTimes.Count >= 3)
        {
            tapTimes.Clear();
            onTripleTap();
        }
    }

    private void OnRemoteConfigTestTriggered()
    {
        string value = FirebaseManager.GetRemoteConfigString("test_key", "TEST_KEY_YOK/DEFAULT");
        Debug.Log($"[FirebaseDebugTestHarness] Remote Config 'test_key' = {value}");
    }

    private void OnCrashTestTriggered()
    {
        Debug.Log("[FirebaseDebugTestHarness] Kasıtlı test crash tetikleniyor...");
        throw new Exception("FirebaseDebugTestHarness kasıtlı test crash'i");
    }
}
