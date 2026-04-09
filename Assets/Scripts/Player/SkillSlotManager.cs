using System;
using System.Collections.Generic;
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


    [Tooltip("무기에 연결된 스킬이 없을 때 사용할 기본 스킬 (없으면 비워두세요)")]
    [SerializeField] private SkillData _defaultSkill;

    [Header("UI Reference")]
    [Tooltip("스킬창의 아이콘을 표시할 InventorySlot 오브젝트")]
    [SerializeField] private InventorySlot _skillSlotUI;


    /// <summary>현재 장착된 스킬 데이터.</summary>
    public SkillData EquippedSkill { get; private set; }

    /// <summary>쿨다운 중이면 true.</summary>
    public bool IsOnCooldown => _cooldownTimer > 0f;

    /// <summary>스킬 장착 시 발생. 새 SkillData 전달.</summary>
    public event Action<SkillData> OnSkillEquipped;

    /// <summary>쿨다운 진행률(0~1) 변화 시 발생. UI 게이지 갱신용.</summary>
    public event Action<float> OnCooldownChanged;

    private StatSystem _stats;
    private PlayerWeaponController _weaponController;
    private float _cooldownTimer;

    private void Awake()
    {
        _stats            = GetComponent<StatSystem>();
        _weaponController = GetComponent<PlayerWeaponController>();

        if (_defaultSkill != null)
            EquipSkill(_defaultSkill);
    }

    private void Start()
    {
        // 초기 스킬 장착 (Awake에서 처리되지 않은 경우 대비)
        if (EquippedSkill == null && _defaultSkill != null)
            EquipSkill(_defaultSkill);
    }

    private void Update()
    {
        // 쿨다운 감소
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer < 0f) _cooldownTimer = 0f;

            // 현재 장착된 스킬로부터 최대 쿨다운 정보를 가져와 비율 산출
            float maxCooldown = 1f;
            if (EquippedSkill != null && EquippedSkill.SkillPrefab != null)
            {
                var skillBase = EquippedSkill.SkillPrefab.GetComponent<SkillBase>();
                if (skillBase != null) maxCooldown = skillBase.Cooldown;
            }
            else if (EquippedSkill != null)
            {
                maxCooldown = EquippedSkill.Cooldown;
            }

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
        
        // UI 아이콘 갱신
        if (_skillSlotUI != null)
            _skillSlotUI.RefreshSlot(skill, 1);

        OnSkillEquipped?.Invoke(skill);
        if (skill != null) Debug.Log($"[SkillSlot] '{skill.Name}' 장착됨 (프리팹 기반)");
        else               Debug.Log("[SkillSlot] 스킬 슬롯 비움");
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

        // ── 프리팹 기반 제원 가져오기 ──────────────────────────────
        float manaCost = EquippedSkill.ManaCost;
        float cooldown = EquippedSkill.Cooldown;
        WeaponType requiredWeapon = EquippedSkill.RequiredWeapon;
        SkillBase skillLogic = null;

        if (EquippedSkill.SkillPrefab != null)
        {
            skillLogic = EquippedSkill.SkillPrefab.GetComponent<SkillBase>();
            if (skillLogic != null)
            {
                manaCost = skillLogic.ManaCost;
                cooldown = skillLogic.Cooldown;
                requiredWeapon = skillLogic.RequiredWeapon;
            }
        }

        // ── 무기 요구 조건 체크 ─────────────────────────────────────
        if (requiredWeapon != WeaponType.None)
        {
            var activeWeapon = (_weaponController != null) ? _weaponController.ActiveBehaviour : null;
            if (activeWeapon == null || activeWeapon.WeaponType != requiredWeapon)
            {
                string weaponName = GetWeaponName(requiredWeapon);
                NotificationUI.Instance?.ShowMessage($"{weaponName}을(를) 착용해야 합니다!");
                Debug.Log($"[SkillSlot] 무기 불일치 (필요: {requiredWeapon})");
                return;
            }
        }

        // 마나 체크
        if (!_stats.HasEnoughMana(manaCost))
        {
            Debug.Log($"[SkillSlot] 마나 부족 (필요: {manaCost}, 현재: {_stats.CurrentMana:F0})");
            return;
        }

        // 시전 성공 - 마나 소모 및 쿨다운 설정
        _stats.ConsumeMana(manaCost);
        _cooldownTimer = cooldown;

        Debug.Log($"[SkillSlot] '{EquippedSkill.Name}' 시전!");
        
        // 스킬 프리팹 실행
        if (EquippedSkill.SkillPrefab != null)
        {
            // 프리팹을 인스턴스화하여 실행 (버프/이펙트 관리 용이)
            GameObject skillObj = Instantiate(EquippedSkill.SkillPrefab, transform.position, Quaternion.identity);
            SkillBase instanceLogic = skillObj.GetComponent<SkillBase>();
            if (instanceLogic != null)
            {
                instanceLogic.Execute(gameObject);
            }
        }
        else
        {
            // 하위 호환: 프리팹 없이 데이터만 있는 경우 직접 실행
            EquippedSkill.Execute(gameObject);
        }
    }

    private string GetWeaponName(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Sword: return "검";
            case WeaponType.Spear: return "창";
            case WeaponType.Bow:   return "활";
            case WeaponType.Staff: return "지팡이";
            default: return "정해진 무기";
        }
    }
}

