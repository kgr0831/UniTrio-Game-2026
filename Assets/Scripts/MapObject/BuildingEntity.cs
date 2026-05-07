using UnityEngine;

/// <summary>
/// 건축물 프리팹에 부착되어 건축물의 내구도와 피격 로직을 담당합니다.
/// 플레이어와 몹이 통과하지 못하도록 반드시 BoxCollider2D가 포함되어야 합니다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class BuildingEntity : MonoBehaviour, IDamageable
{
    [Tooltip("건축물 데이터 (최대 체력 등 참고용)")]
    [SerializeField] private BuildingData _buildingData;
    
    private int _currentHealth;

    public bool IsAlive => _currentHealth > 0;

    private void Awake()
    {
        // Awake 시점에 SO 데이터로부터 체력 로드
        if (_buildingData != null)
        {
            _currentHealth = _buildingData.MaxHealth;
        }
        else
        {
            _currentHealth = 100; // Fallback
        }
    }

    /// <summary>
    /// IDamageable 인터페이스 구현 (결합도 분리)
    /// </summary>
    public void TakeDamage(float damage, GameObject source = null)
    {
        int amount = Mathf.RoundToInt(damage);
        _currentHealth -= amount;
        Debug.Log($"[BuildingEntity] 건축물 피격! 데미지: {amount}, 남은 체력: {_currentHealth}");

        if (_currentHealth <= 0)
        {
            _currentHealth = 0;
            DestroyBuilding();
        }
    }

    /// <summary>
    /// 체력이 0 이하가 되면 호출되어 건물을 파괴합니다.
    /// </summary>
    private void DestroyBuilding()
    {
        // TODO: 오브젝트 풀링 패턴(Object Pool)을 위한 이펙트 재생 등의 로직 추가 권장
        Debug.Log("[BuildingEntity] 건축물이 파괴되었습니다.");
        Destroy(gameObject);
    }
}
