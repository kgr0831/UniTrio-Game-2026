using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 커서 방향에 따른 무기 피봇 회전, 공격 입력, 무기 교체(1/2번 키)를 담당하는 범용 컨트롤러.
/// 무기별 공격 로직/애니메이션은 WeaponBehaviourBase 파생 클래스에 위임합니다.
/// </summary>
public class PlayerWeaponController : MonoBehaviour
{
    [SerializeField] private Transform _weaponPivot;
    [SerializeField] private Animator  _playerAnimator;

    [Header("Weapon Slots (Index = WeaponType - 1)")]
    [Tooltip("0:Sword, 1:Spear, 2:Bow, 3:Staff 순서대로 할당하세요. (None은 제외)")]
    [SerializeField] private WeaponBehaviourBase[] _weaponBehaviours = new WeaponBehaviourBase[4];

    [Tooltip("위와 동일한 순서대로 각 무기의 루트 GameObject를 할당하세요.")]
    [SerializeField] private GameObject[] _weaponObjects = new GameObject[4];


    [Header("Hand Position (손 위치 보정)")]
    [Tooltip("좌/우/하단을 향할 때 피봇을 아래로 내리는 양. 위를 향할 때는 0, 나머지 방향에서 이 값만큼 내려갑니다.")]
    [SerializeField] private float _handYOffset = 0.15f;

    /// <summary>현재 공격 애니메이션 재생 중인지 여부. FSM에서 대시 진입 조건으로 사용합니다.</summary>
    public bool IsAttacking => _activeBehaviour != null && _activeBehaviour.IsAttacking;

    private Camera _mainCamera;
    private float  _camToWorldZ;
    private float  _cursorDx; // UpdateCursorDirection에서 캐싱 → HandleAttackInput에서 콤보 flip에 재사용
    private float  _cursorDistance; // 커서까지의 거리 (무기 동적 생성용)
    // ── 콤보 엔진 ─────────────────────────────────────────
    // _comboStep : 다음 번에 실행될 공격의 타수(step).
    // BeginAttack 호출 후 즉시 증가하므로, UpdateCursorDirection이
    // 이 값을 읽을 때는 항상 "다음 공격"을 위한 방향 프리뷰가 됩니다.
    private int   _comboStep = 1;
    private float _lastAttackEndTime;

    // 입력 버퍼링: 짧은 찰나의 광클도 씹히지 않게 0.3초간 기억
    private bool  _attackQueued;
    private float _attackQueueTime;
    private float _attackStartTime;
    private float _attackCooldownEndTime; // 공격 후 최소 대기 프레임 강제
    private StatSystem _stats;
    private WeaponData _lastRegisteredBonus; // 현재 등록된 보너스 추적

    // 파티클 머티리얼 및 오브젝트 풀 캐싱 (메모리 누수 방지)
    private Material _handMagicMat;
    private ParticleSystem[] _handMagicPool;
    private int _handMagicPoolIndex = 0;
    
    // GC 방지용 미리 생성된 색상 데이터 (인덱스 = WeaponType)
    private Color[] _hdrColors;
    private Gradient[] _convergeGradients;
    private Gradient[] _flashGradients;

    // 현재 활성 무기
    private int                 _currentSlotIndex = 0;
    private WeaponBehaviourBase _activeBehaviour;

    public WeaponBehaviourBase ActiveBehaviour => _activeBehaviour;

    // --- Spear Stack System (Global) ---
    public int SpearStacks { get; private set; }
    public float LastSpearAttackTime { get; private set; }
    public event Action<int> OnSpearStacksChanged;

    /// <summary>무기 장착 완료 시 발생. 새로 장착된 무기의 WeaponType을 전달합니다.</summary>
    public event Action<WeaponType> OnWeaponChanged;

    // 공격 중 스왑 요청을 저장해두는 큐 (-1 = 없음)
    // 공격이 끝난 첫 프레임에 자동 실행되어 피봇 트랜스폼 오염을 방지합니다.
    private int _pendingSlotIndex = -1;

    private void Awake()
    {
        _mainCamera = Camera.main;
        if (_mainCamera != null)
            _camToWorldZ = Mathf.Abs(_mainCamera.transform.position.z - transform.position.z);

        _stats = GetComponent<StatSystem>();

        InitHandMagicPool();

        // 초기 장착은 QuickSlotManager.Start()가 담당합니다.

        // 모든 무기 오브젝트에 부유 모션 컴포넌트를 동적으로 부착하고 비활성화해 둡니다.
        foreach (var obj in _weaponObjects)
        {
            if (obj != null)
            {
                if (obj.GetComponent<FloatingWeaponMotion>() == null)
                    obj.AddComponent<FloatingWeaponMotion>();
                
                obj.SetActive(false);
            }
        }
    }

    private void Update()
    {
        // 패널이 열려있으면 무기 조작 차단
        if (InventoryToggle.Instance != null && InventoryToggle.Instance.IsAnyPanelOpen()) return;

        // 창 스택 만료 체크 (10초)
        if (SpearStacks > 0 && Time.time - LastSpearAttackTime >= 10f)
        {
            ResetSpearStacks();
        }

        CheckAttackFinished();
        FlushPendingSwap();
        UpdateCursorDirection();
        HandleAttackInput();
    }

    public void AddSpearStack()
    {
        SpearStacks = Mathf.Min(5, SpearStacks + 1);
        LastSpearAttackTime = Time.time;
        OnSpearStacksChanged?.Invoke(SpearStacks);
    }

    public void ResetSpearStacks()
    {
        SpearStacks = 0;
        OnSpearStacksChanged?.Invoke(SpearStacks);
    }

    /// <summary>
    /// 무기 장착 요청. 공격 중이면 큐에 저장해 공격 종료 직후 실행합니다.
    /// QuickSlotManager(숫자키)에서 호출됩니다.
    /// </summary>
    public void TryEquipWeapon(int index)
    {
        if (_activeBehaviour != null && _activeBehaviour.IsAttacking)
        {
            // 공격 중 → 큐에 저장 (가장 마지막 요청만 유효)
            _pendingSlotIndex = index;
        }
        else
        {
            _pendingSlotIndex = -1;
            EquipWeapon(index);
        }
    }

    /// <summary>공격이 끝난 프레임에 대기 중인 스왑을 실행합니다.</summary>
    private void FlushPendingSwap()
    {
        if (_pendingSlotIndex < 0) return;
        if (_activeBehaviour != null && _activeBehaviour.IsAttacking) return;

        int idx = _pendingSlotIndex;
        _pendingSlotIndex = -1;
        EquipWeapon(idx);
    }

    /// <summary>무기 데이터(WeaponData)를 기반으로 무기를 장착합니다.</summary>
    public void EquipWeaponByData(WeaponData data)
    {
        if (data == null)
        {
            UnequipAll();
            return;
        }

        // 보너스 업데이트 (장착 시점에 즉시 반영)
        UpdateWeaponBonus(data);

        // WeaponType (None=0, Sword=1, Spear=2, Bow=3, Staff=4) → 배열 인덱스는 -1
        // 배열: [0:Sword, 1:Spear, 2:Bow, 3:Staff]
        int index = (int)data.WeaponType - 1;
        TryEquipWeapon(index);

        if (index >= 0 && index < _weaponBehaviours.Length && _weaponBehaviours[index] != null)
            _weaponBehaviours[index].SetWeaponSprite(data.Icon);
    }

    private void UpdateWeaponBonus(WeaponData data)
    {
        if (_stats == null) return;

        // 기존 보너스 제거
        if (_lastRegisteredBonus != null)
        {
            _stats.UnregisterBonus(_lastRegisteredBonus);
            _lastRegisteredBonus = null;
        }

        // 새 보너스 등록 (합연산)
        if (data != null)
        {
            _stats.RegisterBonus(data);
            _lastRegisteredBonus = data;
        }
    }


    /// <summary>모든 무기를 해제합니다.</summary>
    public void UnequipAll()
    {
        _activeBehaviour?.OnDeactivated();
        _activeBehaviour = null;
        _currentSlotIndex = -1;

        for (int i = 0; i < _weaponObjects.Length; i++)
        {
            if (_weaponObjects[i] == null) continue;
            
            var motion = _weaponObjects[i].GetComponent<FloatingWeaponMotion>();
            if (motion != null && _weaponObjects[i].activeSelf)
                motion.StartFadeOut();
            else
                _weaponObjects[i].SetActive(false);
        }

        UpdateWeaponBonus(null); // 보너스 모두 제거

        OnWeaponChanged?.Invoke(WeaponType.None);
    }


    /// <summary>지정 슬롯 인덱스의 무기를 즉시 장착합니다. 직접 호출 시 공격 중단에 주의하세요.</summary>
    public void EquipWeapon(int index)
    {
        if (index < 0 || index >= _weaponBehaviours.Length)
        {
            UnequipAll();
            return;
        }
        
        // 데이터가 없는 슬롯이면 해제 처리
        if (_weaponBehaviours[index] == null)
        {
            UnequipAll();
            return;
        }

        // 이미 들고 있는 무기면 무시
        if (index == _currentSlotIndex
            && index < _weaponObjects.Length
            && _weaponObjects[index] != null
            && _weaponObjects[index].activeSelf) return;

        // 이전 무기 정리
        _activeBehaviour?.OnDeactivated();

        // 무기 오브젝트 표시 전환 (소멸 시에는 페이드 아웃 적용)
        for (int i = 0; i < _weaponObjects.Length; i++)
        {
            if (_weaponObjects[i] == null) continue;

            if (i == index)
            {
                _weaponObjects[i].SetActive(true);
            }
            else if (_weaponObjects[i].activeSelf)
            {
                var motion = _weaponObjects[i].GetComponent<FloatingWeaponMotion>();
                if (motion != null) motion.StartFadeOut();
                else _weaponObjects[i].SetActive(false);
            }
        }

        _currentSlotIndex = index;
        _activeBehaviour  = _weaponBehaviours[index];

        // 무기 교체 시 콤보 완전 초기화
        _comboStep         = 1;
        _attackQueued      = false;
        _lastAttackEndTime = 0f;

        OnWeaponChanged?.Invoke(_activeBehaviour.WeaponType);
    }


    // ── 커서 방향 / 피봇 회전 ─────────────────────────────

    private void UpdateCursorDirection()
    {
        if (_mainCamera == null) return;

        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z       = _camToWorldZ;
        Vector3 mouseWorld     = _mainCamera.ScreenToWorldPoint(mouseScreenPos);

        float dx = mouseWorld.x - transform.position.x;
        float dy = mouseWorld.y - transform.position.y;

        float sqrMag = dx * dx + dy * dy;
        _cursorDistance = Mathf.Sqrt(sqrMag);

        if (sqrMag > 0.0001f)
        {
            float inv = 1f / _cursorDistance;
            dx *= inv;
            dy *= inv;
        }

        // 공격 시작 시 콤보 flip 즉시 적용을 위해 정규화된 커서 X 방향을 캐싱합니다.
        _cursorDx = dx;

        // - 캐릭터 바라보는 방향 파라미터(DirX, DirY)는 이제 PlayerMovement.cs에서 설정함 -

        // 공격 중에는 피봇 각도를 고정 (주로 근접 무기). 설정에 따라 활처럼 조준을 유지할 수도 있습니다.
        if (_activeBehaviour != null && _activeBehaviour.LockRotationDuringAttack)
        {
            if (_activeBehaviour.IsAttacking) return;
            // 공격 종료 후 페이드아웃 동안 피봇 회전 잠금 (위치/각도 점프 방지)
            if (Time.time - _lastAttackEndTime < 0.15f) return;
        }

        float angle     = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        float rotOffset = _activeBehaviour != null ? _activeBehaviour.PivotRotationOffset : 0f;
        
        _weaponPivot.localEulerAngles = new Vector3(0f, 0f, angle + rotOffset);
        _weaponPivot.localScale = Vector3.one;

        if (_activeBehaviour != null)
        {
            _activeBehaviour.SetFlipY(dx < 0f, dx < 0f);
        }

        // 무기가 등 뒤(위/좌 방향)일 때 Z를 +1 해 플레이어 뒤로 렌더링할지 결정
        // 🔮 마법 무기 컨셉: 공중에 떠다니므로 항상 캐릭터 앞에 렌더링하는 것이 자연스럽습니다.
        bool goBehind = false;
        
        float zDepth = goBehind ? 1f : -1f;
        float depthNudge = goBehind ? 0.001f : -0.001f;

        // 손 위치 보정: 마법으로 조종하므로 손 위치를 따라 위아래로 움직일 필요 없이 일정 높이 유지
        float handY = 0f;
        _weaponPivot.localPosition = new Vector3(0f, handY + depthNudge, zDepth);
    }

    // ── 공격 입력 처리 ────────────────────────────────────

    private void HandleAttackInput()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // "GetMouseButtonDown" (최초 클릭)으로 변경하여 꾹 누르기 자동 연사 제거
        if (Input.GetMouseButtonDown(0))
        {
            // 단타 무기(창 등): 공격 중 클릭 무시 (버퍼링 차단)
            if (_activeBehaviour != null && _activeBehaviour.IsAttacking
                && _activeBehaviour.MaxComboSteps <= 1)
            {
                // 무시
            }
            else
            {
                _attackQueued    = true;
                _attackQueueTime = Time.time;
            }
        }

        // 버퍼 유효 시간(0.3초) 초과 시 파기 (연사 중에는 계속 갱신됨)
        if (_attackQueued && Time.time - _attackQueueTime > 0.3f)
            _attackQueued = false;

        // 공격 중이거나 쿨다운 중이면 대기 (최소 2프레임 간격 보장)
        if (_activeBehaviour == null || _activeBehaviour.IsAttacking) return;
        if (Time.time < _attackCooldownEndTime) return;


        // 콤보 유효 시간이 지났으면 1타로 리셋
        if (Time.time - _lastAttackEndTime > _activeBehaviour.ComboWindow)
            _comboStep = 1;

        if (_attackQueued)
        {
            _attackQueued    = false;
            _attackStartTime = Time.time;

            // 🔮 마법 컨셉: 공격 시작 시 손 위치에 작은 파티클 방출
            SpawnHandMagicEffect();

            // 🔮 무기 소환 거리 동적 보정 (검, 창)
            if (_activeBehaviour.WeaponType == WeaponType.Sword || _activeBehaviour.WeaponType == WeaponType.Spear)
            {
                float finalDist;
                if (_activeBehaviour.WeaponType == WeaponType.Spear)
                {
                    // 창: 피봇 중심에서 시작 (FloatingWeaponMotion이 생성 위치 + 찌르기 궤적 전체 담당)
                    finalDist = 0f;
                }
                else
                {
                    float maxDist = 2.5f;
                    float weaponLengthOffset = 0.8f;
                    finalDist = Mathf.Max(1.0f, maxDist - weaponLengthOffset);
                }

                var floating = _activeBehaviour.GetComponent<FloatingWeaponMotion>();
                if (floating != null)
                {
                    floating.SetBaseLocalPosition(new Vector3(finalDist, 0f, 0f));
                }
            }

            // LockRotationDuringAttack 무기: 새 공격 시작 전 피봇을 현재 커서 방향으로 강제 갱신
            // (이전 공격의 페이드아웃 잠금이 남아있어도 새 공격은 새 커서 방향으로 시작)
            if (_activeBehaviour.LockRotationDuringAttack && _mainCamera != null)
            {
                Vector3 mScreen = Input.mousePosition;
                mScreen.z = _camToWorldZ;
                Vector3 mWorld = _mainCamera.ScreenToWorldPoint(mScreen);
                float adx = mWorld.x - transform.position.x;
                float ady = mWorld.y - transform.position.y;
                float aAngle = Mathf.Atan2(ady, adx) * Mathf.Rad2Deg;
                float aRotOffset = _activeBehaviour.PivotRotationOffset;
                _weaponPivot.localEulerAngles = new Vector3(0f, 0f, aAngle + aRotOffset);
            }

            ApplyAttackStartScale(_comboStep);

            // 공격 시 커서 방향으로 순간 이동
            if (_activeBehaviour.WeaponType == WeaponType.Sword)
            {
                var pm = GetComponent<PlayerMovement>();
                if (pm != null)
                    transform.position += (Vector3)(pm.FacingDirection * 0.3f);
            }

            // 공격 시 무기 오브젝트 활성화 보장 (페이드 아웃으로 꺼졌을 수 있음)
            _activeBehaviour.gameObject.SetActive(true);
            _activeBehaviour.BeginAttack(_comboStep);

            // 다음 클릭/연사를 위해 스텝 순환
            _comboStep = (_comboStep % _activeBehaviour.MaxComboSteps) + 1;
        }
    }

    /// <summary>
    /// 공격 시작 시 콤보 flip을 포함한 피봇 Y-scale을 즉시 적용합니다.
    /// LockRotationDuringAttack 무기는 이후 UpdateCursorDirection이 early return하므로
    /// 이 시점에 미리 설정해야 공격 애니메이션 전체에서 올바른 방향이 유지됩니다.
    /// </summary>
    private void ApplyAttackStartScale(int comboStep)
    {
        if (_activeBehaviour == null || !_activeBehaviour.LockRotationDuringAttack) return;

        bool isAimingLeft = _cursorDx < 0f;
        bool flipY = isAimingLeft;
        
        if (_activeBehaviour.FlipComboDirection && comboStep == 2)
            flipY = !flipY;

        _activeBehaviour.SetFlipY(flipY, isAimingLeft);

        Vector3 euler = _weaponPivot.localEulerAngles;
        euler.x = 0f;
        euler.y = 0f;
        _weaponPivot.localEulerAngles = euler;
        _weaponPivot.localScale = Vector3.one;
    }

    // ── 공격 종료 감시 ────────────────────────────────────

    private void CheckAttackFinished()
    {
        if (_activeBehaviour == null || !_activeBehaviour.IsAttacking) return;

        if (_activeBehaviour.PollFinished(_attackStartTime))
        {
            _lastAttackEndTime = Time.time;
            float baseCooldown = _activeBehaviour.GetPostAttackCooldown();
            
            // 모든 무기에 창 스택에 의한 쿨다운 감소 적용 (스택당 0.02초 감소)
            float finalCooldown = Mathf.Max(0f, baseCooldown - (SpearStacks * 0.02f));
            
            _attackCooldownEndTime = Time.time + finalCooldown;
        }
    }

    /// <summary>
    /// 부활 시 무기 상태를 완전히 초기화합니다.
    /// </summary>
    public void ResetState()
    {
        _comboStep         = 1;
        _attackQueued      = false;
        _lastAttackEndTime = 0f;
        _pendingSlotIndex  = -1;
        
        // 현재 활성 무기의 공격 상태를 강제 중지
        _activeBehaviour?.OnDeactivated();
        
        // 무기 피봇 재활성화는 연출이 모두 끝난 후 OnCutsceneEnd()에서 수행하도록 합니다.
    }

    /// <summary>
    /// 마법 시전 이펙트 초기화 (Awake 시 1회 호출)
    /// 오브젝트 풀링 및 힙 할당(GC) 제거를 위해 모든 객체와 배열을 미리 생성합니다.
    /// </summary>
    private void InitHandMagicPool()
    {
        EnsureHandMagicMaterial();

        // WeaponType에 대응하는 인덱스: 0(None), 1(Sword), 2(Spear), 3(Bow), 4(Staff)
        _hdrColors = new Color[5];
        _convergeGradients = new Gradient[5];
        _flashGradients = new Gradient[5];

        // 레퍼런스 기반 통합 팔레트: 황금 코어 + 연녹색 테두리
        Color[] baseColors = new Color[5] {
            new Color(1f, 0.9f, 0.4f, 1f),   // None: 기본 황금
            new Color(1f, 0.85f, 0.3f, 1f),  // Sword: 순수 황금
            new Color(0.8f, 0.95f, 0.4f, 1f),// Spear: 황금+연녹
            new Color(0.9f, 0.8f, 0.5f, 1f), // Bow: 따뜻한 황금
            new Color(0.7f, 0.95f, 0.5f, 1f) // Staff: 연녹 강조
        };

        for (int i = 0; i < 5; i++)
        {
            Color magic = baseColors[i];
            Color hdr = magic * 6f; hdr.a = 1f;
            _hdrColors[i] = hdr;

            // 수렴 파티클: 빛나면서 다가온 뒤 서서히 페이드아웃
            var cGrad = new Gradient();
            cGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(magic, 0f),
                    new GradientColorKey(Color.white, 0.5f),  // 중간에 밝게
                    new GradientColorKey(magic, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.6f, 0f),     // 시작부터 보임
                    new GradientAlphaKey(1f,   0.4f),   // 40%에서 최대 밝기
                    new GradientAlphaKey(0.7f, 0.7f),   // 70%까지 발광 유지
                    new GradientAlphaKey(0f,   1f)      // 부드럽게 사라짐
                }
            );
            _convergeGradients[i] = cGrad;

            // 플래시: 강렬하게 터진 뒤 서서히 페이드아웃
            var fGrad = new Gradient();
            fGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(magic, 0.4f),
                    new GradientColorKey(magic * 0.5f, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f,   0f),     // 즉시 최대
                    new GradientAlphaKey(0.8f, 0.4f),   // 발광 유지
                    new GradientAlphaKey(0.3f, 0.7f),   // 서서히 감소
                    new GradientAlphaKey(0f,   1f)      // 완전히 사라짐
                }
            );
            _flashGradients[i] = fGrad;
        }

        int poolSize = 3;
        _handMagicPool = new ParticleSystem[poolSize];

        var burst1 = new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30, 45) };
        var burst2 = new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) };
        
        AnimationCurve convergeSizeCurve = new AnimationCurve(new Keyframe(0f, 1.5f), new Keyframe(1f, 0f));
        AnimationCurve flashCurve = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0f));

        for (int i = 0; i < poolSize; i++)
        {
            GameObject rootObj = new GameObject($"HandMagic_Pool_{i}");
            rootObj.transform.SetParent(_weaponPivot);
            rootObj.transform.localPosition = new Vector3(0f, 0.3f, 0f);

            var converge = rootObj.AddComponent<ParticleSystem>();
            converge.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            
            var cMain = converge.main;
            cMain.duration = 0.6f;
            cMain.loop = false;
            cMain.playOnAwake = false;
            cMain.stopAction = ParticleSystemStopAction.Disable; // 재생 완료 후 스스로 꺼짐
            cMain.startLifetime = 0.5f;
            cMain.startSpeed = -3f;
            cMain.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            
            var cEmission = converge.emission;
            cEmission.rateOverTime = 0f;
            cEmission.SetBursts(burst1);

            var cShape = converge.shape;
            cShape.shapeType = ParticleSystemShapeType.Sphere;
            cShape.radius = 1.0f;

            var cSize = converge.sizeOverLifetime;
            cSize.enabled = true;
            cSize.size = new ParticleSystem.MinMaxCurve(1f, convergeSizeCurve);

            var cColor = converge.colorOverLifetime;
            cColor.enabled = true;

            var cRenderer = converge.GetComponent<ParticleSystemRenderer>();
            cRenderer.material = _handMagicMat;
            cRenderer.sortingLayerName = "Weapons";
            cRenderer.sortingOrder = 20;

            GameObject flashObj = new GameObject("HandMagic_Flash");
            flashObj.transform.SetParent(rootObj.transform);
            flashObj.transform.localPosition = Vector3.zero;

            var flash = flashObj.AddComponent<ParticleSystem>();
            flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var fMain = flash.main;
            fMain.duration = 0.15f;
            fMain.loop = false;
            fMain.playOnAwake = false;
            fMain.startDelay = 0.35f;
            fMain.startLifetime = 0.3f;
            fMain.startSpeed = 0f;
            fMain.startSize = 0.8f;

            var fEmission = flash.emission;
            fEmission.rateOverTime = 0f;
            fEmission.SetBursts(burst2);

            var fShape = flash.shape;
            fShape.enabled = false;

            var fSize = flash.sizeOverLifetime;
            fSize.enabled = true;
            fSize.size = new ParticleSystem.MinMaxCurve(1f, flashCurve);

            var fColor = flash.colorOverLifetime;
            fColor.enabled = true;

            var fRenderer = flash.GetComponent<ParticleSystemRenderer>();
            fRenderer.material = _handMagicMat;
            fRenderer.sortingLayerName = "Weapons";
            fRenderer.sortingOrder = 21;

            _handMagicPool[i] = converge;
            rootObj.SetActive(false);
        }
    }

    /// <summary>
    /// 마법 시전 이펙트: 오브젝트 풀에서 꺼내어 재사용 (GC Zero)
    /// </summary>
    private void SpawnHandMagicEffect()
    {
        if (_handMagicPool == null || _handMagicPool.Length == 0) return;

        int weaponIndex = (_activeBehaviour != null) ? (int)_activeBehaviour.WeaponType : 0;
        if (weaponIndex < 0 || weaponIndex >= 5) weaponIndex = 0;

        ParticleSystem converge = _handMagicPool[_handMagicPoolIndex];
        _handMagicPoolIndex = (_handMagicPoolIndex + 1) % _handMagicPool.Length;

        // 색상 갱신
        var cMain = converge.main;
        cMain.startColor = _hdrColors[weaponIndex];

        var cColor = converge.colorOverLifetime;
        cColor.color = _convergeGradients[weaponIndex];

        if (converge.transform.childCount > 0)
        {
            var flash = converge.transform.GetChild(0).GetComponent<ParticleSystem>();
            if (flash != null)
            {
                var fMain = flash.main;
                Color fColor = _hdrColors[weaponIndex];
                fColor.a = 0.8f;
                fMain.startColor = fColor;

                var fColModule = flash.colorOverLifetime;
                fColModule.color = _flashGradients[weaponIndex];
            }
        }

        // 오브젝트 활성화 및 파티클 재생
        converge.gameObject.SetActive(true);
        converge.Play(true);
    }

    /// <summary>
    /// 파티클 머티리얼을 한 번만 생성하여 캐싱합니다.
    /// URP에는 "Default-Particle.png" 빌트인 리소스가 없으므로,
    /// 코드에서 소프트 서클 텍스처를 절차적으로 생성합니다.
    /// </summary>
    private void EnsureHandMagicMaterial()
    {
        if (_handMagicMat != null) return;

        // URP 셰이더 우선 탐색, 없으면 빌트인 폴백
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit");

        _handMagicMat = new Material(particleShader);

        // URP Particles/Unlit: Surface=Transparent, Blend=Additive
        _handMagicMat.SetFloat("_Surface", 1f);  // 0=Opaque, 1=Transparent
        _handMagicMat.SetFloat("_Blend", 1f);    // 0=Alpha, 1=Additive
        _handMagicMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _handMagicMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        _handMagicMat.SetInt("_ZWrite", 0);
        _handMagicMat.renderQueue = 3000;
        _handMagicMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _handMagicMat.EnableKeyword("_BLENDMODE_ADD");

        // 절차적 소프트 서클 텍스처 (둥근 파티클용)
        _handMagicMat.SetTexture("_BaseMap", CreateSoftCircleTexture(32));
        _handMagicMat.SetTexture("_MainTex", CreateSoftCircleTexture(32));
    }

    /// <summary>
    /// 32×32 소프트 서클 텍스처를 절차적 생성합니다.
    /// 중심에서 가장자리로 갈수록 알파가 0으로 감소하여 부드러운 둥근 파티클을 만듭니다.
    /// 한 번만 생성되어 _handMagicMat에 캐싱되므로 GC 부담 없음.
    /// </summary>
    private static Texture2D CreateSoftCircleTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float invRadius = 1f / center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) * invRadius;
                float dy = (y - center) * invRadius;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                // 부드러운 감쇠: 중심=1 → 가장자리=0
                float alpha = Mathf.Clamp01(1f - dist);
                alpha *= alpha; // 제곱 감쇠로 더 부드럽게
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply(false, true); // makeNoLongerReadable=true → 메모리 절약
        return tex;
    }
}
