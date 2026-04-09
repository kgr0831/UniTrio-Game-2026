using System.Collections;
using UnityEngine;

/// <summary>
/// 타격 시 잠깐의 시간 정지(Hit-Stop) 연출을 처리하는 전역 싱글톤 매니저입니다.
/// </summary>
public class HitStopManager : MonoBehaviour
{
    private static HitStopManager _instance;
    public static HitStopManager Instance 
    { 
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<HitStopManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("HitStopManager");
                    _instance = go.AddComponent<HitStopManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private bool _isStopped = false;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 시간을 일정 기간 동안 정지시킵니다. (기본 0.1초)
    /// </summary>
    /// <param name="duration">정지 지속 시간 (비 스케일 시간 기준)</param>
    public void TriggerHitStop(float duration = 0.1f)
    {
        if (_isStopped) return;
        StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        _isStopped = true;
        
        float originalTimeScale = Time.timeScale;
        // 타격감을 위해 완전히 멈추거나 아주 미세하게만 흐르게 설정
        Time.timeScale = 0.01f; 
        
        // unscaledDeltaTime 기준으로 지정된 초만큼 대기
        yield return new WaitForSecondsRealtime(duration);
        
        Time.timeScale = originalTimeScale;
        _isStopped = false;
    }
}
