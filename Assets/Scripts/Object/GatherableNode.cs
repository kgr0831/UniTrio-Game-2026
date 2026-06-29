using System.Collections;
using UnityEngine;

/// <summary>
/// 나무·돌 등 "기본공격으로 캐는" 채집 오브젝트 공용 컴포넌트.
/// (구 TreeHit을 범용화한 클래스 — guid 보존)
///
/// 동작:
///   1) 플레이어 기본공격이 닿으면 SwordHitbox가 IDamageable.TakeDamage를 호출.
///   2) 내구도는 데미지 수치가 아니라 "타격 횟수" 기준 — 1타 = 내구 1 감소.
///      (플레이어 공격력과 무관하게 나무 6대 / 돌 10대 같은 고정 타수를 보장)
///   3) 피격마다 몬스터와 동일한 점멸(HealthSystem flash)·이펙트·사운드는
///      SwordHitbox 쪽에서 그대로 재생됨. 이 컴포넌트는 점멸 타이머만 한 번 더 보장.
///   4) 내구도가 0이 되면 사망: VFX + 사운드 재생, (나무)옆으로 쓰러지거나 (돌)제자리에서
///      페이드아웃하며, 아이템을 흩뿌린 뒤 오브젝트를 파괴.
///
/// 드롭은 몬스터(MonsterLootDropper)와 동일하게 ItemData.DropPrefab + Icon +
/// DroppedItemIdentity 패턴을 사용합니다.
/// </summary>
[RequireComponent(typeof(HealthSystem))]
public class GatherableNode : MonoBehaviour, IDamageable
{
    [Header("Durability (타격 횟수)")]
    [Tooltip("파괴까지 필요한 기본공격 타격 횟수 (나무 6 / 돌 10 등)")]
    [SerializeField] private int _maxHits = 6;

    [Tooltip("맵 자동 스폰 시 기본 스케일에 곱해지는 배율 (예: 돌 2배). MapGenerator가 참조")]
    [SerializeField] private float _spawnScaleMultiplier = 1f;
    public float SpawnScaleMultiplier => _spawnScaleMultiplier;

    [Header("Loot")]
    [Tooltip("드롭할 재료 (Wood / Stone 등)")]
    [SerializeField] private ItemData _lootItem;
    public ItemData LootItem => _lootItem;
    [Tooltip("사망 시 드롭 최소 개수")]
    [SerializeField] private int _minDropCount = 2;
    [Tooltip("사망 시 드롭 최대 개수")]
    [SerializeField] private int _maxDropCount = 5;
    [Tooltip("드롭 아이템이 튀어오를 때 좌우 확산 각도")]
    [SerializeField] private float _dropSpreadAngle = 35f;
    [SerializeField] private float _minJumpForce = 3f;
    [SerializeField] private float _maxJumpForce = 5f;

    [Header("Hit Feedback")]
    [Tooltip("피격 점멸 유지 시간 (HealthSystem flash 재사용)")]
    [SerializeField] private float _flashDuration = 0.15f;

    [Header("Death VFX / SFX")]
    [Tooltip("사망 시 생성할 이펙트 프리팹 (돌=RockBreakEffect 등). 비워두면 간단한 먼지 파티클을 코드로 생성")]
    [SerializeField] private GameObject _deathVfxPrefab;
    [Tooltip("_deathVfxPrefab이 비었을 때 생성되는 기본 먼지 파티클 색 (나무=잎/먼지 톤)")]
    [SerializeField] private Color _simpleDustColor = new Color(0.45f, 0.32f, 0.18f, 1f);
    [Tooltip("사망 효과음. 비워두면 기본 타격음으로 대체")]
    [SerializeField] private AudioClip _deathSound;
    [Range(0f, 1f)]
    [SerializeField] private float _deathSoundVolume = 1f;

    [Header("Death Motion")]
    [Tooltip("켜면 사망 시 옆으로 쓰러짐(나무용), 끄면 제자리 페이드(돌용)")]
    [SerializeField] private bool _toppleOnDeath = true;
    [Tooltip("쓰러질 최대 각도(도)")]
    [SerializeField] private float _toppleAngle = 82f;
    [Tooltip("사망 연출(쓰러짐+페이드) 지속 시간")]
    [SerializeField] private float _fadeDuration = 0.6f;

    private HealthSystem   _health;
    private SpriteRenderer _sprite;
    private Collider2D     _collider;

    private int  _hitsRemaining;
    private bool _isDying;

    // 죽는 중이거나 내구도가 남지 않으면 더 이상 타격 대상이 아님 (SwordHitbox가 무시)
    public bool IsAlive => !_isDying && _hitsRemaining > 0;

    /// <summary>채집 노드 파괴 시 발생. TutorialManager 등이 구독합니다.</summary>
    public static event System.Action<GatherableNode> OnAnyNodeDestroyed;

    private void Awake()
    {
        _health   = GetComponent<HealthSystem>();
        _sprite   = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        // 풀링/재배치 대비 초기화
        _hitsRemaining = Mathf.Max(1, _maxHits);
        _isDying = false;
    }

    public void TakeDamage(float damage, GameObject source = null)
    {
        if (!IsAlive) return;

        // 몬스터와 동일한 피격 점멸 보장 (SwordHitbox도 별도로 동기화하지만 안전망)
        if (_health != null)
            _health.OverrideFlashTimer(_flashDuration);

        // 골렘(BossAI) 공격은 내구도 무시하고 한 방에 파괴
        bool oneShot = source != null && source.GetComponentInParent<BossAI>() != null;
        if (oneShot)
            _hitsRemaining = 0;
        else
            _hitsRemaining--;

        if (_hitsRemaining <= 0)
            Die();
    }

    private void Die()
    {
        if (_isDying) return;
        _isDying = true;

        if (_collider != null) _collider.enabled = false;

        OnAnyNodeDestroyed?.Invoke(this);

        SpawnDeathVfx();
        PlayDeathSound();
        SpawnLoot();

        StartCoroutine(DeathRoutine());
    }

    // ── 사망 연출: 쓰러짐 + 페이드아웃 동시 진행 ─────────────────────────
    private IEnumerator DeathRoutine()
    {
        float startAlpha = _sprite != null ? _sprite.color.a : 1f;

        // 쓰러질 방향(좌/우 랜덤)과 회전 기준점(밑동)
        float toppleDir = Random.value < 0.5f ? 1f : -1f;
        Vector3 pivot = GetBasePoint();
        float appliedAngle = 0f;

        float t = 0f;
        while (t < _fadeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / _fadeDuration);

            if (_toppleOnDeath)
            {
                // 밑동을 축으로 점점 기울임 (ease-out)
                float target = Mathf.SmoothStep(0f, _toppleAngle * toppleDir, k);
                transform.RotateAround(pivot, Vector3.forward, target - appliedAngle);
                appliedAngle = target;
            }

            if (_sprite != null)
            {
                Color c = _sprite.color;
                c.a = Mathf.Lerp(startAlpha, 0f, k);
                _sprite.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    /// <summary>스프라이트 밑동(바닥 중앙) 월드 좌표. 쓰러짐 회전축으로 사용.</summary>
    private Vector3 GetBasePoint()
    {
        if (_sprite != null)
        {
            Bounds b = _sprite.bounds;
            return new Vector3(b.center.x, b.min.y, transform.position.z);
        }
        return transform.position;
    }

    private void SpawnDeathVfx()
    {
        if (_deathVfxPrefab != null)
        {
            Instantiate(_deathVfxPrefab, GetVfxPoint(), Quaternion.identity);
            return;
        }

        // 전용 이펙트가 없으면 간단한 먼지 파티클을 코드로 생성
        SpawnSimpleDust(GetVfxPoint());
    }

    /// <summary>전용 이펙트 프리팹이 없을 때 쓰는 1회성 먼지 파티클 버스트.</summary>
    private void SpawnSimpleDust(Vector3 pos)
    {
        var go = new GameObject("GatherDust");
        go.transform.position = pos;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startColor = _simpleDustColor;
        main.gravityModifier = 0.6f;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.4f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        // 프로젝트에서 검증된 VFX 셰이더 재사용 (SwordHitbox 강타 VFX와 동일)
        Shader vfxShader = Shader.Find("Custom/VFXLit2D");
        if (vfxShader != null)
            renderer.material = new Material(vfxShader);
        renderer.sortingLayerName = "Weapons";
        renderer.sortingOrder = 12;

        ps.Play();
    }

    private Vector3 GetVfxPoint()
    {
        return _sprite != null ? _sprite.bounds.center : transform.position;
    }

    private void PlayDeathSound()
    {
        if (AudioManager.Instance == null) return;

        if (_deathSound != null)
            AudioManager.Instance.PlaySFX(_deathSound, _deathSoundVolume);
        else
            AudioManager.Instance.PlayHit(); // 전용 사운드 미지정 시 폴백
    }

    // ── 드롭: 몬스터와 동일한 DropPrefab + Icon + DroppedItemIdentity 패턴 ──
    private void SpawnLoot()
    {
        if (_lootItem == null)
        {
            Debug.LogWarning($"[GatherableNode] {name}: 드롭 아이템(_lootItem)이 비어 있습니다.");
            return;
        }
        if (_lootItem.DropPrefab == null)
        {
            Debug.LogWarning($"[GatherableNode] {name}: {_lootItem.Name}의 DropPrefab이 설정되지 않았습니다.");
            return;
        }

        int min = Mathf.Max(1, Mathf.Min(_minDropCount, _maxDropCount));
        int max = Mathf.Max(_minDropCount, _maxDropCount);
        int count = Random.Range(min, max + 1);

        Vector3 origin = GetVfxPoint();

        for (int i = 0; i < count; i++)
        {
            GameObject drop = SimpleObjectPool.Instance.Get(
                _lootItem.DropPrefab, origin, Quaternion.identity);
            if (drop == null) continue;

            // 스프라이트는 아이템 아이콘으로 교체 (Drop 프리팹 공용)
            var sr = drop.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && _lootItem.Icon != null)
                sr.sprite = _lootItem.Icon;

            // 아이템 정체 주입 (획득 시 인벤토리로 전달)
            var identity = drop.GetComponent<DroppedItemIdentity>();
            if (identity == null)
                identity = drop.AddComponent<DroppedItemIdentity>();
            identity.Setup(_lootItem, 1);

            // 부채꼴로 통통 튀어오르게
            var magnetic = drop.GetComponent<FloatingMagneticItem>();
            if (magnetic != null)
            {
                float angle = Random.Range(-_dropSpreadAngle, _dropSpreadAngle);
                Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                dir *= Random.Range(_minJumpForce, _maxJumpForce);
                magnetic.InitDrop(dir);
            }
        }
    }
}
