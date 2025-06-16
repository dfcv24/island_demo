using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    [Header("UI组件")]
    public TMP_InputField inputChatField;
    public ScrollRect chatScrollView;
    public Transform chatContent; // ScrollView的Content对象
    public TMP_FontAsset chineseFontAsset;
    
    [Header("NPC设置")]
    public Person targetNPC;
    
    private int lastMessageCount = 0;
    void Start()
    {
        inputChatField.onSubmit.AddListener(OnInputSubmit);
        Debug.Log("DialogueManager初始化完成");
    }
    
    void Update()
    {
        // 更新聊天历史显示
        if (targetNPC != null && chatScrollView != null)
        {
            UpdateChatHistory();
        }
    }
    
    void UpdateChatHistory()
    {
        if (targetNPC?.GetChatHistory() == null || chatContent == null) return;
        
        var chatHistory = targetNPC.GetChatHistory();
        
        // 只在消息数量改变时更新UI
        if (chatHistory.Count != lastMessageCount)
        {
            // 清空现有内容
            foreach (Transform child in chatContent)
            {
                Destroy(child.gameObject);
            }
            
            // 添加每条聊天记录
            foreach (string message in chatHistory)
            {
                CreateMessageObject(message);
            }
            
            lastMessageCount = chatHistory.Count;
            
            // 等待一帧后滚动到底部
            StartCoroutine(ScrollToBottomNextFrame());
        }
    }

    private void CreateMessageObject(string message)
    {
        GameObject messageObj = new GameObject("ChatMessage");
        messageObj.transform.SetParent(chatContent, false); // 注意这里加了false
        
        TextMeshProUGUI messageText = messageObj.AddComponent<TextMeshProUGUI>();
        messageText.text = message;
        messageText.fontSize = 12;
        messageText.font = chineseFontAsset;
        messageText.color = Color.black;
        
        // 添加Content Size Fitter让文本自适应高度
        ContentSizeFitter sizeFitter = messageObj.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        // 设置布局
        RectTransform rectTransform = messageObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(1, 1);
        rectTransform.pivot = new Vector2(0.5f, 1);
    }

    private System.Collections.IEnumerator ScrollToBottomNextFrame()
    {
        yield return null; // 等待一帧
        Canvas.ForceUpdateCanvases();
        chatScrollView.verticalNormalizedPosition = 0f;
    }
    
    void OnInputSubmit(string message)
    {
        if (!string.IsNullOrEmpty(message.Trim()))
        {
            SendChatMessage(message.Trim());
        }
    }
    
    void SendChatMessage(string userMessage)
    {
        inputChatField.text = "";
        // 将消息发送给Person实例处理
        targetNPC.ReceivePlayerMessage(userMessage);
        inputChatField.ActivateInputField();
    }
}
