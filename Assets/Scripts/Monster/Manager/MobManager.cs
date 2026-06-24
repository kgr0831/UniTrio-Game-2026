using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 현재 스폰된 모든 몹을 추적하고 전체 AI 활성/비활성을 관리 (SRP).
/// </summary>
public sealed class MobManager : MonoBehaviour
{
    public static MobManager Instance { get; private set; }

    private readonly HashSet<MonsterBase> _activeMobs = new HashSet<MonsterBase>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterMob(MonsterBase mob)
    {
        _activeMobs.Add(mob);
        // Boss 몹 스폰 시 특별 연출이나 알림을 여기서 방송(Event)할 수 있음.
    }

    public void UnregisterMob(MonsterBase mob)
    {
        _activeMobs.Remove(mob);
    }

    /// <summary>현재 활성화된 적대적 몹 수 반환</summary>
    public int GetHostileMobCount()
    {
        int count = 0;
        foreach (var mob in _activeMobs)
        {
            var data = mob.GetComponent<MonsterRuntimeData>();
            if (data != null && data.Type == MonsterType.Hostile)
            {
                count++;
            }
        }
        return count;
    }
}
