using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System;

public class Functions : MonoBehaviour
{
    private Person targetPerson;
    private bool isMovingToTarget = false; // 是否正在移动到目标位置
    private string pendingCollectItemId = ""; // 待采集的物品ID
    private string pendingCollectCategory = ""; // 待采集的物品类别
    
    public object[] AVAILABLE_FUNCTIONS;
    
    void Awake()
    {
        InitializeAvailableFunctions();
    }

    void Start()
    {
        targetPerson = FindFirstObjectByType<Person>();
        if (targetPerson == null)
        {
            Debug.LogError("未找到Person实例！");
        }
    }
    
    void Update()
    {
        // 检查移动状态
        if (isMovingToTarget && targetPerson != null)
        {
            PersonController controller = targetPerson.personController;
            if (controller != null && !controller.IsAutoMoving())
            {
                // 移动完成
                isMovingToTarget = false;
                
                // 如果有待执行的采集任务，执行它
                if (!string.IsNullOrEmpty(pendingCollectItemId) && !string.IsNullOrEmpty(pendingCollectCategory))
                {
                    targetPerson.CollectAndEat(pendingCollectItemId, pendingCollectCategory);
                    pendingCollectItemId = "";
                    pendingCollectCategory = "";
                }
            }
        }
    }
    
    void InitializeAvailableFunctions()
    {
        AVAILABLE_FUNCTIONS = new object[]
        {
            new {
                type = "function",
                function = new {
                    name = "UpdateKnowledges",
                    description = "更新知识库信息，当玩家提供物品新信息时使用",
                    parameters = new {
                        type = "object",
                        properties = new {
                            itemId = new { type = "string", description = "ID" },
                            category = new { type = "string", description = "类型/名字" },
                            description = new { type = "string", description = "描述信息" }
                        },
                        required = new string[] { "itemId" }
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "MoveToPosition",
                    description = "需要和物品互动时，移动到指定位置",
                    parameters = new {
                        type = "object",
                        properties = new {
                            itemId = new { type = "string", description = "ID" },
                            position = new { type = "array", items = new { type = "number" } }
                        },
                        required = new string[] { "itemId", "position" }
                    }
                }
            },
            new {
                type = "function",
                function = new {
                    name = "CollectAndEat",
                    description = "采集并食用物品",
                    parameters = new {
                        type = "object",
                        properties = new {
                            itemId = new { type = "string", description = "ID" },
                            category = new { type = "string", description = "类型" }
                        },
                        required = new string[] { "itemId", "category" }
                    }
                }
            }
        };
    }
    

    public void ExecuteFunction(string functionName, string arguments)
    {
        if (targetPerson == null)
        {
            Debug.LogError("Person实例为空，无法执行函数");
            return;
        }
        
        try
        {
            switch (functionName)
            {
                case "UpdateKnowledges":
                    var args = JsonConvert.DeserializeObject<Dictionary<string, object>>(arguments);
                    string category = args.ContainsKey("category") ? args["category"].ToString() : "";
                    string description = args.ContainsKey("description") ? args["description"].ToString() : "";

                    targetPerson.UpdateKnowledge(args["itemId"].ToString(), category, description);
                    break;
                case "MoveToPosition":
                    var moveArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(arguments);
                    if (moveArgs.ContainsKey("position"))
                    {
                        Vector3Int position;
                        
                        // 处理position参数，现在是字符串格式 "x,y,z"
                        if (moveArgs["position"] is string positionStr)
                        {
                            string[] coords = positionStr.Split(',');
                            if (coords.Length >= 2)
                            {
                                position = new Vector3Int(
                                    int.Parse(coords[0].Trim()),
                                    int.Parse(coords[1].Trim()),
                                    coords.Length > 2 ? int.Parse(coords[2].Trim()) : 0
                                );
                            }
                            else
                            {
                                Debug.LogError("位置字符串格式错误，应为 'x,y,z' 格式: " + positionStr);
                                break;
                            }
                        }
                        else if (moveArgs["position"] is Newtonsoft.Json.Linq.JArray jArray)
                        {
                            // 兼容数组格式
                            position = new Vector3Int(
                                Mathf.RoundToInt(Convert.ToSingle(jArray[0])),
                                Mathf.RoundToInt(Convert.ToSingle(jArray[1])),
                                jArray.Count > 2 ? Mathf.RoundToInt(Convert.ToSingle(jArray[2])) : 0
                            );
                        }
                        else if (moveArgs["position"] is List<object> positionList)
                        {
                            // 兼容List格式
                            position = new Vector3Int(
                                Mathf.RoundToInt(Convert.ToSingle(positionList[0])),
                                Mathf.RoundToInt(Convert.ToSingle(positionList[1])),
                                positionList.Count > 2 ? Mathf.RoundToInt(Convert.ToSingle(positionList[2])) : 0
                            );
                        }
                        else
                        {
                            Debug.LogError("位置参数格式不支持: " + moveArgs["position"].GetType());
                            break;
                        }
                        
                        targetPerson.MoveToPosition(position);
                        isMovingToTarget = true; // 标记正在移动
                    }
                    else
                    {
                        Debug.LogError("缺少位置参数");
                    }
                    break;
                case "CollectAndEat":
                    var collectArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(arguments);
                    
                    // 检查是否正在移动，如果是则存储待执行的采集任务
                    if (isMovingToTarget)
                    {
                        pendingCollectItemId = collectArgs["itemId"].ToString();
                        pendingCollectCategory = collectArgs["category"].ToString();
                        Debug.Log("正在移动中，采集任务已排队等待");
                    }
                    else
                    {
                        // 立即执行采集
                        targetPerson.CollectAndEat(collectArgs["itemId"].ToString(), collectArgs["category"].ToString());
                    }
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"执行函数错误: {e.Message}");
        }
    }
}