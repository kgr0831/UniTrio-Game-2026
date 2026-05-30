using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적을 관통하는 화살 투사체.
/// 일반 ArrowProjectile과 달리 적에 맞아도 소멸하지 않고 계속 직진합니다.
/// HashSet으로 같은 적 중복 타격을 방지합니다.
/// </summary>
public class PiercingArrowProjectile : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _speed       = 15f;
    [SerializeField] private float _lifeTime    = 3f;
    [SerializeField] private float _maxDistance  = 15f;

    [Header("Damage")]
    [SerializeField] private float _damage = 10f;

    private Vector3 _spawnPos;
    private readonly HashSet<int> _hitTargets = new HashSet<int>();
    private SpriteRenderer _spriteRenderer;
    private ParticleSystem _trailParticle;
    private Color _elementColor;
    private bool _hasElementColor;

    public void SetStats(float speed, float damage)
    {
        _speed  = speed;
        _damage = damage;
    }

    public void SetElementColor(Color hdrColor)
    {
        _elementColor    = hdrColor;
        _hasElementColor = true;

        if (_spriteRenderer != null)
        {
            Color tint = hdrColor;
            tint.a = 1f;
            _spriteRenderer.color = tint;
        }

        if (_trailParticle != null)
        {
            var main = _trailParticle.main;
            Color trailCol = hdrColor;
            trailCol.a = 0.6f;
            main.startColor = trailCol;
        }
    }

    public void SetScale(float scale)
    {
        transform.localScale = Vector3.one * scale;
    }

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();

        // 궤적 파티클 생성
        GameObject trailObj = new GameObject("PiercingTrailVFX");
        trailObj.transform.SetParent(transform);
        trailObj.transform.localPosition = new Vector3(-0.3f, 0, 0);
        trailObj.transform.localRotation = Quaternion.Euler(0, 180, 0);

        _trailParticle = trailObj.AddComponent<ParticleSystem>();
        _trailParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _trailParticle.main;
        main.duration = 1f;
        main.startLifetime = 0.2f;
        main.startSpeed = 0.5f;
        main.startSize = 0.15f;
        main.startColor = new Color(1f, 1f, 1f, 0.6f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        var emission = _trailParticle.emission;
        emission.rateOverTime = 50f;

        var shape = _trailParticle.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.03f;

        var psRenderer = _trailParticle.GetComponent<ParticleSystemRenderer>();
        Material trailMat = new Material(Shader.Find("Custom/VFXLit2D"));
        trailMat.SetFloat("_EmissionIntensity", 3f);
        trailMat.SetColor("_EmissionColor", Color.white);
        trailMat.SetFloat("_LightInfluence", 0.3f);
        psRenderer.material = trailMat;
        psRenderer.sortingLayerName = "Weapons";
        psRenderer.sortingOrder = 9;
    }

    private void OnEnable()
    {
        _spawnPos = transform.position;
        _hitTargets.Clear();

        if (_trailParticle != null)
            _trailParticle.Play();

        if (_hasElementColor)
            SetElementColor(_elementColor);

        Invoke(nameof(DestroySelf), _lifeTime);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(DestroySelf));
        if (_trailParticle != null)
            _trailParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void FixedUpdate()
    {
        transform.Translate(Vector3.right * _speed * Time.fixedDeltaTime, Space.Self);

        if (Vector3.Distance(_spawnPos, transform.position) >= _maxDistance)
            DestroySelf();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Entity"))
        {
            IDamageable target = collision.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive) return;

            Component targetComp = target as Component;
            if (targetComp == null) return;

            int targetId = targetComp.gameObject.GetInstanceID();
            if (!_hitTargets.Add(targetId)) return; // 중복 방지

            target.TakeDamage(_damage, gameObject);
            DebuffApplier.ApplyFromProjectile(collision);
            HitEventManager.NotifyHit(_spawnPos, collision.bounds.center, _hitTargets.Count <= 1);
        }
        else if (collision.CompareTag("Wall"))
        {
            DestroySelf();
        }
        // 적은 관통 (소멸 안 함)
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }
}
