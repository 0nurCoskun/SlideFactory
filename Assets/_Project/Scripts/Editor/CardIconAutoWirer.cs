using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/_Project/Textures/CardIcons/ altında bulunan PNG dosyalarını,
/// dosya adı (uzantısız) CardData.cardId ile eşleşen kartlara otomatik olarak
/// icon olarak bağlar. Sadece icon alanı boş (null) olan kartlar güncellenir,
/// mevcut bir icon asla üzerine yazılmaz.
///
/// Komut satırından çalıştırmak için:
/// Unity.exe -batchmode -nographics -quit -projectPath . -executeMethod CardIconAutoWirer.WireAllFromCommandLine -logFile <path>
/// </summary>
public static class CardIconAutoWirer
{
    private const string CardIconsRoot = "Assets/_Project/Textures/CardIcons";
    private const string CardsRoot = "Assets/_Project/ScriptableObjects/Cards";

    [MenuItem("CardCraft/Auto-Wire Card Icons")]
    public static void WireAllFromMenu()
    {
        WireAll();
    }

    public static void WireAllFromCommandLine()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        WireAll();
    }

    private static void WireAll()
    {
        var pngPaths = Directory.GetFiles(CardIconsRoot, "*.png", SearchOption.AllDirectories);
        var iconByCardId = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var pngPath in pngPaths)
        {
            var unityPath = pngPath.Replace('\\', '/');
            var cardId = Path.GetFileNameWithoutExtension(unityPath);
            iconByCardId[cardId] = unityPath;
            EnsureSpriteImportSettings(unityPath);
        }

        var cardGuids = AssetDatabase.FindAssets("t:CardData", new[] { CardsRoot });
        int wired = 0;
        int alreadySet = 0;
        int noMatch = 0;

        foreach (var guid in cardGuids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var card = AssetDatabase.LoadAssetAtPath<CardData>(assetPath);
            if (card == null) continue;

            if (card.icon != null)
            {
                alreadySet++;
                continue;
            }

            if (string.IsNullOrEmpty(card.cardId) || !iconByCardId.TryGetValue(card.cardId, out var pngPath))
            {
                noMatch++;
                continue;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[CardIconAutoWirer] {pngPath} bir Sprite olarak import edilmemiş (Texture Type ayarını kontrol et).");
                continue;
            }

            card.icon = sprite;
            EditorUtility.SetDirty(card);
            wired++;
            Debug.Log($"[CardIconAutoWirer] {card.cardId} -> {pngPath}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[CardIconAutoWirer] Tamamlandı. Bağlanan: {wired}, zaten doluydu: {alreadySet}, eşleşme yok: {noMatch}");
    }

    /// <summary>
    /// Yeni eklenen PNG'ler Unity'nin varsayılan Texture Type'ıyla (Sprite değil) import
    /// edilmiş olabilir. Bu durumda LoadAssetAtPath&lt;Sprite&gt; null döner. Bu metod,
    /// gerekirse Texture Type'ı Sprite'a çevirip mevcut Kitchen ikonlarıyla aynı ayarlarla
    /// yeniden import eder.
    /// </summary>
    private static void EnsureSpriteImportSettings(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }
        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }
        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }
        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }
        if (importer.spritePixelsPerUnit != 100)
        {
            importer.spritePixelsPerUnit = 100;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
            Debug.Log($"[CardIconAutoWirer] Import ayarları düzeltildi: {assetPath}");
        }
    }
}
