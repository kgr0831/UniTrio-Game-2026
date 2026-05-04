using UnityEngine;

/// <summary>
/// 몹의 무작위 배회 시스템 (SRP).
/// 갱신 주기를 제어해 매 프레임 무작위 계산을 방지 (최적화).
/// 사용법: BTNode에서 Execute() 시 호출하여 다음 이동 방향 획득.
/// </summary>
public sealed class WanderSystem : MonoBehaviour
{
    [Tooltip("배회 시 한 방향으로 이동할 최대 유지 시간 (초)")]
    [SerializeField] private float _minWanderTime = 1f;
    [SerializeField] private float _maxWanderTime = 3f;
    
    [Tooltip("스폰 지점 등을 기준으로 돌아다닐 반경")]
    [SerializeField] private float _wanderRadius = 3f;

    private float   _wanderTimer;
    private Vector2 _currentWanderDirection;
    private Vector2 _basePosition;

    private void OnEnable()
    {
        // 처음 활성화될 때 현재 위치를 베이스 위치로 지정
        _basePosition = transform.position;
    }

    /// <summary>현재 배회의 기준이 되는 위치를 새로 설정 (도망 등이 끝났을 때 사용)</summary>
    public void SetBasePosition(Vector2 pos)
    {
        _basePosition = pos;
    }

    /// <summary>현재 유지 중인 배회 방향 반환</summary>
    public Vector2 GetWanderDirection()
    {
        _wanderTimer -= Time.deltaTime;
        if (_wanderTimer <= 0f)
        {
            // 베이스 위치를 중심으로 일정 반경 내의 랜덤한 목표 지점 생성
            Vector2 randomPoint = _basePosition + Random.insideUnitCircle * _wanderRadius;
            
            // 목표 지점을 향하는 방향 계산
            _currentWanderDirection = (randomPoint - (Vector2)transform.position).normalized;
            _wanderTimer = Random.Range(_minWanderTime, _maxWanderTime);
        }

        return _currentWanderDirection;
    }

    /// <summary>현재 배회 상태 강제 만료 (충돌 시 즉시 방향 전환용)</summary>
    public void ForceRecalculate()
    {
        _wanderTimer = 0f;
    }
}
