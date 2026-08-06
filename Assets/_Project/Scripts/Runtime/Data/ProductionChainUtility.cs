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

    /// <summary>
    /// "Bir sonraki doğru adım" kuralının TEK tanımı: outcomes listesindeki
    /// resultCard'ı DOLU olan İLK outcome kazanır. resultCard'ı boş olan (kartı çöpe
    /// gönderen) outcome'lar atlanır - onlar geçerli hamledir ama zinciri İLERLETMEZ.
    /// Bu yüzden CardData.TryGetOutcome bu iş için KULLANILAMAZ: o metod çöp
    /// istasyonlarında da true döner.
    /// </summary>
    private static bool TryGetNextStep(CardData card, out StationData station, out CardData resultCard)
    {
        station = null;
        resultCard = null;

        if (card == null || card.isFinalProduct || card.outcomes == null) return false;

        foreach (StationOutcome outcome in card.outcomes)
        {
            // StationOutcome bir CLASS olduğu için dizide boş (null) slot olabilir.
            if (outcome == null || outcome.resultCard == null) continue;

            station = outcome.station;
            resultCard = outcome.resultCard;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Verilen kartın ŞU AN hangi istasyona gitmesi gerektiğini söyler.
    /// Yardımcı ok ipucu (SwipeHintArrowView) bunu kullanır.
    /// Kart final ürünse, outcomes boşsa ya da tüm outcome'lar çöpse null döner.
    /// </summary>
    public static StationData GetNextStation(CardData card)
    {
        TryGetNextStep(card, out StationData station, out _);
        return station;
    }

    /// <summary>Verilen ham karttan başlayıp son ürüne kadar olan tüm adımları döndürür.</summary>
    public static List<ChainStep> BuildChain(CardData rawCard)
    {
        List<ChainStep> steps = new List<ChainStep>();
        CardData current = rawCard;
        int safetyCounter = 0; // sonsuz döngüye karşı güvenlik (yanlış yapılandırılmış bir asset olursa)

        while (current != null && safetyCounter < 20)
        {
            // Zincirin "doğru yol" kuralı artık TryGetNextStep'te, TEK noktada duruyor.
            bool hasNext = TryGetNextStep(current, out StationData stationToNext, out CardData nextCard);

            steps.Add(new ChainStep { Card = current, StationToNext = stationToNext });

            if (!hasNext) break;

            current = nextCard;
            safetyCounter++;
        }

        return steps;
    }
}
