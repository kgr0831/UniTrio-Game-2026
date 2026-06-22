using UnityEngine;

/// <summary>
/// 플레이어 상태 머신 (FSM) 코디네이터.
/// 컴포넌트 참조를 보유하고, HealthSystem 이벤트를 구독해 상태 전환을 트리거합니다.
/// 상태 목록: Idle → Walk → Dash → Hit → Death / Cutscene
/// </summary>
[RequireComponent(typeof(PlayerEntity))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerWeaponController))]
[RequireComponent(typeof(DashHandler))]
public class PlayerStateMachine : MonoBehaviour
{
    // ── 컴포넌트 참조 ─────────────────────────────────────────────

    public PlayerEntity           Entity         { get; private set; }
    public PlayerMovement         Movement       { get; private set; }
    public PlayerWeaponController WeaponCtrl     { get; private set; }
    public DashHandler            Dash           { get; private set; }
    public Animator               Animator       { get; private set; }
    public SpriteRenderer         Sprite         { get; private set; }
    public DashAfterimagePool     AfterimagePool { get; private set; }

    // ── 상태 인스턴스 ─────────────────────────────────────────────

    public IdleState     Idle     { get; private set; }
    public WalkState     Walk     { get; private set; }
    public HitState      Hit      { get; private set; }
    public DashState     DashSt   { get; private set; }
    public JustDodgeState JustDodge { get; private set; }
    public DeathState    Death    { get; private set; }
    public CutsceneState Cutscene { get; private set; }

    private PlayerState _current;

    // Inspector에서 현재 상태 이름 실시간 확인용 (Normal/Debug 모드 모두 표시)
    [SerializeField] private string _currentStateName;

    // ── 초기화 ───────────────────────────────────────────────────

    private void Awake()
    {
        Entity         = GetComponent<PlayerEntity>();
        Movement       = GetComponent<PlayerMovement>();
        WeaponCtrl     = GetComponent<PlayerWeaponController>();
        Dash           = GetComponent<DashHandler>();
        Animator       = GetComponent<Animator>();
        Sprite         = GetComponent<SpriteRenderer>();
        // DashAfterimagePool이 씬에 없으면 자동 추가 (Tools > Setup Dash Upgrade 없이도 동작)
        AfterimagePool = GetComponent<DashAfterimagePool>();
        if (AfterimagePool == null)
            AfterimagePool = gameObject.AddComponent<DashAfterimagePool>();

        Idle     = new IdleState(this);
        Walk     = new WalkState(this);
        Hit      = new HitState(this);
        DashSt   = new DashState(this);
        JustDodge = new JustDodgeState(this);
        Death    = new DeathState(this);
        Cutscene = new CutsceneState(this);
    }

    private void Start()
    {
        Entity.Health.OnHit  += HandleHit;
        Entity.Health.OnDied += HandleDied;

        // 피격 화면 오버레이 자동 생성 (씬에 별도 프리팹 불필요)
        GameObject overlayObj = new GameObject("PlayerHitOverlay");
        DontDestroyOnLoad(overlayObj);
        PlayerHitOverlay overlay = overlayObj.AddComponent<PlayerHitOverlay>();
        Entity.Health.OnHit += overlay.TriggerHit;

        TransitionTo(Idle);
    }

    // ── 루프 ─────────────────────────────────────────────────────

    private void Update()      => _current?.Update();
    private void FixedUpdate() => _current?.FixedUpdate();

    // ── 상태 전환 ─────────────────────────────────────────────────

    /// <summary>현재 상태를 종료하고 새 상태로 전환합니다.</summary>
    public void TransitionTo(PlayerState next)
    {
        _current?.Exit();
        _current = next;
        _currentStateName = next?.GetType().Name ?? "None";
        _current?.Enter();
    }

    public bool IsInState<T>() where T : PlayerState => _current is T;

    // ── 이벤트 핸들러 ─────────────────────────────────────────────

    private void HandleHit()
    {
        if (IsInState<DeathState>() || IsInState<DashState>() || IsInState<JustDodgeState>()) return;
        TransitionTo(Hit);
    }

    private void HandleDied() => TransitionTo(Death);

    private void OnDestroy()
    {
        if (Entity?.Health == null) return;
        Entity.Health.OnHit  -= HandleHit;
        Entity.Health.OnDied -= HandleDied;
    }
}
