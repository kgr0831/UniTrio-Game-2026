using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 번이라도 처치한 적이 있는 몹의 ID 목록을 영구 저장/관리 (도감 시스템 기반).
/// PlayerPrefs나 파일 IO 등을 통해 세션 간 유지됨.
/// </summary>
public static class FirstKillRegistry
{
    private static HashSet<int> _killedMonsterIds = new HashSet<int>();

    // 로드 시뮬레이션용
    public static void LoadRegistry()
    {
        // 실제 게임에서는 PlayerPrefs.GetString 등으로 파싱
        string savedData = PlayerPrefs.GetString("KilledMonsters", "");
        if (!string.IsNullOrEmpty(savedData))
        {
            string[] ids = savedData.Split(',');
            foreach (string id in ids)
            {
                if (int.TryParse(id, out int parsed))
                    _killedMonsterIds.Add(parsed);
            }
        }
    }

    private static void SaveRegistry()
    {
        string saveData = string.Join(",", _killedMonsterIds);
        PlayerPrefs.SetString("KilledMonsters", saveData);
        PlayerPrefs.Save();
    }

    /// <summary>현재 몬스터가 처음 처치된 것인지 확인 후 등록. (최초면 true 반환)</summary>
    public static bool RegisterKillIfFirst(int monsterId)
    {
        if (!_killedMonsterIds.Contains(monsterId))
        {
            _killedMonsterIds.Add(monsterId);
            SaveRegistry();
            return true; // 첫 처치임!
        }
        return false;
    }

    public static bool HasKilled(int monsterId)
    {
        return _killedMonsterIds.Contains(monsterId);
    }
}
