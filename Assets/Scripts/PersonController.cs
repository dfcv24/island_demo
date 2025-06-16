using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;

public class PersonController : MonoBehaviour
{
    [Header("人物设置")]
    public Tilemap personTilemap;
    public TileBase personTile;
    public float moveSpeed = 5f; // 移动速度（单位/秒）
    
    [Header("地图引用")]
    public MapManager mapManager;
    
    [Header("角色引用")]
    public Person person; // 添加Person引用
    
    [Header("视野设置")]
    public GameObject visionOverlay; // 视野遮罩对象
    public int dayVisionRange = 5; // 白天视野范围 - 改为public
    public int nightVisionRange = 1; // 夜晚视野范围 - 改为public
    
    private Vector3Int gridPosition; // 网格位置
    private Vector3 targetWorldPosition; // 目标世界位置
    private Vector3 currentWorldPosition; // 当前世界位置
    private bool isMoving = false;
    private Tilemap visionTilemap; // 视野遮罩的Tilemap
    private TileBase visionBlockTile; // 遮罩瓦片
    private Material visionMaterial; // 视野材质
    
    private System.Collections.Generic.Queue<Vector3Int> movementQueue = new System.Collections.Generic.Queue<Vector3Int>();
    private bool isAutoMoving = false;
    
    void Start()
    {
        InitializePosition();
        InitializeVisionSystem();
    }
    
    void InitializePosition()
    {
        int xPosition = mapManager.mapWidth / 2;
        int yPosition = mapManager.mapHeight / 2;
        gridPosition = new Vector3Int(xPosition, yPosition, 0);
        
        // 计算世界位置
        currentWorldPosition = personTilemap.CellToWorld(gridPosition) + personTilemap.tileAnchor;
        targetWorldPosition = currentWorldPosition;
        
        // 在tilemap上放置瓦片
        personTilemap.SetTile(gridPosition, personTile);
    }
    
    void InitializeVisionSystem()
    {
        // 创建视野遮罩系统
        if (visionOverlay == null)
        {
            visionOverlay = new GameObject("VisionOverlay");
            visionTilemap = visionOverlay.AddComponent<Tilemap>();
            TilemapRenderer renderer = visionOverlay.AddComponent<TilemapRenderer>();
            
            // 设置渲染层级，确保在最上层
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 100;
            
            // 创建专用材质
            CreateVisionMaterial();
            renderer.material = visionMaterial;
        }
        else
        {
            visionTilemap = visionOverlay.GetComponent<Tilemap>();
        }
        
        // 创建黑色遮罩瓦片
        CreateVisionBlockTile();
        
        // 初始化视野
        UpdateVision();
    }
    
    void CreateVisionMaterial()
    {
        // 使用Unity内置的透明Shader，降低透明度让视野外区域更清楚
        visionMaterial = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
        visionMaterial.color = new Color(0, 0, 0, 0.1f);
    }
    
    void CreateVisionBlockTile()
    {
        // 创建一个简单的白色方块精灵，使用更低的透明度
        Texture2D texture = new Texture2D(16, 16);
        Color[] pixels = new Color[16 * 16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(1, 1, 1, 0.1f); 
        }
        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
        
        visionBlockTile = ScriptableObject.CreateInstance<Tile>();
        ((Tile)visionBlockTile).sprite = sprite;
    }
    
    void Update()
    {
        UpdatePosition();
        
        // 只在必要时更新视野（避免每帧都更新）
        if (!isMoving)
        {
            UpdateVision();
        }
    }
    
    void UpdateVision()
    {
        if (visionTilemap == null || mapManager == null) return;
        
        // 清除当前视野遮罩
        visionTilemap.SetTilesBlock(
            new BoundsInt(0, 0, 0, mapManager.mapWidth, mapManager.mapHeight, 1),
            new TileBase[mapManager.mapWidth * mapManager.mapHeight]
        );
        
        // 获取当前视野范围
        int visionRange = mapManager.GetIsDay() ? dayVisionRange : nightVisionRange;
        
        // 为视野范围外的区域添加半透明遮罩
        for (int x = 0; x < mapManager.mapWidth; x++)
        {
            for (int y = 0; y < mapManager.mapHeight; y++)
            {
                Vector3Int tilePos = new Vector3Int(x, y, 0);
                float distance = Vector3Int.Distance(gridPosition, tilePos);
                
                // 超出视野范围的区域添加半透明遮罩
                if (distance > visionRange)
                {
                    visionTilemap.SetTile(tilePos, visionBlockTile);
                }
            }
        }
    }
    
    void UpdatePosition()
    {
        if (isMoving)
        {
            // 平滑移动到目标位置
            currentWorldPosition = Vector3.MoveTowards(currentWorldPosition, targetWorldPosition, moveSpeed * Time.deltaTime);
            
            // 检查是否到达目标位置
            if (Vector3.Distance(currentWorldPosition, targetWorldPosition) < 0.01f)
            {
                currentWorldPosition = targetWorldPosition;
                isMoving = false;
                
                // 移动完成，通知Person消耗相关资源
                if (person != null)
                {
                    person.OnMovementCompleted();
                }
                
                // 如果还有队列中的移动，继续下一步
                if (isAutoMoving && movementQueue.Count > 0)
                {
                    MoveToNextPosition();
                }
                else if (movementQueue.Count == 0)
                {
                    isAutoMoving = false;
                }
            }
        }
        else if (isAutoMoving && movementQueue.Count > 0)
        {
            // 开始移动到队列中的下一个位置
            MoveToNextPosition();
        }
    }
    
    // 移动到队列中的下一个位置
    private void MoveToNextPosition()
    {
        if (movementQueue.Count > 0)
        {
            Vector3Int nextPos = movementQueue.Dequeue();
            
            // 更新网格位置
            personTilemap.SetTile(gridPosition, null); // 清除当前位置的瓦片
            gridPosition = nextPos;
            personTilemap.SetTile(gridPosition, personTile); // 在新位置放置瓦片
            
            // 计算新的世界位置
            targetWorldPosition = personTilemap.CellToWorld(gridPosition) + personTilemap.tileAnchor;
            isMoving = true;
            
            // Debug.Log($"移动到: {gridPosition}");
        }
    }
    
    // 移动到指定坐标的公共函数
    public void MoveToPosition(Vector3Int targetPos)
    {
        if (targetPos == gridPosition)
        {
            Debug.Log("已经在目标位置");
            return;
        }
        
        // 计算最短路径
        System.Collections.Generic.List<Vector3Int> path = CalculateShortestPath(gridPosition, targetPos);
        
        if (path.Count > 0)
        {
            // 将路径添加到移动队列
            movementQueue.Clear();
            foreach (Vector3Int pos in path)
            {
                movementQueue.Enqueue(pos);
            }
            
            isAutoMoving = true;
            Debug.Log($"开始移动到 {targetPos}，路径长度: {path.Count}");
        }
        else
        {
            Debug.LogWarning($"无法找到到达 {targetPos} 的路径");
        }
    }
    
    // 计算最短路径（使用A*算法的简化版本）
    private System.Collections.Generic.List<Vector3Int> CalculateShortestPath(Vector3Int start, Vector3Int target)
    {
        System.Collections.Generic.List<Vector3Int> path = new System.Collections.Generic.List<Vector3Int>();
        
        // 简单的直线路径计算（曼哈顿距离）
        Vector3Int current = start;
        
        while (current != target)
        {
            Vector3Int direction = Vector3Int.zero;
            
            // 优先处理X轴移动
            if (current.x < target.x)
                direction.x = 1;
            else if (current.x > target.x)
                direction.x = -1;
            // 然后处理Y轴移动
            else if (current.y < target.y)
                direction.y = 1;
            else if (current.y > target.y)
                direction.y = -1;
            
            current += direction;
            
            // 检查是否是有效位置（在地图范围内且不是水）
            if (IsValidPosition(current))
            {
                path.Add(current);
            }
            else
            {
                // 如果遇到障碍，尝试绕行
                if (!TryFindAlternativePath(current - direction, target, ref path))
                {
                    Debug.LogWarning($"无法到达目标位置 {target}");
                    break;
                }
            }
            
            // 防止无限循环
            if (path.Count > 100)
            {
                Debug.LogWarning("路径计算超出限制");
                break;
            }
        }
        
        return path;
    }
    
    // 检查位置是否有效
    private bool IsValidPosition(Vector3Int pos)
    {
        // 检查是否在地图范围内
        if (pos.x < 0 || pos.x >= mapManager.mapWidth || pos.y < 0 || pos.y >= mapManager.mapHeight)
            return false;
        
        // 检查是否是陆地（不是水）
        return mapManager.GetTileType(pos.x, pos.y) == 1;
    }
    
    // 尝试找到替代路径
    private bool TryFindAlternativePath(Vector3Int current, Vector3Int target, ref System.Collections.Generic.List<Vector3Int> path)
    {
        // 简单的绕行逻辑：尝试四个方向
        Vector3Int[] directions = {
            Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right
        };
        
        foreach (Vector3Int dir in directions)
        {
            Vector3Int testPos = current + dir;
            if (IsValidPosition(testPos))
            {
                path.Add(testPos);
                return true;
            }
        }
        
        return false;
    }
    
    // 检查是否正在自动移动
    public bool IsAutoMoving()
    {
        return isAutoMoving;
    }
    
    // 停止自动移动
    public void StopAutoMovement()
    {
        isAutoMoving = false;
        movementQueue.Clear();
    }
    
    public Vector3 GetWorldPosition()
    {
        return currentWorldPosition;
    }
    
    public Vector3Int GetGridPosition()
    {
        return gridPosition;
    }
}
