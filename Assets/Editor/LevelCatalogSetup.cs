using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// LevelCatalog asset'ini klasör yapısından üretir, level'ların kilit zincirini
/// (requiredPreviousLevel / nextLevel) katalog sırasına göre yeniden kurar ve
/// katalogda tutarsızlık var mı diye denetler.
///
/// 200 level'da bu alanları elle doldurmak imkânsız - zaten mevcut sahnede
/// Page_2'nin butonlarının hâlâ Kitchen level'larını göstermesi bunun elle
/// yapıldığında ne kadar çabuk bozulduğunun kanıtı.
///
/// Tüm menü komutları idempotent'tir, istediğin kadar tekrar çalıştırabilirsin.
/// </summary>
public static class LevelCatalogSetup
{
    private const string CatalogPath = "Assets/_Project/ScriptableObjects/LevelCatalog.asset";
    private const string LevelsRoot = "Assets/_Project/ScriptableObjects/Levels";

    // Tutorial, Level Select listesinde yer almaz - ana menüdeki kendi butonundan açılır.
    private static readonly HashSet<string> IgnoredFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Tutorial"
    };

    /// <summary>
    /// Bölümlerin oyuncuya sunulacağı SIRA - oyunun ilk level'ı Kitchen'ın ilk level'ı,
    /// Kitchen bitince Medieval, en son Alchemist.
    ///
    /// Klasör adlarına göre alfabetik sıralamaya GÜVENİLEMEZ: "Alchemist" alfabetik olarak
    /// en başa geçer ve oyunun ilk level'ı yanlış bölümden gelirdi.
    ///
    /// Yeni bir bölüm eklerken bu listeye de ekle. Listede olmayan klasörler sona,
    /// kendi aralarında alfabetik eklenir ve bir uyarı loglanır.
    /// </summary>
    private static readonly string[] ChapterOrder = { "Kitchen", "Medieval", "Alchemist" };

    [MenuItem("CardCraft/Levels/1 - Build Catalog From Folders")]
    public static void BuildCatalogFromFolders()
    {
        LevelCatalog catalog = LoadOrCreateCatalog();

        List<string> chapterFolders = new List<string>();
        foreach (string folder in Directory.GetDirectories(LevelsRoot))
        {
            if (!IgnoredFolders.Contains(Path.GetFileName(folder))) chapterFolders.Add(folder);
        }

        chapterFolders.Sort((a, b) =>
        {
            int orderA = ChapterOrderIndex(Path.GetFileName(a));
            int orderB = ChapterOrderIndex(Path.GetFileName(b));
            if (orderA != orderB) return orderA.CompareTo(orderB);

            return NaturalCompare(Path.GetFileName(a), Path.GetFileName(b));
        });

        // Var olan bölümlerin elle girilmiş ayarlarını (başlık anahtarı, arka plan)
        // KORUYORUZ - komut tekrar çalıştırıldığında sadece level listeleri tazelenir.
        Dictionary<string, LevelCatalog.Chapter> existing = new Dictionary<string, LevelCatalog.Chapter>(StringComparer.OrdinalIgnoreCase);
        foreach (LevelCatalog.Chapter chapter in catalog.chapters)
        {
            if (chapter != null && !string.IsNullOrEmpty(chapter.chapterId)) existing[chapter.chapterId] = chapter;
        }

        List<LevelCatalog.Chapter> rebuilt = new List<LevelCatalog.Chapter>();
        int totalLevels = 0;

        foreach (string folder in chapterFolders)
        {
            string chapterId = Path.GetFileName(folder);

            if (ChapterOrderIndex(chapterId) == int.MaxValue)
            {
                Debug.LogWarning($"[LevelCatalogSetup] '{chapterId}' klasörü ChapterOrder listesinde yok - " +
                                 "sona eklendi. Sırasını sabitlemek için LevelCatalogSetup.ChapterOrder'a ekle.");
            }

            List<LevelData> levels = LoadLevelsInFolder(folder);
            if (levels.Count == 0) continue;

            if (!existing.TryGetValue(chapterId, out LevelCatalog.Chapter chapter))
            {
                chapter = new LevelCatalog.Chapter
                {
                    chapterId = chapterId,
                    titleLocalizationKey = "ui_" + chapterId.ToLowerInvariant()
                };
            }

            chapter.levels = levels;
            rebuilt.Add(chapter);
            totalLevels += levels.Count;
        }

        catalog.chapters = rebuilt;
        catalog.InvalidateCache();

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        StringBuilder order = new StringBuilder();
        int runningIndex = 0;
        foreach (LevelCatalog.Chapter chapter in rebuilt)
        {
            LevelData first = chapter.levels[0];
            LevelData last = chapter.levels[chapter.levels.Count - 1];
            order.AppendLine($"  {chapter.chapterId}: level {runningIndex + 1}-{runningIndex + chapter.levels.Count} " +
                             $"({first.name} ... {last.name})");
            runningIndex += chapter.levels.Count;
        }

        Debug.Log($"[LevelCatalogSetup] Katalog kuruldu: {rebuilt.Count} bölüm, {totalLevels} level, " +
                  $"{catalog.PageCount} sayfa (sayfa başına {catalog.levelsPerPage}).\n" +
                  $"Bölüm sırası:\n{order}" +
                  "Şimdi '2 - Rebuild Level Chain From Catalog' çalıştırmayı unutma - kilit zinciri " +
                  "bu sıraya göre yeniden kurulur.");

        Selection.activeObject = catalog;
    }

    [MenuItem("CardCraft/Levels/2 - Rebuild Level Chain From Catalog")]
    public static void RebuildLevelChain()
    {
        LevelCatalog catalog = LoadCatalogOrWarn();
        if (catalog == null) return;

        IReadOnlyList<LevelData> ordered = catalog.Levels;
        int changed = 0;

        for (int i = 0; i < ordered.Count; i++)
        {
            LevelData level = ordered[i];
            LevelData previous = i > 0 ? ordered[i - 1] : null;
            LevelData next = i < ordered.Count - 1 ? ordered[i + 1] : null;

            SerializedObject serialized = new SerializedObject(level);
            SerializedProperty requiredProperty = serialized.FindProperty("requiredPreviousLevel");
            SerializedProperty nextProperty = serialized.FindProperty("nextLevel");

            bool dirty = false;

            if (requiredProperty.objectReferenceValue != previous)
            {
                Debug.Log($"[LevelCatalogSetup] {level.name}.requiredPreviousLevel: " +
                          $"{NameOf(requiredProperty.objectReferenceValue)} -> {NameOf(previous)}");
                requiredProperty.objectReferenceValue = previous;
                dirty = true;
            }

            if (nextProperty.objectReferenceValue != next)
            {
                Debug.Log($"[LevelCatalogSetup] {level.name}.nextLevel: " +
                          $"{NameOf(nextProperty.objectReferenceValue)} -> {NameOf(next)}");
                nextProperty.objectReferenceValue = next;
                dirty = true;
            }

            if (!dirty) continue;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            changed++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[LevelCatalogSetup] Kilit zinciri katalog sırasına göre kuruldu. {changed} level güncellendi.");
    }

    [MenuItem("CardCraft/Levels/3 - Validate Catalog")]
    public static void ValidateCatalog()
    {
        LevelCatalog catalog = LoadCatalogOrWarn();
        if (catalog == null) return;

        StringBuilder report = new StringBuilder();
        IReadOnlyList<LevelData> ordered = catalog.Levels;

        report.AppendLine($"[LevelCatalogSetup] Katalog denetimi: {ordered.Count} level, " +
                          $"{catalog.chapters.Count} bölüm, {catalog.PageCount} sayfa.");

        foreach (LevelCatalog.Chapter chapter in catalog.chapters)
        {
            if (chapter == null) continue;

            if (string.IsNullOrEmpty(chapter.titleLocalizationKey))
            {
                report.AppendLine($"  UYARI: '{chapter.chapterId}' bölümünün titleLocalizationKey'i boş - sayfa başlığı görünmez.");
            }

            if (chapter.levels != null && chapter.levels.Count % Mathf.Max(1, catalog.levelsPerPage) != 0)
            {
                report.AppendLine($"  NOT: '{chapter.chapterId}' bölümünde {chapter.levels.Count} level var, " +
                                  $"{catalog.levelsPerPage}'in katı değil - son sayfası yarım dolacak.");
            }
        }

        // Kimlik çakışmaları ilerleme kayıtlarını (PlayerPrefs) birbirine karıştırır.
        HashSet<string> identifiers = new HashSet<string>();
        foreach (LevelData level in ordered)
        {
            if (string.IsNullOrEmpty(level.levelId))
            {
                report.AppendLine($"  UYARI: '{level.name}' için levelId boş - kayıt anahtarı olarak asset adı kullanılacak.");
            }

            if (!identifiers.Add(LevelProgress.GetLevelIdentifier(level)))
            {
                report.AppendLine($"  HATA: '{level.name}' kimliği ({LevelProgress.GetLevelIdentifier(level)}) başka bir level ile ÇAKIŞIYOR.");
            }
        }

        // Diskte olup katalogda olmayan level'lar - Level Select'te hiç görünmezler.
        HashSet<LevelData> inCatalog = new HashSet<LevelData>(ordered);
        foreach (string guid in AssetDatabase.FindAssets("t:LevelData", new[] { LevelsRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null || level.isTutorial || inCatalog.Contains(level)) continue;

            report.AppendLine($"  UYARI: '{path}' katalogda YOK - oyuncuya hiç gösterilmeyecek.");
        }

        // Zincirin katalog sırasıyla uyuşmaması, kilitlerin yanlış açılması demek.
        for (int i = 0; i < ordered.Count; i++)
        {
            LevelData level = ordered[i];
            LevelData expectedPrevious = i > 0 ? ordered[i - 1] : null;
            LevelData expectedNext = i < ordered.Count - 1 ? ordered[i + 1] : null;

            if (level.requiredPreviousLevel != expectedPrevious)
            {
                report.AppendLine($"  HATA: '{level.name}'.requiredPreviousLevel katalog sırasıyla uyuşmuyor " +
                                  $"({NameOf(level.requiredPreviousLevel)} yerine {NameOf(expectedPrevious)} olmalı). " +
                                  "'2 - Rebuild Level Chain From Catalog' çalıştır.");
            }

            if (level.nextLevel != expectedNext)
            {
                report.AppendLine($"  HATA: '{level.name}'.nextLevel katalog sırasıyla uyuşmuyor " +
                                  $"({NameOf(level.nextLevel)} yerine {NameOf(expectedNext)} olmalı). " +
                                  "'2 - Rebuild Level Chain From Catalog' çalıştır.");
            }
        }

        Debug.Log(report.ToString());
    }

    // ------------------------------------------------------------------

    /// <summary>ChapterOrder'daki sıra numarası. Listede yoksa int.MaxValue (sona gider).</summary>
    private static int ChapterOrderIndex(string chapterId)
    {
        for (int i = 0; i < ChapterOrder.Length; i++)
        {
            if (string.Equals(ChapterOrder[i], chapterId, StringComparison.OrdinalIgnoreCase)) return i;
        }

        return int.MaxValue;
    }

    private static List<LevelData> LoadLevelsInFolder(string folder)
    {
        List<LevelData> levels = new List<LevelData>();

        foreach (string guid in AssetDatabase.FindAssets("t:LevelData", new[] { folder.Replace('\\', '/') }))
        {
            LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
            if (level != null && !level.isTutorial) levels.Add(level);
        }

        // FindAssets sırası garanti değil ve düz alfabetik sıralama "LD_kLevel10"u
        // "LD_kLevel2"nin ÖNÜNE koyar - bu yüzden rakam bloklarını sayı olarak
        // karşılaştıran doğal sıralama gerekiyor.
        levels.Sort((a, b) => NaturalCompare(a.name, b.name));
        return levels;
    }

    /// <summary>"LD_kLevel2" &lt; "LD_kLevel10" olacak şekilde karşılaştırır.</summary>
    private static int NaturalCompare(string a, string b)
    {
        int i = 0, j = 0;

        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int startA = i, startB = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;

                // Uzun sayılar int'i taşırabilir; string olarak karşılaştırmak için
                // baştaki sıfırları atıp önce UZUNLUĞA bakıyoruz.
                string numberA = a.Substring(startA, i - startA).TrimStart('0');
                string numberB = b.Substring(startB, j - startB).TrimStart('0');

                if (numberA.Length != numberB.Length) return numberA.Length - numberB.Length;

                int numeric = string.CompareOrdinal(numberA, numberB);
                if (numeric != 0) return numeric;
                continue;
            }

            int comparison = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
            if (comparison != 0) return comparison;

            i++;
            j++;
        }

        return (a.Length - i) - (b.Length - j);
    }

    private static LevelCatalog LoadOrCreateCatalog()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog != null) return catalog;

        catalog = ScriptableObject.CreateInstance<LevelCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"[LevelCatalogSetup] '{CatalogPath}' bulunamadı, yenisi oluşturuldu.");
        return catalog;
    }

    private static LevelCatalog LoadCatalogOrWarn()
    {
        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"[LevelCatalogSetup] '{CatalogPath}' yok. Önce " +
                           "'1 - Build Catalog From Folders' çalıştır.");
            return null;
        }

        catalog.InvalidateCache();
        return catalog;
    }

    private static string NameOf(UnityEngine.Object obj) => obj == null ? "(boş)" : obj.name;
}
