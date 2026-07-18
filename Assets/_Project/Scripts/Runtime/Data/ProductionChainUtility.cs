using System.Collections.Generic;

/// <summary>
/// Bir ham maddenin (Raw CardData) zincirini baştan sona takip edip
/// hangi istasyonlardan geçerek hangi ürüne dönüştüğünü çıkarır.
/// CardData asset'lerindeki outcomes verisini okuyarak çalışır, elle
/// bir "zincir" tanımlamana gerek kalmaz - designer sadece kartları
/// birbirine bağlar, bu sınıf zaten var olan veriden zinciri türetir.
/// </summary>
public static class ProductionChainUtility
{
    public struct ChainStep
    {
        public CardData Card;
        public StationData StationToNext; // null ise bu kart zincirin son ürünüdür
    }

    /// <summary>Verilen ham karttan başlayıp son ürüne kadar olan tüm adımları döndürür.</summary>
    public static List<ChainStep> BuildChain(CardData rawCard)
    {
        List<ChainStep> steps = new List<ChainStep>();
        CardData current = rawCard;
        int safetyCounter = 0; // sonsuz döngüye karşı güvenlik (yanlış yapılandırılmış bir asset olursa)

        while (current != null && safetyCounter < 20)
        {
            StationData stationToNext = null;
            CardData nextCard = null;

            if (!current.isFinalProduct && current.outcomes != null)
            {
                foreach (var outcome in current.outcomes)
                {
                    // Çöpe giden (resultCard == null) outcome'ları atla, sadece
                    // gerçek "ilerleme" sağlayan doğru yolu takip et.
                    if (outcome.resultCard != null)
                    {
                        stationToNext = outcome.station;
                        nextCard = outcome.resultCard;
                        break;
                    }
                }
            }

            steps.Add(new ChainStep { Card = current, StationToNext = stationToNext });

            if (current.isFinalProduct || nextCard == null) break;

            current = nextCard;
            safetyCounter++;
        }

        return steps;
    }
}
