using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.SceneManagement;

/// <summary>
/// Sahnelere (Game.unity, MainMenu.unity) gömülü sabit TMP metinlerini (Resume, Pause,
/// Restart, Main Menu vb. - hiçbir script tarafından runtime'da set edilmeyen buton/panel
/// yazıları) bulup üzerlerine LocalizeStringEvent ekleyerek "UI" String Table'a bağlar.
///
/// EŞLEŞTİRME TAM METİN EŞİTLİĞİYLE yapılır (case-sensitive) ama bu TEK BAŞINA YETERLİ
/// BİR KORUMA DEĞİL: metni runtime'da script'in yazdığı bazı alanların sahnedeki
/// tasarım-zamanı placeholder'ı da aynı metindi (ör. "Three Star Time", "Music") ve
/// yanlışlıkla bağlanmışlardı. Bu yüzden ScriptDrivenObjects listesiyle GameObject adına
/// göre AYRICA hariç tutuyoruz - bkz. o listenin başındaki açıklama.
///
/// "CardCraft/Localization/Setup Locales And Tables" ÖNCE çalıştırılmış olmalı (String
/// Table'lar mevcut olmalı). Script idempotent'tir: tekrar çalıştırmak eksik olanları
/// bağlar, var olanların bozuk listener'ını onarır, hariç tutulanları temizler.
/// </summary>
public static class SceneLocalizationSetup
{
    private const string UITable = "UI";

    // PersistentListenerMode.EventDefined - UnityEvent'in KENDİ argümanını (yani
    // LocalizeStringEvent'in taşıdığı çeviriyi) hedef metoda aktaran tek mod.
    private const int EventDefinedMode = 0;

    // Sahnede birebir bu İngilizce metne sahip TMP_Text objelerini bulup UI tablosundaki
    // ilgili key'e bağlar. GameLocalization.GetUIString ile kullanılan key'lerle birebir aynı.
    private static readonly Dictionary<string, string> TextToKey = new Dictionary<string, string>
    {
        { "Resume", "ui_resume" },
        { "Pause", "ui_pause" },
        { "Restart", "ui_restart" },
        { "Main Menu", "ui_main_menu" },
        { "Levels", "ui_levels" },
        { "Settings", "ui_settings" },
        { "Play", "ui_play" },
        { "Quit", "ui_quit" },
        { "Back", "ui_back" },
        { "Music", "ui_music" },
        { "SFX", "ui_sfx" },
        { "Reset Progress", "ui_reset_progress" },
        { "Recipes", "ui_recipes" },
        { "Close", "ui_close" },
        { "Next Level", "ui_next_level" },
        { "Watch Ad: Continue", "ui_watch_ad_continue" },
        { "Welldone!", "ui_win_text" },
        { "Well done!", "ui_win_text" },
        { "Time's Out!", "ui_lose_text" },
        { "Tutorial", "ui_tutorial_label" },
        { "Kitchen", "ui_kitchen" },
        { "Medieval", "ui_medieval" },
        { "Language", "ui_language" },

        // NOT: "Ingredients", "Level Name", "Station Shuffle", "Total Duration",
        // "Three Star Time", "Two Star Time" BİLEREK burada yok. Bunlar LevelInfoPanelView'ın
        // satırlarının sahnedeki placeholder metinleriydi; o satırların gerçek içeriği
        // runtime'da level_info_* format string'lerinden üretiliyor ve etiketi zaten
        // kendi içinde taşıyor ("Time Limit: 2:00"). UI tablosundaki ui_*_label /
        // ui_ingredients key'leri artık kullanılmıyor.
    };

    /// <summary>
    /// Metni runtime'da SCRIPT tarafından yazılan TMP objeleri - üzerlerine asla
    /// LocalizeStringEvent eklenmemeli.
    ///
    /// Sahnedeki metinleri yalnızca tasarım-zamanı placeholder'ı olduğu için TextToKey ile
    /// eşleşebiliyorlardı. Bağlandıklarında panel açılırken (SetActive -> LocalizeStringEvent
    /// .OnEnable -> RefreshString) script'in yazdığı değerin ÜZERİNE sabit etiketi yazılıyor
    /// ve değer hiç görünmüyordu (ör. yıldız süreleri "Three Star Time" olarak kalıyordu).
    ///
    /// "Music"/"SFX" satırlarında iki ayrı obje var: etiket ("Music") bağlanmalı, yüzdeyi
    /// gösteren ("MusicText") bağlanmamalı - bu yüzden ayıklama metne değil ADA göre.
    /// </summary>
    private static readonly HashSet<string> ScriptDrivenObjects = new HashSet<string>
    {
        // LevelInfoPanelView
        "LevelNameText",
        "RawMaterialCountText",
        "StationShuffleText",
        "TotalDurationText",
        "ThreeStarTimeText",
        "TwoStarTimeText",
        // SettingsView (ses yüzdeleri: "80%")
        "MusicText",
        "SfxText",
    };

    [MenuItem("CardCraft/Localization/Wire Static Scene Text (Game + MainMenu)")]
    public static void WireAllScenes()
    {
        string activeScenePath = SceneManager.GetActiveScene().path;

        WireScene("Assets/Scenes/MainMenu.unity");
        WireScene("Assets/Scenes/Game.unity");

        if (!string.IsNullOrEmpty(activeScenePath))
        {
            EditorSceneManager.OpenScene(activeScenePath);
        }
    }

    private static void WireScene(string scenePath)
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        int wired = 0;
        int repaired = 0;
        int removed = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(includeInactive: true))
            {
                LocalizeStringEvent existing = label.GetComponent<LocalizeStringEvent>();

                if (ScriptDrivenObjects.Contains(label.gameObject.name))
                {
                    if (existing == null) continue;

                    UnityEngine.Object.DestroyImmediate(existing);
                    removed++;
                    continue;
                }

                if (existing != null)
                {
                    // Zaten bağlı - key'e dokunmadan sadece bozuk listener'ı onar.
                    if (!NeedsListenerRebind(existing)) continue;

                    BindListener(existing, label);
                    repaired++;
                    continue;
                }

                if (!TextToKey.TryGetValue(label.text, out string key)) continue;

                LocalizeStringEvent localizeEvent = label.gameObject.AddComponent<LocalizeStringEvent>();
                localizeEvent.StringReference.TableReference = UITable;
                localizeEvent.StringReference.TableEntryReference = key;

                BindListener(localizeEvent, label);
                wired++;
            }
        }

        if (wired > 0 || repaired > 0 || removed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log($"[SceneLocalizationSetup] {scenePath}: wired {wired}, repaired {repaired}, removed {removed}.");
    }

    /// <summary>
    /// OnUpdateString'i TMP_Text.SetText'e DİNAMİK (EventDefined) modda bağlar.
    ///
    /// Eskiden UnityEventTools.AddStringPersistentListener kullanılıyordu; o metot listener'ı
    /// PersistentListenerMode.String ile, yani KAYIT ANINDAKİ SABİT bir string argümanıyla
    /// kaydediyor. Sonuç: dil değişince LocalizeStringEvent çeviriyi OnUpdateString ile
    /// gönderiyor ama listener onu yok sayıp sahnedeki İngilizce metni geri yazıyordu -
    /// arayüz hiçbir zaman Türkçeye geçmiyordu.
    /// </summary>
    private static void BindListener(LocalizeStringEvent localizeEvent, TMP_Text label)
    {
        for (int i = localizeEvent.OnUpdateString.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(localizeEvent.OnUpdateString, i);
        }

        UnityEventTools.AddPersistentListener<string>(localizeEvent.OnUpdateString, label.SetText);

        EditorUtility.SetDirty(localizeEvent);
    }

    private static bool NeedsListenerRebind(LocalizeStringEvent localizeEvent)
    {
        var serialized = new SerializedObject(localizeEvent);
        SerializedProperty calls = serialized.FindProperty("m_UpdateString.m_PersistentCalls.m_Calls");

        if (calls == null || calls.arraySize != 1) return true;

        SerializedProperty mode = calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_Mode");
        return mode == null || mode.intValue != EventDefinedMode;
    }
}
