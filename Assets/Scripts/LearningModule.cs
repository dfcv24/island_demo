using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

[System.Serializable]
public class LearningPattern
{
    public string patternId;
    public string context; // 上下文条件
    public string action; // 执行的行动
    public float successRate; // 成功率
    public int totalAttempts; // 尝试次数
    public int successfulAttempts; // 成功次数
    public float confidence; // 置信度
    public List<string> conditions; // 成功条件
    
    public LearningPattern(string id, string ctx, string act)
    {
        patternId = id;
        context = ctx;
        action = act;
        successRate = 0.5f;
        totalAttempts = 0;
        successfulAttempts = 0;
        confidence = 0f;
        conditions = new List<string>();
    }
    
    public void UpdatePattern(bool success, List<string> currentConditions = null)
    {
        totalAttempts++;
        if (success)
        {
            successfulAttempts++;
            if (currentConditions != null)
            {
                // 学习成功条件
                foreach (string condition in currentConditions)
                {
                    if (!conditions.Contains(condition))
                    {
                        conditions.Add(condition);
                    }
                }
            }
        }
        
        successRate = (float)successfulAttempts / totalAttempts;
        confidence = Mathf.Min(1f, totalAttempts / 10f); // 10次尝试后达到满信心
    }
}

[System.Serializable]
public class Strategy
{
    public string strategyId;
    public string goal; // 目标类型
    public List<string> actionSequence; // 行动序列
    public float effectiveness; // 有效性
    public int usageCount; // 使用次数
    public float averageTime; // 平均完成时间
    public List<string> prerequisites; // 前置条件
    
    public Strategy(string id, string goalType, List<string> actions)
    {
        strategyId = id;
        goal = goalType;
        actionSequence = new List<string>(actions);
        effectiveness = 0.5f;
        usageCount = 0;
        averageTime = 0f;
        prerequisites = new List<string>();
    }
    
    public void UpdateEffectiveness(bool success, float timeSpent)
    {
        usageCount++;
        
        if (success)
        {
            effectiveness = (effectiveness * (usageCount - 1) + 1f) / usageCount;
        }
        else
        {
            effectiveness = (effectiveness * (usageCount - 1) + 0f) / usageCount;
        }
        
        averageTime = (averageTime * (usageCount - 1) + timeSpent) / usageCount;
    }
}

public class LearningModule : MonoBehaviour
{
    [Header("学习设置")]
    [SerializeField] private float learningRate = 0.1f;
    [SerializeField] private float explorationRate = 0.2f; // 探索率（epsilon-greedy）
    [SerializeField] private int maxPatterns = 50;
    [SerializeField] private int maxStrategies = 20;
    
    [SerializeField] private List<LearningPattern> patterns = new List<LearningPattern>();
    [SerializeField] private List<Strategy> strategies = new List<Strategy>();
    [SerializeField] private Dictionary<string, float> actionValues = new Dictionary<string, float>(); // Q值
    
    private MemorySystem memorySystem;
    private string currentStrategy = "";
    private float strategyStartTime = 0f;
    
    void Start()
    {
        memorySystem = GetComponent<MemorySystem>();
        InitializeBasicStrategies();
    }
    
    private void InitializeBasicStrategies()
    {
        // 初始化基本策略
        var survivalStrategy = new Strategy("survival_basic", "survival", 
            new List<string> {"think", "search_food", "move_to_food", "collect_eat"});
        strategies.Add(survivalStrategy);
        
        var learningStrategy = new Strategy("learning_basic", "learning",
            new List<string> {"move_to_item", "ask_about_item"});
        strategies.Add(learningStrategy);
        
        var explorationStrategy = new Strategy("exploration_basic", "exploration",
            new List<string> {"think", "move_explore", "observe"});
        strategies.Add(explorationStrategy);
    }
    
    public void LearnFromExperience(string action, string context, bool success, List<string> conditions = null)
    {
        // 更新行动价值
        UpdateActionValue(action, context, success);
        
        // 更新或创建学习模式
        UpdateLearningPattern(action, context, success, conditions);
        
        // 存储经验到记忆系统
        Vector3 currentPos = GetComponent<Person>().GetGridPosition();
        memorySystem.StoreActionOutcome(action, success ? "成功" : "失败", success, currentPos);
        
        // 如果正在执行策略，更新策略效果
        if (!string.IsNullOrEmpty(currentStrategy))
        {
            UpdateStrategyEffectiveness(success);
        }
        
        Debug.Log($"学习经验: {action} 在 {context} 中 {(success ? "成功" : "失败")}");
    }
    
    private void UpdateActionValue(string action, string context, bool success)
    {
        string key = $"{context}_{action}";
        
        if (!actionValues.ContainsKey(key))
        {
            actionValues[key] = 0.5f;
        }
        
        float reward = success ? 1f : 0f;
        float oldValue = actionValues[key];
        actionValues[key] = oldValue + learningRate * (reward - oldValue);
    }
    
    private void UpdateLearningPattern(string action, string context, bool success, List<string> conditions)
    {
        string patternId = $"{context}_{action}";
        LearningPattern pattern = patterns.FirstOrDefault(p => p.patternId == patternId);
        
        if (pattern == null)
        {
            pattern = new LearningPattern(patternId, context, action);
            patterns.Add(pattern);
            
            // 限制模式数量
            if (patterns.Count > maxPatterns)
            {
                var leastUsed = patterns.OrderBy(p => p.totalAttempts).First();
                patterns.Remove(leastUsed);
            }
        }
        
        pattern.UpdatePattern(success, conditions);
    }
    
    public string RecommendAction(string context, List<string> availableActions)
    {
        // epsilon-greedy策略：有explorationRate的概率探索，否则利用
        if (UnityEngine.Random.Range(0f, 1f) < explorationRate)
        {
            // 探索：随机选择行动
            return availableActions[UnityEngine.Random.Range(0, availableActions.Count)];
        }
        else
        {
            // 利用：选择价值最高的行动
            string bestAction = "";
            float bestValue = float.MinValue;
            
            foreach (string action in availableActions)
            {
                float value = GetActionValue(context, action);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestAction = action;
                }
            }
            
            return string.IsNullOrEmpty(bestAction) ? availableActions[0] : bestAction;
        }
    }
    
    private float GetActionValue(string context, string action)
    {
        string key = $"{context}_{action}";
        
        if (actionValues.ContainsKey(key))
        {
            return actionValues[key];
        }
        
        // 如果没有直接经验，从相似模式推断
        var similarPatterns = patterns.Where(p => p.action == action && p.context.Contains(context.Split('_')[0])).ToList();
        
        if (similarPatterns.Count > 0)
        {
            return similarPatterns.Average(p => p.successRate * p.confidence);
        }
        
        return 0.5f; // 默认值
    }
    
    public Strategy RecommendStrategy(string goalType)
    {
        var applicableStrategies = strategies.Where(s => s.goal == goalType).ToList();
        
        if (applicableStrategies.Count == 0)
        {
            return null;
        }
        
        // 选择最有效的策略，但偶尔探索其他策略
        if (UnityEngine.Random.Range(0f, 1f) < explorationRate)
        {
            return applicableStrategies[UnityEngine.Random.Range(0, applicableStrategies.Count)];
        }
        else
        {
            return applicableStrategies.OrderByDescending(s => s.effectiveness).First();
        }
    }
    
    public void StartStrategy(string strategyId)
    {
        currentStrategy = strategyId;
        strategyStartTime = Time.time;
    }
    
    private void UpdateStrategyEffectiveness(bool success)
    {
        var strategy = strategies.FirstOrDefault(s => s.strategyId == currentStrategy);
        if (strategy != null)
        {
            float timeSpent = Time.time - strategyStartTime;
            strategy.UpdateEffectiveness(success, timeSpent);
        }
    }
    
    public void EndStrategy(bool success)
    {
        if (!string.IsNullOrEmpty(currentStrategy))
        {
            UpdateStrategyEffectiveness(success);
            currentStrategy = "";
            strategyStartTime = 0f;
        }
    }
    
    public List<Memory> GetRelevantExperiences(string action, string context)
    {
        Vector3 currentPos = GetComponent<Person>().GetGridPosition();
        return memorySystem.GetRelevantExperiences(action, currentPos);
    }
    
    public void AdaptToFailure(string failedAction, string context)
    {
        // 失败适应：降低该行动在此上下文中的价值
        LearnFromExperience(failedAction, context, false);
        
        // 尝试从记忆中找到替代方案
        var experiences = GetRelevantExperiences(failedAction, context);
        foreach (var exp in experiences)
        {
            if (exp.data.ContainsKey("success") && (bool)exp.data["success"])
            {
                Debug.Log($"从记忆中学习: 尝试使用 {exp.data["action"]} 替代 {failedAction}");
                break;
            }
        }
    }
    
    public float GetLearningProgress()
    {
        if (patterns.Count == 0) return 0f;
        return patterns.Average(p => p.confidence);
    }
    
    public int GetPatternCount() => patterns.Count;
    public int GetStrategyCount() => strategies.Count;
    public Dictionary<string, float> GetActionValues() => new Dictionary<string, float>(actionValues);
}
