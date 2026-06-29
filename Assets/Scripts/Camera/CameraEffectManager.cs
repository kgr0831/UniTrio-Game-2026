using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Unity.Cinemachine;

public class CameraEffectManager : MonoBehaviour
{
    public Image fadeImage; // 에디터에서 FadeImage를 할당하세요.
    public CinemachineImpulseSource impulseSource;
    
    [Header("Cinemachine Reference")]
    public CinemachineCamera vcam; // 가상 카메라 참조
    
    [Header("Target Settings")]
    public Transform targetTransform; // 연출 대상 (보스 등)
    private CinemachineFollow followComponent; // 컴포넌트 캐싱
    // 원래 카메라 상태를 몽땅 저장할 구조체
    private struct CameraState
    {
        public Transform followTarget;
        public Vector3 followOffset;
        public float orthoSize;
    }
    private CameraState originalState;

    [Header("Boss Intro/Outro Cinematic")]
    [Tooltip("보스로 줌인했을 때의 카메라 거리(FollowOffset.z, 음수일수록 멀다)")]
    [SerializeField] private float _bossZoomZ = -6f;
    [Tooltip("플레이어로 빠르게 줌아웃했을 때의 카메라 거리")]
    [SerializeField] private float _playerZoomZ = -8f;
    [Tooltip("보스로 이동하며 줌인하는 시간(초)")]
    [SerializeField] private float _introToBossDur = 1.2f;
    [Tooltip("플레이어로 빠르게 줌아웃하는 시간(초)")]
    [SerializeField] private float _introToPlayerDur = 0.5f;
    [Tooltip("아레나 전체가 보이게 줌아웃하는 시간(초)")]
    [SerializeField] private float _introToArenaDur = 1.5f;
    [Tooltip("보스 사망 후 플레이어로 줌인 복귀하는 시간(초)")]
    [SerializeField] private float _outroToPlayerDur = 1.0f;
    [Tooltip("아레나 원이 화면에 들어올 때의 여유 배율(1.0=딱 맞음, 클수록 여백)")]
    [SerializeField] private float _arenaFitMargin = 1.12f;
    [Tooltip("각 연출 사이에 잠시 멈추는 시간(초). 보스 줌인 후 정지")]
    [SerializeField] private float _holdBetweenPhases = 0.7f;
    [Tooltip("진입 연출에서 아레나 전체로 줌아웃·고정 후 플레이어로 줌인하기까지의 유지 시간(초)")]
    [SerializeField] private float _introArenaHold = 1.5f;
    [Tooltip("골렘 사망 시 줌아웃·고정 후 플레이어로 복귀하기까지의 정지 시간(초)")]
    [SerializeField] private float _deathHoldBeforeRestore = 1.5f;

    private GameObject _arenaAnchor;     // 아레나 중앙 고정용 임시 앵커
    private Coroutine _cinematicRoutine;

    void Start()
    {
        if (vcam != null)
        {
            followComponent = vcam.GetComponent<CinemachineFollow>();

            // 시작 시 모든 카메라 세팅을 구조체에 박제
            originalState = new CameraState
            {
                followTarget = vcam.Follow,
                followOffset = followComponent.FollowOffset,
                orthoSize = vcam.Lens.OrthographicSize
            };
        }
    }

    void Update()
    {
        // 테스트용 키 입력
        if (Input.GetKeyDown(KeyCode.Alpha1)) 
            StartCoroutine(FadeOut(Color.black, 1.0f)); // 1초 동안 검은색으로

        if (Input.GetKeyDown(KeyCode.Alpha2)) 
            StartCoroutine(FadeOut(Color.white, 1.0f)); // 1초 동안 흰색으로

        if (Input.GetKeyDown(KeyCode.Alpha3)) 
            StartCoroutine(FadeIn(1.0f));               // 1초 동안 다시 투명하게
        
        if (Input.GetKeyDown(KeyCode.Alpha4))
            StartCoroutine(ShakeMultipleTimes(1,1,3));  
        
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            if (targetTransform != null)
                StartCoroutine(ZoomIn(-1f, 2f));
            else
                Debug.LogWarning("targetTransform이 비어있습니다!");
        }

        // 6: 원래 상태로 줌 아웃 및 복귀 (1.0초 동안)
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            StartCoroutine(ZoomOut(2.0f));
        }
        if (Input.GetKeyDown(KeyCode.Alpha7))
            StartCoroutine(StartSlowMotionWhiteOut(0.1f,3));  
        
        
    }

    // 화면이 점점 색상으로 덮임
    public IEnumerator FadeOut(Color targetColor, float duration)
    {
        if (fadeImage == null) { Debug.LogWarning("[CameraEffectManager] fadeImage가 할당되지 않아 FadeOut을 건너뜁니다."); yield break; }

        float elapsed = 0f;
        Color startColor = fadeImage.color;
        // 색상은 유지하되 알파만 0에서 시작하도록 설정
        targetColor.a = 1f; 

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fadeImage.color = Color.Lerp(new Color(targetColor.r, targetColor.g, targetColor.b, 0), targetColor, elapsed / duration);
            yield return null;
        }
    }

    // 화면이 다시 투명해짐
    public IEnumerator FadeIn(float duration)
    {
        if (fadeImage == null) { Debug.LogWarning("[CameraEffectManager] fadeImage가 할당되지 않아 FadeIn을 건너뜁니다."); yield break; }

        float elapsed = 0f;
        Color startColor = fadeImage.color;
        Color targetColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fadeImage.color = Color.Lerp(startColor, targetColor, elapsed / duration);
            yield return null;
        }
    }
    
    // 강도(force), 흔들림 사이의 간격(interval), 반복 횟수(count)
    public IEnumerator ShakeMultipleTimes(float force, float interval, int count)
    {
        if (impulseSource == null)
        {
            Debug.LogWarning("Impulse Source가 없습니다!");
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            // 시네머신 임펄스 발생
            impulseSource.GenerateImpulseWithForce(force);
        
            Debug.Log($"흔들림 발생: {i + 1} / {count}");

            // 마지막 횟수에는 대기하지 않도록 처리
            if (i < count - 1)
            {
                yield return new WaitForSeconds(interval);
            }
        }
    }
    
    public IEnumerator ZoomIn(float targetZ, float duration)
    {
        if (targetTransform == null || vcam == null || followComponent == null) yield break;

        // 1. 타겟을 바꾸기 전, 현재 카메라가 타겟으로부터 얼마나 떨어져 있는지 '실제 거리'를 계산합니다.
        // 이 계산이 들어가야 XY가 순간이동하지 않고 부드럽게 슬라이드하며 이동합니다.
        Vector3 currentWorldPos = vcam.State.RawPosition;
        Vector3 targetWorldPos = targetTransform.position;
        Vector3 diff = currentWorldPos - targetWorldPos;
        
        // 현재의 오프셋을 이 상대적 거리로 강제 설정하여 시작점을 맞춥니다.
        followComponent.FollowOffset = new Vector3(diff.x, diff.y, followComponent.FollowOffset.z);
        
        // 이제 타겟을 교체해도 카메라가 튀지 않습니다.
        vcam.Follow = targetTransform;

        Vector3 startOffset = followComponent.FollowOffset;
        Vector3 goalOffset = new Vector3(0, 0, targetZ); 

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // SmoothStep 대신 선형 t를 사용하여 정확한 등속도 동기화를 꾀함
            float t = elapsed / duration;

            // XY와 Z를 하나의 Vector3.Lerp로 묶어서 동일한 비율로 이동시킵니다.
            // 이래야 XY가 중앙에 도착하는 타이밍과 Z 확대가 끝나는 타이밍이 100% 일치합니다.
            followComponent.FollowOffset = Vector3.Lerp(startOffset, goalOffset, t);

            // 시네머신 엔진의 개입을 막고 매 프레임 위치 강제 갱신
            vcam.ForceCameraPosition(vcam.State.RawPosition, vcam.State.RawOrientation);
            
            yield return null;
        }

        followComponent.FollowOffset = goalOffset;
    }

    public IEnumerator ZoomOut(float duration)
    {
        if (vcam == null || followComponent == null || originalState.followTarget == null) yield break;

        // 1. 타겟을 플레이어로 바꾸기 직전, 현재 카메라의 월드 좌표를 가져옵니다.
        Vector3 currentWorldPos = vcam.State.RawPosition;
    
        // 2. 바뀔 타겟(플레이어)의 현재 위치를 가져옵니다.
        Vector3 playerWorldPos = originalState.followTarget.position;
    
        // 3. 플레이어 기준에서 현재 카메라가 어디에 있는지 상대적 거리를 계산합니다.
        Vector3 diff = currentWorldPos - playerWorldPos;
    
        // 4. 타겟을 플레이어로 바꾸되, 오프셋을 위에서 계산한 값으로 설정하여 '순간이동'을 막습니다.
        followComponent.FollowOffset = new Vector3(diff.x, diff.y, followComponent.FollowOffset.z);
        vcam.Follow = originalState.followTarget;

        // 보간을 위한 시작점과 끝점 설정
        Vector3 startOffset = followComponent.FollowOffset;
        float startSize = vcam.Lens.OrthographicSize;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 줌인과 동일한 비율(t)을 사용하여 모든 축을 동기화
            float t = elapsed / duration;

            // XY 오프셋 복구 + Z축 거리 복구 (동시 진행)
            followComponent.FollowOffset = Vector3.Lerp(startOffset, originalState.followOffset, t);
        
            // 렌즈 사이즈(줌)도 저장된 원본으로 복구
            var lens = vcam.Lens;
            lens.OrthographicSize = Mathf.Lerp(startSize, originalState.orthoSize, t);
            vcam.Lens = lens;

            // 강제 위치 동기화
            vcam.ForceCameraPosition(vcam.State.RawPosition, vcam.State.RawOrientation);
        
            yield return null;
        }

        // 최종 값 고정
        followComponent.FollowOffset = originalState.followOffset;
        var finalLens = vcam.Lens;
        finalLens.OrthographicSize = originalState.orthoSize;
        vcam.Lens = finalLens;
    }

    // ══════════════════════════════════════════════════════════════════
    //  보스 구역 진입/종료 시네마틱
    //  진입: [시간정지+입력차단] 보스로 줌인 → 플레이어로 빠른 줌아웃 → 아레나 전체 줌아웃 후 고정 [복구]
    //  종료: (벽 소멸 + 1초 후 외부에서 호출) 플레이어로 줌인하며 일반 추적 복귀
    // ══════════════════════════════════════════════════════════════════

    /// <summary>보스 구역 진입 연출 시작. 시간 정지(timeScale=0) + 입력 차단 후 카메라 팬, 아레나 전체가 보이게 고정한다.
    /// onArenaReached: 아레나 전체로 줌아웃을 마친 시점(벽 솟음 등을 시작하기 좋은 타이밍)에 호출된다.</summary>
    public void PlayBossIntro(Transform boss, Vector3 arenaCenter, float arenaRadius, PlayerStateMachine playerFSM,
                              System.Action onArenaReached = null)
    {
        if (vcam == null || followComponent == null)
        {
            Debug.LogWarning("[CameraEffectManager] vcam/followComponent가 없어 보스 연출을 건너뜁니다.");
            // 안전장치: 참조가 없어도 벽 솟음은 진행되도록 콜백만 실행하고 반환
            onArenaReached?.Invoke();
            return;
        }
        if (_cinematicRoutine != null) StopCoroutine(_cinematicRoutine);
        _cinematicRoutine = StartCoroutine(BossIntroRoutine(boss, arenaCenter, arenaRadius, playerFSM, onArenaReached));
    }

    private IEnumerator BossIntroRoutine(Transform boss, Vector3 arenaCenter, float arenaRadius, PlayerStateMachine playerFSM,
                                         System.Action onArenaReached)
    {
        // 입력 차단 + 시간 완전 정지 (카메라는 unscaled로 진행)
        if (playerFSM != null) playerFSM.TransitionTo(playerFSM.Cutscene);
        Time.timeScale = 0f;

        // 아레나 중앙 고정용 앵커
        if (_arenaAnchor == null) _arenaAnchor = new GameObject("BossArenaCamAnchor");
        _arenaAnchor.transform.position = new Vector3(arenaCenter.x, arenaCenter.y, 0f);
        float arenaZ = ComputeArenaFitZ(arenaRadius);

        // 복귀할 플레이어 대상 + 원래(일반 추적) 카메라 값 확보
        Transform playerT = (playerFSM != null) ? playerFSM.transform
                                                : (GameObject.FindWithTag("Player") != null ? GameObject.FindWithTag("Player").transform : null);
        Vector3 restoreOffset = (originalState.followTarget != null) ? originalState.followOffset : new Vector3(0f, 0f, -10f);
        float   restoreOrtho  = (originalState.followTarget != null) ? originalState.orthoSize  : vcam.Lens.OrthographicSize;

        // 1) 보스로 이동하며 줌인
        if (boss != null)
            yield return MoveFollowRoutine(boss, new Vector3(0f, 0f, _bossZoomZ), _introToBossDur, true);

        // 연출 사이 잠깐 멈춤 (timeScale=0이므로 Realtime 대기)
        if (_holdBetweenPhases > 0f) yield return new WaitForSecondsRealtime(_holdBetweenPhases);

        // 2) 아레나 전체로 줌아웃 + 고정 (페이드 아웃)
        yield return MoveFollowRoutine(_arenaAnchor.transform, new Vector3(0f, 0f, arenaZ), _introToArenaDur, true);

        // 아레나가 다 보이는 지금 벽 솟음 시작 (실시간으로 솟아오름)
        onArenaReached?.Invoke();

        // 3) 고정된 채 유지 — 이 동안 바위 벽이 솟아오른다(원 구역 전체를 보여주는 비트)
        if (_introArenaHold > 0f) yield return new WaitForSecondsRealtime(_introArenaHold);

        // 4) 벽 생성 후 플레이어로 줌인 (페이드 인) — 연출의 마지막
        if (playerT != null)
            yield return MoveFollowRoutine(playerT, restoreOffset, _outroToPlayerDur, true, restoreOrtho);

        // 5) 고정 해제 + 시간/입력 복구 → 카메라가 플레이어를 추적한 채 보스전 시작
        if (playerT != null) { vcam.Follow = playerT; vcam.LookAt = playerT; }
        Time.timeScale = 1f;
        if (playerFSM != null) playerFSM.TransitionTo(playerFSM.Idle);
        if (_arenaAnchor != null) { Destroy(_arenaAnchor); _arenaAnchor = null; }
        _cinematicRoutine = null;
    }

    /// <summary>골렘 사망 시 호출. 아레나 전체로 (다시) 줌아웃·고정 → _deathHoldBeforeRestore초 후 플레이어로 줌인하며 추적 복귀.</summary>
    public void PlayBossDeathSequence(Vector3 arenaCenter, float arenaRadius)
    {
        if (vcam == null || followComponent == null)
        {
            Debug.LogWarning("[CameraEffectManager] vcam/followComponent가 없어 사망 연출을 건너뜁니다.");
            return;
        }
        if (_cinematicRoutine != null) StopCoroutine(_cinematicRoutine);
        _cinematicRoutine = StartCoroutine(BossDeathRoutine(arenaCenter, arenaRadius));
    }

    private IEnumerator BossDeathRoutine(Vector3 arenaCenter, float arenaRadius)
    {
        Debug.Log("[CameraEffectManager] 사망 연출 시작 — 아레나 고정 유지");

        // 복귀할 플레이어 대상 확보 — originalState가 비어 있어도 태그로 폴백(절대 고정이 안 풀리는 일이 없게)
        Transform playerT = originalState.followTarget;
        if (playerT == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) playerT = p.transform;
        }
        Vector3 restoreOffset = (originalState.followTarget != null) ? originalState.followOffset : new Vector3(0f, 0f, -10f);
        float   restoreOrtho  = (originalState.followTarget != null) ? originalState.orthoSize  : vcam.Lens.OrthographicSize;

        if (_arenaAnchor == null) _arenaAnchor = new GameObject("BossArenaCamAnchor");
        _arenaAnchor.transform.position = new Vector3(arenaCenter.x, arenaCenter.y, 0f);

        // 1) 아레나 전체로 줌아웃 + 고정 (페이드 아웃) — 플레이어 추적 상태에서 부드럽게 빠짐. HitStop 대비 unscaled
        yield return MoveFollowRoutine(_arenaAnchor.transform, new Vector3(0f, 0f, ComputeArenaFitZ(arenaRadius)), _introToArenaDur, true);

        // 2) 고정된 채로 1.5초 대기 (HitStop 등으로 timeScale이 바뀌어도 멈추지 않도록 실시간 대기)
        yield return new WaitForSecondsRealtime(_deathHoldBeforeRestore);
        Debug.Log("[CameraEffectManager] 1.5초 경과 → 플레이어로 페이드인 + 고정 해제");

        // 플레이어로 줌인(페이드 인) — unscaled로 진행해 timeScale 영향 없음
        if (playerT != null)
            yield return MoveFollowRoutine(playerT, restoreOffset, _outroToPlayerDur, true, restoreOrtho);

        // 고정 명시적 해제 — 이후 Cinemachine이 플레이어를 다시 따라가도록 보장
        if (playerT != null)
        {
            vcam.Follow = playerT;
            vcam.LookAt = playerT;
        }
        if (_arenaAnchor != null) { Destroy(_arenaAnchor); _arenaAnchor = null; }
        _cinematicRoutine = null;
        Debug.Log("[CameraEffectManager] 사망 연출 종료 — 플레이어 추적 복귀 완료");
    }

    /// <summary>Follow 타겟을 교체하고 FollowOffset을 goalOffset으로 보간한다(텔레포트 방지). unscaled=true면 timeScale=0에서도 진행.</summary>
    private IEnumerator MoveFollowRoutine(Transform newTarget, Vector3 goalOffset, float duration, bool unscaled, float? goalOrtho = null)
    {
        if (newTarget == null) yield break;

        // 타겟 교체 전, 현재 카메라가 새 타겟으로부터 떨어진 실제 거리로 오프셋을 맞춰 순간이동을 막는다.
        Vector3 currentWorldPos = vcam.State.RawPosition;
        Vector3 diff = currentWorldPos - newTarget.position;
        followComponent.FollowOffset = new Vector3(diff.x, diff.y, followComponent.FollowOffset.z);
        vcam.Follow = newTarget;

        Vector3 startOffset = followComponent.FollowOffset;
        float startOrtho = vcam.Lens.OrthographicSize;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // 부드러운 가속·감속(이징)으로 줌 인/아웃이 급격하지 않게 한다
            float te = Mathf.SmoothStep(0f, 1f, t);

            followComponent.FollowOffset = Vector3.Lerp(startOffset, goalOffset, te);
            if (goalOrtho.HasValue)
            {
                var lens = vcam.Lens;
                lens.OrthographicSize = Mathf.Lerp(startOrtho, goalOrtho.Value, te);
                vcam.Lens = lens;
            }

            vcam.ForceCameraPosition(vcam.State.RawPosition, vcam.State.RawOrientation);
            yield return null;
        }

        followComponent.FollowOffset = goalOffset;
        if (goalOrtho.HasValue)
        {
            var lens = vcam.Lens;
            lens.OrthographicSize = goalOrtho.Value;
            vcam.Lens = lens;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  튜토리얼용 간단 팬: 타겟으로 이동 → 원래 플레이어로 복귀
    // ══════════════════════════════════════════════════════════════════

    /// <summary>카메라를 target 위치로 부드럽게 이동합니다. Z 거리(줌)는 유지됩니다.</summary>
    public IEnumerator PanToTarget(Transform target, float duration, bool unscaled = true)
    {
        if (vcam == null || followComponent == null || target == null) yield break;
        yield return MoveFollowRoutine(target, new Vector3(0f, 0f, followComponent.FollowOffset.z), duration, unscaled);
    }

    /// <summary>카메라를 원래 플레이어 추적 상태로 복귀시킵니다.</summary>
    public IEnumerator PanRestore(float duration, bool unscaled = true)
    {
        if (vcam == null || followComponent == null || originalState.followTarget == null) yield break;
        yield return MoveFollowRoutine(originalState.followTarget, originalState.followOffset, duration, unscaled);
    }

    /// <summary>반지름 arenaRadius인 원이 화면에 모두 들어오는 카메라 거리(FollowOffset.z, 음수)를 계산한다(퍼스펙티브).</summary>
    private float ComputeArenaFitZ(float arenaRadius)
    {
        float fovV  = vcam.Lens.FieldOfView;                       // 수직 FOV(도)
        float halfV = Mathf.Deg2Rad * fovV * 0.5f;
        float aspect = (Screen.height > 0) ? (float)Screen.width / Screen.height : 16f / 9f;

        float dForHeight = (arenaRadius * _arenaFitMargin) / Mathf.Tan(halfV);
        float halfH = Mathf.Atan(Mathf.Tan(halfV) * aspect);
        float dForWidth  = (arenaRadius * _arenaFitMargin) / Mathf.Tan(halfH);

        float d = Mathf.Max(dForHeight, dForWidth);
        return -d;
    }

    // slowAmount: 얼마나 느리게 할 것인지 (0.1f는 10% 속도)
    // duration: 정상으로 돌아오는데 걸리는 시간 (현실 시간 기준)
    public IEnumerator StartSlowMotionWhiteOut(float slowAmount, float duration)
    {
        // 1. [즉시 실행] 시간 속도를 늦추고 화면을 흰색으로 덮음
        Time.timeScale = slowAmount;
    
        // 물리 연산의 간격을 timeScale에 맞춰 조절해야 움직임이 끊기지 않고 부드러움
        Time.fixedDeltaTime = 0.02f * Time.timeScale; 

        // 화면 즉시 화이트 아웃 (알파값 1)
        if (fadeImage != null) fadeImage.color = new Color(1, 1, 1, 1);

        float elapsed = 0f;
        float startScale = slowAmount;
        float targetScale = 1.0f;

        // 2. [서서히 복구] 지정된 시간 동안 원래대로 되돌림
        while (elapsed < duration)
        {
            // 슬로우 모션 중에도 일정한 속도로 계산되도록 unscaledDeltaTime 사용
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 화이트 아웃 서서히 해제 (1 -> 0)
            if (fadeImage != null) fadeImage.color = new Color(1, 1, 1, 1f - t);

            // 게임 속도 서서히 복구 (slowAmount -> 1.0)
            Time.timeScale = Mathf.Lerp(startScale, targetScale, t);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            yield return null;
        }

        // 3. 최종 상태 확정
        if (fadeImage != null) fadeImage.color = new Color(1, 1, 1, 0f);
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
    }
}