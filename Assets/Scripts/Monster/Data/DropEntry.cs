using UnityEngine;

/// <summary>
/// 개별 드롭 아이템 엔트리.
/// 수량별 확률을 배열로 지정할 수 있어, "1개 80%, 2개 15%, 3개 5%" 같은 세밀한 설정이 가능.
/// </summary>
[System.Serializable]
public struct DropEntry
{
    [Tooltip("드롭할 아이템 데이터 (기존 ItemData 참조)")]
    public ItemData Item;

    [Tooltip("이 아이템이 드롭될 기본 확률 (0~1). 예: 0.8 = 80%")]
    [Range(0f, 1f)]
    public float DropChance;

    [Tooltip("최대 드롭 개수")]
    public int MaxCount;

    [Tooltip("개수별 가중치 배열. 인덱스 0 = 1개의 가중치, 인덱스 1 = 2개의 가중치, ...")]
    public float[] CountWeights;
}
