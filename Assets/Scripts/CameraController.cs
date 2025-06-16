using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("相机设置")]
    public float moveSpeed = 10f;
    public float zoomSpeed = 2f;
    public float minZoom = 2f;
    public float maxZoom = 20f;
    
    [Header("地图引用")]
    public MapManager mapManager;

    [Header("跟随设置")]
    public PersonController personController; // Person控制器
    public float followSpeed = 5f;
    public bool enableFollow = true;
    
    private Camera cam;
    private Mouse mouse;
    private Keyboard keyboard;
    
    void Start()
    {
        cam = GetComponent<Camera>();
        mouse = Mouse.current;
        keyboard = Keyboard.current;
        
        // 如果没有设置跟随目标，尝试查找PersonController
        if (personController == null)
        {
            personController = FindFirstObjectByType<PersonController>();
            if (personController != null)
            {
                Debug.Log("自动找到PersonController作为跟随目标");
            }
        }
        
        // 延迟居中相机，让其他组件先初始化
        Invoke("CenterCamera", 0.1f);
    }
    
    void CenterCamera()
    {
        if (mapManager != null)
        {
            // 计算地图中心位置
            float centerX = mapManager.mapWidth / 2f;
            float centerY = mapManager.mapHeight / 2f;
            
            // 设置相机位置（保持Z轴不变）
            transform.position = new Vector3(centerX, centerY, transform.position.z);
            
            Debug.Log($"相机已居中到地图中心: ({centerX}, {centerY})");
        }
        else
        {
            // 如果没有MapManager引用，尝试自动查找
            mapManager = FindFirstObjectByType<MapManager>();
            if (mapManager != null)
            {
                CenterCamera(); // 递归调用
            }
            else
            {
                Debug.LogWarning("未找到MapManager，无法居中相机");
            }
        }
    }
    
    void Update()
    {
        if (enableFollow && personController != null)
        {
            FollowPerson();
        }
        else
        {
            HandleMovement();
        }
        HandleZoom();
    }
    
    void FollowPerson()
    {
        Vector3 personWorldPos = personController.GetWorldPosition();
        Vector3 targetPosition = new Vector3(personWorldPos.x, personWorldPos.y, transform.position.z);
        
        // 使用更平滑的跟随，避免突然移动
        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
    }

    void HandleMovement()
    {
        Vector2 moveInput = Vector2.zero;
        
        // 添加空值检查
        if (keyboard == null) return;
        
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            moveInput.x = -1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            moveInput.x = 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            moveInput.y = -1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            moveInput.y = 1f;
        
        Vector3 movement = new Vector3(moveInput.x, moveInput.y, 0) * moveSpeed * Time.deltaTime;
        transform.Translate(movement);
    }
    
    void HandleZoom()
    {
        // 添加空值检查
        if (mouse == null) return;
        
        Vector2 scrollDelta = mouse.scroll.ReadValue();
        float scroll = scrollDelta.y / 120f; // 标准化滚轮值
        
        if (scroll != 0)
        {
            cam.orthographicSize -= scroll * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        }
    }
}