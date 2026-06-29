using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;

public class TutorialManager : MonoBehaviour
{
    public enum Step { None, Intro, GatherMaterials, CraftSword, EquipHotbar, SpawnBear, FightBear, Outro, Done }

    [System.Serializable]
    public class OutlineBinding
    {
        public DialogueSO targetDialogue;
        public int lineIndex;
        public Transform outlineTarget;
        public float panDuration = 0.6f;
        public float fadeDuration = 0.3f;
    }

    [Header("Dialogue")]
    [Tooltip("ID→DialogueSO 조회용 데이터베이스 (TutorialDialogueDatabase)")]
    [SerializeField] private DialogueDatabase _dialogues;
    [Tooltip("대화 진행 컴포넌트 (Dialogue 프리팹의 DialoguePlayer)")]
    [SerializeField] private DialoguePlayer _dialoguePlayer;

    [Header("Intro")]
    [SerializeField] private IntroBlurController _introBlur;

    [Header("Quest UI")]
    [SerializeField] private QuestTrackerUI _questTracker;

    [Header("UI Gating")]
    [Tooltip("퀘스트1(돌 검 만들기) 완료 전까지 숨길 전체 UI 요소들. 핫바는 여기 포함하지 않습니다.")]
    [SerializeField] private GameObject[] _fullUiElements;

    [Header("Starting Inventory")]
    [Tooltip("시작 시 지급할 무기 (나무 몽둥이/활/지팡이)")]
    [SerializeField] private ItemData[] _startingWeapons;

    [Header("Phase C - 채집/제작 아이템")]
    [SerializeField] private ItemData _woodItem;
    [SerializeField] private ItemData _stoneItem;
    [SerializeField] private ItemData _sturdyStick;
    [SerializeField] private ItemData _sharpStoneBlade;
    [SerializeField] private ItemData _stoneSword;
    [Tooltip("채집 완료로 간주할 나무/돌 최소 수량")]
    [SerializeField] private int _woodNeeded = 2;
    [SerializeField] private int _stoneNeeded = 2;

    [Header("Phase C - 핫바 장착")]
    [Tooltip("핫바(퀵슬롯) 1번 슬롯 인덱스(0 기반)")]
    [SerializeField] private int _equipHotbarSlotIndex = 0;

    [Header("Phase D - 곰 전투")]
    [Tooltip("스폰할 곰의 정적 데이터 (Bear.asset)")]
    [SerializeField] private MonsterData _bearData;
    [Tooltip("튜토리얼 동안 비활성화할 잡몹 스포너들 (Cow/Bear/Skeleton/Necromancer)")]
    [SerializeField] private GameObject[] _ambientSpawners;
    [Tooltip("플레이어 기준 곰 스폰 X 오프셋 (좌측)")]
    [SerializeField] private float _bearSpawnOffsetX = -8f;

    [Header("Phase E - 마무리")]
    [Tooltip("조작법(키 바인딩) 안내 창")]
    [SerializeField] private KeybindingPanel _keybindingPanel;

    [Header("포커스 아웃라인")]
    [Tooltip("카메라 팬 연출을 담당하는 CameraEffectManager")]
    [SerializeField] private CameraEffectManager _cameraEffectManager;
    [Tooltip("대화 줄별 아웃라인 바인딩 목록")]
    [SerializeField] private OutlineBinding[] _outlineBindings;

    [Header("튜토리얼 배치 (플레이어 기준 상대 좌표)")]
    [Tooltip("마법사 NPC (시작 시 플레이어 옆으로 이동)")]
    [SerializeField] private Transform _mageNpc;
    [Tooltip("채집물 루트 (자식 나무/돌을 플레이어 주변에 배치)")]
    [SerializeField] private Transform _gatherablesRoot;
    [Tooltip("채집물 스케일 (원본이 너무 커서 축소)")]
    [SerializeField] private float _gatherableScale = 0.5f;

    [Header("Managers")]
    [SerializeField] private CraftingUIManager _craftingManager;
    [SerializeField] private QuickSlotManager _quickSlotManager;

    public Step Current { get; private set; } = Step.None;

    private bool _intermediatesDone;
    private bool _inventorySubscribed;
    private bool _craftingSubscribed;
    private bool _hotbarSubscribed;
    private bool _gaugeSubscribed;
    private bool _gaugeFullMessageShown;

    // 튜토리얼 시작 시점의 보유 수량 기준선 (기존 인벤토리에 영향받지 않도록 증분으로 감지)
    private int _baseWood, _baseStone, _baseStick, _baseBlade, _baseSword;
    private int _gatheredWoodCount;
    private int _gatheredStoneCount;

    private void Awake()
    {
        // 튜토리얼 시작 전: 잡몹 스포너 비활성화 (Awake에서 끄면 해당 스포너의 Start 스폰을 막음)
        if (_ambientSpawners != null)
            foreach (var s in _ambientSpawners) if (s != null) s.SetActive(false);
    }

    private void Start()
    {
        GrantStartingItems();
        SetFullUiVisible(false);
        PositionTutorialActors(FindPlayer());
        if (_dialoguePlayer != null) _dialoguePlayer.OnLineChanged += OnDialogueLineChanged;
        StartCoroutine(IntroRoutine());
    }

    /// <summary>마법사 NPC와 채집물을 플레이어 위치 기준으로 배치합니다(런타임 시작 위치 차이 보정).</summary>
    private void PositionTutorialActors(Transform player)
    {
        if (player == null) return;
        Vector3 p = player.position;

        if (_mageNpc != null) _mageNpc.position = p + new Vector3(8.0f, 0f, 0f);

        if (_gatherablesRoot != null)
        {
            Vector3[] offs =
            {
                new Vector3(-6.0f,  3.5f, 0f),  // 나무: 좌측 상단 멀리
                new Vector3( 6.5f, -2.5f, 0f),  // 돌: 우측 하단 멀리
            };
            int i = 0;
            foreach (Transform c in _gatherablesRoot)
            {
                if (i < offs.Length) c.position = p + offs[i];

                // MapGenerator의 절차적 생성과 완벽히 동일한 스케일 적용 로직
                SpriteRenderer sr = c.GetComponentInChildren<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    Vector2 spriteSize = sr.sprite.bounds.size;

                    // MapGenerator의 실제 Biome 세팅은 모두 tileSize(1, 1)을 사용합니다.
                    Vector2Int tileSize = new Vector2Int(1, 1);

                    float scaleX = tileSize.x / spriteSize.x;
                    float scaleY = tileSize.y / spriteSize.y;

                    float spawnMult = 1f;
                    var gatherableNode = c.GetComponent<GatherableNode>();
                    if (gatherableNode != null) spawnMult = gatherableNode.SpawnScaleMultiplier;

                    float uniformScale = Mathf.Min(scaleX, scaleY) * spawnMult;
                    c.localScale = new Vector3(uniformScale, uniformScale, 1f);
                }

                i++;
            }
        }
    }

    // ── 시작 처리 ─────────────────────────────────────────
    private void GrantStartingItems()
    {
        if (InventoryManager.Instance == null || _startingWeapons == null) return;
        foreach (var weapon in _startingWeapons)
        {
            if (weapon != null) InventoryManager.Instance.AddItem(weapon, 1);
        }
    }

    private void SetFullUiVisible(bool visible)
    {
        if (_fullUiElements == null) return;
        foreach (var go in _fullUiElements)
        {
            if (go != null) go.SetActive(visible);
        }
    }

    private IEnumerator IntroRoutine()
    {
        Current = Step.Intro;

        // --- 플레이어 입력 및 UI 완전 차단 ---
        var player = FindPlayer();
        if (player == null) yield break;

        var fsm = player.GetComponent<PlayerStateMachine>();
        var interactor = player.GetComponentInChildren<PlayerInteractionDetector>();

        if (fsm != null) fsm.TransitionTo(fsm.Cutscene);
        if (interactor != null) interactor.enabled = false;
        if (InventoryToggle.Instance != null) InventoryToggle.Instance.enabled = false;

        // 1. 완벽한 캔버스 페이드인 (포스트 프로세싱 볼륨 의존성 제거)
        GameObject fadeCanvas = new GameObject("IntroFadeCanvas");
        Canvas canvas = fadeCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        UnityEngine.UI.Image fadeImg = fadeCanvas.AddComponent<UnityEngine.UI.Image>();
        fadeImg.color = Color.black; // 완전 까만 화면

        var camMgr = FindFirstObjectByType<CameraEffectManager>();
        Unity.Cinemachine.CinemachineFollow follow = null;
        float startZ = -5f;
        float targetZ = -10f;

        if (camMgr != null && camMgr.vcam != null)
        {
            // 카메라가 플레이어 위치로 즉시 이동하도록 스무딩 무시(스냅)
            camMgr.vcam.PreviousStateIsValid = false;

            // 게임이 Perspective 카메라이므로 줌인은 FollowOffset의 Z값을 조절해야 합니다.
            follow = camMgr.vcam.GetComponent<Unity.Cinemachine.CinemachineFollow>();
            if (follow != null)
            {
                var offset = follow.FollowOffset;
                offset.z = startZ; // 확 줌인
                follow.FollowOffset = offset;
            }
        }

        // 줌 인 상태를 더 길게 유지 (2.0초)
        yield return new WaitForSecondsRealtime(2.0f);

        // 줌 아웃, 페이드 아웃, 블러 해제를 정확히 동일한 시간(2.0초) 동안 완벽하게 동기화
        float clearTime = 2.0f;
        float t = 0f;
        while (t < clearTime)
        {
            t += Time.unscaledDeltaTime;
            float ratio = t / clearTime;

            // 1. 화면 점점 밝아짐 (블랙아웃 해제)
            fadeImg.color = new Color(0, 0, 0, 1f - ratio);

            // 2. 부드러운 줌 아웃
            if (follow != null)
            {
                var offset = follow.FollowOffset;
                offset.z = Mathf.Lerp(startZ, targetZ, ratio);
                follow.FollowOffset = offset;
            }

            // 3. 블러 효과 서서히 해제
            if (_introBlur != null) _introBlur.SetWeight(1f - ratio);

            yield return null;
        }

        // 최종 값 고정 및 정리
        Destroy(fadeCanvas);
        if (follow != null)
        {
            var offset = follow.FollowOffset;
            offset.z = targetZ;
            follow.FollowOffset = offset;
        }
        if (_introBlur != null) _introBlur.SetWeight(0f);

        // --- 연출 종료 후 입력 및 UI 복구 ---
        if (fsm != null) fsm.TransitionTo(fsm.Idle);
        if (interactor != null) interactor.enabled = true;
        if (InventoryToggle.Instance != null) InventoryToggle.Instance.enabled = true;

        ShowMessage("NPC에게 다가가 대화하세요.");
        ShowMessage("WASD로 이동하고 F/스페이스로 상호작용할 수 있습니다.");

        // 마법사 머리 위에 화살표 표시
        if (_mageNpc != null && TutorialFocusUI.Instance != null)
        {
            TutorialFocusUI.Instance.ShowWorldTarget(_mageNpc, "마법사와 대화하세요 (F)");
        }

        // F키를 눌러 대화가 시작될 때까지 대기
        while (_dialoguePlayer == null || !_dialoguePlayer.IsPlaying) yield return null;

        // 대화 시작되었으므로 화살표 숨김
        if (TutorialFocusUI.Instance != null)
        {
            TutorialFocusUI.Instance.Hide();
        }

        // 대화가 끝날 때까지 대기
        yield return WaitDialogueEnd();

        EnterGatherMaterials();
    }

    private IEnumerator ZoomOutPerspectiveRoutine(Unity.Cinemachine.CinemachineFollow follow, float targetZ, float delay, float duration)
    {
        yield return new WaitForSecondsRealtime(delay);
        float elapsed = 0f;
        float startZ = follow.FollowOffset.z;
        while(elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var offset = follow.FollowOffset;
            offset.z = Mathf.Lerp(startZ, targetZ, elapsed / duration);
            follow.FollowOffset = offset;
            yield return null;
        }
        var finalOffset = follow.FollowOffset;
        finalOffset.z = targetZ;
        follow.FollowOffset = finalOffset;
    }

    private IEnumerator DelayedZoomOut(CameraEffectManager camMgr, float delay, float duration)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (camMgr != null) yield return camMgr.ZoomOut(duration);
    }

    private void EnterGatherMaterials()
    {
        Current = Step.GatherMaterials;

        if (_questTracker != null) _questTracker.Show("튜토리얼 1", "나무/돌 채집");
        ShowMessage("돌 검을 만들기 위해 나무와 돌을 캐세요.");
        ShowMessage("WASD로 이동 후 좌클릭으로 기본 공격을 할 수 있습니다. 기본 공격으로 나무와 돌을 캐세요.");

        // 시작 시점 보유량을 기준선으로 기록 (이후 증분으로 채집/제작 감지)
        _baseWood = Count(_woodItem);
        _baseStone = Count(_stoneItem);
        _baseStick = Count(_sturdyStick);
        _baseBlade = Count(_sharpStoneBlade);
        _baseSword = Count(_stoneSword);
        _gatheredWoodCount = 0;
        _gatheredStoneCount = 0;

        FocusNearestGatherable(_woodItem, "나무를 캐세요");
        SubscribeInventory();

        // 채집 노드가 파괴되는 이벤트를 구독하여 포커스를 실시간 갱신
        GatherableNode.OnAnyNodeDestroyed += OnNodeDestroyed;
    }

    private void FocusNearestGatherable(ItemData item, string hint)
    {
        if (item == null || _gatherablesRoot == null) return;
        var playerTransform = FindPlayer();
        var playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;

        Transform best = null;
        float bestDist = float.PositiveInfinity;
        foreach (Transform c in _gatherablesRoot)
        {
            if (!c.gameObject.activeInHierarchy) continue;
            
            // GatherableNode 컴포넌트가 살아있는지 검증
            var node = c.GetComponent<GatherableNode>();
            if (node != null && !node.IsAlive) continue;

            // 드롭 아이템이 매칭되는지 필터링
            if (node != null && node.LootItem != item) continue;

            float d = Vector3.Distance(c.position, playerPos);
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }

        if (best != null)
        {
            if (TutorialFocusUI.Instance != null)
                TutorialFocusUI.Instance.ShowWorldTarget(best, hint != null ? hint : "이걸 공격하세요!");
        }
        else
        {
            if (TutorialFocusUI.Instance != null)
                TutorialFocusUI.Instance.Hide();
        }
    }

    private void OnNodeDestroyed(GatherableNode node)
    {
        if (Current != Step.GatherMaterials) return;
        StartCoroutine(UpdateFocusAfterNodeDestroyed(node));
    }

    private IEnumerator UpdateFocusAfterNodeDestroyed(GatherableNode destroyedNode)
    {
        // 아이템이 떨어지고 먹을 시간을 고려하여 약간의 프레임 대기 후 포커스 재설정
        yield return new WaitForSeconds(0.5f);
        if (Current != Step.GatherMaterials) yield break;

        int currentWood = Mathf.Max(0, Count(_woodItem) - _baseWood);
        int currentStone = Mathf.Max(0, Count(_stoneItem) - _baseStone);

        if (currentWood < _woodNeeded)
        {
            FocusNearestGatherable(_woodItem, "나무를 캐세요");
        }
        else if (currentStone < _stoneNeeded)
        {
            FocusNearestGatherable(_stoneItem, "돌을 캐세요");
        }
    }

    // ── Phase 1: 채집 카운트 안내 ──────────────────────────
    private void AddGatheredCount(ItemData item, int count)
    {
        if (item == _woodItem)
        {
            _gatheredWoodCount += count;
            if (_gatheredWoodCount > _woodNeeded) _gatheredWoodCount = _woodNeeded;
            ShowMessage($"나무 {_gatheredWoodCount}/{_woodNeeded}");
        }
        else if (item == _stoneItem)
        {
            _gatheredStoneCount += count;
            if (_gatheredStoneCount > _stoneNeeded) _gatheredStoneCount = _stoneNeeded;
            ShowMessage($"돌 {_gatheredStoneCount}/{_stoneNeeded}");
        }
    }

    // ── Phase 1~2: 인벤토리 감지 ──────────────────────────
    private void SubscribeInventory()
    {
        if (_inventorySubscribed) return;
        Core.ItemEvents.OnInventoryChanged += OnInventoryChanged;
        _inventorySubscribed = true;
    }

    private void UnsubscribeInventory()
    {
        if (!_inventorySubscribed) return;
        Core.ItemEvents.OnInventoryChanged -= OnInventoryChanged;
        _inventorySubscribed = false;
    }

    private void SubscribeCrafting()
    {
        if (_craftingSubscribed) return;
        if (_craftingManager != null)
        {
            _craftingManager.OnItemCrafted += OnItemCrafted;
            _craftingSubscribed = true;
        }
    }

    private void UnsubscribeCrafting()
    {
        if (!_craftingSubscribed) return;
        if (_craftingManager != null)
            _craftingManager.OnItemCrafted -= OnItemCrafted;
        _craftingSubscribed = false;
    }

    private void OnItemCrafted(CraftingRecipeSO recipe)
    {
        if (Current != Step.CraftSword || recipe == null) return;

        if (recipe.ResultItem == _sturdyStick || recipe.ResultItem == _sharpStoneBlade)
        {
            if (!_intermediatesDone &&
                Count(_sturdyStick) - _baseStick >= 1 &&
                Count(_sharpStoneBlade) - _baseBlade >= 1)
            {
                OnIntermediatesCrafted();
            }
        }
        else if (recipe.ResultItem == _stoneSword)
        {
            if (Count(_stoneSword) - _baseSword >= 1)
            {
                OnSwordCrafted();
            }
        }
    }

    private void UnsubscribeHotbar()
    {
        if (!_hotbarSubscribed) return;
        if (_quickSlotManager != null)
            _quickSlotManager.OnWeaponEquipped -= OnWeaponEquipped;
        _hotbarSubscribed = false;
    }

    private void OnInventoryChanged()
    {
        if (InventoryManager.Instance == null) return;

        if (Current == Step.GatherMaterials)
        {
            int currentWood = Mathf.Max(0, Count(_woodItem) - _baseWood);
            int currentStone = Mathf.Max(0, Count(_stoneItem) - _baseStone);

            if (currentWood != _gatheredWoodCount)
            {
                int diff = currentWood - _gatheredWoodCount;
                if (diff > 0) AddGatheredCount(_woodItem, diff);
            }
            if (currentStone != _gatheredStoneCount)
            {
                int diff = currentStone - _gatheredStoneCount;
                if (diff > 0) AddGatheredCount(_stoneItem, diff);
            }

            if (currentWood >= _woodNeeded && currentStone >= _stoneNeeded)
            {
                OnMaterialsGathered();
            }
            else
            {
                // 포커스 업데이트
                if (currentWood < _woodNeeded)
                {
                    FocusNearestGatherable(_woodItem, "나무를 캐세요");
                }
                else if (currentStone < _stoneNeeded)
                {
                    FocusNearestGatherable(_stoneItem, "돌을 캐세요");
                }
            }
        }
        else if (Current == Step.CraftSword)
        {
            if (!_intermediatesDone &&
                Count(_sturdyStick) - _baseStick >= 1 &&
                Count(_sharpStoneBlade) - _baseBlade >= 1)
                OnIntermediatesCrafted();

            if (Count(_stoneSword) - _baseSword >= 1)
                OnSwordCrafted();
        }
    }

    private void OnMaterialsGathered()
    {
        Current = Step.CraftSword;
        GatherableNode.OnAnyNodeDestroyed -= OnNodeDestroyed;
        if (TutorialFocusUI.Instance != null) TutorialFocusUI.Instance.Hide();
        PlayDialogue("TUT_006");      // "튼튼한 막대와 날카로운 돌 날을 만들어 보죠"
        ShowMessage("I키를 눌러 인벤토리를 여세요.");
        SubscribeCrafting();
    }

    private void OnIntermediatesCrafted()
    {
        _intermediatesDone = true;
        PlayDialogue("TUT_007");      // "이제 돌 검을 만들어봅시다"
    }

    private void OnSwordCrafted()
    {
        UnsubscribeCrafting();
        UnsubscribeInventory();
        Current = Step.EquipHotbar;
        if (_questTracker != null) _questTracker.Show("튜토리얼 2", "핫바에 무기 장착");
        ShowMessage("돌검이 완성됐어요! 인벤토리에서 돌검을 꺼내 핫바 1번 슬롯에 넣으세요.");
        ShowMessage("인벤토리에서 돌검 아이콘을 드래그해 핫바 1번 슬롯에 넣으세요.");
        FocusHotbarSlot(_equipHotbarSlotIndex, "돌검을 여기에 넣으세요");
        SubscribeHotbar();
    }

    private void FocusHotbarSlot(int slotIndex, string hint)
    {
        if (_quickSlotManager != null && _quickSlotManager.quickSlots != null && _quickSlotManager.quickSlots.Length > 0)
        {
            if (slotIndex >= 0 && slotIndex < _quickSlotManager.quickSlots.Length)
            {
                var slot = _quickSlotManager.quickSlots[slotIndex];
                if (slot != null)
                {
                    if (TutorialFocusUI.Instance != null)
                        TutorialFocusUI.Instance.ShowSlotTarget(slot.GetComponent<RectTransform>(), hint);
                }
            }
        }
    }

    // ── Phase 3: 핫바 장착 감지 ───────────────────────────
    private void SubscribeHotbar()
    {
        if (_hotbarSubscribed) return;
        if (_quickSlotManager != null)
        {
            _quickSlotManager.OnWeaponEquipped += OnWeaponEquipped;
            _hotbarSubscribed = true;
        }
    }

    private void OnWeaponEquipped(WeaponData weapon, int slotIndex)
    {
        if (Current != Step.EquipHotbar) return;
        if (weapon != _stoneSword) return;
        if (slotIndex != _equipHotbarSlotIndex) return;

        if (TutorialFocusUI.Instance != null) TutorialFocusUI.Instance.Hide();
        if (_questTracker != null) _questTracker.Hide();
        UnsubscribeHotbar();
        SetFullUiVisible(true);       // 퀘스트2 완료 → 전체 UI 표시

        Current = Step.SpawnBear;
        StartCoroutine(BearPhaseRoutine());
    }

    // ── Phase D: 곰 전투 ───────────────────────────────────
    private IEnumerator BearPhaseRoutine()
    {
        // 인벤토리/제작창이 닫힐 때까지 대기 (스토리보드: 인벤토리 닫으면 곰 등장)
        yield return null;
        while (InventoryToggle.Instance != null && InventoryToggle.Instance.IsAnyPanelOpen())
            yield return null;

        SpawnTutorialBear();
    }

    private void SpawnTutorialBear()
    {
        if (_bearData == null || _bearData.MonsterPrefab == null)
        {
            Debug.LogWarning("[TutorialManager] 곰 데이터/프리팹이 없습니다.");
            return;
        }

        Transform player = FindPlayer();
        Vector3 basePos = player != null ? player.position : Vector3.zero;
        Vector3 spawnPos = basePos + new Vector3(_bearSpawnOffsetX, 0f, 0f); // 화면 좌측

        GameObject bearObj = Instantiate(_bearData.MonsterPrefab, spawnPos, Quaternion.identity);

        var runtime = bearObj.GetComponent<MonsterRuntimeData>();
        if (runtime != null) runtime.Initialize(_bearData);

        var mobBase = bearObj.GetComponent<MonsterBase>();
        if (mobBase != null && MobManager.Instance != null) MobManager.Instance.RegisterMob(mobBase);

        var health = bearObj.GetComponent<HealthSystem>();
        if (health != null)
        {
            health.SetMaxHp(_bearData.MaxHP, true);
            health.Resurrect();

            System.Action onDied = null;
            onDied = () => { health.OnDied -= onDied; OnBearDied(); };
            health.OnDied += onDied;
        }

        // 차징 적중 전 무적
        var guard = bearObj.AddComponent<TutorialBearGuard>();
        if (guard != null)
        {
            guard.OnFirstChargeHit += OnBearGuardBroken;
        }

        Current = Step.FightBear;
        PlayDialogue("TUT_BEAR_001");
        if (_questTracker != null) _questTracker.Show("튜토리얼 3", "곰 격파");
        ShowMessage("곰이 나타났습니다. 적에게 공격을 성공하면 속성 게이지가 쌓입니다.");
        SubscribeGauge();
    }

    private void OnBearGuardBroken()
    {
        ShowMessage("곰의 방어막이 파괴되었습니다! 일반 공격으로도 데미지를 줄 수 있습니다!");
        PlayDialogue("TUT_BEAR_002");
    }

    private void SubscribeGauge()
    {
        if (_gaugeSubscribed || ElementalWeaponSystem.Instance == null) return;
        ElementalWeaponSystem.Instance.OnGaugeChanged += OnGaugeChanged;
        _gaugeSubscribed = true;
    }

    private void UnsubscribeGauge()
    {
        if (!_gaugeSubscribed || ElementalWeaponSystem.Instance == null) { _gaugeSubscribed = false; return; }
        ElementalWeaponSystem.Instance.OnGaugeChanged -= OnGaugeChanged;
        _gaugeSubscribed = false;
    }

    private void OnGaugeChanged(float ratio, ElementType element)
    {
        if (_gaugeFullMessageShown) return;
        if (ratio >= 1f)
        {
            _gaugeFullMessageShown = true;
            ShowMessage("우클릭을 길게 눌러 무기에 맞는 스킬을 발동할 수 있습니다. 마법사가 알려준 차징 스킬로 곰을 물리치세요.");
            UnsubscribeGauge();
        }
    }

    private void OnBearDied()
    {
        UnsubscribeGauge();
        if (_questTracker != null) _questTracker.Hide();

        // QuestManager에 곰 처치 보고 (퀘스트 목표 추적용)
        QuestEventBridge.ReportMonsterKill("Bear");

        Current = Step.Outro;
        StartCoroutine(OutroRoutine());
    }

    // ── Phase E: 마무리 ────────────────────────────────────
    private IEnumerator OutroRoutine()
    {
        PlayDialogue("EDR_QUEST_001");   // "강하시군요…"
        yield return WaitDialogueEnd();

        if (_keybindingPanel != null)
        {
            _keybindingPanel.Show();
            yield return null;
            while (_keybindingPanel.IsOpen) yield return null;
        }

        // 자유 플레이: 튜토리얼 동안 꺼둔 잡몹 스포너 재활성화
        if (_ambientSpawners != null)
            foreach (var s in _ambientSpawners) if (s != null) s.SetActive(true);

        // QuestManager 연동: 튜토리얼 완료 → 메인 퀘스트 해금
        if (QuestManager.Instance != null)
        {
            // 튜토리얼 퀘스트가 QuestManager에 등록되어 있으면 완료 처리
            if (QuestManager.Instance.GetState("TUT_02") == QuestState.Active)
                QuestManager.Instance.CompleteQuest("TUT_02");
        }

        Current = Step.Done;
    }

    private static Transform FindPlayer()
    {
        var pe = FindFirstObjectByType<PlayerEntity>();
        return pe != null ? pe.transform : null;
    }

    private void OnDisable()
    {
        GatherableNode.OnAnyNodeDestroyed -= OnNodeDestroyed;
        UnsubscribeInventory();
        UnsubscribeCrafting();
        UnsubscribeHotbar();
        UnsubscribeGauge();
        if (_dialoguePlayer != null) _dialoguePlayer.OnLineChanged -= OnDialogueLineChanged;
    }

    // ── 헬퍼 ─────────────────────────────────────────────
    private void PlayDialogue(string id)
    {
        if (_dialoguePlayer == null || _dialogues == null) return;
        var so = _dialogues.Get(id);
        if (so != null) _dialoguePlayer.StartDialogue(so);
        else Debug.LogWarning($"[TutorialManager] 대사 ID를 찾을 수 없습니다: {id}");
    }

    private void ShowMessage(string message)
    {
        if (NotificationUI.Instance != null) NotificationUI.Instance.ShowMessage(message);
    }

    private int Count(ItemData item)
    {
        return (item != null && InventoryManager.Instance != null)
            ? InventoryManager.Instance.GetItemCount(item) : 0;
    }

    /// <summary>현재 재생 중인 대화가 끝날 때까지 대기합니다.</summary>
    private IEnumerator WaitDialogueEnd()
    {
        yield return null; // StartDialogue 직후 IsPlaying이 true가 되도록 한 프레임 양보
        if (_dialoguePlayer == null) yield break;
        while (_dialoguePlayer.IsPlaying) yield return null;
    }

    // ── 아웃라인 연출 ─────────────────────────────────────────────

    private void OnDialogueLineChanged(CharacterSO speaker, string text)
    {
        if (_outlineBindings == null || _dialoguePlayer == null) return;
        foreach (var b in _outlineBindings)
        {
            if (b.targetDialogue == _dialoguePlayer.CurrentDialogue
                && b.lineIndex   == _dialoguePlayer.CurrentLineIndex
                && b.outlineTarget != null)
            {
                StartCoroutine(OutlineSequenceRoutine(b));
                break;
            }
        }
    }

    private IEnumerator OutlineSequenceRoutine(OutlineBinding b)
    {
        // 타자기 효과가 끝날 때까지 대기 (대사가 다 출력된 후 연출 시작)
        while (_dialoguePlayer != null && !_dialoguePlayer.IsLineComplete)
            yield return null;

        // 진행 잠금
        _dialoguePlayer?.LockAdvance();

        // 1. 카메라 → 채집물로 이동
        if (_cameraEffectManager != null)
            yield return _cameraEffectManager.PanToTarget(b.outlineTarget, b.panDuration);

        // 2. 아웃라인 생성 후 페이드 인
        var sr = b.outlineTarget.GetComponentInChildren<SpriteRenderer>();
        TutorialNodeOutline outline = null;
        if (sr != null)
        {
            outline = TutorialNodeOutline.Create(sr);
            outline.SetAlpha(0f);
            float elapsed = 0f;
            while (elapsed < b.fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                outline.SetAlpha(Mathf.Clamp01(elapsed / b.fadeDuration));
                yield return null;
            }
            outline.SetAlpha(1f);
        }

        // 3. 0.7초 유지
        yield return new WaitForSecondsRealtime(0.7f);

        // 4. 아웃라인 페이드 아웃
        if (outline != null)
        {
            float elapsed = 0f;
            while (elapsed < b.fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                outline.SetAlpha(1f - Mathf.Clamp01(elapsed / b.fadeDuration));
                yield return null;
            }
            Destroy(outline.gameObject);
        }

        // 5. 카메라 → 플레이어 복귀
        if (_cameraEffectManager != null)
            yield return _cameraEffectManager.PanRestore(b.panDuration);

        // 6. 진행 잠금 해제
        _dialoguePlayer?.UnlockAdvance();
    }
}
