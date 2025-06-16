using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;
using TMPro;
using System.Linq;

public class MapManager : MonoBehaviour
{
    [Header("地图设置")]
    public int mapWidth = 64;
    public int mapHeight = 64;
    public TMP_FontAsset chineseFontAsset;
    
    [Header("瓦片引用")]
    public Tilemap tilemap;
    public Tilemap tilemapUp;
    public TileBase grassTile;
    public TileBase waterTile;
    public TileBase treeTile;
    public TileBase shrubTile;
    public TileBase redwoodTile;
    public TileBase rockTile;
    public TileBase rabbitTile;
    public TileBase wolfTile;

    [Header("时间和光照设置")]
    public GameObject Clock;
    public Light2D globalLight; // 全局光照
    public float timeSpeed = 8f; // 时间流速比例 1:8
    public Color dayLightColor = Color.white;
    public Color nightLightColor = new Color(0.3f, 0.3f, 0.5f, 1f);
    public float dayLightIntensity = 1f;
    public float nightLightIntensity = 0.3f;
    
    private int[,] mapData;
    private TextMeshProUGUI clockText; // 时钟显示文本
    
    // 时间系统
    private float gameTime = 6f; // 初始时间6点
    private int currentDay = 0;
    
    // 物品信息结构
    [System.Serializable]
    public class ItemInfo
    {
        public string itemId;        // 物品唯一ID，5位数字，从id00001开始
        public string categoryId;    // 类别ID，4位数字，从cid0001开始
        public string itemType;      // 物品类型名称
        public Vector3Int position;
        public TileBase tileBase;
        
        public ItemInfo(string id, string catId, string type, Vector3Int pos, TileBase tile)
        {
            itemId = id;
            categoryId = catId;
            itemType = type;
            position = pos;
            tileBase = tile;
        }
    }
    
    // 记录所有物品的坐标和信息
    public System.Collections.Generic.List<ItemInfo> allItems = new System.Collections.Generic.List<ItemInfo>();
    
    // ID计数器
    private int nextItemId = 1;         // 物品ID计数器，从1开始
    private int nextCategoryId = 1;     // 类别ID计数器，从1开始
    
    // 类别ID映射
    private System.Collections.Generic.Dictionary<string, string> typeToCategoryId = new System.Collections.Generic.Dictionary<string, string>();
    
    void Start()
    { 
        GenerateMap();
        InitializeLighting();
        InitializeClock();
    }
    
    void Update()
    {
        UpdateTimeSystem();
        UpdateLighting();
        UpdateClockDisplay();
    }
    
    void GenerateMap()
    {
        mapData = new int[mapWidth, mapHeight];
        
        // 添加tilemap空值检查
        if (tilemap == null)
        {
            Debug.LogError("Tilemap引用为空，请在Inspector中设置Tilemap组件");
            return;
        }
        
        // 岛屿中心点
        Vector2 islandCenter = new Vector2(mapWidth / 2f, mapHeight / 2f);
        float islandRadius = 20f; // 基础半径
        
        // 存储草地位置
        System.Collections.Generic.List<Vector3Int> grassPositions = new System.Collections.Generic.List<Vector3Int>();
        
        // 生成岛屿地图
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                // 计算到岛屿中心的距离
                float distanceToCenter = Vector2.Distance(new Vector2(x, y), islandCenter);
                
                // 使用多层噪声创建不规则边缘
                float noise1 = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 8f;
                float noise2 = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 4f;
                float noise3 = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 2f;
                
                // 调整半径，创建不规则形状
                float adjustedRadius = islandRadius + noise1 + noise2 + noise3;
                
                if (distanceToCenter <= adjustedRadius)
                {
                    // 在岛屿内部 - 先放置草地
                    mapData[x, y] = 1;
                    tilemap.SetTile(new Vector3Int(x, y, 0), grassTile);
                    grassPositions.Add(new Vector3Int(x, y, 0));
                }
                else
                {
                    // 岛屿外部 - 水
                    mapData[x, y] = 0;
                    tilemap.SetTile(new Vector3Int(x, y, 0), waterTile);
                }
            }
        }
        
        // 在草地上随机放置物品，密度50%
        PlaceRandomItems(grassPositions);
    }
    
    void PlaceRandomItems(System.Collections.Generic.List<Vector3Int> grassPositions)
    {
        // 清空之前的记录并重置计数器
        allItems.Clear();
        nextItemId = 1;
        nextCategoryId = 1;
        typeToCategoryId.Clear();
        
        // 定义物品类型和权重
        TileBase[] itemTiles = { treeTile, shrubTile, redwoodTile, rockTile, rabbitTile, wolfTile };
        string[] itemTypes = { "tree", "shrub", "redwood", "rock", "rabbit", "wolf" };
        float[] itemWeights = { 0.25f, 0.2f, 0.15f, 0.2f, 0.15f, 0.05f };
        
        // 初始化类别ID
        for (int i = 0; i < itemTypes.Length; i++)
        {
            typeToCategoryId[itemTypes[i]] = GenerateCategoryId();
        }
        
        // 随机打乱草地位置并放置物品
        int itemCount = Mathf.RoundToInt(grassPositions.Count * 0.1f);
        for (int i = 0; i < itemCount && i < grassPositions.Count; i++)
        {
            // 随机交换位置
            int randomIndex = Random.Range(i, grassPositions.Count);
            (grassPositions[i], grassPositions[randomIndex]) = (grassPositions[randomIndex], grassPositions[i]);
            
            // 根据权重选择物品类型
            int selectedIndex = SelectRandomItemIndex(itemWeights);
            
            if (itemTiles[selectedIndex] != null)
            {
                Vector3Int position = grassPositions[i];
                tilemapUp.SetTile(position, itemTiles[selectedIndex]);
                
                // 记录物品信息
                allItems.Add(new ItemInfo(
                    GenerateItemId(), 
                    typeToCategoryId[itemTypes[selectedIndex]], 
                    itemTypes[selectedIndex], 
                    position, 
                    itemTiles[selectedIndex]
                ));
            }
        }
        
        Debug.Log($"地图生成完成 - 总物品数量: {allItems.Count}");
    }
    
    // 合并ID生成方法
    string GenerateItemId() => "id" + (nextItemId++).ToString("D5");
    string GenerateCategoryId() => "cid" + (nextCategoryId++).ToString("D4");
    
    // 简化随机选择方法
    int SelectRandomItemIndex(float[] weights)
    {
        float totalWeight = weights.Sum();
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        
        for (int i = 0; i < weights.Length; i++)
        {
            currentWeight += weights[i];
            if (randomValue <= currentWeight) return i;
        }
        return 0;
    }
    
    // 合并物品查找方法
    public ItemInfo GetItemAtPosition(Vector3Int position) => allItems.Find(item => item.position == position);
    public ItemInfo GetItemById(string itemId) => allItems.Find(item => item.itemId == itemId);
    public System.Collections.Generic.List<ItemInfo> GetItemsByType(string itemType) => allItems.FindAll(item => item.itemType == itemType);
    public System.Collections.Generic.List<ItemInfo> GetItemsByCategoryId(string categoryId) => allItems.FindAll(item => item.categoryId == categoryId);

    void InitializeLighting()
    {
        // 如果没有指定全局光照，尝试查找或创建
        if (globalLight == null)
        {
            globalLight = FindFirstObjectByType<Light2D>();
            if (globalLight == null)
            {
                GameObject lightObj = new GameObject("Global Light 2D");
                globalLight = lightObj.AddComponent<Light2D>();
                globalLight.lightType = Light2D.LightType.Global;
            }
        }
    }
    
    void InitializeClock()
    {
        if (Clock != null)
        {
            // 查找Clock下的TextMeshProUGUI组件
            clockText = Clock.GetComponentInChildren<TextMeshProUGUI>();
            
            if (clockText == null)
            {
                // 如果没有找到，创建一个Text组件
                GameObject textObj = new GameObject("ClockText");
                textObj.transform.SetParent(Clock.transform);
                
                clockText = textObj.AddComponent<TextMeshProUGUI>();
                clockText.text = GetTimeString();
                clockText.font = chineseFontAsset;
                clockText.fontSize = 12;
                clockText.color = Color.white;
                clockText.alignment = TextAlignmentOptions.Center;
                
                // 设置RectTransform
                RectTransform rectTransform = textObj.GetComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.anchoredPosition = Vector2.zero;
            }
        }
        else
        {
            Debug.LogWarning("Clock GameObject未设置，无法显示时间");
        }
    }
    
    void UpdateClockDisplay()
    {
        if (clockText != null)
        {
            clockText.text = GetTimeString();
            
            // 根据白天黑夜调整文字颜色
            if (IsDay())
            {
                clockText.color = Color.black;
            }
            else
            {
                clockText.color = Color.black;
            }
        }
    }
    
    void UpdateTimeSystem()
    {
        // 更新游戏时间
        gameTime += Time.deltaTime * timeSpeed / 3600f; // 转换为小时
        
        // 处理时间溢出
        if (gameTime >= 24f)
        {
            gameTime -= 24f;
            currentDay++;
        }
    }
    
    void UpdateLighting()
    {
        if (globalLight == null) return;
        
        bool isDaytime = IsDay();
        
        // 计算光照强度和颜色的平滑过渡
        float transitionFactor = GetLightTransitionFactor();
        
        Color targetColor = isDaytime ? dayLightColor : nightLightColor;
        float targetIntensity = isDaytime ? dayLightIntensity : nightLightIntensity;
        
        // 在日出日落时间进行平滑过渡
        if (IsTransitionTime())
        {
            Color fromColor = isDaytime ? nightLightColor : dayLightColor;
            float fromIntensity = isDaytime ? nightLightIntensity : dayLightIntensity;
            
            globalLight.color = Color.Lerp(fromColor, targetColor, transitionFactor);
            globalLight.intensity = Mathf.Lerp(fromIntensity, targetIntensity, transitionFactor);
        }
        else
        {
            globalLight.color = targetColor;
            globalLight.intensity = targetIntensity;
        }
    }
    
    bool IsDay()
    {
        return gameTime >= 6f && gameTime < 18f;
    }
    
    bool IsTransitionTime()
    {
        // 日出时间 5-7点，日落时间 17-19点
        return (gameTime >= 5f && gameTime <= 7f) || (gameTime >= 17f && gameTime <= 19f);
    }
    
    float GetLightTransitionFactor()
    {
        if (gameTime >= 5f && gameTime <= 7f)
        {
            // 日出过渡
            return (gameTime - 5f) / 2f;
        }
        else if (gameTime >= 17f && gameTime <= 19f)
        {
            // 日落过渡
            return 1f - (gameTime - 17f) / 2f;
        }
        return IsDay() ? 1f : 0f;
    }
    
    // 获取当前时间信息
    public string GetTimeString()
    {
        int hours = Mathf.FloorToInt(gameTime);
        int minutes = Mathf.FloorToInt((gameTime - hours) * 60f);
        return $"第{currentDay}天 {hours:00}:{minutes:00}";
    }
    
    // 添加游戏时间
    public void AddGameTime(float timeInHours)
    {
        gameTime += timeInHours;
        
        // 处理时间溢出
        while (gameTime >= 24f)
        {
            gameTime -= 24f;
            currentDay++;
        }
    }
    
    public bool GetIsDay()
    {
        return IsDay();
    }
    
    public float GetCurrentTime()
    {
        return gameTime;
    }
    
    public int GetCurrentDay()
    {
        return currentDay;
    }
    
    // 获取Person的网格位置
    public Vector3Int GetPersonGridPosition()
    {
        PersonController controller = FindFirstObjectByType<PersonController>();
        if (controller != null)
        {
            Vector2Int pos2D = WorldToMapPosition(controller.GetWorldPosition());
            return new Vector3Int(pos2D.x, pos2D.y, 0);
        }
        return Vector3Int.zero;
    }
    
    public int GetTileType(int x, int y)
    {
        if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight)
            return -1;
        return mapData[x, y];
    }
    
    public Vector2Int WorldToMapPosition(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));
    }
}