using UnityEngine;

/// <summary>
/// 마나 창(Mana Spear) 투사체 로직:
/// 1. Spawn 시 플레이어 상단(Y+0.5)을 따라다니며 마우스를 향해 회전. (애니메이션이 끝날 때까지 대기)
/// 2. 발사 후(Spawn 애니 종료) 콜라이더 활성화 및 고속 직선 이동.
/// 3. 충돌 또는 일정 거리(20칸) 뒤 Hit 애니메이션 재생 후 소멸.
/// </summary>
public class ManaSpearProjectile : MonoBehaviour
{
    private enum State { Spawn, Alive, Hit }
    private State _currentState;

    private float _damage;
    private float _speed;
    private float _maxDistance = 20f;
    
    private GameObject _player;
    private Animator _animator;
    private Collider2D _collider;
    private SpriteRenderer _spriteRenderer;
    private ParticleSystem _gatheringParticle;

    private Vector3 _startFlyPos;
    private Vector2 _flyDirection;

    public void Initialize(GameObject player, float damage, float speed)
    {
        _player = player;
        _damage = damage;
        _speed = speed;
        _currentState = State.Spawn;
    }

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _collider = GetComponent<Collider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer != null)
        {
            Material spearMat = new Material(Shader.Find("Custom/VFXLit2D"));
            spearMat.SetFloat("_EmissionIntensity", 3f);
            spearMat.SetColor("_EmissionColor", new Color(0.2f, 0.6f, 1f, 1f));
            spearMat.SetFloat("_LightInfluence", 0.3f);
            _spriteRenderer.material = spearMat;
        }

        // 2. 기 모으는 이펙트 동적 생성
        GameObject gatherObj = new GameObject("GatheringVFX");
        gatherObj.transform.SetParent(transform);
        gatherObj.transform.localPosition = Vector3.zero;

        _gatheringParticle = gatherObj.AddComponent<ParticleSystem>();
        var main = _gatheringParticle.main;
        main.duration = 1f;
        main.startLifetime = 0.3f;
        main.startSpeed = -15f; // 바깥에서 안으로 빨려들어오는 효과
        main.startSize = 0.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        var emission = _gatheringParticle.emission;
        emission.rateOverTime = 0f;

        var shape = _gatheringParticle.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 2.5f; // 넓은 반경에서 중앙으로
        shape.radiusThickness = 0.1f;

        var psRenderer = _gatheringParticle.GetComponent<ParticleSystemRenderer>();
        Material gatherMat = new Material(Shader.Find("Custom/VFXLit2D"));
        gatherMat.SetFloat("_EmissionIntensity", 4f);
        gatherMat.SetColor("_EmissionColor", new Color(0.2f, 0.6f, 1f, 1f));
        gatherMat.SetFloat("_LightInfluence", 0.3f);
        psRenderer.material = gatherMat;
        psRenderer.sortingLayerName = "Weapons";
        psRenderer.sortingOrder = 10;
    }

    private void OnEnable()
    {
        // 초기화 시 콜라이더 비활성화
        if (_collider != null) _collider.enabled = false;
        _currentState = State.Spawn;

        // 기 모으는 이펙트 시작
        if (_gatheringParticle != null)
        {
            var em = _gatheringParticle.emission;
            em.rateOverTime = 80f; // 빠르게 집중됨
            _gatheringParticle.Play();
        }
    }

    private void Update()
    {
        if (_currentState == State.Spawn)
        {
            if (_player != null)
            {
                // 플레이어 머리 위로 실시간 이동 (기존 0.5에서 2.0으로 상향)
                transform.position = _player.transform.position + new Vector3(0, 2f, 0);

                // 마우스 커서 방향 추적
                Vector3 mouseWorld = GetMouseWorldPosition();
                Vector2 dir = (mouseWorld - transform.position).normalized;
                transform.right = dir; // 화살촉이 커서를 향하도록 설정
                _flyDirection = dir;   // 발사될 방향 업데이트
            }

            // Spawn 애니메이션이 끝났는지 확인
            if (IsAnimationFinished("ManaSpearSpawn"))
            {
                Launch();
            }
        }
        else if (_currentState == State.Hit)
        {
            // Hit 애니메이션이 끝났는지 확인
            if (IsAnimationFinished("ManaSpearHit"))
            {
                ReturnToPool();
            }
        }
    }

    private void FixedUpdate()
    {
        if (_currentState == State.Alive)
        {
            // Transform.right 방향으로 이동 
            transform.Translate(Vector3.right * (_speed * Time.fixedDeltaTime), Space.Self);

            // 최대 비거리 체크
            if (Vector3.Distance(_startFlyPos, transform.position) >= _maxDistance)
            {
                TriggerHit();
            }
        }
    }

    private void Launch()
    {
        _currentState = State.Alive;
        _startFlyPos = transform.position;

        if (_collider != null) _collider.enabled = true;

        // 발사 시 기 모으는 입자 중단
        if (_gatheringParticle != null)
        {
            var em = _gatheringParticle.emission;
            em.rateOverTime = 0f;
            _gatheringParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void TriggerHit()
    {
        if (_currentState == State.Hit) return;

        _currentState = State.Hit;
        if (_collider != null) _collider.enabled = false;

        // Hit 애니메이션 재생
        if (_animator != null)
        {
            _animator.Play("ManaSpearHit", 0, 0f);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (_currentState != State.Alive) return;

        if (collision.CompareTag("Entity") || collision.CompareTag("Wall") || collision.CompareTag("Obstacle"))
        {
            // 데미지 처리 (적일 경우)
            if (collision.CompareTag("Entity"))
            {
                IDamageable target = collision.GetComponent<IDamageable>();
                if (target != null)
                {
                    target.TakeDamage(_damage, gameObject);
                }
            }
            
            TriggerHit();
        }
    }

    private void ReturnToPool()
    {
        if (gameObject.activeSelf)
        {
            // 파티클, 트레일 등을 정리해야 한다면 여기서 초기화
            SimpleObjectPool.Instance.Release(gameObject);
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position;
        Vector3 screenPos = Input.mousePosition;
        screenPos.z = -cam.transform.position.z;
        return cam.ScreenToWorldPoint(screenPos);
    }

    private bool IsAnimationFinished(string stateName)
    {
        if (_animator == null) return true;
        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(stateName) && stateInfo.normalizedTime >= 0.95f;
    }
}
