using UnityEngine;

/// <summary>
/// HealthSystem의 OnHit 이벤트를 받아 아이템 프리팹을 생산하고 부채꼴 범위로 튀어오르게 만듦. (SRP)
/// </summary>
[RequireComponent(typeof(HealthSystem))]
public class ItemDropSpawner : MonoBehaviour
{
    [Header("Drop Settings")]
    [Tooltip("드랍할 아이템 프리팹 (Resources/Prefabs/TreeItem 등)")]
    [SerializeField] private GameObject _itemPrefab;
    [Tooltip("한 번에 스폰할 아이템 갯수")]
    [SerializeField] private int _dropCount = 1;
    [Tooltip("튀어오르는 최소 상승 힘")]
    [SerializeField] private float _minJumpForce = 3f;
    [Tooltip("튀어오르는 최대 상승 힘")]
    [SerializeField] private float _maxJumpForce = 5f;
    [Tooltip("스폰 방향 좌우 각도 (위쪽을 기준으로 확산)")]
    [SerializeField] private float _spreadAngle = 30f;

    private HealthSystem _healthSystem;

    private void Awake()
    {
        _healthSystem = GetComponent<HealthSystem>();
        if (_healthSystem != null)
        {
            _healthSystem.OnHit += SpawnItem;
        }
    }

    private void SpawnItem()
    {
        if (_itemPrefab == null)
        {
            Debug.LogWarning("[ItemDropSpawner] 아이템 프리팹이 할당되지 않았습니다.");
            return;
        }

        for (int i = 0; i < _dropCount; i++)
        {
            GameObject currentItem = Instantiate(_itemPrefab, transform.position, Quaternion.identity);
            
            // 프리팹 안에 있는 FloatingMagneticItem 을 가져와서 초기 투척 물리 로직을 위임하거나, 
            // RigidBody2D가 있다면 여기서 초기 속도를 세팅할 수 있습니다.
            FloatingMagneticItem dropBehaviour = currentItem.GetComponent<FloatingMagneticItem>();
            if (dropBehaviour != null)
            {
                // 부채꼴 내의 랜덤 각도
                float randomAngle = Random.Range(-_spreadAngle, _spreadAngle);
                Vector2 dropDirection = Quaternion.Euler(0, 0, randomAngle) * Vector2.up;
                
                float jumpPower = Random.Range(_minJumpForce, _maxJumpForce);
                dropDirection *= jumpPower;

                dropBehaviour.InitDrop(dropDirection);
            }
        }
    }

    private void OnDestroy()
    {
        if (_healthSystem != null)
        {
            _healthSystem.OnHit -= SpawnItem;
        }
    }
}
