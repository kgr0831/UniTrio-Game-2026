using UnityEngine;

/// <summary>
/// 몹 사망 시 아이템 드롭 로직 (SRP).
/// HealthSystem의 OnDie 이벤트를 구독하여 발동.
/// 풀링 시스템(SimpleObjectPool)을 사용하여 가비지 최소화.
/// </summary>
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(MonsterRuntimeData))]
public sealed class MonsterLootDropper : MonoBehaviour
{
    private HealthSystem       _healthSystem;
    private MonsterRuntimeData _runtime;

    private void Awake()
    {
        _healthSystem = GetComponent<HealthSystem>();
        _runtime      = GetComponent<MonsterRuntimeData>();
    }

    private void OnEnable()
    {
        if (_healthSystem != null)
            _healthSystem.OnDied += HandleDrop;
    }

    private void OnDisable()
    {
        if (_healthSystem != null)
            _healthSystem.OnDied -= HandleDrop;
    }

    private void HandleDrop()
    {
        if (_runtime == null || _runtime.Data == null || _runtime.Data.DropTable == null) return;

        Debug.Log($"[MonsterLootDropper] {_runtime.Data.MonsterName} 사망, 드롭테이블 순회 시작! (엔트리 수: {_runtime.Data.DropTable.Length})");

        foreach (var entry in _runtime.Data.DropTable)
        {
            if (entry.Item == null) continue;

            float rand = Random.value;
            if (rand <= entry.DropChance)
            {
                int dropCount = CalculateDropCount(entry);
                if (dropCount > 0)
                {
                    Debug.Log($"[MonsterLootDropper] 드롭 확정: {entry.Item.Name} x{dropCount} (확률 굴림: {rand:F2} <= {entry.DropChance})");
                    SpawnDrop(entry.Item, dropCount);
                }
            }
            else
            {
                Debug.Log($"[MonsterLootDropper] 드롭 실패: {entry.Item.Name} (확률 굴림: {rand:F2} > {entry.DropChance})");
            }
        }
    }

    private int CalculateDropCount(DropEntry entry)
    {
        if (entry.CountWeights == null || entry.CountWeights.Length == 0) return 1;

        float totalWeight = 0f;
        for (int i = 0; i < entry.CountWeights.Length; i++)
        {
            totalWeight += entry.CountWeights[i];
        }

        // Random.Range 에서 float 인자를 사용하여 소수점 난수를 가져옵니다.
        float randomVal = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        for (int i = 0; i < entry.CountWeights.Length; i++)
        {
            currentWeight += entry.CountWeights[i];
            if (randomVal <= currentWeight)
            {
                // 인덱스 0 = 1개, 1 = 2개...
                return Mathf.Min(i + 1, entry.MaxCount);
            }
        }
        return 1;
    }

    private void SpawnDrop(ItemData itemData, int count)
    {
        if (itemData == null || itemData.DropPrefab == null)
        {
            Debug.LogWarning($"[MonsterLootDropper] 획득 불가: {itemData?.name} 아이템의 DropPrefab이 설정되지 않았습니다.");
            return;
        }

        // 약간 무작위로 위치를 확실히 흩뿌려 여러 아이템이 완전히 시각적으로 구분되게 설계 (1.0f로 넓힘)
        Vector2 randomOffset = Random.insideUnitCircle * 1.0f;
        Vector3 spawnPos = transform.position + (Vector3)randomOffset;

        GameObject dropObj = SimpleObjectPool.Instance.Get(itemData.DropPrefab, spawnPos, Quaternion.identity);

        // 시각적 설정: 스프라이트 교체
        var sr = dropObj.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && itemData.Icon != null)
        {
            sr.sprite = itemData.Icon;
        }

        // 아이템 식별자 부착 및 데이터 주입
        var identity = dropObj.GetComponent<DroppedItemIdentity>();
        if (identity == null)
            identity = dropObj.AddComponent<DroppedItemIdentity>();

        identity.Setup(itemData, count);

        // 흩뿌려진 아이템이 물리적으로 위로 통통 튀며 떨어지도록 설정
        var magneticItem = dropObj.GetComponent<FloatingMagneticItem>();
        if (magneticItem != null)
        {
            float randomUpVelocity = Random.Range(3f, 5f);
            magneticItem.InitDrop(new Vector2(randomOffset.x * 2f, randomUpVelocity));
        }
    }
}
