using System;
using UnityEngine;

/// <summary>
/// 플레이어의 현재 행동 상태를 정의합니다.
/// </summary>
public enum PlayerMode
{
    Combat,
    Building
}

/// <summary>
/// 플레이어의 전역 상태(전투, 건축 등)를 관리하고 상태 전환 이벤트를 발생시키는 컴포넌트입니다.
/// </summary>
public class PlayerStateManager : MonoBehaviour
{
    public PlayerMode CurrentMode { get; private set; } = PlayerMode.Combat;

    public event Action<PlayerMode> OnModeChanged;

    /// <summary>
    /// 플레이어의 모드를 변경합니다.
    /// </summary>
    public void SetMode(PlayerMode newMode)
    {
        if (CurrentMode == newMode) return;

        CurrentMode = newMode;
        Debug.Log($"[PlayerStateManager] 상태 전환: {CurrentMode}");
        
        OnModeChanged?.Invoke(CurrentMode);
    }
}
