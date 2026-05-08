using System.Collections.Generic;
using UnityEngine;

public class SwordHitbox : MonoBehaviour
{
    [Header("Stats")]
    [Tooltip("Player 루트 오브젝트의 PlayerEntity 컴포넌트를 인스펙터에서 연결하세요.")]
    [SerializeField] private PlayerEntity _playerEntity;
    [Tooltip("기능 무기 고유의 추가 데미지. (WeaponData에 값이 있다면 이 값은 0으로 두는 것을 권장합니다.)")]
    [SerializeField] private float _baseDamage = 0f;

    [Tooltip("이 무기가 공격할 수 있는 대상의 태그 목록. 기본값은 'Enemy'.")]
    [SerializeField] private string[] _targetTags = new string[] { "Enemy", "Tree" };

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
    }

    public void ResetSwingHits()
    {
        _hitThisSwing.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
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

        target.TakeDamage(damage, gameObject);

        if (isBashActive)
        {
            if (CameraShakeController.Instance != null)
                CameraShakeController.Instance.Shake(0.15f, 0.25f);
            if (HitStopManager.Instance != null)
                HitStopManager.Instance.TriggerHitStop(0.12f);

            SpawnBashImpactVFX(other);
        }
        else
        {
            if (CameraShakeController.Instance != null)
                CameraShakeController.Instance.Shake(0.06f, 0.05f);
            if (HitStopManager.Instance != null)
                HitStopManager.Instance.TriggerHitStop(0.02f);
        }

        float stunDuration = (GetAttackDuration() + 0.05f) * 0.67f;
        ApplyHitStun(other, stunDuration);
        ApplyFlashSync(other, stunDuration);

        SpawnHitVFX(other);
        SpawnDamageText(other, damage);
    }

    private void SpawnBashImpactVFX(Collider2D enemyCollider)
    {
        Vector3 hitPoint = enemyCollider.ClosestPoint(transform.position);

        GameObject bashVfxObj = new GameObject("BashImpactVFX");
        bashVfxObj.transform.position = hitPoint;
        Destroy(bashVfxObj, 2f);

        Material glowMat = new Material(Shader.Find("Custom/SpriteGlow"));
        glowMat.EnableKeyword("_USE_MAIN_ALPHA_AS_GLOW");
        glowMat.SetFloat("_GlowIntensity", 4f);
        glowMat.SetColor("_GlowColor", new Color(1f, 0.2f, 0.1f, 1f));

        // 1. Shockwave (원형 충격파)
        GameObject shockObj = new GameObject("Shockwave");
        shockObj.transform.SetParent(bashVfxObj.transform);
        shockObj.transform.localPosition = Vector3.zero;
        ParticleSystem shockPs = shockObj.AddComponent<ParticleSystem>();
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

    private void SpawnHitVFX(Collider2D enemyCollider)
    {
        if (_hitVfxPrefabs == null || _hitVfxPrefabs.Length == 0) return;

        Vector3 closestHitPoint = enemyCollider.ClosestPoint(transform.position);
        Vector3 enemyCenter     = enemyCollider.bounds.center;
        Vector3 dirToCenter     = (enemyCenter - closestHitPoint).normalized;

        float   maxDist      = Vector3.Distance(closestHitPoint, enemyCenter);
        float   actualOffset = Mathf.Min(_vfxOffsetTowardsEnemy, maxDist * 0.5f);
        Vector3 spawnPos     = closestHitPoint + dirToCenter * actualOffset;

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
