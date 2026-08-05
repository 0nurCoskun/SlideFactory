using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyundaki TÜM level'ların SIRASINI ve bölüm (chapter) gruplamasını tutan tek
/// kaynak. Level Select ekranı artık sahnede elle dizilmiş butonlardan değil,
/// BU asset'ten üretiliyor - 200 level eklemek 200 GameObject kopyalamak değil,
/// buraya 200 LevelData sürüklemek demek.
///
/// Level'ın "kaçıncı level" olduğu (butonda görünen numara) burada listedeki
/// GLOBAL sırasından geliyor; LevelData'nın kendisinde bir index alanı YOK,
/// böylece araya level eklemek diğer asset'leri elle güncellemeyi gerektirmiyor.
///
/// Sayfalar bir bölümün ORTASINDAN bölünmez: her bölüm kendi içinde
/// levelsPerPage'lik parçalara ayrılır, yani her sayfa tek bir bölüme aittir
/// (sayfa başlığı da bu yüzden tek bir bölüm adı olabiliyor).
/// </summary>
[CreateAssetMenu(fileName = "LevelCatalog", menuName = "CardCraft/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    /// <summary>Bir dünya/tema (Kitchen, Medieval...) ve ona ait level'lar - SIRALI.</summary>
    [Serializable]
    public class Chapter
    {
        [Tooltip("Sadece editör/log tarafında ayırt etmek için. Oyuncuya gösterilmez.")]
        public string chapterId;

        [Tooltip("Sayfa başlığının \"UI\" String Table'daki anahtarı (ör. ui_kitchen). " +
                 "Yeni bir anahtar eklerken UIStrings.json'a da eklemeyi ve " +
                 "\"CardCraft/Localization/Setup Locales And Tables\"i çalıştırmayı unutma.")]
        public string titleLocalizationKey;

        [Tooltip("Opsiyonel - bu bölümün sayfalarında kullanılacak arka plan. Boşsa prefab'daki kalır.")]
        public Sprite pageBackground;

        [Tooltip("Bu bölümün level'ları - EKRANDA GÖRÜNECEK SIRAYLA.")]
        public List<LevelData> levels = new List<LevelData>();
    }

    /// <summary>Tek bir sayfanın hangi bölüme ait olduğu ve hangi level aralığını gösterdiği.</summary>
    public struct PageInfo
    {
        public int chapterIndex;
        public int firstLevelIndex; // global index
        public int levelCount;
    }

    [Tooltip("Bölümler - oyuncunun ilerleyeceği SIRAYLA.")]
    public List<Chapter> chapters = new List<Chapter>();

    [Tooltip("Bir sayfada kaç level butonu gösterilecek. Sayfa prefab'ındaki GridLayoutGroup'a " +
             "sığacak sayı olmalı (mevcut tasarımda 2 sütun x 5 satır = 10).")]
    [Min(1)] public int levelsPerPage = 10;

    // ------------------------------------------------------------------
    // Runtime cache - SERIALIZE EDİLMEZ. ScriptableObject'in serialize edilen
    // alanlarına runtime'da dokunmuyoruz (bkz. CLAUDE.md); bunlar sadece
    // aramaları O(1) yapmak için bir kez kurulan yardımcı tablolar.
    // ------------------------------------------------------------------
    [NonSerialized] private List<LevelData> _ordered;
    [NonSerialized] private Dictionary<LevelData, int> _indexByLevel;
    [NonSerialized] private Dictionary<string, int> _indexById;
    [NonSerialized] private List<PageInfo> _pages;
    [NonSerialized] private bool _built;

    public int LevelCount { get { EnsureBuilt(); return _ordered.Count; } }
    public int PageCount { get { EnsureBuilt(); return _pages.Count; } }
    public IReadOnlyList<LevelData> Levels { get { EnsureBuilt(); return _ordered; } }

    /// <summary>Asset yeniden yüklendiğinde/domain reload'da cache'i tazelemek için.</summary>
    private void OnEnable() => InvalidateCache();

    /// <summary>Inspector'da bölüm/level listesi değiştirildiğinde cache'i tazeler.</summary>
    private void OnValidate() => InvalidateCache();

    /// <summary>Editör araçları listeyi değiştirdikten sonra bunu çağırmalı.</summary>
    public void InvalidateCache()
    {
        _built = false;
    }

    public void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        _ordered = new List<LevelData>();
        _indexByLevel = new Dictionary<LevelData, int>();
        _indexById = new Dictionary<string, int>();
        _pages = new List<PageInfo>();

        if (chapters == null) return;

        for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
        {
            Chapter chapter = chapters[chapterIndex];
            if (chapter?.levels == null) continue;

            int chapterFirstIndex = _ordered.Count;

            foreach (LevelData level in chapter.levels)
            {
                if (level == null)
                {
                    Debug.LogError($"[LevelCatalog] '{name}' -> bölüm {chapterIndex} ({chapter.chapterId}) " +
                                   "içinde BOŞ bir level girişi var, atlanıyor.");
                    continue;
                }

                if (level.isTutorial)
                {
                    // Tutorial, Level Select listesinde yer almaz - ana menüdeki kendi
                    // butonundan açılır ve ilerleme kaydı da tutulmaz.
                    Debug.LogError($"[LevelCatalog] '{name}' -> tutorial level '{level.name}' katalogda " +
                                   "olmamalı, atlanıyor.");
                    continue;
                }

                if (_indexByLevel.ContainsKey(level))
                {
                    Debug.LogError($"[LevelCatalog] '{name}' -> '{level.name}' katalogda BİRDEN FAZLA kez " +
                                   "geçiyor, ikincisi atlanıyor.");
                    continue;
                }

                string identifier = LevelProgress.GetLevelIdentifier(level);
                if (_indexById.ContainsKey(identifier))
                {
                    Debug.LogError($"[LevelCatalog] '{name}' -> '{identifier}' kimliği birden fazla level " +
                                   $"tarafından kullanılıyor ('{level.name}'). İlerleme kayıtları çakışır.");
                }

                _indexByLevel[level] = _ordered.Count;
                _indexById[identifier] = _ordered.Count;
                _ordered.Add(level);
            }

            int chapterLevelCount = _ordered.Count - chapterFirstIndex;
            int perPage = Mathf.Max(1, levelsPerPage);

            // Sayfalar bölüm SINIRINI aşmaz - son sayfa yarım dolabilir, GridLayoutGroup
            // 10'dan az çocukla da sorunsuz çalışır.
            for (int offset = 0; offset < chapterLevelCount; offset += perPage)
            {
                _pages.Add(new PageInfo
                {
                    chapterIndex = chapterIndex,
                    firstLevelIndex = chapterFirstIndex + offset,
                    levelCount = Mathf.Min(perPage, chapterLevelCount - offset)
                });
            }
        }
    }

    public bool TryGetIndex(LevelData level, out int index)
    {
        EnsureBuilt();
        index = -1;
        return level != null && _indexByLevel.TryGetValue(level, out index);
    }

    public bool TryGetIndexById(string levelId, out int index)
    {
        EnsureBuilt();
        index = -1;
        return !string.IsNullOrEmpty(levelId) && _indexById.TryGetValue(levelId, out index);
    }

    /// <summary>Butonda gösterilecek GLOBAL level numarası (1'den başlar). Bulunamazsa 0.</summary>
    public int GetDisplayNumber(LevelData level)
    {
        return TryGetIndex(level, out int index) ? index + 1 : 0;
    }

    public LevelData GetLevelAtIndex(int index)
    {
        EnsureBuilt();
        return index >= 0 && index < _ordered.Count ? _ordered[index] : null;
    }

    /// <summary>Bu level'ın bulunduğu sayfanın indexi. Katalogda yoksa -1.</summary>
    public int GetPageIndexOf(LevelData level)
    {
        return TryGetIndex(level, out int index) ? GetPageIndexOfLevelIndex(index) : -1;
    }

    /// <summary>Bu kimliğe sahip level'ın bulunduğu sayfanın indexi. Katalogda yoksa -1.</summary>
    public int GetPageIndexOfId(string levelId)
    {
        return TryGetIndexById(levelId, out int index) ? GetPageIndexOfLevelIndex(index) : -1;
    }

    private int GetPageIndexOfLevelIndex(int levelIndex)
    {
        EnsureBuilt();

        for (int page = 0; page < _pages.Count; page++)
        {
            PageInfo info = _pages[page];
            if (levelIndex >= info.firstLevelIndex && levelIndex < info.firstLevelIndex + info.levelCount)
            {
                return page;
            }
        }

        return -1;
    }

    public PageInfo GetPage(int pageIndex)
    {
        EnsureBuilt();
        return pageIndex >= 0 && pageIndex < _pages.Count ? _pages[pageIndex] : default;
    }

    public Chapter GetChapterForPage(int pageIndex)
    {
        EnsureBuilt();
        if (pageIndex < 0 || pageIndex >= _pages.Count) return null;

        int chapterIndex = _pages[pageIndex].chapterIndex;
        return chapterIndex >= 0 && chapterIndex < chapters.Count ? chapters[chapterIndex] : null;
    }

    /// <summary>Bir sayfadaki slot'a denk gelen level. Sayfa yarım doluysa null dönebilir.</summary>
    public LevelData GetLevelAt(int pageIndex, int slot)
    {
        EnsureBuilt();
        if (pageIndex < 0 || pageIndex >= _pages.Count) return null;

        PageInfo info = _pages[pageIndex];
        if (slot < 0 || slot >= info.levelCount) return null;

        return GetLevelAtIndex(info.firstLevelIndex + slot);
    }

    public LevelData GetPrevious(LevelData level)
    {
        return TryGetIndex(level, out int index) ? GetLevelAtIndex(index - 1) : null;
    }

    public LevelData GetNext(LevelData level)
    {
        return TryGetIndex(level, out int index) ? GetLevelAtIndex(index + 1) : null;
    }

    /// <summary>
    /// Oyuncunun HENÜZ TAMAMLAMADIĞI ilk level'ın indexi - Level Select ekranının
    /// varsayılan olarak açılacağı "sınır" (frontier). Hepsi tamamlanmışsa son
    /// level'ın indexini döner. Katalog boşsa -1.
    /// </summary>
    public int GetFrontierIndex()
    {
        EnsureBuilt();
        if (_ordered.Count == 0) return -1;

        for (int i = 0; i < _ordered.Count; i++)
        {
            if (!LevelProgress.IsLevelCompleted(_ordered[i])) return i;
        }

        return _ordered.Count - 1;
    }
}
