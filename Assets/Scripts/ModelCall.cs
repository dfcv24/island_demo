using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;

[System.Serializable]
public class ChatMessage
{
    public string role;
    public string content;
    public string reasoning_content;
    public ToolCall[] tool_calls;
}

[System.Serializable]
public class ToolCall
{
    public string id;
    public string type;
    public FunctionCall function;
}

[System.Serializable]
public class FunctionCall
{
    public string name;
    public string arguments;
}

[System.Serializable]
public class ChatRequest
{
    public string model;
    public ChatMessage[] messages;
    public float temperature = 0f;
    public int max_tokens = 2000;
    public object[] tools;

    public ChatRequest(string modelName, bool includeFunctions = true)
    {
        model = modelName;
        if (!includeFunctions) tools = null;
    }
}

[System.Serializable]
public class ChatResponse
{
    public ChatChoice[] choices;
}

[System.Serializable]
public class ChatChoice
{
    public ChatMessage message;
}

public class ModelCall : MonoBehaviour
{
    private const string API_URL = "http://36.103.234.236:8050/v1/chat/completions";
    private const string API_THINK_URL = "http://36.103.234.236:7012/v1/chat/completions";
    private const string MAIN_MODEL = "Qwen3-32B";
    private const string THINK_MODEL = "DS-Qwen3-8B";
    
    private string systemPrompt = "";
    private string thinkPrompt = "";
    private static ModelCall instance;
    public static ModelCall Instance => instance;
    private Functions functions;
    
    private readonly List<string> understandingMessages = new List<string>
    {
        "我明白了", "好的", "明白了", "了解", "我知道了"
    };

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSystemPrompts();
            InitializeFunctions();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadSystemPrompts()
    {
        systemPrompt = Resources.Load<TextAsset>("SystemPrompt")?.text ?? "";
        thinkPrompt = Resources.Load<TextAsset>("SystemPromptThink")?.text ?? "";
    }

    private void InitializeFunctions()
    {
        functions = FindFirstObjectByType<Functions>();
        if (functions == null)
        {
            GameObject functionsObj = new GameObject("Functions");
            functions = functionsObj.AddComponent<Functions>();
        }
    }

    public void ToolsModel(string userMessage, System.Action<string> onResponse)
    {
        StartCoroutine(SendChatRequestCoroutine(userMessage, onResponse, true));
    }

    public void ThinkModel(string userMessage, System.Action<string> onResponse)
    {
        StartCoroutine(SendChatRequestCoroutine(userMessage, onResponse, false));
    }

    private IEnumerator SendChatRequestCoroutine(string userMessage, System.Action<string> onResponse, bool isMainModel)
    {
        if (!ValidateSystem(onResponse)) yield break;

        string contextPrompt = BuildContextPrompt(isMainModel);
        ChatRequest request = CreateChatRequest(contextPrompt, userMessage, isMainModel);
        
        yield return StartCoroutine(ExecuteRequest(request, onResponse, isMainModel));
    }

    private bool ValidateSystem(System.Action<string> onResponse)
    {
        if (functions?.AVAILABLE_FUNCTIONS == null)
        {
            Debug.LogWarning("Functions未初始化");
            onResponse?.Invoke("系统未完全初始化");
            return false;
        }
        return true;
    }

    private string BuildContextPrompt(bool isMainModel)
    {
        Person person = FindFirstObjectByType<Person>();
        StringBuilder contextBuilder = new StringBuilder();
        
        contextBuilder.AppendLine(isMainModel ? systemPrompt : thinkPrompt);

        if (person != null)
        {
            AppendKnowledgeBase(contextBuilder, person);
            AppendChatHistory(contextBuilder, person);
            
            if (isMainModel)
            {
                AppendCurrentPosition(contextBuilder, person);
            }
        }

        return contextBuilder.ToString();
    }

    private void AppendKnowledgeBase(StringBuilder builder, Person person)
    {
        var knowledges = person.GetKnowledges();
        if (knowledges.Count == 0) return;

        builder.AppendLine("\n<knowledge_base>");
        foreach (var kvp in knowledges)
        {
            var item = kvp.Value;
            builder.AppendLine($"  <item>");
            builder.AppendLine($"    <id>{kvp.Key}</id>");
            builder.AppendLine($"    <position>{item.itemPosition.x},{item.itemPosition.y},{item.itemPosition.z}</position>");
            builder.AppendLine($"    <category>{item.itemCategory}</category>");
            builder.AppendLine($"    <description>{item.itemDescription}</description>");
            builder.AppendLine($"  </item>");
        }
        builder.AppendLine("</knowledge_base>");
    }

    private void AppendChatHistory(StringBuilder builder, Person person)
    {
        var chatHistory = person.GetChatHistory();
        if (chatHistory.Count == 0) return;

        builder.AppendLine("\n<chat_history>");
        foreach (string history in chatHistory)
        {
            builder.AppendLine($"  <message>{history}</message>");
        }
        builder.AppendLine("</chat_history>");
    }

    private void AppendCurrentPosition(StringBuilder builder, Person person)
    {
        builder.AppendLine("\n<current_position>");
        if (person.personController != null)
        {
            Vector3Int currentPos = person.personController.GetGridPosition();
            builder.AppendLine($"  <position>{currentPos.x},{currentPos.y},{currentPos.z}</position>");
        }
        builder.AppendLine("</current_position>");
    }

    private ChatRequest CreateChatRequest(string contextPrompt, string userMessage, bool isMainModel)
    {
        string model = isMainModel ? MAIN_MODEL : THINK_MODEL;
        ChatRequest request = new ChatRequest(model, isMainModel);
        
        request.messages = new ChatMessage[]
        {
            new ChatMessage { role = "system", content = contextPrompt },
            new ChatMessage { role = "user", content = userMessage }
        };

        if (isMainModel)
        {
            request.tools = functions.AVAILABLE_FUNCTIONS;
        }

        return request;
    }

    private IEnumerator ExecuteRequest(ChatRequest request, System.Action<string> onResponse, bool isMainModel)
    {
        string url = isMainModel ? API_URL : API_THINK_URL;
        byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 60;

            yield return www.SendWebRequest();

            ProcessResponse(www, onResponse, isMainModel);
        }
    }

    private void ProcessResponse(UnityWebRequest www, System.Action<string> onResponse, bool isMainModel)
    {
        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"请求错误: {www.error}");
            onResponse?.Invoke("出现错误");
            return;
        }

        try
        {
            var response = JsonConvert.DeserializeObject<ChatResponse>(www.downloadHandler.text);
            var message = response?.choices?[0]?.message;
            Debug.Log("响应内容: " + www.downloadHandler.text);

            if (isMainModel && message?.tool_calls != null && message.tool_calls.Length > 0)
            {
                ExecuteFunctionCalls(message.tool_calls);
            }

            string reply = GetReplyContent(message?.content);
            onResponse?.Invoke(reply);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"解析错误: {e.Message}");
            onResponse?.Invoke("出现错误");
        }
    }

    private void ExecuteFunctionCalls(ToolCall[] toolCalls)
    {
        foreach (var toolCall in toolCalls)
        {
            functions.ExecuteFunction(toolCall.function.name, toolCall.function.arguments);
        }
    }

    private string GetReplyContent(string content)
    {
        string reply = content?.Trim();
        if (string.IsNullOrEmpty(reply))
        {
            reply = understandingMessages[Random.Range(0, understandingMessages.Count)];
        }
        return reply;
    }
}
