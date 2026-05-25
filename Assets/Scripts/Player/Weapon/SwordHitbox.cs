using System.Collections.Generic;
using UnityEngine;

public class SwordHitbox : MonoBehaviour
{
    [Header("Stats")]
    [Tooltip("Player 루트 오브젝트의 PlayerEntity 컴포넌트를 인스펙터에서 연결하세요.")]
    [SerializeField] private PlayerEntity _playerEntity;
    [Tooltip("기능 무기 고유의 추가 데미지. (WeaponData에 값이 있다면 이 값은 0으로 두는 것을 권장합니다.)")]
    [SerializeField] private float _baseDamage = 0f;

    [Tooltip("이 무기가 공격할 수 있는 대상의 태그 목록. 기본값은 'Entity'.")]
    [SerializeField] private string[] _targetTags = new string[] { "Entity", "Tree" };

    [Header("Hit VFX")]
    [SerializeField] private GameObject[] _hitVfxPrefabs;
    [SerializeField] private float _vfxOffsetTowardsEnemy = 0.3f;

    [Header("Damage Text")]
    [SerializeField] private GameObject _damageTextPrefab;

    private WeaponBehaviourBase _weaponBehaviour;
    private readonly HashSet<int> _hitThisSwing = new HashSet<int>();

    private void Awake()
    {
        _weaponBehaviour = GetComponentInParent<WeaponBehaviourBase>();
        if (_playerEntity == null)
            _playerEntity = GetComponentInParent<PlayerEntity>();
    }

    /// <summary>이번 스윙에서 타격한 적 수를 반환합니다.</summary>
    public int HitCount => _hitThisSwing.Count;

    public void ResetSwingHits()
    {
        _hitThisSwing.Clear();
    }

    /// <summary>
    /// 외부(SpearTipHitbox 등)에서 이미 타격한 적의 ID를 등록하여
    /// 일반 히트박스에서 중복 데미지가 발생하지 않게 합니다.
    /// </summary>
    public void RegisterExternalHit(int targetId)
    {
        _hitThisSwing.Add(targetId);
    }

    /// <summary>이번 스윙에서 해당 적이 이미 타격되었는지 확인합니다.</summary>
    public bool ContainsHit(int targetId)
    {
        return _hitThisSwing.Contains(targetId);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 태그 기반 1차 필터 + IDamageable 기반 2차 필터
        bool isValidTag = false;
        if (_targetTags != null)
        {
            foreach (var t in _targetTags)
            {
                if (other.CompareTag(t))
                {
                    isValidTag = true;
                    break;
                }
            }
        }

        // "Entity" 태그를 가진 대상도 항상 허용 (동물 등 MonsterBase 대상)
        if (!isValidTag && other.CompareTag("Entity"))
            isValidTag = true;

        if (!isValidTag) return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null || !target.IsAlive) return;

        Component targetComp = target as Component;
        if (targetComp == null) return;
        int targetId = targetComp.gameObject.GetInstanceID();
        if (!_hitThisSwing.Add(targetId)) return;

        // (StatAtk + WeaponBaseDmg) * 1.0 * (BashMultiplier)
        float atk    = _playerEntity != null ? _playerEntity.TotalAtk : 0f;
        float damage = DamageCalculator.CalcOutgoingDamage(atk, _baseDamage);
        
        // 강타(Bash) 배율 적용 — 스윙 시작 시 이미 소모된 배율을 읽음
        bool isBashActive = false;
        if (_weaponBehaviour != null)
        {
            float bashMult = _weaponBehaviour.CurrentSwingBashMultiplier;
            damage *= bashMult;
            if (bashMult > 1.0f) isBashActive = true;
        }

        // ★ 창(Spear) 팁 크리티컬: 적이 창 끝 사거리에 있으면 1.5배 데미지 + VFX
        bool isTipCritical = false;
        if (_weaponBehaviour != null && _weaponBehaviour is SpearBehaviour spear)
        {
            if (spear.IsTipHit(other.transform.position))
            {
                damage *= spear.TipDamageMultiplier;
                isTipCritical = true;
            }
        }

        // 에러 방지: 타격 전 위치 캐싱 (TakeDamage 직후 오브젝트 파괴 가능)
        Vector3 targetPosition = other.transform.position;
        Vector3 hitPoint = other.ClosestPoint(transform.position);

        target.TakeDamage(damage, gameObject);

        if (isTipCritical)
        {
            if (CameraShakeController.Instance != null) CameraShakeController.Instance.Shake(0.12f, 0.15f);
            if (HitStopManager.Instance != null) HitStopManager.Instance.TriggerHitStop(0.06f);

            SpearBehaviour spearVfx = _weaponBehaviour as SpearBehaviour;
            if (spearVfx != null) spearVfx.SpawnTipVFX(hitPoint);
        }
        else if (isBashActive)
        {
            if (CameraShakeController.Instance != null) CameraShakeController.Instance.Shake(0.15f, 0.25f);
            if (HitStopManager.Instance != null) HitStopManager.Instance.TriggerHitStop(0.12f);

            SpawnBashImpactVFX(hitPoint);
        }
        else
        {
            if (CameraShakeController.Instance != null) CameraShakeController.Instance.Shake(0.06f, 0.05f);
            if (HitStopManager.Instance != null) HitStopManager.Instance.TriggerHitStop(0.02f);
        }

        // other가 파괴되지 않았을 경우에만 스턴, VFX 등을 적용
        if (other != null && other.gameObject != null)
        {
            float stunDuration = (GetAttackDuration() + 0.05f) * 0.67f;
            ApplyHitStun(other, stunDuration);
            ApplyFlashSync(other, stunDuration);
            SpawnHitVFX(other, hitPoint);
            SpawnDamageText(other, damage);
        }

        // 속성 디버프 적용
        if (other != null && other.gameObject != null)
        {
            DebuffReceiver debuffReceiver = other.GetComponentInParent<DebuffReceiver>();
            if (debuffReceiver != null && _playerEntity != null)
            {
                var elemSystem = ElementalWeaponSystem.Instance;
                if (elemSystem != null)
                    DebuffApplier.Apply(debuffReceiver, elemSystem.CurrentElement, _playerEntity.Stats);
            }
        }

        // 속성 게이지 증가 연동 (이벤트 발송)
        Transform playerRoot = _playerEntity != null ? _playerEntity.transform : transform.root;
        bool isFirstHit = _hitThisSwing.Count <= 1;
        HitEventManager.NotifyHit(playerRoot.position, targetPosition, isFirstHit);
    }

    private void SpawnBashImpactVFX(Vector3 hitPoint)
    {
        GameObject bashVfxObj = new GameObject("BashImpactVFX");
        bashVfxObj.transform.position = hitPoint;
        Destroy(bashVfxObj, 2f);

        Material glowMat = new Material(Shader.Find("Custom/VFXLit2D"));
        glowMat.SetFloat("_EmissionIntensity", 4f);
        glowMat.SetColor("_EmissionColor", new Color(1f, 0.2f, 0.1f, 1f));
        glowMat.SetFloat("_LightInfluence", 0.3f);

        // 1. Shockwave (원형 충격파)
        GameObject shockObj = new GameObject("Shockwave");
        shockObj.transform.SetParent(bashVfxObj.transform);
        shockObj.transform.localPosition = Vector3.zero;
        ParticleSystem shockPs = shockObj.AddComponent<ParticleSystem>();
        shockPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var smain = shockPs.main;
        smain.duration = 0.5f; smain.loop = false;
        smain.startLifetime = 0.3f;
        smain.startSpeed = 0f;
        smain.startSize = 0.2f; // 크기 대폭 축소
        smain.startColor = new Color(1f, 0.4f, 0.2f, 0.8f);
        smain.playOnAwake = false;
        
        var semission = shockPs.emission;
        semission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });
        
        var sSize = shockPs.sizeOverLifetime;
        sSize.enabled = true;
        sSize.size = new ParticleSystem.MinMaxCurve(1f, 2f); // 확장도 절반으로 축소

        var sColor = shockPs.colorOverLifetime;
        sColor.enabled = true;
        Gradient sg = new Gradient();
        sg.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        sColor.color = sg;

        var sRen = shockPs.GetComponent<ParticleSystemRenderer>();
        sRen.material = glowMat;
        sRen.sortingLayerName = "Weapons";
        sRen.sortingOrder = 15;
        shockPs.Play();

        // 3. 파편 (Heavy Debris) - 크기 더 대폭 축소
        GameObject debrisObj = new GameObject("HeavyDebris");
        debrisObj.transform.SetParent(bashVfxObj.transform);
        debrisObj.transform.localPosition = Vector3.zero;
        ParticleSystem debrisPs = debrisObj.AddComponent<ParticleSystem>();
        debrisPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var dmain = debrisPs.main;
        dmain.duration = 1f; dmain.loop = false;
        dmain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        dmain.startSpeed = new ParticleSystem.MinMaxCurve(5f, 15f);
        dmain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f); // 픽셀 크기로 대폭 확 줄임
        dmain.startColor = new Color(1f, 0.7f, 0.2f, 1f);
        dmain.playOnAwake = false;

        var dEmission = debrisPs.emission;
        dEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25) });

        var dShape = debrisPs.shape;
        dShape.shapeType = ParticleSystemShapeType.Sphere;

        var dDrag = debrisPs.limitVelocityOverLifetime;
        dDrag.enabled = true;
        dDrag.limit = 0f;
        dDrag.dampen = 0.18f; // 살짝 더 묵직하게 감쇠

        var dRen = debrisPs.GetComponent<ParticleSystemRenderer>();
        dRen.material = glowMat;
        dRen.sortingLayerName = "Weapons";
        dRen.sortingOrder = 16;
        debrisPs.Play();
    }

    private void SpawnHitVFX(Collider2D enemyCollider, Vector3 hitPoint)
    {
        if (_hitVfxPrefabs == null || _hitVfxPrefabs.Length == 0) return;

        Vector3 enemyCenter     = enemyCollider.bounds.center;
        Vector3 dirToCenter     = (enemyCenter - hitPoint).normalized;

        float   maxDist      = Vector3.Distance(hitPoint, enemyCenter);
        float   actualOffset = Mathf.Min(_vfxOffsetTowardsEnemy, maxDist * 0.5f);
        Vector3 spawnPos     = hitPoint + dirToCenter * actualOffset;

        // 피격 방향 기반 회전 (검 → 적 방향)
        Vector3 hitDir = (enemyCenter - transform.position).normalized;
        float angle = Mathf.Atan2(hitDir.y, hitDir.x) * Mathf.Rad2Deg;

        GameObject prefab = _hitVfxPrefabs[Random.Range(0, _hitVfxPrefabs.Length)];
        SimpleObjectPool.Instance.Get(prefab, spawnPos, Quaternion.Euler(0f, 0f, angle + Random.Range(-15f, 15f)));
    }

    private float GetAttackDuration()
    {
        int combo = _weaponBehaviour != null ? _weaponBehaviour.CurrentComboStep : 1;
        float baseDur;
        float speedMult = 1.0f;
        switch (combo)
        {
            case 1:  baseDur = 0.35f / speedMult; break;
            case 2:  baseDur = 0.45f / speedMult; break;
            default: baseDur = 0.40f / (speedMult * 0.8f); break;
        }
        return baseDur;
    }

    private void ApplyHitStun(Collider2D enemyCollider, float duration)
    {
        HitStaggerHandler stagger = enemyCollider.GetComponentInParent<HitStaggerHandler>();
        if (stagger != null)
            stagger.ApplyStagger(duration);
    }

    private void ApplyFlashSync(Collider2D enemyCollider, float duration)
    {
        HealthSystem hp = enemyCollider.GetComponentInParent<HealthSystem>();
        if (hp != null)
            hp.OverrideFlashTimer(duration);
    }

    private void SpawnDamageText(Collider2D enemyCollider, float damageAmount)
    {
        if (_damageTextPrefab == null) return;

        Vector3 spawnPos = enemyCollider.bounds.center + Vector3.up * 0.5f;

        GameObject textObj = SimpleObjectPool.Instance.Get(_damageTextPrefab, spawnPos, Quaternion.identity);
        DamageText dmgText = textObj.GetComponent<DamageText>();
        if (dmgText != null) dmgText.Setup(Mathf.RoundToInt(damageAmount));
    }
}
