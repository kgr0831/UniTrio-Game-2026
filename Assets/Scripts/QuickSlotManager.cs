using UnityEngine;

public class QuickSlotManager : MonoBehaviour
{
    // 인스펙터에서 하이어라키에 있는 8개 슬롯을 순서대로 드래그해서 넣으세요.
    // 이 슬롯들은 인게임 QuickSlotPanel의 퀵슬롯입니다.
    public InventorySlot[] quickSlots; 

    [Header("Inventory Hotbar Mirror")]
    [Tooltip("인벤토리 패널 내 HotbarObject의 8개 슬롯 (미러 동기화 대상)")]
    [SerializeField] private InventorySlot[] _inventoryHotbarSlots;

    [Tooltip("상자 패널 내 HotbarObject의 8개 슬롯 (미러 동기화 대상)")]
    [SerializeField] private InventorySlot[] _boxHotbarSlots;

    [Header("Player References")]
    [SerializeField] private StatSystem _stats;
    [SerializeField] private PlayerWeaponController _weaponController;

    // 슬롯별 독립 쿨다운 타이머
    private float[] _cooldowns;
    private float[] _cooldownMaxes;

    // 현재 무기가 장착된 퀵슬롯 인덱스 (-1 = 무기 미장착)
    private int _activeWeaponSlotIndex = -1;

    public event System.Action<WeaponData, int> OnWeaponEquipped;

    // ── 차징 스킬 상태 추적 ──────────────────────────────────────
    private int            _chargingSlotIndex = -1;
    private BowBehaviour   _chargingBow;
    private float          _chargingCooldown;
    private float          _chargingMaxCooldown;

    // ── 핫바 미러 동기화용 캐싱 (GC 방지, 폴링 기반) ──────────────
    private ItemData[] _lastQuickSlotData;
    private int[]      _lastQuickSlotCounts;
    private ItemData[] _lastHotbarData;
    private int[]      _lastHotbarCounts;
    private ItemData[] _lastBoxHotbarData;
    private int[]      _lastBoxHotbarCounts;

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

            // 미러 동기화용 캐시 배열 초기화
            _lastQuickSlotData   = new ItemData[quickSlots.Length];
            _lastQuickSlotCounts = new int[quickSlots.Length];
        }

        if (_inventoryHotbarSlots != null && _inventoryHotbarSlots.Length > 0)
        {
            _lastHotbarData   = new ItemData[_inventoryHotbarSlots.Length];
            _lastHotbarCounts = new int[_inventoryHotbarSlots.Length];
        }

        if (_boxHotbarSlots != null && _boxHotbarSlots.Length > 0)
        {
            _lastBoxHotbarData   = new ItemData[_boxHotbarSlots.Length];
            _lastBoxHotbarCounts = new int[_boxHotbarSlots.Length];
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
        UpdateCooldowns();
        HandleChargingRelease();
        HandleQuickSlotInput();
        CheckActiveWeaponSlotStale();
    }

    private void LateUpdate()
    {
        // 핫바 미러 동기화 (LateUpdate에서 변경 감지 → 반대쪽 반영)
        SyncHotbarMirror();
    }

    // ── 쿨다운 업데이트 (SRP: 쿨다운 로직만 담당) ─────────────────
    private void UpdateCooldowns()
    {
        if (_cooldowns == null) return;

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

    // ── 차징 스킬 키 릴리즈 감지 ─────────────────────────────────
    private void HandleChargingRelease()
    {
        if (_chargingSlotIndex < 0) return;

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
    }

    // ── 1~8 키 입력 처리 ─────────────────────────────────────────
    private void HandleQuickSlotInput()
    {
        // 차징 중 다른 모든 입력 차단
        if (_chargingSlotIndex >= 0) return;

        for (int i = 0; i < quickSlots.Length; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if (InventoryToggle.Instance != null && InventoryToggle.Instance.IsAnyPanelOpen()) continue;

                if (quickSlots[i] != null && quickSlots[i].currentData != null)
                {
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

        // ── 타입별 사용 로직 (OCP: 새 타입은 else if 추가로 확장) ──
        if (data is WeaponData weapon)
        {
            // 무기 장착: PlayerWeaponController에 위임 (DIP)
            if (_weaponController != null)
            {
                _weaponController.EquipWeaponByData(weapon);
                _activeWeaponSlotIndex = slotIndex;
                OnWeaponEquipped?.Invoke(weapon, slotIndex);
                Debug.Log($"[QuickSlotManager] 무기 '{weapon.Name}' 장착 (슬롯 {slotIndex + 1})");
            }
        }
        else if (data is ConsumableData consumable)
        {
            Debug.Log($"{consumable.Name} 사용 (회복량: {consumable.HealAmount})");
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

        // ── 차징 스킬 분기 (AimedShot 등) ──────────────────────
        if (skillData is AimedShotSkillData)
        {
            _chargingSlotIndex   = slotIndex;
            _chargingCooldown    = cooldown;
            _chargingMaxCooldown = cooldown;

            skillData.Execute(player);

            if (_weaponController != null)
                _chargingBow = _weaponController.ActiveBehaviour as BowBehaviour;

            return;
        }

        // 일반 스킬: 즉시 쿨다운 적용
        if (_cooldowns != null)
        {
            _cooldowns[slotIndex] = cooldown;
            _cooldownMaxes[slotIndex] = cooldown;
        }

        // 프리팹 인스턴스화 실행 (레거시 vs SO 분기)
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

    private bool _wasBoxActive = false;
    private bool _wasInvActive = false;

    // ── 핫바 미러 동기화 ──────────────────────────────────────────
    // 인벤토리 내 HotbarObject 슬롯 ↔ 인게임 QuickSlotPanel 슬롯
    // 한쪽이 변경되면 다른 쪽을 자동 동기화합니다.
    // GC 방지: 캐싱된 이전 값과 비교하여 변경분만 처리.
    private void SyncHotbarMirror()
    {
        if (quickSlots == null) return;
        int count = quickSlots.Length;

        if (_lastQuickSlotData == null) return;

        // 패널들의 현재 화면 표시(활성화) 상태를 실시간 체크합니다.
        bool isBoxCurrentlyActive = _boxHotbarSlots != null && _boxHotbarSlots.Length > 0 && _boxHotbarSlots[0] != null && _boxHotbarSlots[0].gameObject.activeInHierarchy;
        bool isInvCurrentlyActive = _inventoryHotbarSlots != null && _inventoryHotbarSlots.Length > 0 && _inventoryHotbarSlots[0] != null && _inventoryHotbarSlots[0].gameObject.activeInHierarchy;

        // 상자 패널이 비활성화 -> 활성화 상태로 열리는 시점 (꺼져있는 동안 고여있던 구 데이터 덮어쓰기)
        if (isBoxCurrentlyActive && !_wasBoxActive)
        {
            if (_lastBoxHotbarData != null)
            {
                for (int i = 0; i < _lastBoxHotbarData.Length; i++)
                {
                    _lastBoxHotbarData[i] = null;
                    _lastBoxHotbarCounts[i] = 0;

                    if (_boxHotbarSlots != null && i < _boxHotbarSlots.Length && _boxHotbarSlots[i] != null)
                    {
                        InventorySlot bs = _boxHotbarSlots[i];
                        InventorySlot qs = (quickSlots != null && i < quickSlots.Length) ? quickSlots[i] : null;
                        if (qs != null)
                        {
                            bs.RefreshSlot(qs.currentData, qs.currentCount);
                        }
                        else
                        {
                            bs.RefreshSlot(null, 0);
                        }
                    }
                }
            }
        }
        _wasBoxActive = isBoxCurrentlyActive;

        // 인벤토리 패널이 비활성화 -> 활성화 상태로 열리는 시점 (꺼져있는 동안 고여있던 구 데이터 덮어쓰기)
        if (isInvCurrentlyActive && !_wasInvActive)
        {
            if (_lastHotbarData != null)
            {
                for (int i = 0; i < _lastHotbarData.Length; i++)
                {
                    _lastHotbarData[i] = null;
                    _lastHotbarCounts[i] = 0;

                    if (_inventoryHotbarSlots != null && i < _inventoryHotbarSlots.Length && _inventoryHotbarSlots[i] != null)
                    {
                        InventorySlot hs = _inventoryHotbarSlots[i];
                        InventorySlot qs = (quickSlots != null && i < quickSlots.Length) ? quickSlots[i] : null;
                        if (qs != null)
                        {
                            hs.RefreshSlot(qs.currentData, qs.currentCount);
                        }
                        else
                        {
                            hs.RefreshSlot(null, 0);
                        }
                    }
                }
            }
        }
        _wasInvActive = isInvCurrentlyActive;

        for (int i = 0; i < count; i++)
        {
            InventorySlot qs = quickSlots[i];
            if (qs == null) continue;

            // 1. 인벤토리 내 핫바 슬롯 동기화
            if (_inventoryHotbarSlots != null && i < _inventoryHotbarSlots.Length && _inventoryHotbarSlots[i] != null && _lastHotbarData != null)
            {
                InventorySlot hs = _inventoryHotbarSlots[i];
                
                // [안전 가드]: 인벤토리 핫바 슬롯이 비어있는데, 진짜 퀵슬롯에는 아이템이 있고, 캐시 기록이 없는 경우
                if (hs.currentData == null && qs.currentData != null && _lastHotbarData[i] == null)
                {
                    hs.RefreshSlot(qs.currentData, qs.currentCount);
                }
                else if (qs.currentData != _lastQuickSlotData[i] || qs.currentCount != _lastQuickSlotCounts[i])
                {
                    hs.RefreshSlot(qs.currentData, qs.currentCount);
                }
                else if (hs.currentData != _lastHotbarData[i] || hs.currentCount != _lastHotbarCounts[i])
                {
                    qs.RefreshSlot(hs.currentData, hs.currentCount);
                    _lastQuickSlotData[i] = hs.currentData;
                    _lastQuickSlotCounts[i] = hs.currentCount;
                }
            }

            // 2. 상자 패널 내 핫바 슬롯 동기화
            if (_boxHotbarSlots != null && i < _boxHotbarSlots.Length && _boxHotbarSlots[i] != null && _lastBoxHotbarData != null)
            {
                InventorySlot bs = _boxHotbarSlots[i];

                // [안전 가드]: 상자 핫바 슬롯이 비어있는데, 진짜 퀵슬롯에는 아이템이 있고, 캐시 기록이 없는 경우 (상자 패널 오픈 직후)
                if (bs.currentData == null && qs.currentData != null && _lastBoxHotbarData[i] == null)
                {
                    bs.RefreshSlot(qs.currentData, qs.currentCount);
                }
                else if (qs.currentData != _lastQuickSlotData[i] || qs.currentCount != _lastQuickSlotCounts[i])
                {
                    bs.RefreshSlot(qs.currentData, qs.currentCount);
                }
                else if (bs.currentData != _lastBoxHotbarData[i] || bs.currentCount != _lastBoxHotbarCounts[i])
                {
                    qs.RefreshSlot(bs.currentData, bs.currentCount);
                    _lastQuickSlotData[i] = bs.currentData;
                    _lastQuickSlotCounts[i] = bs.currentCount;
                }
            }
        }

        // 루프 종료 후 변경된 최종 렌더링 상태를 캐시로 업데이트 (순환 동기화 가드)
        for (int i = 0; i < count; i++)
        {
            InventorySlot qs = quickSlots[i];
            if (qs != null)
            {
                _lastQuickSlotData[i]   = qs.currentData;
                _lastQuickSlotCounts[i] = qs.currentCount;
            }

            if (_inventoryHotbarSlots != null && i < _inventoryHotbarSlots.Length && _inventoryHotbarSlots[i] != null && _lastHotbarData != null)
            {
                _lastHotbarData[i]   = _inventoryHotbarSlots[i].currentData;
                _lastHotbarCounts[i] = _inventoryHotbarSlots[i].currentCount;
            }

            if (_boxHotbarSlots != null && i < _boxHotbarSlots.Length && _boxHotbarSlots[i] != null && _lastBoxHotbarData != null)
            {
                _lastBoxHotbarData[i]   = _boxHotbarSlots[i].currentData;
                _lastBoxHotbarCounts[i] = _boxHotbarSlots[i].currentCount;
            }
        }
    }

    // ── 현재 장착한 무기 슬롯의 상태 변경 감지 및 자동 해제 ────────
    private void CheckActiveWeaponSlotStale()
    {
        if (_activeWeaponSlotIndex < 0) return;

        if (quickSlots == null || _activeWeaponSlotIndex >= quickSlots.Length)
        {
            _activeWeaponSlotIndex = -1;
            return;
        }

        InventorySlot activeSlot = quickSlots[_activeWeaponSlotIndex];
        
        // 현재 무기가 장착된 슬롯의 데이터가 비어있거나, 무기가 아닌 딴 아이템으로 덮어씌워진 경우
        if (activeSlot == null || activeSlot.currentData == null || !(activeSlot.currentData is WeaponData))
        {
            Debug.Log($"[QuickSlotManager] 장착 중인 슬롯 {_activeWeaponSlotIndex + 1}의 무기가 사라지거나 교체되었습니다. 무기를 자동 해제합니다.");
            
            if (_weaponController == null) 
                _weaponController = FindObjectOfType<PlayerWeaponController>();

            if (_weaponController != null)
            {
                _weaponController.UnequipAll();
            }

            _activeWeaponSlotIndex = -1;
        }
    }
}