using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Combo/puan sistemini Game sahnesine bağlayan editör aracı.
/// Sahne YAML'ını elle kurcalamak yerine Unity'nin kendi API'siyle çalışır.
/// </summary>
public static class ComboScoreWiringTool
{
    private const string GameScenePath = "Assets/Scenes/Game.unity";

    /// <summary>
    /// Combo/puan sistemini Game sahnesine bağlar. TEKRAR ÇALIŞTIRILABİLİR (idempotent):
    /// zaten var olan objeleri/komponentleri yeniden oluşturmaz, sadece eksikleri tamamlar.
    /// </summary>
    [MenuItem("CardCraft/Combo Score/Wire Up Game Scene")]
    public static void Wire()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        StringBuilder log = new StringBuilder();
        log.AppendLine("===== WIRE BEGIN =====");

        GameManager gameManager = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        LevelResultView resultView = Object.FindAnyObjectByType<LevelResultView>(FindObjectsInactive.Include);

        if (gameManager == null || resultView == null)
        {
            Debug.LogError("[Wire] GameManager ya da LevelResultView bulunamadı - işlem iptal.");
            return;
        }

        // --- 1) ScoreManager: GameManager ile AYNI objeye (her zaman aktif) ---
        ScoreManager scoreManager = Object.FindAnyObjectByType<ScoreManager>(FindObjectsInactive.Include);
        if (scoreManager == null)
        {
            scoreManager = gameManager.gameObject.AddComponent<ScoreManager>();
            log.AppendLine($"+ ScoreManager eklendi: {GetPath(scoreManager.transform)}");
        }
        else
        {
            log.AppendLine($"= ScoreManager zaten var: {GetPath(scoreManager.transform)}");
        }

        SetRef(scoreManager, "gameManager", gameManager, log);
        SetRef(gameManager, "scoreManager", scoreManager, log);
        SetRef(resultView, "scoreManager", scoreManager, log);

        // --- 2) Win / Lose panellerine skor metinleri ---
        GameObject winPanel = GetRefObject(resultView, "winPanel");
        GameObject losePanel = GetRefObject(resultView, "losePanel");

        if (winPanel != null)
        {
            TMP_Text style = FindStyleTemplate(winPanel.transform);
            TMP_Text winScore = EnsureText(winPanel.transform, "ScoreText", style, 64f, "Score: 0", log);
            TMP_Text winBest = EnsureText(winPanel.transform, "BestScoreText", style, 44f, "Best: 0", log);
            TMP_Text badge = EnsureText(winPanel.transform, "NewRecordBadge", style, 52f, "NEW RECORD!", log);

            LayoutBetween(winPanel.transform, winScore, winBest, badge, log);

            SetRef(resultView, "winScoreText", winScore, log);
            SetRef(resultView, "winBestScoreText", winBest, log);
            SetRef(resultView, "newRecordBadge", badge.gameObject, log);

            // Rozet kapalı başlamalı (Awake da kapatıyor ama sahnede de doğru dursun).
            badge.gameObject.SetActive(false);
        }

        if (losePanel != null)
        {
            TMP_Text style = FindStyleTemplate(losePanel.transform);
            TMP_Text loseScore = EnsureText(losePanel.transform, "ScoreText", style, 64f, "Score: 0", log);
            TMP_Text loseBest = EnsureText(losePanel.transform, "BestScoreText", style, 44f, "Best: 0", log);

            LayoutBetween(losePanel.transform, loseScore, loseBest, null, log);

            SetRef(resultView, "loseScoreText", loseScore, log);
            SetRef(resultView, "loseBestScoreText", loseBest, log);
        }

        // --- 3) Oynanış HUD'ı: Timer objesini klonlayıp stil/yerleşimi devral ---
        LevelTimerView timerView = Object.FindAnyObjectByType<LevelTimerView>(FindObjectsInactive.Include);
        if (timerView != null)
        {
            BuildHud(timerView, gameManager, scoreManager, log);
        }
        else
        {
            log.AppendLine("! LevelTimerView bulunamadı - HUD atlandı.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Sahne KAYDEDİLDİ.");
        log.AppendLine("===== WIRE END =====");
        Debug.Log(log.ToString());
    }

    /// <summary>
    /// Timer objesinin bir kopyasından skor HUD'ı kurar - böylece font, arka plan ve
    /// anchor ayarları elle taklit edilmeden birebir devralınır.
    /// </summary>
    private static void BuildHud(LevelTimerView timerView, GameManager gameManager, ScoreManager scoreManager, StringBuilder log)
    {
        // TimerText'in kendisi değil, ONU TAŞIYAN kapsayıcı (arka planlı "Timer") klonlanır.
        Transform timerRoot = timerView.transform.parent != null ? timerView.transform.parent : timerView.transform;
        Transform parent = timerRoot.parent;

        Transform existing = parent != null ? parent.Find("ScoreDisplay") : null;
        GameObject hudGo;

        if (existing != null)
        {
            hudGo = existing.gameObject;
            log.AppendLine("= ScoreDisplay zaten var, yeniden kullanılıyor.");
        }
        else
        {
            hudGo = Object.Instantiate(timerRoot.gameObject, parent);
            hudGo.name = "ScoreDisplay";
            hudGo.SetActive(true);
            log.AppendLine("+ ScoreDisplay oluşturuldu (Timer klonu).");
        }

        // Klonun içindeki zamanlayıcı davranışını sök - bu obje artık skoru gösteriyor.
        foreach (LevelTimerView stray in hudGo.GetComponentsInChildren<LevelTimerView>(true))
        {
            Object.DestroyImmediate(stray);
        }

        TMP_Text[] texts = hudGo.GetComponentsInChildren<TMP_Text>(true);
        if (texts.Length == 0)
        {
            log.AppendLine("! ScoreDisplay içinde TMP metni yok - HUD tamamlanamadı.");
            return;
        }

        TMP_Text scoreText = texts[0];
        scoreText.gameObject.name = "ScoreText";
        StripLocalization(scoreText.gameObject);
        scoreText.text = "Score: 0";
        scoreText.enableAutoSizing = false;

        // Çarpan metni: skor metninin bir kopyası, biraz küçük ve hemen altında.
        Transform multiplierExisting = scoreText.transform.parent.Find("MultiplierText");
        TMP_Text multiplierText;

        if (multiplierExisting != null)
        {
            multiplierText = multiplierExisting.GetComponent<TMP_Text>();
        }
        else
        {
            GameObject mGo = Object.Instantiate(scoreText.gameObject, scoreText.transform.parent);
            mGo.name = "MultiplierText";
            multiplierText = mGo.GetComponent<TMP_Text>();
            StripLocalization(mGo);

            RectTransform sRect = scoreText.rectTransform;
            RectTransform mRect = multiplierText.rectTransform;
            mRect.anchorMin = sRect.anchorMin;
            mRect.anchorMax = sRect.anchorMax;
            mRect.pivot = sRect.pivot;
            mRect.sizeDelta = sRect.sizeDelta;
            mRect.anchoredPosition = sRect.anchoredPosition + new Vector2(0f, -Mathf.Max(40f, sRect.sizeDelta.y));
        }

        multiplierText.fontSize = Mathf.Max(24f, scoreText.fontSize * 0.8f);
        multiplierText.text = string.Empty;
        multiplierText.enableAutoSizing = false;

        // HUD'ı Timer'ın üstünden aşağı kaydır ki üst üste binmesinler.
        RectTransform hudRect = hudGo.GetComponent<RectTransform>();
        RectTransform timerRect = timerRoot.GetComponent<RectTransform>();
        if (hudRect != null && timerRect != null && existing == null)
        {
            float drop = Mathf.Max(90f, timerRect.sizeDelta.y + 30f);
            hudRect.anchoredPosition = timerRect.anchoredPosition + new Vector2(0f, -drop);
            log.AppendLine($"  ScoreDisplay konumu: {hudRect.anchoredPosition} (Timer: {timerRect.anchoredPosition})");
        }

        ScoreHudView hud = hudGo.GetComponent<ScoreHudView>();
        if (hud == null) hud = hudGo.AddComponent<ScoreHudView>();

        SetRef(hud, "gameManager", gameManager, log);
        SetRef(hud, "scoreManager", scoreManager, log);
        SetRef(hud, "scoreText", scoreText, log);
        SetRef(hud, "multiplierText", multiplierText, log);
    }

    /// <summary>Skor metinlerini yıldızlarla butonlar arasındaki boşluğa yerleştirir.</summary>
    private static void LayoutBetween(Transform panel, TMP_Text score, TMP_Text best, TMP_Text badge, StringBuilder log)
    {
        RectTransform stars = panel.Find("StarsContainer") as RectTransform;
        RectTransform buttons = panel.Find("Buttons") as RectTransform;
        if (stars == null || buttons == null)
        {
            log.AppendLine($"! {panel.name}: StarsContainer/Buttons bulunamadı, konum elle ayarlanmalı.");
            return;
        }

        float midY = (stars.anchoredPosition.y + buttons.anchoredPosition.y) * 0.5f;
        float width = Mathf.Max(400f, Mathf.Abs(stars.sizeDelta.x));

        ApplyRect(score, stars, new Vector2(stars.anchoredPosition.x, midY + 30f), width, 70f);
        ApplyRect(best, stars, new Vector2(stars.anchoredPosition.x, midY - 35f), width, 50f);
        if (badge != null)
            ApplyRect(badge, stars, new Vector2(stars.anchoredPosition.x, midY - 85f), width, 55f);

        log.AppendLine($"  {panel.name}: skor metinleri y={midY:0} civarına yerleştirildi " +
                        $"(stars y={stars.anchoredPosition.y:0}, buttons y={buttons.anchoredPosition.y:0}).");
    }

    private static void ApplyRect(TMP_Text text, RectTransform reference, Vector2 position, float width, float height)
    {
        RectTransform rect = text.rectTransform;
        rect.anchorMin = reference.anchorMin;
        rect.anchorMax = reference.anchorMax;
        rect.pivot = reference.pivot;
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
        text.alignment = TextAlignmentOptions.Center;
    }

    /// <summary>Panelin içinden font/stil kopyalanacak bir TMP metni seçer (başlık tercih edilir).</summary>
    private static TMP_Text FindStyleTemplate(Transform panel)
    {
        TMP_Text[] candidates = panel.GetComponentsInChildren<TMP_Text>(true);
        return candidates.Length > 0 ? candidates[0] : null;
    }

    private static TMP_Text EnsureText(Transform parent, string name, TMP_Text style, float fontSize, string placeholder, StringBuilder log)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            TMP_Text found = existing.GetComponent<TMP_Text>();
            if (found != null)
            {
                log.AppendLine($"= {GetPath(existing)} zaten var.");
                return found;
            }
        }

        GameObject go;
        if (style != null)
        {
            go = Object.Instantiate(style.gameObject, parent);
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<TextMeshProUGUI>();
        }

        go.name = name;
        go.SetActive(true);
        StripLocalization(go);

        TMP_Text text = go.GetComponent<TMP_Text>();

        // Kopyalanan şablonun altında başka objeler olabilir - temizle.
        // Transform üzerinde dolaşırken DestroyImmediate çağırmak eleman ATLATIR,
        // o yüzden önce listeye topla.
        System.Collections.Generic.List<GameObject> children = new System.Collections.Generic.List<GameObject>();
        foreach (Transform child in go.transform) children.Add(child.gameObject);
        foreach (GameObject child in children) Object.DestroyImmediate(child);

        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.text = placeholder;
        text.alignment = TextAlignmentOptions.Center;

        log.AppendLine($"+ {GetPath(go.transform)} oluşturuldu.");
        return text;
    }

    /// <summary>
    /// Kopyalanan objedeki LocalizeStringEvent'i söker. Kalırsa string table'daki
    /// metni runtime'da ÜZERİNE YAZAR ve skor hiç görünmez.
    /// Tip ismiyle aranıyor ki bu editör aracı Localization paketine derleme
    /// bağımlılığı taşımasın.
    /// </summary>
    private static void StripLocalization(GameObject go)
    {
        foreach (Component c in go.GetComponents<Component>())
        {
            if (c == null) continue;
            if (c.GetType().Name == "LocalizeStringEvent") Object.DestroyImmediate(c);
        }
    }

    private static GameObject GetRefObject(Object target, string propertyName)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(propertyName);
        return prop != null ? prop.objectReferenceValue as GameObject : null;
    }

    private static void SetRef(Object target, string propertyName, Object value, StringBuilder log)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(propertyName);

        if (prop == null)
        {
            log.AppendLine($"! {target.name}.{propertyName} alanı YOK - atlanıyor.");
            return;
        }

        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        log.AppendLine($"  {target.GetType().Name}.{propertyName} = {(value != null ? value.name : "null")}");
    }

    /// <summary>
    /// Yerleşimi GEOMETRİK olarak doğrular: panellerin çocuklarının dünya köşelerini
    /// hesaplayıp yeni skor metinlerinin mevcut elemanlarla ÇAKIŞIP çakışmadığına bakar.
    /// Ekran görüntüsü alamadığımız için doğrulamanın tek güvenilir yolu bu.
    /// </summary>
    public static void Verify()
    {
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("===== VERIFY BEGIN =====");

        LevelResultView resultView = Object.FindAnyObjectByType<LevelResultView>(FindObjectsInactive.Include);
        GameObject[] panels = { GetRefObject(resultView, "winPanel"), GetRefObject(resultView, "losePanel") };

        string[] newNames = { "ScoreText", "BestScoreText", "NewRecordBadge" };

        foreach (GameObject panel in panels)
        {
            if (panel == null) continue;
            sb.AppendLine($"--- {panel.name} ---");

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                sb.AppendLine($"  PANEL rect: {RectInfo(panelRect)}");
            }

            foreach (Transform child in panel.transform)
            {
                RectTransform r = child as RectTransform;
                if (r == null) continue;
                bool isNew = System.Array.IndexOf(newNames, child.name) >= 0;
                sb.AppendLine($"  {(isNew ? ">>" : "  ")} {child.name,-18} {RectInfo(r)}");
            }

            // Yeni metinler mevcut elemanlarla çakışıyor mu?
            foreach (string n in newNames)
            {
                Transform t = panel.transform.Find(n);
                if (t == null) continue;
                RectTransform a = t as RectTransform;

                foreach (Transform other in panel.transform)
                {
                    if (other == t) continue;
                    if (System.Array.IndexOf(newNames, other.name) >= 0) continue;
                    if (other.name == "Background") continue;

                    RectTransform b = other as RectTransform;
                    if (b == null) continue;

                    if (Overlaps(a, b))
                        sb.AppendLine($"  !! CAKISMA: {n} <-> {other.name}");
                }
            }
        }

        sb.AppendLine("--- HUD ---");
        ScoreHudView hud = Object.FindAnyObjectByType<ScoreHudView>(FindObjectsInactive.Include);
        if (hud == null)
        {
            sb.AppendLine("  !! ScoreHudView YOK");
        }
        else
        {
            sb.AppendLine($"  {GetPath(hud.transform)}  {RectInfo(hud.GetComponent<RectTransform>())}");
            foreach (TMP_Text t in hud.GetComponentsInChildren<TMP_Text>(true))
                sb.AppendLine($"    {t.name,-16} size={t.fontSize} {RectInfo(t.rectTransform)}");

            LevelTimerView timer = Object.FindAnyObjectByType<LevelTimerView>(FindObjectsInactive.Include);
            if (timer != null)
            {
                RectTransform timerRoot = timer.transform.parent as RectTransform;
                sb.AppendLine($"  Timer kok: {RectInfo(timerRoot)}");
                if (Overlaps(hud.GetComponent<RectTransform>(), timerRoot))
                    sb.AppendLine("  !! CAKISMA: ScoreDisplay <-> Timer");
            }
        }

        sb.AppendLine("--- FINAL REF CHECK ---");
        DumpAllRefs(sb);

        sb.AppendLine("===== VERIFY END =====");
        Debug.Log(sb.ToString());
    }

    private static void DumpAllRefs(StringBuilder sb)
    {
        GameManager gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        ScoreManager sm = Object.FindAnyObjectByType<ScoreManager>(FindObjectsInactive.Include);
        LevelResultView rv = Object.FindAnyObjectByType<LevelResultView>(FindObjectsInactive.Include);
        ScoreHudView hud = Object.FindAnyObjectByType<ScoreHudView>(FindObjectsInactive.Include);

        if (gm != null) DumpObjectRef(new SerializedObject(gm), "scoreManager", sb);
        if (sm != null) DumpObjectRef(new SerializedObject(sm), "gameManager", sb);

        if (rv != null)
        {
            SerializedObject so = new SerializedObject(rv);
            DumpObjectRef(so, "scoreManager", sb);
            DumpObjectRef(so, "winScoreText", sb);
            DumpObjectRef(so, "winBestScoreText", sb);
            DumpObjectRef(so, "newRecordBadge", sb);
            DumpObjectRef(so, "loseScoreText", sb);
            DumpObjectRef(so, "loseBestScoreText", sb);
        }

        if (hud != null)
        {
            SerializedObject so = new SerializedObject(hud);
            DumpObjectRef(so, "gameManager", sb);
            DumpObjectRef(so, "scoreManager", sb);
            DumpObjectRef(so, "scoreText", sb);
            DumpObjectRef(so, "multiplierText", sb);
        }
    }

    private static bool Overlaps(RectTransform a, RectTransform b)
    {
        if (a == null || b == null) return false;

        Vector3[] ca = new Vector3[4];
        Vector3[] cb = new Vector3[4];
        a.GetWorldCorners(ca);
        b.GetWorldCorners(cb);

        Rect ra = new Rect(ca[0].x, ca[0].y, ca[2].x - ca[0].x, ca[2].y - ca[0].y);
        Rect rb = new Rect(cb[0].x, cb[0].y, cb[2].x - cb[0].x, cb[2].y - cb[0].y);
        return ra.Overlaps(rb);
    }

    private static string RectInfo(RectTransform r)
    {
        if (r == null) return "<null>";

        Vector3[] c = new Vector3[4];
        r.GetWorldCorners(c);
        return $"anchor=({r.anchorMin.x:0.##},{r.anchorMin.y:0.##})-({r.anchorMax.x:0.##},{r.anchorMax.y:0.##}) " +
               $"pivot=({r.pivot.x:0.##},{r.pivot.y:0.##}) pos={r.anchoredPosition} size={r.sizeDelta} " +
               $"worldY=[{c[0].y:0}..{c[1].y:0}] worldX=[{c[0].x:0}..{c[2].x:0}]";
    }

    /// <summary>
    /// Panelleri geçici olarak AÇIP layout'u zorla yeniden kurar ve GERÇEK (runtime)
    /// boyutları ölçer. Buttons'ta ContentSizeFitter olduğu için edit-time yüksekliği
    /// 0 görünüyor - bu yüzden doğrudan ölçmeden yerleşim kararı verilemez.
    /// </summary>
    public static void Measure()
    {
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("===== MEASURE BEGIN =====");

        LevelResultView resultView = Object.FindAnyObjectByType<LevelResultView>(FindObjectsInactive.Include);
        GameObject[] panels = { GetRefObject(resultView, "winPanel"), GetRefObject(resultView, "losePanel") };

        foreach (GameObject panel in panels)
        {
            if (panel == null) continue;

            bool wasActive = panel.activeSelf;
            panel.SetActive(true);

            Canvas.ForceUpdateCanvases();
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            Canvas.ForceUpdateCanvases();

            sb.AppendLine($"--- {panel.name} (GERCEK boyutlar) ---");
            foreach (Transform child in panel.transform)
            {
                RectTransform r = child as RectTransform;
                if (r == null) continue;

                Vector3[] c = new Vector3[4];
                r.GetWorldCorners(c);
                sb.AppendLine($"  {child.name,-18} worldY=[{c[0].y:0}..{c[1].y:0}] h={(c[1].y - c[0].y):0} " +
                               $"sizeDelta={r.sizeDelta}");
            }

            panel.SetActive(wasActive);
        }

        sb.AppendLine("===== MEASURE END =====");
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Skor metinlerini ÖLÇÜLMÜŞ boş alanlara taşır (bkz. Measure).
    /// Değerler elle hesaplandı çünkü panellerdeki anchor çerçeveleri farklı:
    /// WinPanel'de yıldız/başlık ÜSTE (0.5,1), LosePanel'de MERKEZE (0.5,0.5) bağlı.
    /// </summary>
    public static void Reposition()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("===== REPOSITION BEGIN =====");

        LevelResultView resultView = Object.FindAnyObjectByType<LevelResultView>(FindObjectsInactive.Include);
        GameObject winPanel = GetRefObject(resultView, "winPanel");
        GameObject losePanel = GetRefObject(resultView, "losePanel");

        // WinPanel: üst çerçeve. Yıldızlar pos y=-550 (world 95..213). Metinleri
        // yıldızların hemen ALTINA, kartın içinde kalacak şekilde koy.
        Move(winPanel, "ScoreText", -700f, 60f, sb);
        Move(winPanel, "BestScoreText", -775f, 44f, sb);
        Move(winPanel, "NewRecordBadge", -845f, 48f, sb);

        // LosePanel: merkez çerçeve. Butonların üstü world 268, yıldızların altı 406.
        // Bu 138 birimlik GERÇEK boşluğa yerleştir.
        Move(losePanel, "ScoreText", 195f, 60f, sb);
        Move(losePanel, "BestScoreText", 125f, 44f, sb);

        // HUD: çarpan metni skor metniyle AYNI kutuyu doldurduğu için üst üste biniyordu.
        // Kutunun tamamı kadar aşağı indirip skorun hemen ALTINA, kutunun dışına al.
        ScoreHudView hud = Object.FindAnyObjectByType<ScoreHudView>(FindObjectsInactive.Include);
        if (hud != null)
        {
            RectTransform hudRect = hud.GetComponent<RectTransform>();
            Transform mt = hud.transform.Find("ScoreText/MultiplierText");
            if (mt == null) mt = hud.transform.Find("MultiplierText");

            // Klonlama sırasında çarpan, skorun KARDEŞİ olarak eklendi.
            foreach (TMP_Text candidate in hud.GetComponentsInChildren<TMP_Text>(true))
            {
                if (candidate.name != "MultiplierText") continue;

                RectTransform r = candidate.rectTransform;
                float boxHeight = hudRect != null ? hudRect.sizeDelta.y : 90f;
                r.anchoredPosition = new Vector2(r.anchoredPosition.x, -(boxHeight + 5f));

                Vector3[] c = new Vector3[4];
                r.GetWorldCorners(c);
                sb.AppendLine($"  HUD/MultiplierText -> pos.y={r.anchoredPosition.y} worldY=[{c[0].y:0}..{c[1].y:0}]");
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        sb.AppendLine("Sahne KAYDEDİLDİ.");
        sb.AppendLine("===== REPOSITION END =====");
        Debug.Log(sb.ToString());
    }

    private static void Move(GameObject panel, string childName, float posY, float fontSize, StringBuilder sb)
    {
        if (panel == null) return;

        Transform t = panel.transform.Find(childName);
        if (t == null)
        {
            sb.AppendLine($"! {panel.name}/{childName} bulunamadı.");
            return;
        }

        RectTransform r = t as RectTransform;
        r.anchoredPosition = new Vector2(r.anchoredPosition.x, posY);

        TMP_Text text = t.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
        }

        Vector3[] c = new Vector3[4];
        r.GetWorldCorners(c);
        sb.AppendLine($"  {panel.name}/{childName} -> pos.y={posY} font={fontSize} worldY=[{c[0].y:0}..{c[1].y:0}]");
    }

    /// <summary>Sahnenin mevcut yapısını Console'a döker - bağlamadan önce durumu görmek için.</summary>
    public static void Dump()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("===== DUMP BEGIN =====");

        sb.AppendLine("--- FULL HIERARCHY ---");
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            AppendHierarchy(root.transform, 0, sb);
        }

        sb.AppendLine("--- KEY COMPONENTS ---");
        AppendFound<GameManager>(sb);
        AppendFound<LevelResultView>(sb);
        AppendFound<LevelTimerManager>(sb);
        AppendFound<StationAssignmentManager>(sb);
        AppendFound<ScoreManager>(sb);
        AppendFound<ScoreHudView>(sb);
        AppendFound<SafeArea>(sb);
        AppendFound<LevelTimerView>(sb);
        AppendFound<Canvas>(sb);

        sb.AppendLine("--- LevelResultView SERIALIZED REFS ---");
        LevelResultView resultView = Object.FindAnyObjectByType<LevelResultView>(FindObjectsInactive.Include);
        if (resultView == null)
        {
            sb.AppendLine("  !! LevelResultView BULUNAMADI");
        }
        else
        {
            SerializedObject so = new SerializedObject(resultView);
            DumpObjectRef(so, "winPanel", sb);
            DumpObjectRef(so, "losePanel", sb);
            DumpObjectRef(so, "nextLevelButton", sb);
            DumpObjectRef(so, "scoreManager", sb);
            DumpObjectRef(so, "winScoreText", sb);
            DumpObjectRef(so, "loseScoreText", sb);

            SerializedProperty stars = so.FindProperty("starImages");
            sb.AppendLine($"  starImages.arraySize = {(stars != null ? stars.arraySize : -1)}");
        }

        sb.AppendLine("--- GameManager SERIALIZED REFS ---");
        GameManager gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm == null)
        {
            sb.AppendLine("  !! GameManager BULUNAMADI");
        }
        else
        {
            SerializedObject so = new SerializedObject(gm);
            DumpObjectRef(so, "levelTimerManager", sb);
            DumpObjectRef(so, "scoreManager", sb);
            DumpObjectRef(so, "fallbackLevelData", sb);
        }

        sb.AppendLine("--- EXISTING TMP TEXTS (font/style kopyalamak icin aday) ---");
        foreach (TMP_Text t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            sb.AppendLine($"  {GetPath(t.transform)}  | font={(t.font != null ? t.font.name : "null")} size={t.fontSize} text=\"{Truncate(t.text)}\"");
        }

        sb.AppendLine("===== DUMP END =====");
        Debug.Log(sb.ToString());
    }

    private static string Truncate(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        value = value.Replace("\n", "\\n");
        return value.Length <= 30 ? value : value.Substring(0, 30) + "...";
    }

    private static void DumpObjectRef(SerializedObject so, string propertyName, StringBuilder sb)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            sb.AppendLine($"  {propertyName}: <PROPERTY YOK>");
            return;
        }

        Object value = prop.objectReferenceValue;
        if (value == null)
        {
            sb.AppendLine($"  {propertyName}: <BOS>");
            return;
        }

        GameObject go = value as GameObject;
        Component comp = value as Component;
        string path = go != null ? GetPath(go.transform) : (comp != null ? GetPath(comp.transform) : value.name);
        sb.AppendLine($"  {propertyName}: {path}");
    }

    private static void AppendFound<T>(StringBuilder sb) where T : Component
    {
        T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        if (found.Length == 0)
        {
            sb.AppendLine($"  {typeof(T).Name}: YOK");
            return;
        }

        foreach (T item in found)
        {
            sb.AppendLine($"  {typeof(T).Name}: {GetPath(item.transform)}");
        }
    }

    private static void AppendHierarchy(Transform t, int depth, StringBuilder sb)
    {
        if (depth > 5) return;

        string indent = new string(' ', depth * 2);
        string components = string.Empty;

        foreach (Component c in t.GetComponents<Component>())
        {
            if (c == null) continue;
            string n = c.GetType().Name;
            if (n == "Transform" || n == "RectTransform" || n == "CanvasRenderer") continue;
            components += (components.Length > 0 ? "," : string.Empty) + n;
        }

        sb.AppendLine($"{indent}{t.name}{(t.gameObject.activeSelf ? "" : " [inactive]")}{(components.Length > 0 ? "  <" + components + ">" : "")}");

        for (int i = 0; i < t.childCount; i++)
        {
            AppendHierarchy(t.GetChild(i), depth + 1, sb);
        }
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        Transform current = t.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }
}
