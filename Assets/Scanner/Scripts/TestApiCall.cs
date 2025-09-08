using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class TestApiCall : MonoBehaviour
{
    [Header("DO NOT ship real key in client! Use a server proxy in prod.")]
    [SerializeField] private string openAiApiKey = "YOUR_API_KEY";
    [SerializeField] private string model = "gpt-4o-mini";

    [ContextMenu("Send Image File Sample")]
    public void SendImageFileSample()
    {
        var path = Application.dataPath + "/test.jpeg"; // положи файл для пробы
        var question = "Кратко опиши, что на фото. Отвечай по-русски.";
        StartCoroutine(SendImageFile(path, question));
    }

    private System.Collections.IEnumerator SendImageFile(string imagePath, string userPrompt)
    {
        // читаем файл и кодируем в base64 data URL
        byte[] bytes = File.ReadAllBytes(imagePath);
        string ext = Path.GetExtension(imagePath).ToLowerInvariant();
        string mime = (ext == ".jpg" || ext == ".jpeg") ? "jpeg" :
                      (ext == ".png") ? "png" :
                      (ext == ".webp") ? "webp" : "png";
        string b64 = Convert.ToBase64String(bytes);
        string dataUrl = $"data:image/{mime};base64,{b64}";

        // собираем JSON с контентом: текст + картинка
        string json = $@"{{
  ""model"": ""{model}"",
  ""input"": [
    {{
      ""role"": ""user"",
      ""content"": [
        {{ ""type"": ""input_text"", ""text"": ""{Escape(userPrompt)}"" }},
        {{ ""type"": ""input_image"", ""image_url"": ""{dataUrl}"" }}
      ]
    }}
  ]
}}";

        using var req = new UnityWebRequest("https://api.openai.com/v1/responses", "POST");
        byte[] body = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Authorization", $"Bearer {openAiApiKey}");
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"OpenAI error {req.responseCode}: {req.error}\n{req.downloadHandler.text}");
            yield break;
        }

        // Парсим коротко (как и раньше) — достаём output_text
        var root = JsonUtility.FromJson<ResponsesRoot>(req.downloadHandler.text);
        string ai =
            root?.output != null && root.output.Length > 0 &&
            root.output[0]?.content != null && root.output[0].content.Length > 0
                ? root.output[0].content[0].text
                : null;

        Debug.Log($"AI: {ai}");
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

    [Serializable] public class ResponsesRoot { public OutputItem[] output; }
    [Serializable] public class OutputItem { public ContentItem[] content; }
    [Serializable] public class ContentItem { public string type; public string text; }
}
