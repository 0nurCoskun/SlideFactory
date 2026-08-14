using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace CardCraft.Editor.ComfyUI
{
    /// <summary>
    /// ComfyUI'nin HTTP API'si üzerinden pixel-art görsel üretimi yapan Editor penceresi.
    /// ComfyUI, Unity'nin dışında ayrı bir process olarak çalışır (varsayılan port 8188);
    /// bu pencere ona sadece HTTP üzerinden istek atar.
    ///
    /// Kurulum:
    /// 1) ComfyUI'yi kur ve çalıştır (bkz. https://github.com/comfyanonymous/ComfyUI).
    ///    Pixel-art'a uygun bir checkpoint/LoRA yükle (ör. bir "pixel art" LoRA'sı, CivitAI).
    /// 2) ComfyUI web arayüzünde workflow'u kur: CheckpointLoader + LoRA (opsiyonel) +
    ///    2x CLIPTextEncode (pozitif/negatif) + EmptyLatentImage + KSampler + VAEDecode +
    ///    SaveImage.
    /// 3) Pozitif ve negatif CLIPTextEncode node'larını sağ tık > Title ile tam olarak
    ///    "Positive Prompt" ve "Negative Prompt" olarak yeniden adlandır (bu pencere bu
    ///    başlıklardan node'ları bulup prompt metnini enjekte ediyor).
    /// 4) ComfyUI menüsünden Workflow > Export (API) ile workflow'u JSON olarak dışa aktar,
    ///    bu penceredeki "Workflow JSON" alanından seç.
    /// 5) Prompt/boyut/seed gir, Generate'e bas. Görsel indirilip Assets içine pixel-art
    ///    sprite import ayarlarıyla (Point filter, sıkıştırmasız, verilen Pixels Per Unit)
    ///    otomatik yerleştirilir.
    /// </summary>
    public class ComfyPixelArtGenerator : EditorWindow
    {
        private const string PrefPrefix = "ComfyPixelArt_";

        private string _serverUrl = "http://127.0.0.1:8188";
        private string _workflowJsonPath = "";
        private string _positivePrompt = "pixel art, game item icon, clean outline, limited palette";
        private string _negativePrompt = "blurry, anti-aliasing, smooth gradient, photorealistic, text, watermark";
        private int _width = 512;
        private int _height = 512;
        private int _seed = -1;
        private string _outputFolder = "Assets/_Project/Textures/Generated";
        private string _fileNameBase = "generated_icon";
        private int _targetPixelSize = 32;
        private int _pixelsPerUnit = 100;

        private bool _isBusy;
        private string _statusMessage = "";

        [MenuItem("CardCraft/Comfy Pixel Art Generator")]
        public static void ShowWindow()
        {
            GetWindow<ComfyPixelArtGenerator>("Comfy Pixel Art");
        }

        private void OnEnable()
        {
            _serverUrl = EditorPrefs.GetString(PrefPrefix + "ServerUrl", _serverUrl);
            _workflowJsonPath = EditorPrefs.GetString(PrefPrefix + "WorkflowPath", _workflowJsonPath);
            _outputFolder = EditorPrefs.GetString(PrefPrefix + "OutputFolder", _outputFolder);
            _targetPixelSize = EditorPrefs.GetInt(PrefPrefix + "TargetPixelSize", _targetPixelSize);
            _pixelsPerUnit = EditorPrefs.GetInt(PrefPrefix + "PixelsPerUnit", _pixelsPerUnit);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ComfyUI Sunucusu", EditorStyles.boldLabel);
            _serverUrl = EditorGUILayout.TextField("Server URL", _serverUrl);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Workflow", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _workflowJsonPath = EditorGUILayout.TextField("Workflow JSON", _workflowJsonPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                var picked = EditorUtility.OpenFilePanel("ComfyUI API-format workflow seç", "", "json");
                if (!string.IsNullOrEmpty(picked)) _workflowJsonPath = picked;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Prompt", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Positive");
            _positivePrompt = EditorGUILayout.TextArea(_positivePrompt, GUILayout.Height(50));
            EditorGUILayout.LabelField("Negative");
            _negativePrompt = EditorGUILayout.TextArea(_negativePrompt, GUILayout.Height(40));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Üretim Ayarları", EditorStyles.boldLabel);
            _width = EditorGUILayout.IntField("Latent Width", _width);
            _height = EditorGUILayout.IntField("Latent Height", _height);
            _seed = EditorGUILayout.IntField("Seed (-1 = rastgele)", _seed);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Çıktı", EditorStyles.boldLabel);
            _outputFolder = EditorGUILayout.TextField("Output Folder (Assets/...)", _outputFolder);
            _fileNameBase = EditorGUILayout.TextField("Dosya Adı", _fileNameBase);
            _targetPixelSize = EditorGUILayout.IntField("Hedef Pixel Boyutu (0 = kapalı)", _targetPixelSize);
            _pixelsPerUnit = EditorGUILayout.IntField("Sprite Pixels Per Unit", _pixelsPerUnit);

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(_isBusy);
            if (GUILayout.Button("Generate", GUILayout.Height(32)))
            {
                SavePrefs();
                _ = GenerateAsync();
            }
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, _isBusy ? MessageType.Info : MessageType.None);
            }
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PrefPrefix + "ServerUrl", _serverUrl);
            EditorPrefs.SetString(PrefPrefix + "WorkflowPath", _workflowJsonPath);
            EditorPrefs.SetString(PrefPrefix + "OutputFolder", _outputFolder);
            EditorPrefs.SetInt(PrefPrefix + "TargetPixelSize", _targetPixelSize);
            EditorPrefs.SetInt(PrefPrefix + "PixelsPerUnit", _pixelsPerUnit);
        }

        private async Task GenerateAsync()
        {
            if (string.IsNullOrEmpty(_workflowJsonPath) || !File.Exists(_workflowJsonPath))
            {
                _statusMessage = "Geçerli bir workflow JSON dosyası seç.";
                Repaint();
                return;
            }

            _isBusy = true;
            _statusMessage = "Workflow hazırlanıyor...";
            Repaint();

            try
            {
                var workflow = JObject.Parse(File.ReadAllText(_workflowJsonPath));
                int actualSeed = _seed >= 0 ? _seed : new System.Random().Next(0, int.MaxValue);
                PatchWorkflow(workflow, actualSeed);

                var body = new JObject
                {
                    ["prompt"] = workflow,
                    ["client_id"] = Guid.NewGuid().ToString()
                };

                _statusMessage = "ComfyUI'ye gönderiliyor...";
                Repaint();
                var promptResponse = await PostJsonAsync(_serverUrl.TrimEnd('/') + "/prompt", body.ToString());
                var promptId = promptResponse["prompt_id"]?.ToString();
                if (string.IsNullOrEmpty(promptId))
                {
                    _statusMessage = "ComfyUI prompt_id döndürmedi: " + promptResponse;
                    return;
                }

                _statusMessage = $"Üretim bekleniyor (prompt_id={promptId})...";
                Repaint();

                JObject historyEntry = null;
                for (int attempt = 0; attempt < 240; attempt++) // ~120 sn'ye kadar bekle
                {
                    await Task.Delay(500);
                    var history = await GetJsonAsync(_serverUrl.TrimEnd('/') + "/history/" + promptId);
                    if (history != null && history.TryGetValue(promptId, out var entry))
                    {
                        historyEntry = entry as JObject;
                        break;
                    }
                }

                if (historyEntry == null)
                {
                    _statusMessage = "Zaman aşımı: ComfyUI görseli zamanında üretmedi.";
                    return;
                }

                var outputs = historyEntry["outputs"] as JObject;
                if (outputs == null)
                {
                    _statusMessage = "ComfyUI history'sinde 'outputs' bulunamadı.";
                    return;
                }

                if (!AssetDatabase.IsValidFolder(_outputFolder))
                {
                    Directory.CreateDirectory(_outputFolder);
                    AssetDatabase.Refresh();
                }

                int savedCount = 0;
                foreach (var nodeProperty in outputs.Properties())
                {
                    var images = nodeProperty.Value["images"] as JArray;
                    if (images == null) continue;

                    foreach (var image in images)
                    {
                        var filename = image["filename"]?.ToString();
                        var subfolder = image["subfolder"]?.ToString() ?? "";
                        var type = image["type"]?.ToString() ?? "output";
                        if (string.IsNullOrEmpty(filename)) continue;

                        var viewUrl = $"{_serverUrl.TrimEnd('/')}/view?filename={UnityWebRequest.EscapeURL(filename)}" +
                                      $"&subfolder={UnityWebRequest.EscapeURL(subfolder)}&type={UnityWebRequest.EscapeURL(type)}";
                        var bytes = await GetBytesAsync(viewUrl);
                        if (bytes == null) continue;

                        savedCount++;
                        var suffix = savedCount > 1 ? $"_{savedCount}" : "";
                        var assetPath = $"{_outputFolder}/{_fileNameBase}{suffix}.png";
                        // Aynı dosya adı yeniden kullanılıyor olabilir; eski .meta içindeki sprite
                        // rect verisi yeni (ör. downscale sonrası küçülmüş) boyuta uymayabilir.
                        // Önce sil ki reimport temiz başlasın.
                        AssetDatabase.DeleteAsset(assetPath);
                        File.WriteAllBytes(assetPath, bytes);
                    }
                }

                AssetDatabase.Refresh();

                if (_targetPixelSize > 0)
                {
                    for (int i = 1; i <= savedCount; i++)
                    {
                        var suffix = i > 1 ? $"_{i}" : "";
                        DownscaleToPixelGrid($"{_outputFolder}/{_fileNameBase}{suffix}.png", _targetPixelSize);
                    }
                    AssetDatabase.Refresh();
                }

                for (int i = 1; i <= savedCount; i++)
                {
                    var suffix = i > 1 ? $"_{i}" : "";
                    ApplyPixelArtImportSettings($"{_outputFolder}/{_fileNameBase}{suffix}.png", _pixelsPerUnit);
                }
                AssetDatabase.SaveAssets();

                _statusMessage = savedCount > 0
                    ? $"Tamamlandı: {savedCount} görsel {_outputFolder} içine kaydedildi."
                    : "ComfyUI çıktısında görsel bulunamadı.";
            }
            catch (Exception ex)
            {
                _statusMessage = "Hata: " + ex.Message;
                Debug.LogError($"[ComfyPixelArtGenerator] {ex}");
            }
            finally
            {
                _isBusy = false;
                Repaint();
            }
        }

        /// <summary>
        /// Workflow JSON'ındaki node'ları title/class_type üzerinden bulup prompt, boyut ve
        /// seed değerlerini enjekte eder. Bkz. sınıf başındaki kurulum notları.
        /// </summary>
        private void PatchWorkflow(JObject workflow, int seed)
        {
            foreach (var property in workflow.Properties())
            {
                var node = property.Value as JObject;
                if (node == null) continue;

                var classType = node["class_type"]?.ToString();
                var title = node["_meta"]?["title"]?.ToString();
                var inputs = node["inputs"] as JObject;
                if (inputs == null) continue;

                if (title == "Positive Prompt")
                {
                    SetPromptText(inputs, _positivePrompt);
                }
                else if (title == "Negative Prompt")
                {
                    SetPromptText(inputs, _negativePrompt);
                }
                else if (classType == "EmptyLatentImage")
                {
                    inputs["width"] = _width;
                    inputs["height"] = _height;
                }
                else if (classType == "KSampler")
                {
                    inputs["seed"] = seed;
                }
            }
        }

        /// <summary>
        /// Plain CLIPTextEncode "text" alanı kullanır; CLIPTextEncodeSDXL ise ayrı "text_g"
        /// ve "text_l" alanlarına sahiptir. İkisini de destekle.
        /// </summary>
        private static void SetPromptText(JObject inputs, string prompt)
        {
            if (inputs.ContainsKey("text"))
            {
                inputs["text"] = prompt;
            }
            if (inputs.ContainsKey("text_g")) inputs["text_g"] = prompt;
            if (inputs.ContainsKey("text_l")) inputs["text_l"] = prompt;
        }

        /// <summary>
        /// Diffusion çıktısı piksel-mükemmel değildir; görseli nearest-neighbor ile hedef
        /// piksel ızgarasına indirger (ör. 32x32) ki gerçek pixel-art görünümü elde edilsin.
        /// </summary>
        private static void DownscaleToPixelGrid(string assetPath, int targetSize)
        {
            var bytes = File.ReadAllBytes(assetPath);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(bytes);

            var result = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, false);
            for (int y = 0; y < targetSize; y++)
            {
                for (int x = 0; x < targetSize; x++)
                {
                    float u = (x + 0.5f) / targetSize;
                    float v = (y + 0.5f) / targetSize;
                    int srcX = Mathf.Clamp(Mathf.FloorToInt(u * source.width), 0, source.width - 1);
                    int srcY = Mathf.Clamp(Mathf.FloorToInt(v * source.height), 0, source.height - 1);
                    result.SetPixel(x, y, source.GetPixel(srcX, srcY));
                }
            }
            result.Apply();

            File.WriteAllBytes(assetPath, result.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(result);
        }

        /// <summary>
        /// CardIconAutoWirer'daki kart ikonu import kurallarıyla tutarlı, pixel-art'a uygun
        /// sprite import ayarları (Point filter, sıkıştırmasız, mipmap kapalı).
        /// </summary>
        private static void ApplyPixelArtImportSettings(string assetPath, int pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.SaveAndReimport();
        }

        private static async Task<JObject> PostJsonAsync(string url, string jsonBody)
        {
            using var request = new UnityWebRequest(url, "POST");
            var bodyBytes = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var op = request.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
                throw new Exception($"POST {url} başarısız: {request.error}\n{request.downloadHandler.text}");

            return JObject.Parse(request.downloadHandler.text);
        }

        private static async Task<JObject> GetJsonAsync(string url)
        {
            using var request = UnityWebRequest.Get(url);
            var op = request.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
                return null;

            return JObject.Parse(request.downloadHandler.text);
        }

        private static async Task<byte[]> GetBytesAsync(string url)
        {
            using var request = UnityWebRequest.Get(url);
            var op = request.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[ComfyPixelArtGenerator] GET {url} başarısız: {request.error}");
                return null;
            }

            return request.downloadHandler.data;
        }
    }
}
