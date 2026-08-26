using UnityEngine;

/// <summary>
/// "Reklamları Kaldır" IAP satın alımının KALICI durumunu PlayerPrefs üzerinden tutar.
/// LevelProgress/ScoreProgress ile aynı desen: tek sorumluluk, statik sınıf, PlayerPrefs-backed.
///
/// NOT: Gerçek satın alma doğrulaması (receipt validation) burada YAPILMAZ - bu sınıf sadece
/// "reklamsız mı" bayrağını saklar. IAPManager (Unity IAP entegrasyonu eklendiğinde) başarılı
/// bir satın alma/restore sonrası SetAdsRemoved(true) çağıracak; bu sınıf onun ÜZERİNE bina
/// edilmiyor çünkü AdManager, IAP SDK'sı henüz kurulmamışken bile bu bayrağı okuyabilmeli.
/// </summary>
public static class MonetizationProgress
{
    private const string AdsRemovedKey = "monetization_ads_removed";

    public static bool AreAdsRemoved()
    {
        return PlayerPrefs.GetInt(AdsRemovedKey, 0) == 1;
    }

    /// <summary>Sadece TRUE yönünde çağrılmalı (satın alma/restore başarılı olduğunda) - false'a
    /// geri döndürecek bir akış yok, o yüzden burada "never lowers" koruması bilerek YOK.</summary>
    public static void SetAdsRemoved(bool removed)
    {
        PlayerPrefs.SetInt(AdsRemovedKey, removed ? 1 : 0);
        PlayerPrefs.Save();
    }
}
