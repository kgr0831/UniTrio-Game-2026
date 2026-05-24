using UnityEngine;

/// <summary>
/// 몹의 정적 데이터 (ScriptableObject).
/// 기존 프로젝트의 ItemData 패턴을 따르며, CreateAssetMenu로 에디터에서 생성.
/// </summary>
[CreateAssetMenu(fileName = "NewMonsterData", menuName = "Data/Monster")]
public class MonsterData : ScriptableObject
{
    [Header("═══ Identity ═══")]
    public int       MonsterId;         // 고유 ID (FirstKillRegistry에서 사용)
    public string    MonsterName;       // 표시명
    public MonsterType Type;            // Neutral / Hostile / Boss

    [Header("═══ Base Stats ═══")]
    public float MaxHP;                 // 최대 체력
    public float ATK;                   // 공격력
    public float DEF;                   // 방어력
    public float Speed;                 // 이동속도 (Unity 단위/초)
    [Tooltip("공격 속도 배율. 1.0 = 1초, 2.0 = 0.5초, 0.5 = 2초. 공식: 쿨다운 = 1 / AttackSpeed")]
    public float AttackSpeed;           // 공격 속도 배율
    [Tooltip("공격 범위 표시 후 실제 타격까지의 지연 시간 (초)")]
    public float AttackDelay;           // 공격 딜레이 (초)

    [Header("═══ Attack Range ═══")]
    public AttackShapeData AttackShape; // 공격 범위 데이터

    [Header("═══ Detection ═══")]
    [Tooltip("플레이어 감지 반경 (Unity 단위). 보스는 무한으로 별도 처리.")]
    public float DetectionRadius;       // 감지 반경

    [Header("═══ Drop Table ═══")]
    public DropEntry[] DropTable;       // 드롭 아이템 테이블

    [Header("═══ Elemental Resistance ═══")]
    [Tooltip("불 속성 저항")]
    public float FireResistance;
    [Tooltip("얼음 속성 저항")]
    public float IceResistance;
    [Tooltip("땅 속성 저항")]
    public float EarthResistance;

    [Header("═══ Visuals ═══")]
    public Sprite     IdleSprite;       // 기본 스프라이트
    public RuntimeAnimatorController AnimController; // Animator Controller

    [Header("═══ Prefab ═══")]
    [Tooltip("풀링에 사용되는 몹 프리팹")]
    public GameObject MonsterPrefab;    // 몹 프리팹
}
