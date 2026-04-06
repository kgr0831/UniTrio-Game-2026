using System;
using UnityEngine;

/// <summary>
/// 스킬 슬롯 1칸을 관리하는 컴포넌트.
/// - 인벤토리에서 SkillData 드래그 앤 드롭 → EquipSkill() 호출
/// - Q키 입력 → StatSystem 마나 체크 후 시전
/// - 쿨다운 타이머 (GC 없는 Update 방식)
/// - OnCooldownChanged 이벤트로 UI 쿨다운 게이지 갱신
/// </summary>
[RequireComponent(typeof(StatSystem))]
public class SkillSlotManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private KeyCode _useKey = KeyCode.Q;

    /// <summary>현재 장착된 스킬 데이터.</summary>
    public SkillData EquippedSkill { get; private set; }

    /// <summary>쿨다운 중이면 true.</summary>
    public bool IsOnCooldown => _cooldownTimer > 0f;

    /// <summary>스킬 장착 시 발생. 새 SkillData 전달.</summary>
    public event Action<SkillData> OnSkillEquipped;

    /// <summary>쿨다운 진행률(0~1) 변화 시 발생. UI 게이지 갱신용.</summary>
    public event Action<float> OnCooldownChanged;

    private StatSystem _stats;
    private float _cooldownTimer;

    private void Awake()
    {
        _stats = GetComponent<StatSystem>();
    }

    private void Update()
    {
        // 쿨다운 감소 (타이머 방식, GC 없음)
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer < 0f) _cooldownTimer = 0f;

            float maxCooldown = EquippedSkill != null ? EquippedSkill.Cooldown : 1f;
            OnCooldownChanged?.Invoke(_cooldownTimer / maxCooldown);
        }

        if (Input.GetKeyDown(_useKey))
            TryUseSkill();
    }

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>
    /// 스킬 슬롯에 스킬을 장착합니다. 인벤토리 드래그 앤 드롭 처리기에서 호출합니다.
    /// </summary>
    public void EquipSkill(SkillData skill)
    {
        EquippedSkill  = skill;
        _cooldownTimer = 0f;
        OnSkillEquipped?.Invoke(skill);
        Debug.Log($"[SkillSlot] '{skill?.Name}' 장착됨");
    }

    /// <summary>
    /// 스킬 시전 시도. 마나 부족 또는 쿨다운 중이면 무시합니다.
    /// </summary>
    public void TryUseSkill()
    {
        if (EquippedSkill == null) return;
        if (IsOnCooldown)
        {
            Debug.Log($"[SkillSlot] 쿨다운 중 ({_cooldownTimer:F1}s 남음)");
            return;
        }
        if (!_stats.HasEnoughMana(EquippedSkill.ManaCost))
        {
            Debug.Log($"[SkillSlot] 마나 부족 (필요: {EquippedSkill.ManaCost}, 현재: {_stats.CurrentMana:F0})");
            return;
        }

        _stats.ConsumeMana(EquippedSkill.ManaCost);
        _cooldownTimer = EquippedSkill.Cooldown;

        Debug.Log($"[SkillSlot] '{EquippedSkill.Name}' 시전! (마나 -{EquippedSkill.ManaCost})");
        // TODO: 스킬 타입별 효과 구현 (Milestone 이후)
    }
}
