using UnityEngine;

/// <summary>
/// 무기를 마법으로 공중에 띄우는(둥실둥실) 연출을 담당합니다.
/// 기존 공격 로직과 충돌하지 않도록 LateUpdate에서 이전 프레임의 오프셋을 빼고 새 오프셋을 더하는 비파괴적 방식을 사용합니다.
/// </summary>
[RequireComponent(typeof(WeaponBehaviourBase))]
public class FloatingWeaponMotion : MonoBehaviour
{
    [Header("Idle Floating")]
    [SerializeField] private float _bobAmplitude = 0.08f;
    [SerializeField] private float _bobFrequency = 2.0f;
    [SerializeField] private float _tiltAmplitude = 3f;
    [SerializeField] private float _tiltFrequency = 1.5f;

    [Header("Attack Response")]
    [Tooltip("공격 시 앞으로 튀어나가는 거리")]
    [SerializeField] private float _attackLungeDistance = 0.15f;
    [SerializeField] private float _lungeSpeed = 15f;
    [SerializeField] private float _returnSpeed = 8f;

    [Header("Equip Effect")]
    [SerializeField] private float _equipDropHeight = 0.5f;
    [SerializeField] private float _equipSpeed = 2f; // 속도를 낮춰 페이드 인이 눈에 띄게 조정

    [Header("Slash Arc Settings (Sword)")]
    [SerializeField] private float _slashDistance = -0.1f; // 검과 플레이어의 거리 (인스펙터에서 조절 가능)
    [SerializeField] private float _slashScale = 1.2f;    // 검의 크기 (인스펙터에서 조절 가능)
    [SerializeField] private float _slashEasePower = 4.0f; // 비선형 감속 강도 (빠르게 벤 후 점점 느려짐)
    [SerializeField] private float _attackSpeedMultiplier = 1.0f;

    private WeaponBehaviourBase _weapon;
    
    // 절대값 기반 비파괴적 Transform 수정 (드리프트 완전 방지)
    // 위치/회전: 매 프레임 저장 → 복원 → 오프셋 적용
    // 스케일: SwordBehaviour가 flipY를 직접 설정하므로 save/restore하지 않음
    //         대신 Awake에서 저장한 _restBaseScale + 현재 Y부호 + punch로 매 프레임 절대 계산
    private Vector3 _savedBasePos;
    private Quaternion _savedBaseRot;
    private Vector3 _restBaseScale; // Awake에서 저장하는 원본 스케일
    private bool _hasAppliedOffset = false;
    
    private float _timeOffset;
    private float _currentLunge;
    private float _equipDropOffset;

    // 부드러운 전환을 위한 보간 변수
    private float _currentBobY;
    private float _currentTiltZ;
    private float _currentSlashRot;
    private float _currentAlpha = 1f;
    private float _currentScalePunch = 1f; 
    private float _orbitAngle; // 플레이어 주변을 도는 공전 각도
    private float _lastAttackEndTime;

    private SpriteRenderer[] _renderers;
    private Color[] _originalColors;

    [Header("Burst Particles")]
    [ColorUsage(true, true)]
    [SerializeField] private Color _burstParticleColor = new Color(1f, 1f, 1f, 1f); // 무기 색상과 다른 파티클 고유 색 (인스펙터에서 수정 가능, 기본 하얀색)

    [Header("Ghost Trail (Afterimage)")]
    [SerializeField] private float _ghostSpawnDistance = 0.015f; // 너무 빽빽하지 않게 간격 약간 확대
    private float _ghostLifeTime = 0.18f;
    private Vector3 _lastGhostLocalPos;
    private SpriteRenderer _mainSpriteRenderer;
    private System.Collections.Generic.Queue<SpriteRenderer> _ghostPool = new System.Collections.Generic.Queue<SpriteRenderer>();
    private System.Collections.Generic.List<SpriteRenderer> _activeGhosts = new System.Collections.Generic.List<SpriteRenderer>();
    private bool _isFadingOut = false;

    private void Awake()
    {
        _weapon = GetComponent<WeaponBehaviourBase>();
        _timeOffset = Random.Range(0f, 100f);
        _restBaseScale = transform.localScale; // 원본 스케일 저장 (flipY 계산용)

        // 무기 타입별 기본값 자동 설정
        if (_weapon != null)
        {
            switch (_weapon.WeaponType)
            {
                case WeaponType.Sword:
                    _attackLungeDistance = 0.5f; 
                    break;
                case WeaponType.Spear:
                    _attackLungeDistance = 0.7f;
                    break;
                case WeaponType.Bow:
                    _attackLungeDistance = 0.1f;
                    _bobAmplitude = 0.05f; 
                    break;
                case WeaponType.Staff:
                    _attackLungeDistance = 0.1f;
                    _bobAmplitude = 0.1f;
                    break;
            }
        }

        // 🔮 모든 스프라이트를 마법 에너지로 변환 (무기 본체 + VFX 이펙트 모두)
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (allRenderers != null)
        {
            System.Collections.Generic.List<SpriteRenderer> weaponRenderers = new System.Collections.Generic.List<SpriteRenderer>();
            Color auraColor = GetWeaponAuraColor();
            Shader glowShader = Shader.Find("Custom/SpriteGlow");

            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                string objName = r.gameObject.name.ToLower();
                bool isVfx = objName.Contains("vfx") || objName.Contains("effect") || objName.Contains("trail");

                if (isVfx) 
                {
                    // VFX도 알파 페이딩(소멸 효과)을 위해 렌더러 목록에는 포함시킵니다.
                    // 다만 머티리얼 교체(Glow)는 하지 않고 원래 머티리얼을 유지합니다.
                }
                else if (glowShader != null)
                {
                    Material energyMat = new Material(glowShader);
                    energyMat.EnableKeyword("_USE_OUTLINE_GLOW");
                    energyMat.SetColor("_GlowColor", auraColor * 1.5f);
                    energyMat.SetFloat("_GlowIntensity", 2.0f);
                    energyMat.SetFloat("_OutlineWidth", 1.5f);
                    energyMat.SetFloat("_InteriorAlpha", 0.3f);
                    r.material = energyMat;
                }

                // 모든 렌더러 캐싱 (알파 제어용)
                if (_mainSpriteRenderer == null && !isVfx) _mainSpriteRenderer = r;
                weaponRenderers.Add(r);
            }
            
            _renderers = weaponRenderers.ToArray();

            Color energyColor = new Color(
                Mathf.Lerp(auraColor.r, 1f, 0.6f),
                Mathf.Lerp(auraColor.g, 1f, 0.6f),
                Mathf.Lerp(auraColor.b, 1f, 0.6f),
                1f // 내부도 확실하게 보이도록 알파 1.0f 적용 (투명도 버그 해결)
            );

            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].color = energyColor;
                    _originalColors[i] = energyColor;
                }
            }
        }
    }



    /// <summary>
    /// 레퍼런스 기반 통합 색상: 황금(Gold) 코어 + 연녹색(Pale Green) 테두리
    /// 무기 타입별로 미세한 색조 변화만 줍니다.
    /// </summary>
    private Color GetWeaponAuraColor()
    {
        if (_weapon == null) return new Color(1f, 0.85f, 0.4f, 1f);
        switch (_weapon.WeaponType)
        {
            case WeaponType.Sword: return new Color(1f, 0.85f, 0.3f, 1f);  // 순수 황금
            case WeaponType.Spear: return new Color(0.8f, 0.95f, 0.4f, 1f); // 황금+연녹
            case WeaponType.Bow:   return new Color(0.9f, 0.8f, 0.5f, 1f);  // 따뜻한 황금
            case WeaponType.Staff: return new Color(0.7f, 0.95f, 0.5f, 1f); // 연녹 강조
            default: return new Color(1f, 0.9f, 0.4f, 1f);
        }
    }

    /// <summary>
    /// 잔상(Ghost) 생성 로직: 특정 위치와 회전값에 잔상을 배치합니다.
    /// </summary>
    private void SpawnGhostTrail(Vector3 position, Quaternion rotation)
    {
        if (_mainSpriteRenderer == null || _mainSpriteRenderer.sprite == null) return;

        SpriteRenderer ghost = null;
        while (_ghostPool.Count > 0)
        {
            var g = _ghostPool.Dequeue();
            if (g != null)
            {
                ghost = g;
                break;
            }
        }

        if (ghost == null)
        {
            GameObject obj = new GameObject("WeaponGhost");
            obj.transform.SetParent(null); 
            ghost = obj.AddComponent<SpriteRenderer>();
            
            Shader glowShader = Shader.Find("Custom/SpriteGlow");
            if (glowShader != null)
            {
                Material ghostMat = new Material(glowShader);
                ghostMat.EnableKeyword("_USE_MAIN_ALPHA_AS_GLOW");
                ghostMat.SetFloat("_GlowIntensity", 2.5f);
                ghostMat.SetColor("_GlowColor", GetWeaponAuraColor() * 3f);
                ghost.material = ghostMat;
            }
            
            ghost.sortingLayerID = _mainSpriteRenderer.sortingLayerID;
            ghost.sortingOrder = _mainSpriteRenderer.sortingOrder - 1; 
        }

        ghost.gameObject.SetActive(true);
        ghost.sprite = _mainSpriteRenderer.sprite;
        ghost.transform.position = position;
        ghost.transform.rotation = rotation;

        ghost.transform.localScale = _mainSpriteRenderer.transform.lossyScale;
        ghost.flipX = _mainSpriteRenderer.flipX;
        ghost.flipY = _mainSpriteRenderer.flipY;

        _activeGhosts.Add(ghost);
        StartCoroutine(FadeGhostCoroutine(ghost));
    }

    private System.Collections.IEnumerator FadeGhostCoroutine(SpriteRenderer ghost)
    {
        float elapsed = 0f;
        Color baseColor = GetWeaponAuraColor();
        // 3타는 더 잘 보이도록 기본 알파를 약간 높임
        float startAlpha = (_weapon != null && _weapon.CurrentComboStep == 3) ? 0.7f : 0.4f;
        float lifeTime = (_weapon != null && _weapon.CurrentComboStep == 3) ? 0.28f : _ghostLifeTime;

        while (elapsed < lifeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifeTime;
            
            // 검(Sword)인 경우 레트로 느낌을 위해 알파값을 4단계로 뚝뚝 끊어지게 스텝 처리
            if (_weapon != null && _weapon.WeaponType == WeaponType.Sword)
            {
                int steps = 4;
                t = Mathf.Floor(t * steps) / steps;
            }
            
            // 알파 페이드 (크기 확장은 사용자 요청으로 제거)
            Color c = baseColor;
            c.a = Mathf.Lerp(startAlpha, 0f, t);
            ghost.color = c;
            
            yield return null;
        }

        ghost.gameObject.SetActive(false);
        _activeGhosts.Remove(ghost);
        _ghostPool.Enqueue(ghost);
    }

    private void EmitSwordParticles(int count)
    {
        if (_weapon == null || _mainSpriteRenderer == null) return;
        
        GameObject particleObj = new GameObject("SwordBurstParticle");
        particleObj.transform.position = _mainSpriteRenderer.transform.position;
        particleObj.transform.rotation = _mainSpriteRenderer.transform.rotation;
        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f); // 더 오래 머물도록 수명 증가
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);   // 아주 천천히 드리프트하듯 이동
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);  // 입자를 더 작게 하여 스프라이트 형태 유지
        main.startColor = _burstParticleColor * 4.0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)count) });
        
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sprite;
        shape.sprite = _mainSpriteRenderer.sprite;
        shape.randomDirectionAmount = 0.1f; // 처음엔 거의 뭉쳐있도록 방향 랜덤성 최소화
        shape.sphericalDirectionAmount = 0f;
        
        // 부드럽게 페이드아웃
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.3f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        // 아주 천천히 작아짐
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, 0.5f);
        
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        Shader urpParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (urpParticleShader != null)
        {
            Material mat = new Material(urpParticleShader);
            renderer.material = mat;
        }
        
        ps.Play();
    }

    private void OnEnable()
    {
        _equipDropOffset = _equipDropHeight;
        _currentAlpha = 0f;
        _isFadingOut = false;
        _lastGhostLocalPos = transform.localPosition;
        
        // 모션 변수 초기화
        _orbitAngle = 0f;
        _currentSlashRot = 0f;
        _currentLunge = 0f;
        _currentScalePunch = 1f;
        
        // 위치/회전 복원 시스템 초기화 (스케일은 매 프레임 절대 계산하므로 저장 불필요)
        _hasAppliedOffset = false;
        _savedBasePos = transform.localPosition;
        _savedBaseRot = transform.localRotation;
    }

    /// <summary>
    /// 외부에서 무기를 부드럽게 소멸시키고 싶을 때 호출합니다.
    /// </summary>
    public void StartFadeOut(float speed = 5f)
    {
        _isFadingOut = true;
    }

    private void OnDisable()
    {
        // 비활성화 전: 위치/회전만 복원 (스케일은 SwordBehaviour가 관리)
        if (_hasAppliedOffset)
        {
            transform.localPosition = _savedBasePos;
            transform.localRotation = _savedBaseRot;
            transform.localScale = _restBaseScale; // 원본 스케일로 복원
            _hasAppliedOffset = false;
        }

        // 코루틴 강제 종료를 대비한 색상 복원
        if (_renderers != null && _originalColors != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].color = _originalColors[i];
            }
        }

        // 잔상 즉시 제거
        StopAllCoroutines();
        for (int i = _activeGhosts.Count - 1; i >= 0; i--)
        {
            if (_activeGhosts[i] != null)
            {
                _activeGhosts[i].gameObject.SetActive(false);
                _ghostPool.Enqueue(_activeGhosts[i]);
            }
        }
        _activeGhosts.Clear();
    }

    // 다단계 공격 모션 상태
    private float _attackPhaseTime;
    private bool  _wasAttacking;
    private int   _lastComboStep = -1; // 콤보 스텝 변화 감지용

    private void LateUpdate()
    {
        // ═══════════════════════════════════════════════════════════
        // 위치/회전: 절대 저장/복원 (드리프트 0)
        // 스케일: SwordBehaviour가 flipY를 Update()에서 설정하므로
        //         save/restore하면 덮어써짐 → _restBaseScale + 부호 감지로 절대 계산
        // ═══════════════════════════════════════════════════════════

        // 1. 위치/회전만 복원 (스케일은 건드리지 않음 → SwordBehaviour의 flipY 보존)
        if (_hasAppliedOffset)
        {
            transform.localPosition = _savedBasePos;
            transform.localRotation = _savedBaseRot;
            // 스케일은 복원하지 않음! SwordBehaviour가 Update()에서 설정한 flipY를 보존
            _hasAppliedOffset = false;
        }

        // 2. 현재 위치/회전을 기본 상태로 캡처 (SwordBehaviour 변경사항 포함)
        _savedBasePos = transform.localPosition;
        _savedBaseRot = transform.localRotation;
        // 스케일에서 flipY 부호만 감지 (SwordBehaviour가 설정한 값)
        float flipSign = Mathf.Sign(transform.localScale.y);

        // 3. 공격 상태 감지
        bool isAttacking = _weapon != null && _weapon.IsAttacking;
        int currentStep = isAttacking ? _weapon.CurrentComboStep : -1;

        bool newAttack = (isAttacking && !_wasAttacking) || 
                         (isAttacking && currentStep != _lastComboStep && _lastComboStep != -1);

        bool triggerStartParticle = false;
        bool triggerEndParticle = false;

        if (newAttack)
        {
            _attackPhaseTime = 0f;
            _currentLunge = 0f;
            _currentScalePunch = 1f;
            
            _lastGhostLocalPos = transform.localPosition;
            triggerStartParticle = true;
        }
        else if (!isAttacking && _wasAttacking)
        {
            _lastAttackEndTime = Time.time;
            triggerEndParticle = true;
        }

        _wasAttacking = isAttacking;
        _lastComboStep = currentStep;

        float lungeX = 0f;
        float lungeY = 0f;
        float slashRotZ = 0f;
        float scaleMult = 1f;

        if (isAttacking)
        {
            float speedMult = _attackSpeedMultiplier;
            // 3타(360도 회전) 속도 상향 조정 (원래 속도의 80% 수준)
            if (_weapon != null && _weapon.CurrentComboStep == 3) speedMult *= 0.8f;

            _attackPhaseTime += Time.deltaTime * speedMult;
            float t = _attackPhaseTime;
            float dist = _attackLungeDistance;
            
            if (_weapon != null && _weapon.WeaponType == WeaponType.Sword)
            {
                // ── 검 전용 3타 콤보 (오르빗 애니메이션) ──
                int combo = _weapon.CurrentComboStep;
                float duration = (combo == 1) ? 0.35f : (combo == 2) ? 0.45f : 0.40f;
                float p = Mathf.Clamp01(t / duration);

                if (combo == 1) // 1타: Top(90) -> Cursor(0) -> Bottom(-90) 180도 반원
                {
                    // 비선형 이징 (초반 가속, 후반 감속)
                    float ease = 1f - Mathf.Pow(1f - p, _slashEasePower);
                    
                    _orbitAngle = Mathf.Lerp(90f, -90f, ease);
                    slashRotZ = -45f; // 원본 스프라이트 45도 감안 보정
                    
                    lungeX = dist * _slashDistance; 
                    scaleMult = _slashScale; // 1,2,3타 모두 동일한 크기 유지
                }
                else if (combo == 2) // 2타: Bottom(-90) -> Cursor(0) -> Top(90) 180도 반원
                {
                    float ease = 1f - Mathf.Pow(1f - p, _slashEasePower);
                    
                    _orbitAngle = Mathf.Lerp(-90f, 90f, ease);
                    slashRotZ = 45f; // SwordBehaviour에서 Y축이 반전(flipY=-1)되므로 45도 보정
                    
                    lungeX = dist * _slashDistance; 
                    scaleMult = _slashScale; 
                }
                else if (combo == 3) // 3타: 360도 대회전
                {
                    // 3타는 초반 가속이 너무 심해 잔상이 안 보이는 문제를 해결하기 위해 이징 파워를 낮춤 (더 균일한 속도)
                    float ease = 1f - Mathf.Pow(1f - p, 2.0f); 
                    
                    _orbitAngle = Mathf.Lerp(90f, 90f - 360f, ease); 
                    slashRotZ = 45f; 
                    
                    lungeX = dist * _slashDistance; 
                    scaleMult = _slashScale; 
                }
            }
            else
            {
                // ── 창, 활, 지팡이용 기본 직선/반동 공격 로직 ──
                float minRot = -30f;
                float maxRot = 40f;
                float startRot = 0f;
                
                if (_weapon != null && _weapon.CurrentComboStep % 2 == 0)
                {
                    startRot = maxRot;
                    float temp = minRot;
                    minRot = maxRot + 15f; 
                    maxRot = temp;         
                }

                if (t < 0.06f)
                {
                    float p = t / 0.06f;
                    float ease = p * p;
                    lungeX = Mathf.Lerp(0f, -dist * 0.4f, ease);
                    slashRotZ = Mathf.Lerp(startRot, minRot, ease); 
                    scaleMult = Mathf.Lerp(1f, 0.9f, ease);
                }
                else if (t < 0.18f)
                {
                    float p = (t - 0.06f) / 0.12f;
                    float ease = 1f - Mathf.Pow(2f, -10f * p);
                    lungeX = Mathf.Lerp(-dist * 0.4f, dist * 1.3f, ease); 
                    lungeY = Mathf.Sin(p * Mathf.PI) * dist * 0.15f; 
                    slashRotZ = Mathf.Lerp(minRot, maxRot, ease); 
                    scaleMult = Mathf.Lerp(0.9f, 2.5f, ease); 
                }
                else if (t < 0.30f)
                {
                    float p = (t - 0.18f) / 0.12f;
                    float overshoot = Mathf.Sin(p * Mathf.PI) * 0.3f;
                    lungeX = Mathf.Lerp(dist * 1.3f, dist * 0.6f, p) + overshoot * dist;
                    slashRotZ = Mathf.Lerp(maxRot, maxRot - (maxRot - startRot) * 0.15f, p); 
                    scaleMult = Mathf.Lerp(2.5f, 1.05f, p); 
                }
                else
                {
                    float p = Mathf.Min((t - 0.30f) / 0.15f, 1f);
                    float damping = Mathf.Exp(-8f * p);
                    float vibration = Mathf.Sin(p * Mathf.PI * 3f) * damping;
                    lungeX = dist * 0.6f * (1f - p) + vibration * dist * 0.1f;
                    float currentRestRot = startRot * (1f - p);
                    slashRotZ = currentRestRot + vibration * 3f;
                    scaleMult = 1f + vibration * 0.05f;
                }
            }
        }
        else
        {
            // 공격 중이 아닐 때: 원래 상태로 복귀하지 않고 그 자리에서 소멸 (사용자 요청)
            // 복구(Lerp) 로직을 제거하여 애니메이션 종료 지점에서 그대로 사라지게 함
            lungeX = _currentLunge;
            slashRotZ = _currentSlashRot;
            
            // 소멸 중일 때 크기 보존
            scaleMult = _currentScalePunch;
        }

        if (isAttacking) 
        {
            _currentLunge = lungeX;
            _currentSlashRot = slashRotZ;
        }
        _currentScalePunch = scaleMult;

        // 공격 시 나타났다 사라지므로 부유(Idle) 모션 무력화
        _currentBobY = 0f;
        _currentTiltZ = 0f;
        _equipDropOffset = 0f; // 오르빗 중심이 위로 치우치지 않도록 장착 애니메이션 오프셋 제거

        // 🌟 1. 가시성 제어: 공격 중일 때만 나타나고, 끝나면 서서히 사라짐
        if (isAttacking)
        {
            // 공격 중: 빠르게 나타남
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, 1f, Time.deltaTime * 10f);
        }
        else if (_isFadingOut)
        {
            // 무기 교체 시: 서서히 사라짐
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, 0f, Time.deltaTime * 5f);
            if (_currentAlpha <= 0.001f && _activeGhosts.Count == 0) 
            {
                gameObject.SetActive(false);
                return;
            }
        }
        else
        {
            // 공격 종료 후 대기 상태: 소멸 (더 빠르게 사라지게 하여 잔상 남는 현상 방지)
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, 0f, Time.deltaTime * 10f);
            
            // 완전히 사라지면 오브젝트 끄기 (사용자 요청: 공격 끝나면 무조건 사라져야 함)
            // 단, 활성화된 잔상이 남아있을 경우 잔상이 다 사라질 때까지 대기
            if (_currentAlpha <= 0.001f && _activeGhosts.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }
        }

        // 알파값 실제 적용
        if (_renderers != null && _originalColors != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    Color c = _originalColors[i];
                    c.a = _originalColors[i].a * _currentAlpha;
                    _renderers[i].color = c;
                }
            }
        }

        if (_weapon != null && _weapon.DisableFloatingMotion)
        {
            _hasAppliedOffset = false;
            return;
        }

        // 3. 새 오프셋 계산 (현재 transform.localPosition이 원본 상태임)
        Vector3 basePos = transform.localPosition;
        Vector3 newPos = basePos;

        if (_weapon != null && _weapon.WeaponType == WeaponType.Sword)
        {
            // 검 전용: 플레이어 주변을 오르빗(Orbit)하는 궤적
            newPos.x += lungeX;
            newPos.y += lungeY;
            newPos = Quaternion.Euler(0, 0, _orbitAngle) * newPos;
        }
        else
        {
            // 기존 창/지팡이용 직선 돌진 및 로컬 회전
            Quaternion rot = Quaternion.Euler(0, 0, _currentTiltZ + slashRotZ);
            Vector3 lungeVec = rot * new Vector3(lungeX, lungeY, 0f);
            newPos += lungeVec;
        }

        newPos.y += _currentBobY + _equipDropOffset;

        // 오프셋 적용: 저장해둔 기본 상태에 새 오프셋을 더해 최종 Transform 설정
        float totalRotAngle = _currentTiltZ + _currentSlashRot + _orbitAngle;
        
        transform.localPosition = newPos;
        transform.localRotation = _savedBaseRot * Quaternion.Euler(0, 0, totalRotAngle);
        
        // 스케일: _restBaseScale + flipY 부호 + punch로 절대 계산 (누적/드리프트 없음)
        transform.localScale = new Vector3(
            _restBaseScale.x * _currentScalePunch,
            _restBaseScale.y * flipSign * _currentScalePunch,
            _restBaseScale.z
        );
        
        // 오프셋이 적용되었음을 표시 (다음 프레임에서 복원 필요)
        _hasAppliedOffset = true;

        // 🌟 4. 무기 위치가 완벽히 적용된 직후에 잔상을 생성해야 제 위치에 생성됩니다!
        if (isAttacking && _weapon != null && _weapon.WeaponType == WeaponType.Sword)
        {
            // 속도가 빠를수록(초반에 p가 작을 때) 더 촘촘하게 생성되도록 간격 가변 적용
            float t_p = 0.5f;
            if (_weapon != null && _weapon.WeaponType == WeaponType.Sword)
            {
                int combo = _weapon.CurrentComboStep;
                float duration = (combo == 1) ? 0.35f : (combo == 2) ? 0.45f : 0.40f;
                t_p = Mathf.Clamp01(_attackPhaseTime / duration);
            }

            float dynamicThreshold = _ghostSpawnDistance;
            if (t_p < 0.5f) 
            {
                // 빽빽함 조절: 초반 가중치 완화
                float multiplier = (_weapon != null && _weapon.CurrentComboStep == 3) ? 0.6f : 0.8f;
                dynamicThreshold *= Mathf.Lerp(multiplier, 1.0f, t_p * 2f);
            }

            // [로컬 좌표계 기준 이동 거리 계산]
            // 플레이어가 이동할 때 잔상이 길게 늘어지는 현상을 방지하기 위해 로컬 좌표를 사용합니다.
            Vector3 currentLocalPos = transform.localPosition;
            float totalDist = Vector3.Distance(_lastGhostLocalPos, currentLocalPos);
            
            if (totalDist >= dynamicThreshold)
            {
                int spawnCount = Mathf.FloorToInt(totalDist / dynamicThreshold);
                // 최대 생성 수 제한을 10으로 낮춤 (너무 빽빽하지 않게)
                spawnCount = Mathf.Min(spawnCount, 10); 

                for (int i = 1; i <= spawnCount; i++)
                {
                    float lerpVal = (float)i / spawnCount;
                    Vector3 localSpawnPos = Vector3.Lerp(_lastGhostLocalPos, currentLocalPos, lerpVal);
                    // 월드 좌표로 변환하여 생성 (잔상은 그 자리에 머물러야 하므로)
                    Vector3 worldSpawnPos = transform.parent != null ? transform.parent.TransformPoint(localSpawnPos) : transform.TransformPoint(localSpawnPos);
                    SpawnGhostTrail(worldSpawnPos, transform.rotation);
                }
                _lastGhostLocalPos = currentLocalPos;
            }
        }

        // 🌟 5. 파티클 역시 궤적 계산이 모두 끝난 뒤에 생성해야 무기 끝/시작 지점과 일치합니다!
        if (_weapon != null && _weapon.WeaponType == WeaponType.Sword)
        {
            if (triggerStartParticle) EmitSwordParticles(15);
            if (triggerEndParticle) EmitSwordParticles(50);
        }

        // 🌟 6. 알파값 적용 (이미 상단에서 처리됨 - 중복 제거 가능하지만 안전을 위해 유지하거나 상단 로직과 통합)
        // 위에서 이미 _renderers 컬러를 _currentAlpha에 맞춰 업데이트했습니다.
    }

    /// <summary>
    /// 무기의 생성 위치(기준점)를 동적으로 변경합니다.
    /// 마우스 커서 위치에 무기를 소환할 때 기존 부유 오프셋을 해치지 않고 기준 위치만 옮깁니다.
    /// </summary>
    public void SetBaseLocalPosition(Vector3 newPos)
    {
        // 절대값 기반: 저장된 기준 위치를 갱신하고, 현재 적용 중인 오프셋만큼 더함
        Vector3 currentOffset = transform.localPosition - _savedBasePos;
        _savedBasePos = newPos;
        transform.localPosition = newPos + currentOffset;
    }

}
