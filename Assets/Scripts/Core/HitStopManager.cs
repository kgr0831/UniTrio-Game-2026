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
    /// 시간을 일정 기간 동안 정지(또는 감속)시킵니다. (기본 0.1초, 0.01배속)
    /// </summary>
    /// <param name="duration">정지 지속 시간 (비 스케일 시간 기준)</param>
    /// <param name="scale">정지 동안 적용할 timeScale (1보다 작을수록 강한 정지감. 활 슬로우 등은 0.2 사용)</param>
    public void TriggerHitStop(float duration = 0.1f, float scale = 0.01f)
    {
        if (_isStopped) return;
        // 이미 일시정지/컷씬 등으로 시간이 멈춰 있으면(HitStop이 아닌 다른 시스템 소유) 끼어들지 않는다.
        // → 인벤토리/보스 컷씬과 timeScale을 두고 싸워 0.2배속·정지 상태가 잔류하는 문제 방지.
        if (Time.timeScale <= 0.0001f) return;
        StartCoroutine(HitStopRoutine(duration, scale));
    }

    private IEnumerator HitStopRoutine(float duration, float scale)
    {
        _isStopped = true;

        // 타격감을 위해 잠시 감속
        Time.timeScale = scale;

        // unscaledDeltaTime 기준으로 지정된 초만큼 대기
        yield return new WaitForSecondsRealtime(duration);

        // 현재값(다른 HitStop이 끼어든 값)이 아니라 항상 기준값 1로 복원해야
        // 감속 상태가 영구히 잔류하지 않는다.
        Time.timeScale = 1f;
        _isStopped = false;
    }
}
