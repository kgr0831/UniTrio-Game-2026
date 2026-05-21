using System;
using UnityEngine;

/// <summary>
/// 글로벌 타격 이벤트를 관리하는 정적 매니저입니다.
/// 무기, 투사체 등에서 적중 시 이벤트를 발생시키고, 
/// 게이지 시스템 등이 이를 구독하여 처리합니다. (SOLID - 단일 책임, 의존성 역전 원칙 준수)
/// </summary>
public static class HitEventManager
{
    /// <summary>
    /// 적이 타격받았을 때 발생하는 이벤트.
    /// param 1 (Vector3): 타격의 출처(플레이어 또는 무기 발사 위치)
    /// param 2 (Vector3): 적중한 대상의 위치
    /// param 3 (bool): 이번 스윙(또는 다중 타격 중) 첫 번째 적중인지 여부
    /// </summary>
    public static event Action<Vector3, Vector3, bool> OnEnemyHit;

    /// <summary>
    /// 적중 시 호출하여 구독 중인 시스템들에 타격 사실을 알립니다.
    /// </summary>
    /// <param name="sourcePosition">공격의 출처 (예: 플레이어 위치, 발사 위치)</param>
    /// <param name="targetPosition">타격 대상의 위치</param>
    /// <param name="isFirstHit">이번 공격의 첫 적중 여부</param>
    public static void NotifyHit(Vector3 sourcePosition, Vector3 targetPosition, bool isFirstHit)
    {
        OnEnemyHit?.Invoke(sourcePosition, targetPosition, isFirstHit);
    }
}
