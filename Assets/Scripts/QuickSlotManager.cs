using UnityEngine;

public class QuickSlotManager : MonoBehaviour
{
    // 인스펙터에서 하이어라키에 있는 8개 슬롯을 순서대로 드래그해서 넣으세요.
    public InventorySlot[] quickSlots; 

    [Header("Player References")]
    [SerializeField] private StatSystem _stats;
    [SerializeField] private PlayerWeaponController _weaponController;

    // 슬롯별 독립 쿨다운 타이머
    private float[] _cooldowns;
    private float[] _cooldownMaxes;

    // ── 차징 스킬 상태 추적 ──────────────────────────────────────
    private int            _chargingSlotIndex = -1;
    private BowBehaviour   _chargingBow;
    private float          _chargingCooldown;
    private float          _chargingMaxCooldown;

    private void Start()
    {
        if (_weaponController == null)
            _weaponController = FindObjectOfType<PlayerWeaponController>();
        if (_stats == null)
            _stats = FindObjectOfType<StatSystem>();

        ClearAllSlots();
        if (quickSlots != null)
        {
            _cooldowns = new float[quickSlots.Length];
            _cooldownMaxes = new float[quickSlots.Length];
        }
    }

    public void ClearAllSlots()
    {
        if (quickSlots == null) return;
        foreach (var slot in quickSlots)
        {
            if (slot != null) slot.RefreshSlot(null, 0);
        }
        Debug.Log("[QuickSlot] 모든 슬롯 초기화 완료");
    }

    void Update()
    {
        if (_cooldowns != null)
        {
            for (int i = 0; i < _cooldowns.Length; i++)
            {
                if (_cooldowns[i] > 0f)
                {
                    _cooldowns[i] -= Time.deltaTime;
                    if (_cooldowns[i] < 0f) _cooldowns[i] = 0f;
                }

                // 쿨다운 오버레이 업데이트
                if (i < quickSlots.Length && quickSlots[i] != null && quickSlots[i].cooldownOverlay != null)
                {
                    if (_cooldowns[i] > 0f && _cooldownMaxes[i] > 0f)
                    {
                        quickSlots[i].cooldownOverlay.enabled = true;
                        quickSlots[i].cooldownOverlay.fillAmount = _cooldowns[i] / _cooldownMaxes[i];
                    }
                    else
                    {
                        quickSlots[i].cooldownOverlay.enabled = false;
                    }
                }
            }
        }

        // ── 차징 중 키 릴리즈 감지 ──────────────────────────────
        if (_chargingSlotIndex >= 0)
        {
            if (Input.GetKeyUp(KeyCode.Alpha1 + _chargingSlotIndex))
            {
                // 차징 종료 → 발사
                if (_chargingBow != null)
                    _chargingBow.ReleaseAimedShot();

                // 쿨다운 적용 (발사 시점부터)
                if (_cooldowns != null)
                {
                    _cooldowns[_chargingSlotIndex]    = _chargingCooldown;
                    _cooldownMaxes[_chargingSlotIndex] = _chargingMaxCooldown;
                }

                _chargingSlotIndex = -1;
                _chargingBow       = null;
            }
            return; // 차징 중 다른 모든 입력 차단
        }

        // 1~8 키 입력 체크
        for (int i = 0; i < quickSlots.Length; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                Debug.Log($"[QuickSlotManager] 퀵슬롯 {i+1} 입력 감지. PanelOpen 상태: {(InventoryToggle.Instance != null && InventoryToggle.Instance.IsAnyPanelOpen())}");
                if (InventoryToggle.Instance != null && InventoryToggle.Instance.IsAnyPanelOpen()) continue; // 개별 차단

                if (quickSlots[i] != null && quickSlots[i].currentData != null)
                {
                    Debug.Log($"[QuickSlotManager] {quickSlots[i].currentData.Name} 사용 시도");
                    UseItem(quickSlots[i], i);
                }
                else
                {
                    Debug.Log($"[QuickSlotManager] 슬롯 {i+1}이 비어있습니다!");
                }
            }
        }
    }

    private void UseItem(InventorySlot slot, int slotIndex)
    {
        ItemData data = slot.currentData;

        var stateManager = FindObjectOfType<PlayerStateManager>();
        var placementController = FindObjectOfType<BuildingPlacementController>();

        // 건축 아이템이 아닌 다른 아이템을 사용하려고 하면, 건축 모드 해제
        if (!(data is BuildingData) && stateManager != null && stateManager.CurrentMode == PlayerMode.Building)
        {
            stateManager.SetMode(PlayerMode.Combat);
            if (placementController != null) placementController.SetBuildingData(null);
        }

        // 1. 타입별 사용 로직 처리
        if (data is ConsumableData consumable)
        {
            // 아직 Player의 회복 로직이 연결되어 있지 않으면 Debug 로그로 확인
            Debug.Log($"{consumable.Name} 사용 (회복량: {consumable.HealAmount})");
            // 소모품은 사용 후 수량 차감
            ConsumeItem(slot);
        }
        else if (data is SkillData skill)
        {
            TryUseSkill(skill, slotIndex);
        }
        else if (data is BuildingData building)
        {
            if (stateManager != null)
            {
                stateManager.SetMode(PlayerMode.Building);

                // 무기 오브젝트 비활성화 및 타격 로직 차단
                if (_weaponController != null)
                {
                    _weaponController.UnequipAll();
                }

                if (placementController != null)
                {
                    placementController.SetBuildingData(building);
                }
            }
        }
        else
        {
            Debug.Log($"{data?.Name}은(는) 사용할 수 없는 아이템입니다.");
        }
    }

    private void TryUseSkill(SkillData skillData, int slotIndex)
    {
        // 최후의 안전장치: 핫 리로드 등으로 래퍼런스가 유실되었거나 Start()가 누락된 경우 즉시 복구
        if (_weaponController == null) _weaponController = FindObjectOfType<PlayerWeaponController>();
        if (_stats == null) _stats = FindObjectOfType<StatSystem>();

        // 쿨다운 체크
        if (_cooldowns != null && _cooldowns[slotIndex] > 0f)
        {
            if (NotificationUI.Instance != null) NotificationUI.Instance.ShowMessage("아직 쿨다운 중입니다!");
            else Debug.Log($"[QuickSlot] 쿨다운 중 ({_cooldowns[slotIndex]:F1}s)");
            return;
        }

        // 프리팹에서 제원 추출 (레거시 코드 전용)
        float manaCost = skillData.ManaCost;
        float cooldown = skillData.Cooldown;
        WeaponType requiredWeapon = skillData.RequiredWeapon;

        // SkillData 원본 클래스를 그대로 사용할 때만 구형 프리팹 제원을 추출합니다.
        if (skillData.GetType() == typeof(SkillData) && skillData.SkillPrefab != null)
        {
            SkillBase skillLogic = skillData.SkillPrefab.GetComponent<SkillBase>();
            if (skillLogic != null)
            {
                manaCost = skillLogic.ManaCost;
                cooldown = skillLogic.Cooldown;
                requiredWeapon = skillLogic.RequiredWeapon;
            }
        }

        // 무기 요구 조건 체크
        if (requiredWeapon != WeaponType.None)
        {
            var activeBehaviour = _weaponController != null ? _weaponController.ActiveBehaviour : null;
            if (activeBehaviour == null || activeBehaviour.WeaponType != requiredWeapon)
            {
                string weaponName = GetWeaponName(requiredWeapon);
                if (NotificationUI.Instance != null) NotificationUI.Instance.ShowMessage($"{weaponName}을(를) 장착해주세요!");
                else Debug.LogWarning($"{weaponName} 장착 필요");
                return;
            }
        }

        // 마나 체크
        if (_stats != null && !_stats.HasEnoughMana(manaCost))
        {
            if (NotificationUI.Instance != null) NotificationUI.Instance.ShowMessage("마나가 부족합니다!");
            else Debug.Log("마나가 부족합니다!");
            return;
        }

        // 시전 성공: 마나 소비
        if (_stats != null) _stats.ConsumeMana(manaCost);

        // 플레이어 GameObject 참조 (스킬 Execute에 전달)
        GameObject player = _weaponController != null ? _weaponController.gameObject : gameObject;
        Debug.Log($"[QuickSlotManager] 스킬 시전 대상: {player.name}, StatSystem: {(player.GetComponent<StatSystem>() != null)}, PlayerWeaponController: {(player.GetComponent<PlayerWeaponController>() != null)}");

        // ── 차징 스킬 분기 (AimedShot 등) ──────────────────────
        if (skillData is AimedShotSkillData)
        {
            // 차징 스킬: 마나만 소비, 쿨다운은 발사 시 적용
            _chargingSlotIndex   = slotIndex;
            _chargingCooldown    = cooldown;
            _chargingMaxCooldown = cooldown;

            skillData.Execute(player);

            // BowBehaviour 참조 저장 (키 릴리즈 시 ReleaseAimedShot 호출용)
            if (_weaponController != null)
                _chargingBow = _weaponController.ActiveBehaviour as BowBehaviour;

            return; // 쿨다운은 ReleaseAimedShot 시 적용
        }

        // 일반 스킬: 즉시 쿨다운 적용
        if (_cooldowns != null)
        {
            _cooldowns[slotIndex] = cooldown;
            _cooldownMaxes[slotIndex] = cooldown;
        }

        // 프리팹 인스턴스화 실행 (레거시 vs SO 분기)
        // 만약 SkillData를 상속받은 커스텀 클래스(BashSkillData 등)라면 무조건 SO의 Execute()를 우선 실행합니다.
        if (skillData.SkillPrefab != null && skillData.GetType() == typeof(SkillData))
        {
            GameObject skillObj = Instantiate(skillData.SkillPrefab, player.transform.position, Quaternion.identity);
            SkillBase instance = skillObj.GetComponent<SkillBase>();
            if (instance != null) instance.Execute(player);
        }
        else
        {
            skillData.Execute(player);
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
            default: return "해당 무기";
        }
    }

    // 소모품 전용 차감 로직
    private void ConsumeItem(InventorySlot slot)
    {
        int newCount = slot.currentCount - 1;

        if (newCount <= 0)
        {
            slot.RefreshSlot(null, 0);
        }
        else
        {
            slot.RefreshSlot(slot.currentData, newCount);
        }
    }
}