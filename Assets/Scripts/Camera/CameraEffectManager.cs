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
    
    // slowAmount: 얼마나 느리게 할 것인지 (0.1f는 10% 속도)
    // duration: 정상으로 돌아오는데 걸리는 시간 (현실 시간 기준)
    public IEnumerator StartSlowMotionWhiteOut(float slowAmount, float duration)
    {
        // 1. [즉시 실행] 시간 속도를 늦추고 화면을 흰색으로 덮음
        Time.timeScale = slowAmount;
    
        // 물리 연산의 간격을 timeScale에 맞춰 조절해야 움직임이 끊기지 않고 부드러움
        Time.fixedDeltaTime = 0.02f * Time.timeScale; 

        // 화면 즉시 화이트 아웃 (알파값 1)
        fadeImage.color = new Color(1, 1, 1, 1);

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
            fadeImage.color = new Color(1, 1, 1, 1f - t);

            // 게임 속도 서서히 복구 (slowAmount -> 1.0)
            Time.timeScale = Mathf.Lerp(startScale, targetScale, t);
            Time.fixedDeltaTime = 0.02f * Time.timeScale;

            yield return null;
        }

        // 3. 최종 상태 확정
        fadeImage.color = new Color(1, 1, 1, 0f);
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
    }
}