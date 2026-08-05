using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Level Select ekranını "sahneye elle dizilmiş 20 buton"dan "katalogdan runtime'da
/// üretilen sayfalar"a taşıyan editör araçları.
///
/// İki adım BİLEREK ayrıldı:
///   1 - Extract Prefabs From Scene   : YIKICI DEĞİL. Mevcut Level1Button ve Page_1'den
///                                      prefab üretir, sahneye dokunmaz. Prefab'ları
///                                      Prefab Mode'da gözden geçirebilirsin.
///   2 - Convert Scene To Runtime Pages: YIKICI. Page_1/Page_2'yi (20 butonla birlikte)
///                                      siler ve LevelSelectView'ı kurar.
///
/// 2. adımı çalıştırmadan ÖNCE projeyi commit'lemiş ol.
/// </summary>
public static class LevelSelectSetup
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string PrefabFolder = "Assets/_Project/Prefabs/UI/LevelSelect";
    private const string LevelButtonPrefabPath = PrefabFolder + "/LevelButton.prefab";
    private const string LevelPagePrefabPath = PrefabFolder + "/LevelPage.prefab";
    private const string CatalogPath = "Assets/_Project/ScriptableObjects/LevelCatalog.asset";
    private const string ArrowLeftSpritePath = "Assets/ThirdParty/kenney_ui-pack-rpg-expansion/PNG/arrowBrown_left.png";
    private const string ArrowRightSpritePath = "Assets/ThirdParty/kenney_ui-pack-rpg-expansion/PNG/arrowBrown_right.png";

    // SceneLocalizationSetup, metni birebir eşleşen TMP objelerine LocalizeStringEvent
    // ekliyor ("Kitchen" -> ui_kitchen gibi). Prefab'lardaki placeholder metinler o
    // tablodaki HİÇBİR metinle eşleşmemeli, yoksa sahneye düşen bir kopya yanlış
    // anahtara bağlanabilir.
    private const string ButtonNumberPlaceholder = "0";
    private const string ChapterTitlePlaceholder = "Chapter";

    [MenuItem("CardCraft/Level Select/1 - Extract Prefabs From Scene")]
    public static void ExtractPrefabs()
    {
        string previousScene = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

        Transform page1 = FindInScene(scene, "Canvas", "LevelSelectPanel", "Viewport", "Content", "Page_1");
        if (page1 == null)
        {
            Debug.LogError("[LevelSelectSetup] 'Canvas/LevelSelectPanel/Viewport/Content/Page_1' bulunamadı. " +
                           "Sahne zaten dönüştürülmüş olabilir.");
            return;
        }

        Transform buttonContainer = page1.Find("ButtonContainer");
        Transform level1Button = buttonContainer != null ? buttonContainer.Find("Level1Button") : null;

        if (buttonContainer == null || level1Button == null)
        {
            Debug.LogError("[LevelSelectSetup] Page_1 altında 'ButtonContainer/Level1Button' bulunamadı.");
            return;
        }

        EnsurePrefabFolder();

        bool buttonOk = ExtractButtonPrefab(level1Button.gameObject);
        bool pageOk = ExtractPagePrefab(page1.gameObject);

        // Geçici kopyalar yok edildi ama sahne yine de "dirty" işaretlendi - kaydedip
        // temiz bırakıyoruz ki sonraki OpenScene bir kaydetme uyarısı çıkarmasın.
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(previousScene) && previousScene != MainMenuScenePath)
        {
            EditorSceneManager.OpenScene(previousScene);
        }

        if (buttonOk && pageOk)
        {
            Debug.Log($"[LevelSelectSetup] Prefab'lar yazıldı:\n  {LevelButtonPrefabPath}\n  {LevelPagePrefabPath}\n" +
                      "Prefab Mode'da kontrol et (numara metni, yıldızlar, 2 sütunlu grid), sonra " +
                      "'2 - Convert Scene To Runtime Pages' çalıştır.");
        }
    }

    [MenuItem("CardCraft/Level Select/2 - Convert Scene To Runtime Pages")]
    public static void ConvertScene()
    {
        string previousScene = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

        // ASSET'LER SAHNE AÇILDIKTAN SONRA YÜKLENMELİ. OpenScene(Single) önceki sahneyi
        // kapatırken kullanılmayan asset'leri bellekten atıyor; sahneden ÖNCE yüklenen
        // referanslar bu sırada geçersizleşiyor ve SerializedProperty'ye yazıldığında
        // sessizce fileID 0 (boş) olarak kaydediliyorlardı.
        LevelButton buttonPrefab = LoadPrefabComponent<LevelButton>(LevelButtonPrefabPath);
        LevelPageView pagePrefab = LoadPrefabComponent<LevelPageView>(LevelPagePrefabPath);

        if (buttonPrefab == null || pagePrefab == null)
        {
            Debug.LogError("[LevelSelectSetup] Prefab'lar yok/okunamadı. Önce " +
                           "'1 - Extract Prefabs From Scene' çalıştır.");
            return;
        }

        LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"[LevelSelectSetup] '{CatalogPath}' yok. Önce " +
                           "'CardCraft/Levels/1 - Build Catalog From Folders' çalıştır.");
            return;
        }

        Transform levelSelectPanel = FindInScene(scene, "Canvas", "LevelSelectPanel");
        Transform viewport = levelSelectPanel != null ? levelSelectPanel.Find("Viewport") : null;
        Transform content = viewport != null ? viewport.Find("Content") : null;

        if (levelSelectPanel == null || viewport == null || content == null)
        {
            Debug.LogError("[LevelSelectSetup] 'Canvas/LevelSelectPanel/Viewport/Content' bulunamadı.");
            return;
        }

        // Elle dizilmiş sayfaları ve 20 butonu sil - artık runtime'da üretiliyorlar.
        int removed = content.childCount;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(content.GetChild(i).gameObject);
        }

        LevelSelectView view = levelSelectPanel.GetComponent<LevelSelectView>();
        if (view == null) view = levelSelectPanel.gameObject.AddComponent<LevelSelectView>();

        SerializedObject serialized = new SerializedObject(view);
        serialized.FindProperty("catalog").objectReferenceValue = catalog;
        serialized.FindProperty("pageScrollSnap").objectReferenceValue = viewport.GetComponent<PageScrollSnap>();
        // Sahnede her ikisinden de tek bir tane var (kök objeler), sıra önemsiz.
        serialized.FindProperty("pageIndicatorManager").objectReferenceValue =
            Object.FindAnyObjectByType<PageIndicatorManager>(FindObjectsInactive.Include);
        serialized.FindProperty("contentRoot").objectReferenceValue = content as RectTransform;
        serialized.FindProperty("levelInfoPanelView").objectReferenceValue =
            Object.FindAnyObjectByType<LevelInfoPanelView>(FindObjectsInactive.Include);
        serialized.FindProperty("levelPagePrefab").objectReferenceValue = pagePrefab;
        serialized.FindProperty("levelButtonPrefab").objectReferenceValue = buttonPrefab;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // Yazdıktan sonra GERÇEKTEN oturmuş mu diye doğruluyoruz - boş kalan bir referans
        // ancak oyun çalışırken fark edilirdi.
        string missing = VerifyWiring(view);
        if (missing != null)
        {
            Debug.LogError($"[LevelSelectSetup] Şu alanlar bağlanamadı: {missing}. " +
                           "Inspector'dan elle atayabilirsin.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (!string.IsNullOrEmpty(previousScene) && previousScene != MainMenuScenePath)
        {
            EditorSceneManager.OpenScene(previousScene);
        }

        Debug.Log($"[LevelSelectSetup] Sahne dönüştürüldü: Content'ten {removed} elle dizilmiş sayfa silindi, " +
                  "LevelSelectPanel'e LevelSelectView eklendi ve referansları bağlandı.\n" +
                  "KALAN ELLE ADIM: PageIndicatorManager üzerinde maxDots'u ayarla ve istersen " +
                  "pageCounterText / prevPageButton / nextPageButton alanlarını doldur.");
    }

    [MenuItem("CardCraft/Level Select/3 - Create Page Nav Controls")]
    public static void CreatePageNavControls()
    {
        string previousScene = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

        Transform panel = FindInScene(scene, "Canvas", "LevelSelectPanel");
        if (panel == null)
        {
            Debug.LogError("[LevelSelectSetup] 'Canvas/LevelSelectPanel' bulunamadı.");
            return;
        }

        PageIndicatorManager indicator = Object.FindAnyObjectByType<PageIndicatorManager>(FindObjectsInactive.Include);
        if (indicator == null)
        {
            Debug.LogError("[LevelSelectSetup] Sahnede PageIndicatorManager yok.");
            return;
        }

        // ÖNEMLİ: Bu objeler PageIndicators_Holder'ın ALTINA konulmamalı. Holder'ın tüm
        // çocukları CreateIndicators() içinde silinip nokta olarak yeniden üretiliyor -
        // oraya konan ok butonları ilk Rebuild'de yok olurdu. Bu yüzden holder'ın
        // KARDEŞİ olarak LevelSelectPanel'in altına ekleniyorlar.
        Button prev = CreateArrowButton(panel, "PrevPageButton", ArrowLeftSpritePath, new Vector2(-300f, -65f));
        Button next = CreateArrowButton(panel, "NextPageButton", ArrowRightSpritePath, new Vector2(300f, -65f));
        TMP_Text counter = CreateCounterText(panel, "PageCounterText", new Vector2(0f, -150f));

        SerializedObject serialized = new SerializedObject(indicator);
        serialized.FindProperty("prevPageButton").objectReferenceValue = prev;
        serialized.FindProperty("nextPageButton").objectReferenceValue = next;
        serialized.FindProperty("pageCounterText").objectReferenceValue = counter;
        serialized.FindProperty("maxDots").intValue = Mathf.Max(1, serialized.FindProperty("maxDots").intValue);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (!string.IsNullOrEmpty(previousScene) && previousScene != MainMenuScenePath)
        {
            EditorSceneManager.OpenScene(previousScene);
        }

        Debug.Log("[LevelSelectSetup] Sayfa gezinme kontrolleri kuruldu ve PageIndicatorManager'a bağlandı:\n" +
                  "  PrevPageButton / NextPageButton / PageCounterText (LevelSelectPanel altında).\n" +
                  "KONUMLARI TAHMİNİ - Scene view'da sürükleyerek istediğin yere al. Sayaç yalnızca " +
                  $"sayfa sayısı maxDots'u ({indicator.maxDots}) aşınca görünür.");

        Selection.activeGameObject = prev.gameObject;
    }

    /// <summary>Varsa mevcut objeyi tazeler, yoksa oluşturur - komut tekrar çalıştırılabilir.</summary>
    private static Button CreateArrowButton(Transform parent, string objectName, string spritePath, Vector2 anchoredPosition)
    {
        Transform existing = parent.Find(objectName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform));

        if (existing == null)
        {
            go.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            // Kaynak sprite 22x21 piksel - parmakla basılabilir olması için büyütülüyor.
            rect.sizeDelta = new Vector2(88f, 84f);
        }

        Image image = GetOrAdd<Image>(go);
        image.sprite = LoadFirstSprite(spritePath);
        image.preserveAspect = true;

        Button button = GetOrAdd<Button>(go);
        button.targetGraphic = image;

        // Ses, diğer butonlarla aynı desende: UIButtonSound + OnClick'e kalıcı listener.
        // Sayfa değiştirme listener'ını PageIndicatorManager runtime'da kendisi ekliyor.
        UIButtonSound sound = GetOrAdd<UIButtonSound>(go);
        if (!HasPersistentCall(button, sound))
        {
            UnityEventTools.AddPersistentListener(button.onClick, sound.PlayClickSound);
        }

        EditorUtility.SetDirty(go);
        return button;
    }

    private static TMP_Text CreateCounterText(Transform parent, string objectName, Vector2 anchoredPosition)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null) return existing.GetComponent<TMP_Text>();

        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(320f, 70f);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 36f;
        text.text = "1 / 1";

        // Yazı tipini/rengini paneldeki mevcut bir metinden kopyalıyoruz ki ekranın
        // geri kalanıyla uyumsuz görünmesin.
        TMP_Text reference = parent.GetComponentInChildren<TMP_Text>(true);
        if (reference != null && reference != text)
        {
            text.font = reference.font;
            text.color = reference.color;
        }

        EditorUtility.SetDirty(go);
        return text;
    }

    /// <summary>
    /// Multiple sprite modundaki PNG'lerde kullanılabilir Sprite, texture'ın ALT ASSET'idir
    /// (Project penceresinde oku açınca görünen "arrowBrown_left_0"). LoadAssetAtPath&lt;Sprite&gt;
    /// bu durumda güvenilir olmadığı için tüm alt asset'ler taranıyor.
    /// </summary>
    private static Sprite LoadFirstSprite(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Sprite sprite) return sprite;
        }

        Debug.LogError($"[LevelSelectSetup] '{path}' içinde Sprite bulunamadı. Texture Type " +
                       "'Sprite (2D and UI)' mi kontrol et.");
        return null;
    }

    private static bool HasPersistentCall(Button button, Object target)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == target) return true;
        }

        return false;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Prefab'dan bir component referansı alır. LoadAssetAtPath'i doğrudan bir Component
    /// tipiyle çağırmak prefab'larda güvenilir değil - prefab'ın ANA asset'i GameObject'tir,
    /// bu yüzden önce onu yükleyip component'i üzerinden alıyoruz.
    /// </summary>
    private static T LoadPrefabComponent<T>(string path) where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return prefab != null ? prefab.GetComponent<T>() : null;
    }

    /// <summary>Boş kalan alanların isimlerini döner, hepsi doluysa null.</summary>
    private static string VerifyWiring(LevelSelectView view)
    {
        string[] fields =
        {
            "catalog", "pageScrollSnap", "pageIndicatorManager",
            "contentRoot", "levelInfoPanelView", "levelPagePrefab", "levelButtonPrefab"
        };

        SerializedObject serialized = new SerializedObject(view);
        var empty = fields.Where(f => serialized.FindProperty(f).objectReferenceValue == null).ToArray();

        return empty.Length == 0 ? null : string.Join(", ", empty);
    }

    private static bool ExtractButtonPrefab(GameObject source)
    {
        GameObject temp = Object.Instantiate(source, source.transform.parent);
        temp.name = "LevelButton";

        try
        {
            LevelButton levelButton = temp.GetComponent<LevelButton>();
            if (levelButton == null)
            {
                Debug.LogError("[LevelSelectSetup] Level1Button üzerinde LevelButton component'i yok.");
                return false;
            }

            // Butonun üzerindeki numara metni: DOĞRUDAN çocuklardaki TMP_Text
            // (StarsContainer'ın altındakiler değil).
            TMP_Text numberText = DirectChildComponents<TMP_Text>(temp.transform).FirstOrDefault();
            if (numberText == null)
            {
                Debug.LogError("[LevelSelectSetup] Level1Button altında numara için bir TMP_Text bulunamadı.");
                return false;
            }

            numberText.text = ButtonNumberPlaceholder;

            SerializedObject serialized = new SerializedObject(levelButton);
            serialized.FindProperty("numberText").objectReferenceValue = numberText;
            // levelData ve levelInfoPanelView artık runtime'da Bind() ile geliyor.
            serialized.FindProperty("levelData").objectReferenceValue = null;
            serialized.FindProperty("levelInfoPanelView").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(temp, LevelButtonPrefabPath);
            return true;
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
    }

    private static bool ExtractPagePrefab(GameObject source)
    {
        GameObject temp = Object.Instantiate(source, source.transform.parent);
        temp.name = "LevelPage";

        try
        {
            Transform buttonContainer = temp.transform.Find("ButtonContainer");
            if (buttonContainer == null)
            {
                Debug.LogError("[LevelSelectSetup] Page_1 altında 'ButtonContainer' bulunamadı.");
                return false;
            }

            // Elle dizilmiş 10 butonu at - sayfa runtime'da doldurulacak.
            for (int i = buttonContainer.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(buttonContainer.GetChild(i).gameObject);
            }

            // Sütun sayısı şu an container GENİŞLİĞİNDEN türetiliyor (Constraint: Flexible).
            // 200 level'da bunu şansa bırakmak istemiyoruz - 2 sütuna sabitliyoruz.
            GridLayoutGroup grid = buttonContainer.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 2;
            }

            // Page_2'nin ButtonContainer'ında olup Page_1'inkinde olmayan bir LayoutElement
            // vardı - iki sayfa zaten birbirinden ayrışmış durumda. Prefab tek ve
            // kesin bir sürüm olmalı.
            LayoutElement strayLayoutElement = buttonContainer.GetComponent<LayoutElement>();
            if (strayLayoutElement != null) Object.DestroyImmediate(strayLayoutElement);

            // Başlık: sayfanın DOĞRUDAN çocuklarındaki TMP_Text (ör. "Kitchen").
            TMP_Text headerText = DirectChildComponents<TMP_Text>(temp.transform).FirstOrDefault();
            if (headerText == null)
            {
                Debug.LogError("[LevelSelectSetup] Page_1 altında başlık için bir TMP_Text bulunamadı.");
                return false;
            }

            headerText.gameObject.name = "ChapterTitle";
            headerText.text = ChapterTitlePlaceholder;

            // LocalizeStringEvent KORUNUYOR - LevelPageView, bölümün anahtarını
            // StringReference üzerinden değiştiriyor, böylece dil değişimi bedava geliyor.
            LocalizeStringEvent headerLocalize = headerText.GetComponent<LocalizeStringEvent>();
            if (headerLocalize == null)
            {
                Debug.LogWarning("[LevelSelectSetup] Sayfa başlığında LocalizeStringEvent yok - bölüm adı " +
                                 "GameLocalization üzerinden yazılacak ve dil değişiminde kendiliğinden tazelenmeyecek.");
            }

            LevelPageView pageView = temp.GetComponent<LevelPageView>();
            if (pageView == null) pageView = temp.AddComponent<LevelPageView>();

            SerializedObject serialized = new SerializedObject(pageView);
            serialized.FindProperty("headerText").objectReferenceValue = headerText;
            serialized.FindProperty("headerLocalize").objectReferenceValue = headerLocalize;
            serialized.FindProperty("buttonContainer").objectReferenceValue = buttonContainer as RectTransform;
            serialized.FindProperty("backgroundImage").objectReferenceValue = temp.GetComponent<Image>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(temp, LevelPagePrefabPath);
            return true;
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
    }

    private static System.Collections.Generic.IEnumerable<T> DirectChildComponents<T>(Transform parent) where T : Component
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            T component = parent.GetChild(i).GetComponent<T>();
            if (component != null) yield return component;
        }
    }

    /// <summary>
    /// Verilen isim zincirini sahnede arar ve son halkanın Transform'unu döner.
    ///
    /// Zincirin İLK halkası kök obje OLMAK ZORUNDA DEĞİL - bu sahnede örneğin "Canvas",
    /// "Main Camera"nın altında duruyor. Bu yüzden ilk isim tüm hiyerarşide (pasif objeler
    /// dahil) aranır, sonra oradan aşağı inilir. GameObject.Find pasif objeleri bulamadığı
    /// için kullanılmıyor.
    /// </summary>
    private static Transform FindInScene(Scene scene, params string[] path)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != path[0]) continue;

                Transform current = candidate;
                for (int i = 1; i < path.Length && current != null; i++)
                {
                    current = current.Find(path[i]);
                }

                if (current != null) return current;
            }
        }

        return null;
    }

    private static void EnsurePrefabFolder()
    {
        if (AssetDatabase.IsValidFolder(PrefabFolder)) return;
        AssetDatabase.CreateFolder("Assets/_Project/Prefabs/UI", "LevelSelect");
    }
}
