using System;
using UnityEngine;

/// <summary>
/// 골드(재화) 관리 싱글턴.
/// 골드 보유량 추적, 추가/차감을 처리합니다.
/// </summary>
public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance { get; private set; }

    /// <summary>골드 변경 시 발생 (현재 보유량).</summary>
    public event Action<int> OnGoldChanged;

    [Header("Settings")]
    [Tooltip("시작 골드")]
    [SerializeField] private int _startingGold = 0;

    private int _gold;

    /// <summary>현재 보유 골드.</summary>
    public int Gold
    {
        get => _gold;
        private set
        {
            if (_gold == value) return;
            _gold = Mathf.Max(0, value);
            OnGoldChanged?.Invoke(_gold);
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        _gold = _startingGold;
    }

    /// <summary>골드를 추가합니다.</summary>
    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        Gold += amount;
        Debug.Log($"[GoldManager] +{amount}G → 보유: {Gold}G");
    }

    /// <summary>골드를 차감합니다. 잔액 부족 시 false 반환.</summary>
    public bool SpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (_gold < amount)
        {
            Debug.Log($"[GoldManager] 골드 부족: 보유 {_gold}G < 필요 {amount}G");
            return false;
        }
        Gold -= amount;
        Debug.Log($"[GoldManager] -{amount}G → 보유: {Gold}G");
        return true;
    }

    /// <summary>골드가 충분한지 확인합니다.</summary>
    public bool HasEnoughGold(int amount) => _gold >= amount;
}
