using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

[System.Serializable]
public class PlannedAction
{
    public string actionId;
    public string actionType;
    public string description;
    public Vector3Int? targetPosition;
    public string targetItemId;
    public bool isCompleted;
    
    public PlannedAction(string id, string type, string desc)
    {
        actionId = id;
        actionType = type;
        description = desc;
        isCompleted = false;
    }
}

public class ActionPlanner : MonoBehaviour
{
    [SerializeField] private Queue<PlannedAction> actionQueue = new Queue<PlannedAction>();
    [SerializeField] private PlannedAction currentAction;
    
    private Person person;
    private GoalManager goalManager;
    
    void Start()
    {
        person = GetComponent<Person>();
        goalManager = GetComponent<GoalManager>();
    }
    
    public void PlanActionsForGoal(Goal goal)
    {
        // 清空当前行动队列
        actionQueue.Clear();
        
        switch (goal.goalType)
        {
            case "survival":
                PlanSurvivalActions(goal);
                break;
            case "exploration":
                PlanExplorationActions(goal);
                break;
            case "learning":
                PlanLearningActions(goal);
                break;
            case "social":
                PlanSocialActions(goal);
                break;
        }
        
        Debug.Log($"为目标 '{goal.description}' 规划了 {actionQueue.Count} 个行动");
    }
    
    private void AddAction(string type, string description)
    {
        string actionId = $"action_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        PlannedAction action = new PlannedAction(actionId, type, description);
        actionQueue.Enqueue(action);
    }
    
    private void AddMoveToItemAction(string itemId, Vector3 itemPosition)
    {
        string actionId = $"action_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
        PlannedAction action = new PlannedAction(actionId, "move_to_item", "移动到物品");
        action.targetItemId = itemId;
        action.targetPosition = new Vector3Int(
            Mathf.RoundToInt(itemPosition.x), 
            Mathf.RoundToInt(itemPosition.y), 
            Mathf.RoundToInt(itemPosition.z)
        );
        actionQueue.Enqueue(action);
    }
    
    private void PlanSurvivalActions(Goal goal)
    {
        if (goal.description.Contains("饿") || goal.description.Contains("食物"))
        {
            // 寻找食物的行动序列
            AddAction("think", "分析当前饥饿状况");
            AddAction("search_food", "在知识库中搜索可食用物品");
            AddAction("move_to_item", "移动到找到的食物位置");
            AddAction("collect_eat", "采集并食用找到的食物");
        }
    }
    
    private void PlanExplorationActions(Goal goal)
    {
        // 探索未知区域的行动序列
        AddAction("think", "分析探索目标");
        AddAction("move_explore", "移动到未探索的位置");
        AddAction("observe", "观察周围环境");
    }
    
    private void PlanLearningActions(Goal goal)
    {
        // 学习新知识的行动序列
        AddAction("identify_unknown", "找到知识库中的未知物品");
        
        // 找到未知物品并添加其信息到move_to_item行动中
        var knowledges = person.GetKnowledges();
        foreach (var kvp in knowledges)
        {
            var item = kvp.Value;
            if (item.itemCategory == "unknown" || item.itemDescription == "unknown")
            {
                AddMoveToItemAction(kvp.Key, item.itemPosition);
                AddAction("ask_about_item", "向玩家询问物品信息");
                break; // 一次只处理一个未知物品
            }
        }
    }
    
    private void PlanSocialActions(Goal goal)
    {
        // 社交互动的行动序列
        AddAction("think", "思考社交需求");
        AddAction("initiate_conversation", "主动与玩家交流");
    }
    
    public PlannedAction GetNextAction()
    {
        if (currentAction != null && !currentAction.isCompleted)
        {
            return currentAction;
        }
        
        if (actionQueue.Count > 0)
        {
            currentAction = actionQueue.Dequeue();
            Debug.Log($"执行行动: {currentAction.description}");
            return currentAction;
        }
        
        return null;
    }
    
    public void CompleteCurrentAction()
    {
        if (currentAction != null)
        {
            currentAction.isCompleted = true;
            Debug.Log($"完成行动: {currentAction.description}");
            currentAction = null;
        }
    }
    
    public bool HasPendingActions()
    {
        return actionQueue.Count > 0 || (currentAction != null && !currentAction.isCompleted);
    }
    
    public void ClearActions()
    {
        actionQueue.Clear();
        currentAction = null;
    }
}
