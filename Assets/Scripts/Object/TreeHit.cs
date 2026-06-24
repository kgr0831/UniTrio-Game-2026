using UnityEngine;

[RequireComponent(typeof(HealthSystem))]
public class TreeHit : MonoBehaviour, IDamageable
{
    private HealthSystem _healthSystem;

    // 나무는 영원히 살아있는 상태로 취급 (무한 파밍)
    public bool IsAlive => true; 

    private void Awake()
    {
        _healthSystem = GetComponent<HealthSystem>();
    }

    public void TakeDamage(float damage, GameObject source = null)
    {
        if (_healthSystem != null)
        {
            // HealthSystem에 0의 데미지를 가해서 실제 체력을 깎지 않고 피격 연출(플래시, OnHit 이벤트)만 유발함.
            _healthSystem.ApplyDamage(0f);
        }
    }
}
