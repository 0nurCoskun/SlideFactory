using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// Level Select'teki TEK BİR SAYFA. Bir bölüm başlığı (Kitchen, Workshop...) ve
/// altında level butonlarının dizildiği bir GridLayoutGroup taşır.
///
/// Sayfa prefab'dan üretilir; hangi bölüme ait olduğunu Configure() ile, hangi
/// butonları göstereceğini ise Populate() ile öğrenir. Butonlar SAYFA GÖRÜNÜR
/// ALANA YAKLAŞINCA basılır (bkz. LevelSelectView) - 200 level'ın 2000 UI objesini
/// aynı anda sahnede tutmamak için.
/// </summary>
public class LevelPageView : MonoBehaviour
{
    [Header("Başlık")]
    [SerializeField] private TMP_Text headerText;

    [Tooltip("Başlığın üzerindeki LocalizeStringEvent. Atanırsa bölüm adı doğrudan " +
             "String Table'a bağlanır ve dil değişince kendiliğinden güncellenir.")]
    [SerializeField] private LocalizeStringEvent headerLocalize;

    [Header("İçerik")]
    [Tooltip("Level butonlarının ebeveyni - GridLayoutGroup'un olduğu obje.")]
    [SerializeField] private RectTransform buttonContainer;

    [Tooltip("Opsiyonel - bölüme özel arka plan atanacaksa.")]
    [SerializeField] private Image backgroundImage;

    private readonly List<LevelButton> _buttons = new List<LevelButton>();

    public RectTransform ButtonContainer => buttonContainer;

    /// <summary>Bu sayfanın butonları şu an basılı mı? LevelSelectView tembel yüklemede buna bakar.</summary>
    public bool IsPopulated { get; private set; }

    /// <summary>Sayfanın hangi bölüme ait olduğunu (başlık + arka plan) uygular.</summary>
    public void Configure(LevelCatalog.Chapter chapter)
    {
        if (chapter == null) return;

        ApplyTitle(chapter.titleLocalizationKey);

        if (backgroundImage != null && chapter.pageBackground != null)
        {
            backgroundImage.sprite = chapter.pageBackground;
        }
    }

    private void ApplyTitle(string localizationKey)
    {
        if (string.IsNullOrEmpty(localizationKey)) return;

        if (headerLocalize != null)
        {
            // Tercih edilen yol: metni biz yazmıyoruz, String Table referansını
            // değiştiriyoruz. Dil değiştiğinde LocalizeStringEvent kendi kendine
            // yeniden çeviriyor - ayrıca OnLanguageChanged dinlemeye gerek kalmıyor.
            headerLocalize.StringReference.SetReference(GameLocalization.UITable, localizationKey);
            headerLocalize.RefreshString();
            return;
        }

        // LocalizeStringEvent yoksa (prefab eksik kurulmuşsa) en azından metni yazalım.
        if (headerText != null) headerText.text = GameLocalization.GetUIString(localizationKey);
    }

    /// <summary>
    /// Bu sayfaya düşen level'lar için buton üretir. Butonlar dışarıdan verilen
    /// "rent" fabrikasından gelir (LevelSelectView'ın havuzu) - sayfa değiştikçe
    /// sürekli Instantiate/Destroy yapmamak için.
    /// </summary>
    public void Populate(LevelCatalog catalog, int pageIndex, Func<RectTransform, LevelButton> rent, LevelInfoPanelView infoPanel)
    {
        if (IsPopulated || catalog == null || rent == null || buttonContainer == null) return;
        IsPopulated = true;

        LevelCatalog.PageInfo page = catalog.GetPage(pageIndex);

        for (int slot = 0; slot < page.levelCount; slot++)
        {
            int levelIndex = page.firstLevelIndex + slot;
            LevelData level = catalog.GetLevelAtIndex(levelIndex);
            if (level == null) continue;

            LevelButton button = rent(buttonContainer);
            if (button == null) continue;

            // Havuzdan gelen buton sıraya en sona eklenmeli, yoksa GridLayoutGroup
            // level'ları karışık sırada dizer.
            button.transform.SetAsLastSibling();
            button.Bind(level, levelIndex + 1, infoPanel);

            _buttons.Add(button);
        }
    }

    /// <summary>Sayfa görünür pencereden çıkınca butonlarını havuza geri verir.</summary>
    public void Clear(Action<LevelButton> release)
    {
        if (!IsPopulated) return;
        IsPopulated = false;

        foreach (LevelButton button in _buttons)
        {
            if (button == null) continue;
            release?.Invoke(button);
        }

        _buttons.Clear();
    }
}
