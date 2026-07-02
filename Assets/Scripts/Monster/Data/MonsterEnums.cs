using UnityEngine;

/// <summary>몹 종류 구분</summary>
public enum MonsterType
{
    Neutral  = 0,  // 중립
    Hostile  = 1,  // 적대적
    Boss     = 2   // 보스
}

/// <summary>몬스터 효과음(SFX) 종류. AudioManager의 종별 클립 배열 인덱스로 사용.</summary>
public enum MonsterSfxKind
{
    None        = 0,
    Skeleton    = 1,
    Bear        = 2,
    Necromancer = 3
}

/// <summary>공격 범위 형태 구분</summary>
public enum AttackShapeType
{
    Rectangle = 0,  // 사각형 (Width x Height)
    Fan       = 1,  // 부채꼴 (Angle + Radius)
    Circle    = 2   // 원형 (Radius)
}

/// <summary>몹의 행동 상태 enum (BT 노드 판별용)</summary>
public enum MonsterState
{
    Idle,       // 대기
    Eat,        // 먹기
    Wander,     // 자유 배회
    Chase,      // 추격 (적대적)
    Flee,       // 도망 (중립)
    Attack,     // 공격 중
    Stagger,    // 피격 경직
    Death       // 사망
}
