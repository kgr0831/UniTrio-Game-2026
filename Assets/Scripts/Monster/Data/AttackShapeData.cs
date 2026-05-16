using UnityEngine;

/// <summary>
/// 공격 범위 정의. 크기 1 = Unity 단위 1.
/// </summary>
[System.Serializable]
public struct AttackShapeData
{
    [Tooltip("범위 형태")]
    public AttackShapeType ShapeType;

    [Header("Rectangle (사각형)")]
    [Tooltip("사각형 가로 크기 (Unity 단위). 예: 3 = 3칸")]
    public float RectWidth;
    [Tooltip("사각형 세로 크기 (Unity 단위). 예: 1 = 1칸")]
    public float RectHeight;

    [Header("Fan (부채꼴)")]
    [Tooltip("부채꼴 중심각 (도 단위). 예: 90 = 90°")]
    public float FanAngle;
    [Tooltip("부채꼴 반지름 (Unity 단위)")]
    public float FanRadius;

    [Header("Circle (원형)")]
    [Tooltip("원형 반지름 (Unity 단위)")]
    public float CircleRadius;
}
