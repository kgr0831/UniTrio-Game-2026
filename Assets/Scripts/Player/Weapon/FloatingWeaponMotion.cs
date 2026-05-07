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
    [SerializeField] private float _equipSpeed = 10f;

    private WeaponBehaviourBase _weapon;
    
    // 비파괴적 Transform 수정을 위한 캐시
    private Vector3 _lastPosOffset;
    private Quaternion _lastRotOffset = Quaternion.identity;
    
    private float _timeOffset;
    private float _currentLunge;
    private float _equipDropOffset;

    // 파티클 및 렌더러 페이드인 처리용
    private ParticleSystem _auraParticles;
    private SpriteRenderer[] _renderers;
    private Color[] _originalColors;

    // 부드러운 전환을 위한 보간 변수
    private float _currentBobY;
    private float _currentTiltZ;
    private float _currentAlpha;
    private float _currentScalePunch; // 공격 시 역동적 스케일 펀치
    private float _lastScaleMult = 1f; // 비파괴적 스케일 복원용 (이전 프레임 배수)

    private void Awake()
    {
        _weapon = GetComponent<WeaponBehaviourBase>();
        _timeOffset = Random.Range(0f, 100f);

        // 무기 타입별 기본값 자동 설정
        if (_weapon != null)
        {
            switch (_weapon.WeaponType)
            {
                case WeaponType.Sword:
                    _attackLungeDistance = 0.5f; // 더 과감하게 튀어나감
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
            
            CreateMagicAura();
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

                if (glowShader != null)
                {
                    Material energyMat = new Material(glowShader);
                    if (isVfx)
                    {
                        // VFX 이펙트: 전체 발광 모드로 화려하게
                        energyMat.EnableKeyword("_USE_MAIN_ALPHA_AS_GLOW");
                        energyMat.SetColor("_GlowColor", auraColor * 6f);
                        energyMat.SetFloat("_GlowIntensity", 4f);
                    }
                    else
                    {
                        // 무기 본체: 외곽선만 강하게 빛남
                        energyMat.EnableKeyword("_USE_OUTLINE_GLOW");
                        energyMat.SetColor("_GlowColor", auraColor * 6f); // HDR×6
                        energyMat.SetFloat("_GlowIntensity", 10f); // 매우 밝게
                        energyMat.SetFloat("_OutlineWidth", 3f);
                    }
                    r.material = energyMat;
                }

                if (!isVfx)
                    weaponRenderers.Add(r);
            }
            
            _renderers = weaponRenderers.ToArray();

            Color energyColor = new Color(
                Mathf.Lerp(auraColor.r, 1f, 0.6f),
                Mathf.Lerp(auraColor.g, 1f, 0.6f),
                Mathf.Lerp(auraColor.b, 1f, 0.6f),
                0.18f // 내부도 살짝 빛나게
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

    private Color GetWeaponAuraColor()
    {
        if (_weapon == null) return new Color(1f, 1f, 1f, 1f);
        switch (_weapon.WeaponType)
        {
            case WeaponType.Sword: return new Color(1f, 0.2f, 0.2f, 1f); // 붉은 불꽃
            case WeaponType.Spear: return new Color(0.2f, 0.5f, 1f, 1f); // 푸른 번개
            case WeaponType.Bow:   return new Color(0.5f, 0.2f, 1f, 1f); // 보라 맥동
            case WeaponType.Staff: return new Color(0.2f, 0.8f, 1f, 1f); // 하늘 별
            default: return Color.white;
        }
    }

    private void CreateMagicAura()
    {
        if (_weapon == null) return;

        GameObject auraObj = new GameObject("MagicAura_" + _weapon.WeaponType);
        auraObj.transform.SetParent(this.transform);
        auraObj.transform.localPosition = Vector3.zero;
        auraObj.transform.localRotation = Quaternion.identity;

        _auraParticles = auraObj.AddComponent<ParticleSystem>();
        // AddComponent 시 자동 재생되므로 속성 변경 전 정지
        _auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _auraParticles.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f); // 충분히 오래 살아야 궤적이 보임
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);   // 거의 제자리, 살짝 퍼짐
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        // World 시뮬레이션: 생성된 위치에 남아서 공격 궤적(잔상)을 형성
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.gravityModifier = -0.15f; // 살짝 위로 떠오르며 사라짐

        Color auraColor = GetWeaponAuraColor();
        Color hdrColor = auraColor * 8f; // HDR: URP Bloom이 사라질 때까지 발광 유지
        hdrColor.a = 1f;
        main.startColor = hdrColor;

        var emission = _auraParticles.emission;
        // 정지 상태에서는 거의 안 보이고, 이동(공격 스윙) 시 궤적을 따라 빽빽하게 생성
        emission.rateOverTime = 8f;       // 최소한의 아이들 파티클
        emission.rateOverDistance = 120f;  // 이동 궤적에 빽빽한 빛 잔상

        var shape = _auraParticles.shape;

        // 무기 유형별 방출 형태 + 칼날 끝(스윙 궤적의 바깥쪽)으로 오프셋
        switch (_weapon.WeaponType)
        {
            case WeaponType.Sword:
                // 칼날 끝(tip) 기준: 스프라이트 size=1.6, localY=-0.306 → 끝은 약 Y≈1.0
                auraObj.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                shape.shapeType = ParticleSystemShapeType.BoxEdge;
                shape.scale = new Vector3(0.1f, 0.35f, 0.1f); // 칼날 끝 상단만 좁게
                break;
            case WeaponType.Spear:
                // 창끝(날 부분)으로 더 위쪽
                auraObj.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                shape.shapeType = ParticleSystemShapeType.BoxEdge;
                shape.scale = new Vector3(0.08f, 0.4f, 0.1f);
                break;
            case WeaponType.Bow:
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.5f;
                break;
            case WeaponType.Staff:
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.2f;
                auraObj.transform.localPosition = new Vector3(0f, 1f, 0f);
                break;
        }

        // 크기: 원래 크기 유지하다가 후반부에 부드럽게 줄어듦
        var sizeOverLifetime = _auraParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.6f, 0.8f),   // 60%까지 거의 유지
            new Keyframe(1f, 0f)         // 마지막에 사라짐
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // 색상/투명도: HDR 발광을 유지하다가 부드럽게 페이드아웃
        var colorOverLifetime = _auraParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),      // 탄생: 밝은 흰색 플래시
                new GradientColorKey(auraColor, 0.15f),      // 빠르게 무기 고유색으로
                new GradientColorKey(auraColor, 0.7f),       // 70%까지 고유색 유지 (빛남)
                new GradientColorKey(auraColor * 0.5f, 1f)   // 끝: 어두워지며 사라짐
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f,   0f),    // 즉시 최대 밝기
                new GradientAlphaKey(0.9f, 0.5f),  // 절반까지 거의 유지
                new GradientAlphaKey(0.4f, 0.8f),  // 서서히 감소
                new GradientAlphaKey(0f,   1f)     // 완전히 사라짐
            }
        );
        colorOverLifetime.color = grad;

        // Particles/Standard Unlit + Additive 블렌딩 (둥근 원형 발광)
        var renderer = _auraParticles.GetComponent<ParticleSystemRenderer>();
        Material auraMat = new Material(Shader.Find("Particles/Standard Unlit"));
        auraMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        auraMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // Additive
        auraMat.SetInt("_ZWrite", 0);
        auraMat.renderQueue = 3000;
        auraMat.SetTexture("_MainTex", GetSharedSoftCircle());
        renderer.material = auraMat;
        renderer.sortingLayerName = "Weapons";
        renderer.sortingOrder = 10;
    }

    private void OnEnable()
    {
        // OnEnable 시 오프셋 초기화
        _lastPosOffset = Vector3.zero;
        _lastRotOffset = Quaternion.identity;
        
        // 공격할 때만 나타나므로 장착 시 애니메이션은 초기화
        _equipDropOffset = 0f; 
        _currentLunge = 0f;
        _currentAlpha = 0f;
        _currentScalePunch = 0f;

        if (_auraParticles != null)
        {
            _auraParticles.Play();
            var emission = _auraParticles.emission;
            emission.enabled = false;
        }
    }

    private void OnDisable()
    {
        // 비활성화 전 추가했던 오프셋을 원래대로 돌려놓음 (매우 중요)
        transform.localPosition -= _lastPosOffset;
        transform.localRotation = transform.localRotation * Quaternion.Inverse(_lastRotOffset);
        
        _lastPosOffset = Vector3.zero;
        _lastRotOffset = Quaternion.identity;

        // 코루틴 강제 종료를 대비한 색상 복원
        if (_renderers != null && _originalColors != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].color = _originalColors[i];
            }
        }
    }

    // 다단계 공격 모션 상태
    private float _attackPhaseTime;
    private bool  _wasAttacking;
    private int   _lastComboStep = -1; // 콤보 스텝 변화 감지용

    private void LateUpdate()
    {
        // 1. 이전 프레임 오프셋 제거 (원본 상태 복구)
        transform.localPosition -= _lastPosOffset;
        transform.localRotation = transform.localRotation * Quaternion.Inverse(_lastRotOffset);

        // 2. 새 오프셋 계산
        bool isAttacking = _weapon != null && _weapon.IsAttacking;

        // 공격 시작 감지 → 모션 타이머 리셋
        // 콤보 연속 시 IsAttacking이 같은 프레임에서 false→true가 되어 감지가 안 되는 문제를
        // CurrentComboStep 변화로 보완합니다.
        int currentStep = isAttacking ? _weapon.CurrentComboStep : -1;
        bool newAttack = (isAttacking && !_wasAttacking) || 
                         (isAttacking && currentStep != _lastComboStep && _lastComboStep != -1);
        if (newAttack)
        {
            _attackPhaseTime = 0f;
        }
        _wasAttacking = isAttacking;
        _lastComboStep = currentStep;

        // 공격 중이면 타이머 진행
        if (isAttacking)
            _attackPhaseTime += Time.deltaTime;

        // ── 다단계 비선형 공격 모션 ──────────────────────────
        // Phase 1 (0~0.06s): 당김(Anticipation) - 무기가 살짝 뒤로 빠짐
        // Phase 2 (0.06~0.18s): 가속 돌진(Slash) - 폭발적 가속으로 전방 이동 + 회전
        // Phase 3 (0.18~0.30s): 오버슈트(Overshoot) - 관성으로 목표를 넘어감
        // Phase 4 (0.30s~): 감쇠 복귀(Settle) - 스프링처럼 진동하며 안정

        float lungeX = 0f;     // 전방 이동
        float lungeY = 0f;     // 수직 흔들림
        float slashRotZ = 0f;  // 베기 회전
        float scaleMult = 1f;  // 스케일

        if (isAttacking)
        {
            float t = _attackPhaseTime;
            float dist = _attackLungeDistance;

            if (t < 0.06f) // Phase 1: 당김
            {
                float p = t / 0.06f;
                // Ease-In Quad: 부드럽게 뒤로
                float ease = p * p;
                lungeX = Mathf.Lerp(0f, -dist * 0.4f, ease);
                slashRotZ = Mathf.Lerp(0f, -15f, ease); // 살짝 뒤로 기울임
                scaleMult = Mathf.Lerp(1f, 0.9f, ease); // 약간 줄어듦 (힘 모으기)
            }
            else if (t < 0.18f) // Phase 2: 가속 돌진
            {
                float p = (t - 0.06f) / 0.12f;
                // Ease-Out Expo: 폭발적 가속 후 감속
                float ease = 1f - Mathf.Pow(2f, -10f * p);
                lungeX = Mathf.Lerp(-dist * 0.4f, dist * 1.3f, ease); // 뒤→앞 (오버슈트까지)
                lungeY = Mathf.Sin(p * Mathf.PI) * dist * 0.15f; // 살짝 위로 호를 그리며
                slashRotZ = Mathf.Lerp(-15f, 25f, ease); // 크게 베기 회전
                scaleMult = Mathf.Lerp(0.9f, 1.25f, ease); // 확 커짐 (임팩트)
            }
            else if (t < 0.30f) // Phase 3: 오버슈트 바운스
            {
                float p = (t - 0.18f) / 0.12f;
                // Ease-Out Back: 넘어갔다 돌아옴
                float overshoot = Mathf.Sin(p * Mathf.PI) * 0.3f;
                lungeX = Mathf.Lerp(dist * 1.3f, dist * 0.6f, p) + overshoot * dist;
                slashRotZ = Mathf.Lerp(25f, 5f, p);
                scaleMult = Mathf.Lerp(1.25f, 1.05f, p);
            }
            else // Phase 4: 감쇠 안정
            {
                float p = Mathf.Min((t - 0.30f) / 0.15f, 1f);
                // 감쇠 진동: 살짝 떨리면서 안정
                float damping = Mathf.Exp(-8f * p);
                float vibration = Mathf.Sin(p * Mathf.PI * 3f) * damping;
                lungeX = dist * 0.6f * (1f - p) + vibration * dist * 0.1f;
                slashRotZ = 5f * (1f - p) + vibration * 3f;
                scaleMult = 1f + vibration * 0.05f;
            }
        }
        else
        {
            // 비공격 상태: 부드럽게 0으로 복귀
            _currentLunge = Mathf.Lerp(_currentLunge, 0f, Time.deltaTime * _returnSpeed);
            lungeX = _currentLunge;
        }

        // 공격 중일 때 _currentLunge 동기화 (비공격 상태 전환 시 부드럽게 이어지도록)
        if (isAttacking) _currentLunge = lungeX;

        float targetBobY = 0f;
        float targetTiltZ = 0f;

        // 공격 중이 아닐 때만 둥실둥실
        if (!isAttacking)
        {
            targetBobY = Mathf.Sin((Time.time + _timeOffset) * _bobFrequency) * _bobAmplitude;
            targetTiltZ = Mathf.Sin((Time.time + _timeOffset) * _tiltFrequency) * _tiltAmplitude;
        }

        _currentBobY = Mathf.Lerp(_currentBobY, targetBobY, Time.deltaTime * 15f);
        _currentTiltZ = Mathf.Lerp(_currentTiltZ, targetTiltZ, Time.deltaTime * 15f);

        // 🌟 무기 가시성 제어
        if (isAttacking)
        {
            _currentAlpha = Mathf.Lerp(_currentAlpha, 1f, Time.deltaTime * 25f);
        }
        else
        {
            _currentAlpha = 0f;
        }
        _currentScalePunch = scaleMult;

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

        if (_auraParticles != null)
        {
            var emission = _auraParticles.emission;
            emission.enabled = isAttacking;
        }

        // 3. 새 오프셋 적용
        Vector3 lungeOffset = new Vector3(lungeX, lungeY, 0f);
        
        // Bob(부유)은 부모 회전에 구애받지 않고 항상 월드의 위아래(World Y)로 움직이도록 보정
        Vector3 localUp = transform.parent != null ? transform.parent.InverseTransformDirection(Vector3.up) : Vector3.up;
        Vector3 bobOffset = localUp * _currentBobY;

        _lastPosOffset = lungeOffset + bobOffset;
        _lastRotOffset = Quaternion.Euler(0f, 0f, _currentTiltZ + slashRotZ);

        transform.localPosition += _lastPosOffset;
        transform.localRotation = transform.localRotation * _lastRotOffset;

        // 스케일: 비파괴적 방식 (이전 배수 되돌리고 새 배수 적용)
        // SwordBehaviour의 콤보 방향 반전 등을 보존합니다.
        if (_lastScaleMult != 0f)
        {
            float invLast = 1f / _lastScaleMult;
            transform.localScale = new Vector3(
                transform.localScale.x * invLast,
                transform.localScale.y * invLast,
                transform.localScale.z
            );
        }
        _lastScaleMult = _currentScalePunch;
        transform.localScale = new Vector3(
            transform.localScale.x * _lastScaleMult,
            transform.localScale.y * _lastScaleMult,
            transform.localScale.z
        );
    }

    /// <summary>
    /// 무기의 생성 위치(기준점)를 동적으로 변경합니다.
    /// 마우스 커서 위치에 무기를 소환할 때 기존 부유 오프셋을 해치지 않고 기준 위치만 옮깁니다.
    /// </summary>
    public void SetBaseLocalPosition(Vector3 newPos)
    {
        // 1. 기존에 적용된 부유 오프셋 원상 복구
        transform.localPosition -= _lastPosOffset;
        // 2. 새로운 기준 위치 적용
        transform.localPosition = newPos;
        // 3. 부유 오프셋 다시 적용
        transform.localPosition += _lastPosOffset;
    }

    private static Texture2D _sharedSoftCircle;
    private static Texture2D GetSharedSoftCircle()
    {
        if (_sharedSoftCircle == null)
            _sharedSoftCircle = CreateSoftCircleTexture(32);
        return _sharedSoftCircle;
    }

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
                float alpha = Mathf.Clamp01(1f - dist);
                alpha *= alpha; // 부드러운 감쇠
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply(false, true);
        return tex;
    }
}
