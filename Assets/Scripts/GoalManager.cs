using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class Goal
{
    public string goalId;
    public string goalType; // "survival", "exploration", "social", "learning"
    public string description;
    public int priority; // 1-10, 10最高
    public float urgency; // 0-1, 紧急程度
    public bool isCompleted;
    public bool isActive;
    public List<string> requiredActions; // 需要执行的行动列表
    
    public Goal(string id, string type, string desc, int prio, float urg)
    {
        goalId = id;
        goalType = type;
        description = desc;
        priority = prio;
        urgency = urg;
        isCompleted = false;
        isActive = false;
        requiredActions = new List<string>();
    }
}

public class GoalManager : MonoBehaviour
{
    [SerializeField] private List<Goal> activeGoals = new List<Goal>();
    [SerializeField] private List<Goal> completedGoals = new List<Goal>();
    
    public Goal currentGoal { get; private set; }
    
    public void AddGoal(string id, string type, string description, int priority, float urgency)
    {
        Goal newGoal = new Goal(id, type, description, priority, urgency);
        activeGoals.Add(newGoal);
        SortGoalsByPriority();
        Debug.Log($"添加新目标: {description}");
    }
    
    public void CompleteGoal(string goalId)
    {
        Goal goal = activeGoals.FirstOrDefault(g => g.goalId == goalId);
        if (goal != null)
        {
            goal.isCompleted = true;
            goal.isActive = false;
            completedGoals.Add(goal);
            activeGoals.Remove(goal);
            Debug.Log($"完成目标: {goal.description}");
            
            if (currentGoal == goal)
            {
                currentGoal = null;
                SelectNextGoal();
            }
        }
    }
    
    public Goal GetCurrentGoal()
    {
        if (currentGoal == null || currentGoal.isCompleted)
        {
            SelectNextGoal();
        }
        return currentGoal;
    }
    
    private void SelectNextGoal()
    {
        if (activeGoals.Count > 0)
        {
            // 选择优先级最高且紧急程度最高的目标
            currentGoal = activeGoals
                .Where(g => !g.isCompleted)
                .OrderByDescending(g => g.priority)
                .ThenByDescending(g => g.urgency)
                .FirstOrDefault();
                
            if (currentGoal != null)
            {
                currentGoal.isActive = true;
                Debug.Log($"选择新目标: {currentGoal.description}");
            }
        }
    }
    
    private void SortGoalsByPriority()
    {
        activeGoals = activeGoals
            .OrderByDescending(g => g.priority)
            .ThenByDescending(g => g.urgency)
            .ToList();
    }
    
    public void UpdateGoalUrgency(string goalId, float newUrgency)
    {
        Goal goal = activeGoals.FirstOrDefault(g => g.goalId == goalId);
        if (goal != null)
        {
            goal.urgency = newUrgency;
            SortGoalsByPriority();
        }
    }
    
    public List<Goal> GetActiveGoals()
    {
        return activeGoals;
    }
}
