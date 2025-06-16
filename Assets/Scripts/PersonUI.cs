using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PersonUI : MonoBehaviour
{
    [Header("UI设置")]
    public TMP_FontAsset chineseFontAsset;
    public Canvas uiCanvas;
    
    [Header("UI面板")]
    public GameObject satietyPanel; // 饱腹值面板
    private TextMeshProUGUI satietyText; // 饱腹值文本
    
    // 气泡相关
    private GameObject speechBubble;
    private TextMeshProUGUI bubbleText;
    private Coroutine hideBubbleCoroutine;
    
    // 外部引用
    private Person person;
    private PersonController personController;
    
    void Start()
    {
        // 获取引用
        person = GetComponent<Person>();
        personController = GetComponent<PersonController>();
        
        CreateSpeechBubble();
        InitializeSatietyPanel();
        
        Debug.Log("PersonUI组件已启动");
    }
    
    void Update()
    {
        // 实时更新气泡位置（如果气泡正在显示）
        UpdateBubblePosition();
    }
    
    void UpdateBubblePosition()
    {
        if (speechBubble != null && speechBubble.activeSelf && bubbleText != null)
        {
            Vector3 worldPos = Vector3.zero;
            if (personController != null)
            {
                worldPos = personController.GetWorldPosition();
            }
            else
            {
                worldPos = transform.position;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;

            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0) return; // 在相机后面

            // 更新气泡位置
            RectTransform bubbleRect = speechBubble.GetComponent<RectTransform>();
            bubbleRect.position = screenPos + new Vector3(0, 40, 0);
        }
    }
    
    public void ShowMessageInBubble(string message)
    {
        if (speechBubble != null && bubbleText != null)
        {
            // 获取角色世界位置
            Vector3 worldPos = Vector3.zero;

            if (personController != null)
            {
                worldPos = personController.GetWorldPosition();
            }
            else
            {
                worldPos = transform.position;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("找不到主相机");
                return;
            }

            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0) return; // 在相机后面

            // 设置文本内容
            bubbleText.text = message;
            
            // 强制更新文本布局
            Canvas.ForceUpdateCanvases();
            
            // 根据文字长度调整气泡大小
            float textWidth = bubbleText.preferredWidth + 20; // 加一些边距
            float textHeight = bubbleText.preferredHeight + 20;
            
            // 限制最小和最大宽度
            textWidth = Mathf.Clamp(textWidth, 100f, 400f);
            textHeight = Mathf.Clamp(textHeight, 40f, 200f);
            
            RectTransform bubbleRect = speechBubble.GetComponent<RectTransform>();
            bubbleRect.sizeDelta = new Vector2(textWidth, textHeight);
            
            // 设置气泡位置
            bubbleRect.position = screenPos + new Vector3(0, 40, 0);

            // 显示气泡
            speechBubble.SetActive(true);

            // 停止之前的隐藏协程
            if (hideBubbleCoroutine != null)
            {
                StopCoroutine(hideBubbleCoroutine);
            }

            // 开始新的隐藏协程，3秒后隐藏
            hideBubbleCoroutine = StartCoroutine(HideBubbleAfterDelay(3f));
            
            // Debug.Log($"显示气泡消息: {message}");
        }
        else
        {
            Debug.LogError("气泡组件未正确初始化");
        }
    }
    
    IEnumerator HideBubbleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (speechBubble != null)
        {
            speechBubble.SetActive(false);
        }
    }
    
    void CreateSpeechBubble()
    {
        // 创建气泡背景
        speechBubble = new GameObject("SpeechBubble");
        speechBubble.transform.SetParent(uiCanvas.transform);

        // 添加背景图片
        Image bubbleImage = speechBubble.AddComponent<Image>();
        bubbleImage.color = new Color(1f, 1f, 1f, 0.95f);

        // 设置气泡大小
        RectTransform bubbleRect = speechBubble.GetComponent<RectTransform>();
        bubbleRect.sizeDelta = new Vector2(250, 60);

        // 创建文本
        GameObject textObject = new GameObject("BubbleText");
        textObject.transform.SetParent(speechBubble.transform);

        bubbleText = textObject.AddComponent<TextMeshProUGUI>();
        bubbleText.text = "";
        bubbleText.fontSize = 14;
        bubbleText.color = Color.black;
        bubbleText.alignment = TextAlignmentOptions.Center;

        // 设置中文字体
        SetChineseFont();

        // 设置文本填满气泡
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = new Vector2(-10, -10); // 留一点边距
        textRect.anchoredPosition = Vector2.zero;

        // 初始隐藏
        speechBubble.SetActive(false);
    }

    void SetChineseFont()
    {
        if (chineseFontAsset != null)
        {
            bubbleText.font = chineseFontAsset;
            return;
        }

        // 尝试自动查找中文字体
        TMP_FontAsset[] allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (TMP_FontAsset font in allFonts)
        {
            string fontName = font.name.ToLower();
            if (fontName.Contains("chinese") || fontName.Contains("中文") ||
                fontName.Contains("noto") || fontName.Contains("source han"))
            {
                bubbleText.font = font;
                break;
            }
        }
    }

    public void UpdateSatietyDisplay(int satiety)
    {
        if (satietyText != null)
        {
            satietyText.text = $"饱腹值: {satiety}";
        }
    }

    void InitializeSatietyPanel()
    {
        if (satietyPanel == null)
        {
            CreateSatietyPanel();
        }
        else
        {
            // 如果已经有面板，获取组件引用
            satietyText = satietyPanel.GetComponentInChildren<TextMeshProUGUI>();
        }
        
        if (satietyPanel != null)
        {
            satietyPanel.SetActive(true);
        }
    }
    
    void CreateSatietyPanel()
    {
        // 创建饱腹值面板
        satietyPanel = new GameObject("SatietyPanel");
        satietyPanel.transform.SetParent(uiCanvas.transform);
        
        // 添加背景
        Image panelBg = satietyPanel.AddComponent<Image>();
        panelBg.color = new Color(0, 0, 0, 0.7f);
        
        // 设置面板位置和大小（左上角）
        RectTransform panelRect = satietyPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(10, -10);
        panelRect.sizeDelta = new Vector2(150, 40);
        
        // 创建文本
        GameObject textObj = new GameObject("SatietyText");
        textObj.transform.SetParent(satietyPanel.transform);
        
        satietyText = textObj.AddComponent<TextMeshProUGUI>();
        satietyText.text = "饱腹值: 80";
        satietyText.fontSize = 16;
        satietyText.color = Color.white;
        satietyText.alignment = TextAlignmentOptions.Center;
        
        // 设置文本位置填满面板
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(5, 5);
        textRect.offsetMax = new Vector2(-5, -5);
    }
}