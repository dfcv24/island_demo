using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class KnowledgeItem
{
    public Vector3 itemPosition;
    public string categoryId;
    public string itemCategory;
    public string itemDescription;
    
    public KnowledgeItem(Vector3 position, string catId, string category, string description)
    {
        itemPosition = position;
        categoryId = catId;
        itemCategory = category;
        itemDescription = description;
    }
}

public class Person : MonoBehaviour
{
    [Header("绑定设置")]
    public PersonController personController;
    public PersonUI personUI;

    [Header("饱腹值设置")]
    [Range(0, 100)]
    public int satiety = 80;

    // Agent组件
    private GoalManager goalManager;
    private ActionPlanner actionPlanner;
    private MemorySystem memorySystem;
    private LearningModule learningModule;

    // 基础状态
    private List<string> chatHistory = new List<string>();
    private Dictionary<string, KnowledgeItem> knowledges = new Dictionary<string, KnowledgeItem>();
    
    // 交互状态控制
    private bool waitingForPlayerResponse = false;
    private string currentQuestionContext = "";
    private bool executingAction = false;
    
    // 添加行动间数据传递
    private string lastFoundItemId = "";
    private Vector3? lastFoundItemPosition = null;

    #region 初始化
    void Start()
    {
        InitializeAgentComponents();
        Debug.Log("Person Agent已启动");
    }

    private void InitializeAgentComponents()
    {
        goalManager = GetComponent<GoalManager>() ?? gameObject.AddComponent<GoalManager>();
        actionPlanner = GetComponent<ActionPlanner>() ?? gameObject.AddComponent<ActionPlanner>();
        memorySystem = GetComponent<MemorySystem>() ?? gameObject.AddComponent<MemorySystem>();
        learningModule = GetComponent<LearningModule>() ?? gameObject.AddComponent<LearningModule>();
    }
    #endregion

    #region 主循环
    void Update()
    {
        // 首次启动初始化
        if (chatHistory.Count == 0)
        {
            InitializeFirstTime();
            return;
        }
        
        // 等待玩家回复时暂停决策
        if (waitingForPlayerResponse)
        {
            return;
        }
        
        // 正在执行action时等待完成
        if (executingAction)
        {
            return;
        }
        
        // Agent决策循环
        ProcessAgentDecision();
        CheckAround();
    }

    private void InitializeFirstTime()
    {
        knowledges.Add("id001", new KnowledgeItem(GetGridPosition(), "cid01", "unknown", "自己"));
        ShowMessageInBubble("这是哪儿？我是谁？");
        SetWaitingForResponse("initial");
    }
    #endregion

    #region Agent决策系统
    private void ProcessAgentDecision()
    {
        Goal currentGoal = goalManager.GetCurrentGoal();
        
        if (currentGoal == null)
        {
            GenerateNewGoals();
            return;
        }
        
        if (!actionPlanner.HasPendingActions())
        {
            GenerateIntelligentPlan(currentGoal);
        }
        
        ExecuteNextAction();
    }

    private void GenerateNewGoals()
    {
        // 生存需求
        if (satiety <= 70)
        {
            goalManager.AddGoal("goal_hunger", "survival", "寻找食物缓解饥饿", 9, 0.8f);
            return;
        }
        
        // 学习需求
        foreach (var kvp in knowledges)
        {
            KnowledgeItem item = kvp.Value;
            if (item.itemCategory == "unknown" || item.itemDescription == "unknown")
            {
                goalManager.AddGoal($"goal_learn_{kvp.Key}", "learning", $"了解未知物品 {kvp.Key}", 6, 0.5f);
                return;
            }
        }
        
        // 探索需求
        if (goalManager.GetActiveGoals().Count == 0)
        {
            goalManager.AddGoal("goal_explore", "exploration", "探索周围环境", 3, 0.2f);
        }
    }

    private void GenerateIntelligentPlan(Goal goal)
    {
        Strategy recommendedStrategy = learningModule.RecommendStrategy(goal.goalType);
        
        if (recommendedStrategy != null)
        {
            Debug.Log($"使用策略: {recommendedStrategy.strategyId} (效果: {recommendedStrategy.effectiveness:F2})");
            learningModule.StartStrategy(recommendedStrategy.strategyId);
        }
        
        actionPlanner.PlanActionsForGoal(goal);
    }
    #endregion

    #region 行动执行系统
    private void ExecuteNextAction()
    {
        PlannedAction action = actionPlanner.GetNextAction();
        if (action == null) return;
        
        // 标记开始执行action
        executingAction = true;
        
        switch (action.actionType)
        {
            case "think":           ExecuteThinkAction(action); break;
            case "search_food":     ExecuteSearchFoodAction(action); break;
            case "move_to_item":    ExecuteMoveToItemAction(action); break;
            case "collect_eat":     ExecuteCollectEatAction(action); break;
            case "ask_about_item":  ExecuteAskAboutItemAction(action); break;
            default:                CompleteAction(); break;
        }
    }

    private void CompleteAction()
    {
        actionPlanner.CompleteCurrentAction();
        executingAction = false; // 标记action执行完成
    }

    private void ExecuteThinkAction(PlannedAction action)
    {
        string details = action.description;
        
        var relevantMemories = memorySystem.RecallMemories("outcome", "", GetGridPosition(), 10f);
        if (relevantMemories.Count > 0)
        {
            details += $" 我记得之前{relevantMemories.First().content}";
        }
        
        ModelCall.Instance.ThinkModel(details, (response) => {
            ShowMessageInBubble(response);
            memorySystem.StoreMemory("action", $"思考：{response}", GetGridPosition(), 0.6f);
            learningModule.LearnFromExperience("think", GetCurrentContext(), !string.IsNullOrEmpty(response));
            CompleteAction();
        });
    }

    private void ExecuteSearchFoodAction(PlannedAction action)
    {
        string foundItemId = null;
        Vector3? foundItemPosition = null;
        
        // 从记忆中搜索食物
        var foodMemories = memorySystem.RecallMemories("observation", "食物", GetGridPosition(), 20f);
        
        foreach (var memory in foodMemories)
        {
            if (memory.data.ContainsKey("item_id"))
            {
                string itemId = memory.data["item_id"] as string;
                if (knowledges.ContainsKey(itemId))
                {
                    foundItemId = itemId;
                    foundItemPosition = knowledges[itemId].itemPosition;
                    break;
                }
            }
        }
        
        // 从知识库中搜索食物
        if (foundItemId == null)
        {
            foreach (var kvp in knowledges)
            {
                KnowledgeItem item = kvp.Value;
                if (item.itemCategory.Contains("苹果") || item.itemCategory.Contains("食物"))
                {
                    foundItemId = kvp.Key;
                    foundItemPosition = item.itemPosition;
                    
                    var data = new Dictionary<string, object> { {"item_id", kvp.Key} };
                    memorySystem.StoreMemory("observation", $"发现食物：{item.itemCategory}", item.itemPosition, 0.8f, data);
                    break;
                }
            }
        }
        
        // 将找到的食物信息存储到共享状态中
        if (foundItemId != null && foundItemPosition.HasValue)
        {
            lastFoundItemId = foundItemId;
            lastFoundItemPosition = foundItemPosition;
        }
        else
        {
            lastFoundItemId = "";
            lastFoundItemPosition = null;
        }
        
        bool success = foundItemId != null;
        learningModule.LearnFromExperience("search_food", GetCurrentContext(), success);
        CompleteAction();
    }

    private void ExecuteCollectEatAction(PlannedAction action)
    {
        bool success = false;
        
        // 优先使用共享状态中的食物信息
        string foodId = lastFoundItemId;
        
        // 如果共享状态中没有，尝试从当前位置查找
        if (string.IsNullOrEmpty(foodId))
        {
            foreach (var kvp in knowledges)
            {
                KnowledgeItem item = kvp.Value;
                if ((item.itemCategory.Contains("苹果") || item.itemCategory.Contains("食物")) &&
                    Vector3.Distance(item.itemPosition, GetGridPosition()) <= 1f)
                {
                    foodId = kvp.Key;
                    break;
                }
            }
        }
        
        if (!string.IsNullOrEmpty(foodId) && knowledges.ContainsKey(foodId))
        {
            KnowledgeItem food = knowledges[foodId];
            CollectAndEat(foodId, food.itemCategory);
            
            memorySystem.StoreMemory("action", $"成功采集并食用：{food.itemCategory}", food.itemPosition, 0.9f);
            goalManager.CompleteGoal("goal_hunger");
            learningModule.EndStrategy(true);
            success = true;
            
            // 清空共享状态
            lastFoundItemId = "";
            lastFoundItemPosition = null;
        }
        
        learningModule.LearnFromExperience("collect_eat", GetCurrentContext(), success);
        CompleteAction();
    }

    private void ExecuteAskAboutItemAction(PlannedAction action)
    {
        // 优先使用action中指定的目标物品
        string targetItemId = action.targetItemId;
        KnowledgeItem targetItem = null;
        
        if (!string.IsNullOrEmpty(targetItemId) && knowledges.ContainsKey(targetItemId))
        {
            targetItem = knowledges[targetItemId];
        }
        else
        {
            // 如果action中没有指定，查找第一个未知物品
            foreach (var kvp in knowledges)
            {
                KnowledgeItem item = kvp.Value;
                if (item.itemCategory == "unknown" || item.itemDescription == "unknown")
                {
                    targetItemId = kvp.Key;
                    targetItem = item;
                    break;
                }
            }
        }
        
        if (targetItem != null)
        {
            string question = $"我想了解这个物品{targetItemId}的信息，设计一个询问句";
            
            var askMemories = memorySystem.RecallMemories("interaction", "询问", targetItem.itemPosition, 5f);
            if (askMemories.Count > 0)
            {
                question = $"我想了解更多关于这个物品{targetItemId}的详细信息";
            }
            
            ModelCall.Instance.ThinkModel(question, (response) => {
                ShowMessageInBubble(response);
                
                var data = new Dictionary<string, object> { 
                    {"item_id", targetItemId}, 
                    {"question", response},
                    {"waiting_for_answer", true}
                };
                memorySystem.StoreMemory("interaction", $"询问物品：{response}", targetItem.itemPosition, 0.7f, data);
                
                // 设置等待状态
                SetWaitingForResponse(targetItemId);
                
                learningModule.LearnFromExperience("ask_about_item", GetCurrentContext(), true);
                CompleteAction();
                actionPlanner.ClearActions();
            });
            return;
        }
        
        learningModule.LearnFromExperience("ask_about_item", GetCurrentContext(), false);
        CompleteAction();
    }

    private void ExecuteMoveToItemAction(PlannedAction action)
    {
        // 优先使用action中的目标信息
        if (action.targetPosition.HasValue && !string.IsNullOrEmpty(action.targetItemId))
        {
            Vector3Int targetPos = action.targetPosition.Value;
            string targetItemId = action.targetItemId;

            // 检查物品是否在地图上（z轴为0）
            if (targetPos.z != 0)
            {
                Debug.Log($"物品 {targetItemId} 不在地图上（z={targetPos.z}），跳过移动");
                learningModule.LearnFromExperience("move_to_item", GetCurrentContext(), true);
                CompleteAction();
                return;
            }

            if (GetGridPosition() == targetPos)
            {
                learningModule.LearnFromExperience("move_to_item", GetCurrentContext(), true);
                CompleteAction();
            }
            else
            {
                personController.MoveToPosition(targetPos);
            }
        }
        // 如果action中没有目标信息，使用共享状态中的信息
        else if (!string.IsNullOrEmpty(lastFoundItemId) && lastFoundItemPosition.HasValue)
        {
            Vector3 itemPos = lastFoundItemPosition.Value;
            
            // 检查物品是否在地图上（z轴为0）
            if (itemPos.z != 0)
            {
                Debug.Log($"物品 {lastFoundItemId} 不在地图上（z={itemPos.z}），跳过移动");
                learningModule.LearnFromExperience("move_to_item", GetCurrentContext(), true);
                CompleteAction();
                return;
            }

            Vector3Int targetPos = new Vector3Int(
                Mathf.RoundToInt(itemPos.x),
                Mathf.RoundToInt(itemPos.y),
                0
            );

            if (GetGridPosition() == targetPos)
            {
                learningModule.LearnFromExperience("move_to_item", GetCurrentContext(), true);
                CompleteAction();
            }
            else
            {
                personController.MoveToPosition(targetPos);
            }
        }
        else
        {
            Debug.LogWarning("目标物品信息不完整，无法执行移动到物品的行动");
            learningModule.LearnFromExperience("move_to_item", GetCurrentContext(), false);
            CompleteAction();
        }
    }
    #endregion

    #region 环境感知系统
    private void CheckAround()
    {
        if (personController.mapManager == null) return;

        Vector3Int personPos = GetGridPosition();
        int visionRange = personController.mapManager.GetIsDay() ? 
            personController.dayVisionRange : personController.nightVisionRange;

        foreach (MapManager.ItemInfo item in personController.mapManager.allItems)
        {
            float distance = Mathf.Abs(personPos.x - item.position.x) + Mathf.Abs(personPos.y - item.position.y);
            
            if (distance <= visionRange && !knowledges.ContainsKey(item.itemId))
            {
                // 知识同步
                string knownCategory = "unknown";
                string knownDescription = "unknown";

                foreach (var kvp in knowledges)
                {
                    KnowledgeItem existingItem = kvp.Value;
                    if (existingItem.categoryId == item.categoryId &&
                        existingItem.itemCategory != "unknown" &&
                        existingItem.itemDescription != "unknown")
                    {
                        knownCategory = existingItem.itemCategory;
                        knownDescription = existingItem.itemDescription;
                        break;
                    }
                }

                KnowledgeItem newKnowledge = new KnowledgeItem(item.position, item.categoryId, knownCategory, knownDescription);
                knowledges.Add(item.itemId, newKnowledge);
                
                var data = new Dictionary<string, object> { {"item_id", item.itemId}, {"category_id", item.categoryId} };
                memorySystem.StoreMemory("observation", $"发现新物品：{item.itemId}", item.position, 0.7f, data);
                
                Debug.Log($"发现新物品 ID:{item.itemId}, 类别ID:{item.categoryId}, 位置:{item.position}");
            }
        }
    }
    #endregion

    #region 玩家交互系统
    public void ReceivePlayerMessage(string message)
    {
        Debug.Log($"接收到玩家消息: {message}");
        chatHistory.Add($"user: {message}");

        // 确保玩家在知识库中
        string playerId = "id000";
        Vector3 playerPosition = new Vector3(0, 0, 1);
        if (!knowledges.ContainsKey(playerId))
        {
            knowledges.Add(playerId, new KnowledgeItem(playerPosition, "cid00", "unknown", "player"));
        }

        // 存储交互记忆
        var data = new Dictionary<string, object> { {"player_message", message} };
        memorySystem.StoreMemory("interaction", $"玩家说：{message}", playerPosition, 0.8f, data);

        // 通过tool-call处理
        if (ModelCall.Instance != null)
        {
            ModelCall.Instance.ToolsModel(message, ModelResponseTools);
        }
        else
        {
            Debug.LogError("ModelCall实例未找到");
        }
    }

    private void ModelResponseTools(string response)
    {
        if (!string.IsNullOrEmpty(response))
        {
            ShowMessageInBubble(response);
            memorySystem.StoreMemory("interaction", $"AI回复：{response}", GetGridPosition(), 0.7f);
        }
        
        // 重置等待状态
        if (waitingForPlayerResponse)
        {
            ResetWaitingState();
        }
    }

    private void SetWaitingForResponse(string context)
    {
        waitingForPlayerResponse = true;
        currentQuestionContext = context;
        Debug.Log($"开始等待玩家回复，问题上下文: {context}");
    }

    private void ResetWaitingState()
    {
        Debug.Log($"重置等待状态，之前的问题上下文: {currentQuestionContext}");
        
        waitingForPlayerResponse = false;
        currentQuestionContext = "";
        
        // 完成学习目标
        var currentGoal = goalManager.GetCurrentGoal();
        if (currentGoal != null && currentGoal.goalType == "learning")
        {
            goalManager.CompleteGoal(currentGoal.goalId);
            learningModule.EndStrategy(true);
        }
        
        Debug.Log("已重置等待状态，可以继续执行其他任务");
    }
    #endregion

    #region 知识管理
    public void UpdateKnowledge(string itemId, string category = "", string description = "")
    {
        if (!knowledges.ContainsKey(itemId)) return;

        KnowledgeItem targetItem = knowledges[itemId];
        string categoryId = targetItem.categoryId;

        // 更新目标物品
        if (!string.IsNullOrEmpty(category)) targetItem.itemCategory = category;
        if (!string.IsNullOrEmpty(description)) targetItem.itemDescription = description;

        // 存储学习记忆
        memorySystem.StoreMemory("learning", $"学到：{itemId}是{category}", targetItem.itemPosition, 0.9f);
        learningModule.LearnFromExperience("learn_item", GetCurrentContext(), true);

        // 同步更新相同类别ID的所有物品
        foreach (var kvp in knowledges)
        {
            KnowledgeItem item = kvp.Value;
            if (item.categoryId == categoryId && kvp.Key != itemId)
            {
                if (!string.IsNullOrEmpty(category)) item.itemCategory = category;
                if (!string.IsNullOrEmpty(description)) item.itemDescription = description;
            }
        }

        Debug.Log($"知识库已更新: {itemId} - {targetItem.itemCategory} - {targetItem.itemDescription}");
    }
    #endregion

    #region 工具方法
    public void OnMovementCompleted()
    {
        ConsumeSatiety(1);
        memorySystem.StoreMemory("action", "完成移动", GetGridPosition(), 0.5f);
        
        PlannedAction currentAction = actionPlanner.GetNextAction();
        if (currentAction != null && currentAction.actionType == "move_to_item")
        {
            CompleteAction(); // 使用统一的完成方法
        }
    }

    public void CollectAndEat(string itemId, string category)
    {
        if (knowledges.ContainsKey(itemId))
        {
            KnowledgeItem item = knowledges[itemId];
            if (item.itemCategory == category)
            {
                ShowMessageInBubble($"采集并食用物品: {category}");
                ConsumeSatiety(-20);
            }
        }
    }

    public void ConsumeSatiety(int amount = 1)
    {
        satiety = Mathf.Clamp(satiety - amount, 0, 100);
        personUI.UpdateSatietyDisplay(satiety);

        if (satiety <= 0)
        {
            Debug.Log("饱腹值过低！");
        }
    }

    private string GetCurrentContext()
    {
        string context = $"satiety_{satiety/25}";
        
        if (personController != null && personController.IsAutoMoving())
        {
            context += "_moving";
        }
        
        int nearbyItems = knowledges.Values.Count(item => 
            Vector3.Distance(item.itemPosition, GetGridPosition()) <= 3f);
        
        if (nearbyItems > 0)
        {
            context += "_items_nearby";
        }
        
        return context;
    }

    public void ShowMessageInBubble(string message)
    {
        personUI.ShowMessageInBubble(message);
        chatHistory.Add($"assistant: {message}");
    }

    // 公共接口
    public Dictionary<string, KnowledgeItem> GetKnowledges() => knowledges;
    public Vector3Int GetGridPosition() => personController.GetGridPosition();
    public List<string> GetChatHistory() => chatHistory;
    public void MoveToPosition(Vector3Int position) => personController.MoveToPosition(position);
    #endregion
}
