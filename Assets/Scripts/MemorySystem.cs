using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

[System.Serializable]
public class Memory
{
    public string memoryId;
    public string memoryType; // "action", "observation", "interaction", "emotion", "outcome"
    public string content;
    public Vector3 location;
    public float timestamp;
    public float importance; // 0-1，重要性
    public float emotionalWeight; // -1到1，情感权重
    public Dictionary<string, object> data;
    public int accessCount; // 访问次数
    public float lastAccessTime;
    
    public Memory(string id, string type, string content, Vector3 loc, float importance = 0.5f)
    {
        memoryId = id;
        memoryType = type;
        this.content = content;
        location = loc;
        timestamp = Time.time;
        this.importance = importance;
        emotionalWeight = 0f;
        data = new Dictionary<string, object>();
        accessCount = 0;
        lastAccessTime = Time.time;
    }
    
    public void Access()
    {
        accessCount++;
        lastAccessTime = Time.time;
        // 访问次数越多，重要性稍微增加
        importance = Mathf.Min(1f, importance + 0.01f);
    }
}

public class MemorySystem : MonoBehaviour
{
    [Header("记忆设置")]
    [SerializeField] private int maxWorkingMemories = 7; // 工作记忆容量（7±2法则）
    [SerializeField] private int maxLongTermMemories = 100; // 长期记忆容量
    [SerializeField] private float memoryDecayRate = 0.1f; // 记忆衰减率
    [SerializeField] private float consolidationThreshold = 0.7f; // 巩固阈值
    
    [SerializeField] private List<Memory> workingMemory = new List<Memory>(); // 工作记忆
    [SerializeField] private List<Memory> longTermMemory = new List<Memory>(); // 长期记忆
    [SerializeField] private List<Memory> episodicMemory = new List<Memory>(); // 情景记忆
    
    void Update()
    {
        // 定期进行记忆整理
        if (Time.time % 10f < Time.deltaTime) // 每10秒整理一次
        {
            ConsolidateMemories();
            DecayMemories();
        }
    }
    
    public void StoreMemory(string type, string content, Vector3 location, float importance = 0.5f, Dictionary<string, object> additionalData = null)
    {
        string memoryId = $"mem_{DateTime.Now.Ticks}";
        Memory newMemory = new Memory(memoryId, type, content, location, importance);
        
        if (additionalData != null)
        {
            foreach (var kvp in additionalData)
            {
                newMemory.data[kvp.Key] = kvp.Value;
            }
        }
        
        // 先存入工作记忆
        workingMemory.Add(newMemory);
        
        // 如果工作记忆超容量，移除最不重要的
        if (workingMemory.Count > maxWorkingMemories)
        {
            var leastImportant = workingMemory.OrderBy(m => m.importance * (1f / (m.accessCount + 1))).First();
            workingMemory.Remove(leastImportant);
            
            // 如果重要性足够，转入长期记忆
            if (leastImportant.importance > consolidationThreshold)
            {
                TransferToLongTermMemory(leastImportant);
            }
        }
        
        Debug.Log($"存储记忆: {type} - {content}");
    }
    
    public List<Memory> RecallMemories(string queryType = "", string queryContent = "", Vector3? location = null, float radius = 5f)
    {
        List<Memory> results = new List<Memory>();
        
        // 从工作记忆中搜索
        results.AddRange(SearchMemories(workingMemory, queryType, queryContent, location, radius));
        
        // 从长期记忆中搜索
        results.AddRange(SearchMemories(longTermMemory, queryType, queryContent, location, radius));
        
        // 从情景记忆中搜索
        results.AddRange(SearchMemories(episodicMemory, queryType, queryContent, location, radius));
        
        // 标记访问并按相关性排序
        foreach (var memory in results)
        {
            memory.Access();
        }
        
        return results.OrderByDescending(m => CalculateRelevance(m, queryType, queryContent, location)).ToList();
    }
    
    private List<Memory> SearchMemories(List<Memory> memorySet, string queryType, string queryContent, Vector3? location, float radius)
    {
        return memorySet.Where(m =>
        {
            bool typeMatch = string.IsNullOrEmpty(queryType) || m.memoryType == queryType;
            bool contentMatch = string.IsNullOrEmpty(queryContent) || m.content.Contains(queryContent);
            bool locationMatch = !location.HasValue || Vector3.Distance(m.location, location.Value) <= radius;
            
            return typeMatch && contentMatch && locationMatch;
        }).ToList();
    }
    
    private float CalculateRelevance(Memory memory, string queryType, string queryContent, Vector3? location)
    {
        float relevance = memory.importance;
        
        // 类型匹配加分
        if (!string.IsNullOrEmpty(queryType) && memory.memoryType == queryType)
            relevance += 0.3f;
        
        // 内容匹配加分
        if (!string.IsNullOrEmpty(queryContent) && memory.content.Contains(queryContent))
            relevance += 0.2f;
        
        // 位置接近加分
        if (location.HasValue)
        {
            float distance = Vector3.Distance(memory.location, location.Value);
            relevance += Mathf.Max(0, (10f - distance) / 10f) * 0.2f;
        }
        
        // 访问频率加分
        relevance += Mathf.Min(0.2f, memory.accessCount * 0.02f);
        
        // 时间衰减
        float timeSinceCreation = Time.time - memory.timestamp;
        relevance *= Mathf.Exp(-timeSinceCreation * memoryDecayRate / 100f);
        
        return relevance;
    }
    
    private void ConsolidateMemories()
    {
        // 将重要的工作记忆转入长期记忆
        var importantMemories = workingMemory.Where(m => m.importance > consolidationThreshold && m.accessCount >= 2).ToList();
        
        foreach (var memory in importantMemories)
        {
            workingMemory.Remove(memory);
            TransferToLongTermMemory(memory);
        }
    }
    
    private void TransferToLongTermMemory(Memory memory)
    {
        // 检查是否是情景记忆（包含位置和时间信息的重要记忆）
        if (memory.importance > 0.8f && (memory.memoryType == "interaction" || memory.memoryType == "outcome"))
        {
            episodicMemory.Add(memory);
            if (episodicMemory.Count > maxLongTermMemories / 2)
            {
                var oldest = episodicMemory.OrderBy(m => m.timestamp).First();
                episodicMemory.Remove(oldest);
            }
        }
        else
        {
            longTermMemory.Add(memory);
            if (longTermMemory.Count > maxLongTermMemories)
            {
                var leastRelevant = longTermMemory.OrderBy(m => m.importance * (1f / (Time.time - m.lastAccessTime + 1))).First();
                longTermMemory.Remove(leastRelevant);
            }
        }
        
        Debug.Log($"记忆巩固: {memory.content} -> 长期记忆");
    }
    
    private void DecayMemories()
    {
        // 对所有记忆进行衰减
        foreach (var memory in workingMemory.Concat(longTermMemory).Concat(episodicMemory))
        {
            float timeSinceAccess = Time.time - memory.lastAccessTime;
            if (timeSinceAccess > 60f) // 超过60秒未访问开始衰减
            {
                memory.importance *= Mathf.Exp(-memoryDecayRate * Time.deltaTime);
            }
        }
        
        // 移除重要性过低的记忆
        workingMemory.RemoveAll(m => m.importance < 0.1f);
        longTermMemory.RemoveAll(m => m.importance < 0.05f);
    }
    
    public void StoreActionOutcome(string action, string outcome, bool success, Vector3 location)
    {
        var data = new Dictionary<string, object>
        {
            {"action", action},
            {"outcome", outcome},
            {"success", success}
        };
        
        float importance = success ? 0.8f : 0.9f; // 失败的经验更重要
        StoreMemory("outcome", $"执行{action}，结果：{outcome}", location, importance, data);
    }
    
    public List<Memory> GetRelevantExperiences(string action, Vector3 location)
    {
        return RecallMemories("outcome", action, location, 10f);
    }
    
    public int GetWorkingMemoryCount() => workingMemory.Count;
    public int GetLongTermMemoryCount() => longTermMemory.Count;
    public int GetEpisodicMemoryCount() => episodicMemory.Count;
}
