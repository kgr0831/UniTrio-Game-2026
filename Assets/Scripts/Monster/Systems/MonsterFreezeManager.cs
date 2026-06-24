using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 "전용" 시간 정지 전역 플래그.
/// Time.timeScale=0은 플레이어까지 멈추므로, 회피 저스트 카운터(F) 동안
/// 플레이어는 일반 시간(timeScale=1)으로 움직이되 몬스터/몬스터 투사체만 멈추기 위해 사용한다.
///
/// 동작:
/// - <see cref="IsFrozen"/>를 각 몬스터 시스템(MonsterBase·SkeletonMonster·NecromancerMonster의 Update,
///   MonsterNavigator·MonsterAttackHandler·HitStaggerHandler, 몬스터 투사체)이 보고 per-frame 로직을 early-return 한다.
/// - 애니메이션은 timeScale=1에서는 계속 재생되므로, Freeze/Unfreeze 진입·해제 시점(edge)에만
///   각 몬스터 Animator.speed를 스냅샷 저장 후 0으로, 해제 시 원복한다.
///   (매 프레임 강제하지 않으므로 HitStaggerHandler 등의 speed 조작과 충돌하지 않는다.)
/// </summary>
public static class MonsterFreezeManager
{
    /// <summary>true이면 모든 몬스터 행동/이동/애니/투사체가 정지한다.</summary>
    public static bool IsFrozen { get; private set; }

    // Freeze 진입 시점의 스냅샷 (해제 시 원복용)
    private static readonly Dictionary<Animator, float> _savedSpeeds = new Dictionary<Animator, float>();
    private static readonly Dictionary<Rigidbody2D, Vector2> _savedVels = new Dictionary<Rigidbody2D, Vector2>();

    /// <summary>모든 몬스터(+보스)를 정지시킨다. (회피 저스트 카운터 진입 시 호출)</summary>
    public static void Freeze()
    {
        if (IsFrozen) return;
        IsFrozen = true;

        _savedSpeeds.Clear();
        _savedVels.Clear();

        // 일반 몬스터(Bear/Skeleton/Necromancer 등 MonsterBase 파생)
        var monsters = Object.FindObjectsByType<MonsterBase>(FindObjectsSortMode.None);
        foreach (var m in monsters)
            FreezeEntity(m != null ? m.gameObject : null);

        // 보스(골렘 등 BossAI 구동) — MonsterBase가 아니므로 별도로 포함.
        // 골렘은 Navigator가 없어 rb 속도도 함께 멈춘다(돌진 중 미끄러짐 방지).
        var bosses = Object.FindObjectsByType<BossAI>(FindObjectsSortMode.None);
        foreach (var b in bosses)
            FreezeEntity(b != null ? b.gameObject : null);
    }

    private static void FreezeEntity(GameObject go)
    {
        if (go == null) return;

        var anim = go.GetComponent<Animator>();
        if (anim != null && !_savedSpeeds.ContainsKey(anim))
        {
            _savedSpeeds[anim] = anim.speed;
            anim.speed = 0f;
        }

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null && !_savedVels.ContainsKey(rb))
        {
            _savedVels[rb] = rb.linearVelocity;
            rb.linearVelocity = Vector2.zero;
        }
    }

    /// <summary>정지를 해제하고 애니메이터 속도·속도를 원복한다. (카운터 종료 시 호출)</summary>
    public static void Unfreeze()
    {
        if (!IsFrozen) return;
        IsFrozen = false;

        foreach (var kv in _savedSpeeds)
            if (kv.Key != null) kv.Key.speed = kv.Value;

        foreach (var kv in _savedVels)
            if (kv.Key != null) kv.Key.linearVelocity = kv.Value;

        _savedSpeeds.Clear();
        _savedVels.Clear();
    }

    // Enter Play Mode Options(도메인 리로드 비활성) 환경에서도 항상 깨끗한 상태로 시작
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        IsFrozen = false;
        _savedSpeeds.Clear();
        _savedVels.Clear();
    }
}
