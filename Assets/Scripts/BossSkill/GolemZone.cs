using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "골렘 구역" — 플레이어가 구역에 일정 깊이 이상 진입하면 발동하는 보스 아레나.
///
/// 발동 시:
///   1) 플레이어를 제외한 모든 몬스터(MonsterBase)를 99999 고정 데미지로 즉사
///   2) 발동 시점의 플레이어 위치 약간 위쪽을 중심으로 골렘(프리팹)을 생성
///   3) 그 중심을 기준으로 가시 바위 벽이 둥글게 솟아올라 탈출을 막음 (Step 2~4)
///   4) 골렘이 죽으면 벽이 땅속으로 사라짐
///
/// 트리거 콜라이더(IsTrigger)를 같은 오브젝트에 둬야 하며, 발동은 단 한 번만 일어난다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class GolemZone : MonoBehaviour
{
    [Header("References")]
    [Tooltip("중앙에 생성할 골렘 보스 프리팹 (Resources/Prefabs/Test/Golem)")]
    [SerializeField] private GameObject _golemPrefab;
    [Tooltip("플레이어를 식별하는 태그")]
    [SerializeField] private string _playerTag = "Player";
    [Tooltip("즉사 데미지 텍스트 프리팹(미지정 시 Resources/Prefabs/PlayerAttack/DmgText 자동 로드)")]
    [SerializeField] private GameObject _damageTextPrefab;

    [Header("Activation")]
    [Tooltip("트리거 경계로부터 이만큼 더 안쪽으로 들어오면 발동(작을수록 빨리). '들어온 정도' 기준")]
    [SerializeField] private float _activationDepth = 1.5f;
    [Tooltip("중심(=보스)을 플레이어의 '진입 방향'으로 밀어내는 정도(벽 반지름 대비 비율). 클수록 보스가 더 멀리(반대편) 생성")]
    [Range(0f, 0.95f)] [SerializeField] private float _centerOffsetRatio = 0.8f;
    [Tooltip("진입 속도가 이보다 작으면(거의 정지) 위쪽을 기본 진입 방향으로 사용")]
    [SerializeField] private float _minEntrySpeed = 0.05f;

    [Header("Monster Wipe")]
    [Tooltip("발동 시 구역 내 몬스터에게 입히는 고정 즉사 데미지")]
    [SerializeField] private float _wipeDamage = 99999f;

    [Header("Wall")]
    [Tooltip("원형 벽의 반지름(공간 크기)")]
    [SerializeField] private float _wallRadius = 24f;
    [Tooltip("벽을 구성하는 가시 바위 세그먼트 수(많을수록 촘촘히 겹침. 반지름에 맞춰 자동 보정됨)")]
    [SerializeField] private int _wallSegments = 44;

    [Header("Golem")]
    [Tooltip("골렘 생성 후 이 시간(초) 뒤에 행동/공격 시작")]
    [SerializeField] private float _golemActivateDelay = 1f;

    [Header("Cinematic (진입/사망 카메라 연출)")]
    [Tooltip("카메라 연출 매니저(미지정 시 씬에서 자동 검색)")]
    [SerializeField] private CameraEffectManager _cameraEffect;

    [Header("Confine (골렘 사망 전까지 가둠)")]
    [Tooltip("플레이어가 머무는 한계(벽 반지름에서 뺀 여유)")]
    [SerializeField] private float _playerConfineInset = 1f;
    [Tooltip("골렘이 머무는 한계(벽 반지름에서 뺀 여유, 골렘이 크므로 더 크게)")]
    [SerializeField] private float _golemConfineInset = 3f;

    private bool _activated;
    private Vector3 _arenaCenter;
    private GameObject _golemInstance;
    private HealthSystem _golemHealth;
    private GolemWallRing _wallRing;
    private Collider2D _zoneCollider;
    private ArenaConfine _playerConfine;

    // 현재 활성화된 골렘 구역들 — 몹 스포너가 '원 내부 생성 금지' 판정에 사용
    private static readonly List<GolemZone> _activeArenas = new List<GolemZone>();

    /// <summary>worldPos가 활성화된 어떤 골렘 구역(원) 안에 있으면 true.</summary>
    public static bool IsInsideAnyArena(Vector3 worldPos)
    {
        for (int i = 0; i < _activeArenas.Count; i++)
        {
            var z = _activeArenas[i];
            if (z == null) continue;
            Vector2 d = (Vector2)worldPos - (Vector2)z._arenaCenter;
            if (d.sqrMagnitude <= z._wallRadius * z._wallRadius) return true;
        }
        return false;
    }

    private void Awake()
    {
        _zoneCollider = GetComponent<Collider2D>();

        // 이 콜라이더는 '플레이어 진입 감지'용 트리거일 뿐, 물리 벽이나 시야 장애물이 아니다.
        // Default 레이어에 있으면 몹들의 시야(LoS)·배회/도망 경로탐색이 사용하는 obstacleMask(=Default)에
        // 이 거대한 트리거(반경 24)가 잡혀, 구역 안에서 생성된 몹/동물이 플레이어에 전혀 반응하지 못한다.
        // 벽(GolemWallRing)과 동일하게 'Ignore Raycast'로 옮긴다.
        // (충돌 매트릭스상 Ignore Raycast는 Player와 계속 충돌하므로 OnTriggerStay2D 진입 감지는 정상 동작)
        int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycast >= 0) gameObject.layer = ignoreRaycast;
    }

    private void Reset()
    {
        // 에디터에서 컴포넌트 추가 시 콜라이더를 트리거로 자동 설정
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (_activated) return;
        if (!other.CompareTag(_playerTag)) return;

        // '들어온 정도' = 트리거 경계로부터의 침투 깊이. 어느 방향으로 들어와도 깊이가 임계 이상이면 발동.
        if (_zoneCollider == null) return;
        var cd = _zoneCollider.Distance(other);
        if (cd.isOverlapped && -cd.distance >= _activationDepth)
            Activate(other.transform);
    }

    private void Activate(Transform player)
    {
        if (_activated) return;
        _activated = true;

        // 진입 방향(플레이어 이동 방향)을 구해, 그 방향으로 중심(=보스)을 밀어낸다.
        // → 어느 방향에서 들어와도 보스는 '들어온 쪽의 반대편(진행 방향 앞)'에 생성되고, 플레이어는 진입 쪽 가장자리에 위치.
        Vector2 entryDir = Vector2.up; // 기본값(거의 정지 상태로 진입 시)
        var prb = player.GetComponent<Rigidbody2D>();
        if (prb != null && prb.linearVelocity.sqrMagnitude > _minEntrySpeed * _minEntrySpeed)
            entryDir = prb.linearVelocity.normalized;

        _arenaCenter = player.position + (Vector3)(entryDir * (_wallRadius * _centerOffsetRatio));
        _arenaCenter.z = 0f;

        Debug.Log($"[GolemZone] 발동 — 진입방향 {entryDir}, 중심 {_arenaCenter}");

        // 활성 아레나 등록 → 이후 이 원 내부에는 몹이 생성되지 않음
        if (!_activeArenas.Contains(this)) _activeArenas.Add(this);

        WipeMonsters(player.gameObject);
        SpawnGolem();
        SpawnWalls();
        if (_wallRing != null) _wallRing.SetPlayer(player); // 플레이어 가림 시 가시 반투명
        ConfinePlayerAndGolem(player.gameObject);
        PlayBossCinematicIntro(player); // 시간정지+입력차단 → 보스 줌인 → 플레이어 → 아레나 전체 고정
    }

    /// <summary>보스 구역 진입 카메라 연출을 시작한다(CameraEffectManager 위임).</summary>
    private void PlayBossCinematicIntro(Transform player)
    {
        if (_cameraEffect == null) _cameraEffect = FindFirstObjectByType<CameraEffectManager>();
        if (_cameraEffect == null)
        {
            Debug.LogWarning("[GolemZone] CameraEffectManager를 찾지 못해 진입 연출을 건너뜁니다.");
            if (_wallRing != null) _wallRing.PlayRise(); // 연출 없이도 벽은 솟아오르게
            return;
        }

        Transform bossT = _golemInstance != null ? _golemInstance.transform : null;
        _cameraEffect.targetTransform = bossT; // 기존 ZoomIn 등과의 호환을 위해 설정
        var playerFSM = player.GetComponent<PlayerStateMachine>();
        // 카메라가 아레나 전체를 비추는 순간 벽을 솟아오르게 한 뒤, 유지 시간 후 플레이어로 줌인 복귀
        _cameraEffect.PlayBossIntro(bossT, _arenaCenter, _wallRadius, playerFSM,
            onArenaReached: () => { if (_wallRing != null) _wallRing.PlayRise(); });
    }

    /// <summary>플레이어와 골렘을 아레나 원 안에 가둔다(골렘 사망 시 해제).</summary>
    private void ConfinePlayerAndGolem(GameObject playerObj)
    {
        _playerConfine = playerObj.GetComponent<ArenaConfine>();
        if (_playerConfine == null) _playerConfine = playerObj.AddComponent<ArenaConfine>();
        _playerConfine.Init(_arenaCenter, Mathf.Max(1f, _wallRadius - _playerConfineInset));

        if (_golemInstance != null)
        {
            var gc = _golemInstance.GetComponent<ArenaConfine>();
            if (gc == null) gc = _golemInstance.AddComponent<ArenaConfine>();
            gc.Init(_arenaCenter, Mathf.Max(1f, _wallRadius - _golemConfineInset));
        }
    }

    /// <summary>플레이어를 제외한 모든 MonsterBase를 즉사시킨다. (골렘은 MonsterBase가 아니므로 제외됨)</summary>
    private void WipeMonsters(GameObject playerObj)
    {
        var monsters = FindObjectsByType<MonsterBase>(FindObjectsSortMode.None);
        int killed = 0;
        foreach (var mob in monsters)
        {
            if (mob == null) continue;
            if (mob.gameObject == playerObj) continue; // 안전장치(플레이어는 MonsterBase가 아니지만 방어적으로)
            Vector3 pos = mob.transform.position;
            mob.TakeDamage(_wipeDamage, gameObject);
            SpawnWipeDamageText(pos, _wipeDamage); // 고정(지형) 데미지 텍스트 = 흰색
            killed++;
        }
        Debug.Log($"[GolemZone] 몬스터 {killed}마리 즉사 처리");
    }

    private GameObject _resolvedDmgTextPrefab;
    private void SpawnWipeDamageText(Vector3 worldPos, float dmg)
    {
        if (_resolvedDmgTextPrefab == null)
            _resolvedDmgTextPrefab = _damageTextPrefab != null
                ? _damageTextPrefab
                : Resources.Load<GameObject>("Prefabs/PlayerAttack/DmgText");
        if (_resolvedDmgTextPrefab == null || SimpleObjectPool.Instance == null) return;

        var obj = SimpleObjectPool.Instance.Get(_resolvedDmgTextPrefab, worldPos + Vector3.up * 0.5f, Quaternion.identity);
        var dt = obj.GetComponent<DamageText>();
        if (dt != null) dt.Setup(Mathf.RoundToInt(dmg), Color.white); // 고정 데미지 = 흰색
    }

    /// <summary>중심에 골렘 프리팹을 생성하고 사망 이벤트를 구독한다.</summary>
    private void SpawnGolem()
    {
        if (_golemPrefab == null)
        {
            Debug.LogError("[GolemZone] _golemPrefab이 비어 있습니다. 인스펙터에서 Golem 프리팹을 할당하세요.");
            return;
        }

        _golemInstance = Instantiate(_golemPrefab, _arenaCenter, Quaternion.identity);

        // 돌진 등 이동 스킬이 벽을 넘지 않도록 아레나 경계 주입(벽 안쪽으로 약간 여유)
        var ai = _golemInstance.GetComponent<BossAI>();
        if (ai != null)
        {
            ai.SetArenaBounds(_arenaCenter, Mathf.Max(1f, _wallRadius - 2f));
            // 생성 직후엔 멈춰 있다가 _golemActivateDelay초 뒤 행동/공격 시작
            ai.enabled = false;
            StartCoroutine(EnableGolemAI(ai, _golemActivateDelay));
        }

        _golemHealth = _golemInstance.GetComponentInChildren<HealthSystem>();
        if (_golemHealth != null)
            _golemHealth.OnDied += OnGolemDied;
        else
            Debug.LogWarning("[GolemZone] 골렘에 HealthSystem이 없어 사망 시 벽 제거를 구독하지 못했습니다.");
    }

    private IEnumerator EnableGolemAI(BossAI ai, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (ai != null) ai.enabled = true;
    }

    private void OnGolemDied()
    {
        Debug.Log("[GolemZone] 골렘 사망 → 벽 소멸 + 가둠 해제");
        // 아레나 해제 → 다시 원 내부에도 몹 생성 가능
        _activeArenas.Remove(this);
        // 플레이어 가둠 해제(골렘 가둠은 골렘과 함께 파괴됨)
        if (_playerConfine != null) { Destroy(_playerConfine); _playerConfine = null; }
        DespawnWalls();
        PlayBossCinematicOutro(); // 사망 시: 아레나 전체로 다시 줌아웃·고정 → 1.5초 후 플레이어로 복귀
    }

    /// <summary>골렘 사망 카메라 연출(줌아웃·고정 후 복귀)을 시작한다.</summary>
    private void PlayBossCinematicOutro()
    {
        if (_cameraEffect == null) _cameraEffect = FindFirstObjectByType<CameraEffectManager>();
        if (_cameraEffect != null) _cameraEffect.PlayBossDeathSequence(_arenaCenter, _wallRadius);
    }

    // ── 벽 (Step 2~4에서 구현) ─────────────────────────────────────────────

    /// <summary>중심 기준으로 가시 바위 벽을 솟아오르게 생성. (쉐이더·SpriteMask 연출은 Step 3~4)</summary>
    private void SpawnWalls()
    {
        _wallRing = GolemWallRing.Create(_arenaCenter, _wallRadius, _wallSegments);
        _wallRing.SetReveal(0f); // 솟기 전까지 땅속에 숨김(카메라가 아레나를 비출 때 PlayRise로 솟음)
        Debug.Log($"[GolemZone] 벽 생성(숨김 상태) — 반지름 {_wallRadius}, 세그먼트 {_wallSegments}");
    }

    /// <summary>벽을 땅속으로 가라앉히며 제거.</summary>
    private void DespawnWalls()
    {
        if (_wallRing != null)
            _wallRing.PlaySink();
        Debug.Log("[GolemZone] 벽 소멸");
    }

    private void OnDestroy()
    {
        if (_golemHealth != null)
            _golemHealth.OnDied -= OnGolemDied;
        _activeArenas.Remove(this);
        // 구역이 파괴돼도 플레이어 가둠이 남지 않게 정리
        if (_playerConfine != null) Destroy(_playerConfine);
    }

    private void OnDrawGizmosSelected()
    {
        // 트리거(구역) 영역 시각화 — 이 안으로 _activationDepth만큼 들어오면 발동
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.7f);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }

        // 벽 반지름(중심 추정: 현재 위치 기준) 시각화
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * (_wallRadius * _centerOffsetRatio), _wallRadius);
    }
}
