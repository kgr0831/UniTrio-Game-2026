using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 거리를 일정하게 띄워(예: 10씩) 몹을 한 번에 한 마리씩 순차적으로(MaxCount까지) 생성 및 유지 관리합니다.
/// 풀링을 활용하며, 한 마리가 죽으면 빈 자리를 파악하고 딜레이 후 해당 자리에 재생성하는 구조(SRP).
/// </summary>
public sealed class MobSpawner : MonoBehaviour
{
    [Tooltip("스폰할 몹의 정적 데이터")]
    [SerializeField] private MonsterData _targetData;

    [Tooltip("유지할 몹의 최대 마리 수")]
    [SerializeField] private int _maxSpawnCount = 20;

    [Tooltip("각 몹 사이의 간격 (예: 10이면 0, 10, 20 위치에 생성)")]
    [SerializeField] private float _gridSpacing = 10f;

    [Tooltip("순차적 스폰 간격 (초)")]
    [SerializeField] private float _initialSpawnInterval = 0.5f;

    [Tooltip("몹이 죽은 후 새롭게 스폰되기까지의 딜레이 (초)")]
    [SerializeField] private float _respawnDelay = 2f;

    [Tooltip("사망 후 풀로 반환되기까지의 딜레이 (초). 사망 애니메이션/페이드아웃이 끝날 때까지 살려두려면 늘린다.")]
    [SerializeField] private float _deathReturnDelay = 0.2f;

    [Tooltip("에디터 내 시작 시 자동 스폰 여부")]
    [SerializeField] private bool _spawnOnStart = true;

    private int _currentCount = 0;
    
    // 비어있는 그리드 슬롯 인덱스를 관리 (죽으면 반환, 스폰시 가져감)
    private Queue<int> _availableIndices = new Queue<int>();

    private void Start()
    {
        // 0부터 MaxCount-1까지 모든 인덱스를 사용 가능 상태로 초기화
        for (int i = 0; i < _maxSpawnCount; i++)
        {
            _availableIndices.Enqueue(i);
        }

        if (_spawnOnStart)
        {
            StartCoroutine(InitialSpawnRoutine());
        }
    }

    private IEnumerator InitialSpawnRoutine()
    {
        for (int i = 0; i < _maxSpawnCount; i++)
        {
            SpawnOne();
            // 한 번에 모두 겹치지 않도록 한 마리씩 순차 스폰
            yield return new WaitForSeconds(_initialSpawnInterval);
        }
    }

    private Vector3 GetGridPosition(int index)
    {
        // 정사각형에 가까운 격자 형태로 배치하기 위해 행/열 계산
        int columns = Mathf.CeilToInt(Mathf.Sqrt(_maxSpawnCount));
        int row = index / columns;
        int col = index % columns;

        float x = col * _gridSpacing;
        float y = row * _gridSpacing;

        // 스포너의 위치를 기준으로 정렬
        return transform.position + new Vector3(x, y, 0f);
    }

    private void SpawnOne()
    {
        if (_targetData == null || _targetData.MonsterPrefab == null) return;
        if (_currentCount >= _maxSpawnCount) return;
        if (_availableIndices.Count == 0) return;

        // 빈 자리 인덱스를 하나 뽑아 해당 위치에 소환
        int slotIndex = _availableIndices.Dequeue();
        Vector3 spawnPos = GetGridPosition(slotIndex);

        // 골렘 구역(원) 내부에는 몹을 생성하지 않는다. 자리 반환 후 잠시 뒤 재시도(구역이 사라지면 다시 채워짐).
        if (GolemZone.IsInsideAnyArena(spawnPos))
        {
            _availableIndices.Enqueue(slotIndex);
            StartCoroutine(RetrySpawnLater());
            return;
        }

        // SimpleObjectPool을 통한 스폰
        GameObject mobObj = SimpleObjectPool.Instance.Get(_targetData.MonsterPrefab, spawnPos, Quaternion.identity);
        mobObj.transform.SetParent(this.transform);

        // 런타임 데이터 초기화 주입
        var runtime = mobObj.GetComponent<MonsterRuntimeData>();
        if (runtime != null)
        {
            runtime.Initialize(_targetData);
        }

        // 매니저에 등록
        var mobBase = mobObj.GetComponent<MonsterBase>();
        if (mobBase != null && MobManager.Instance != null)
        {
            MobManager.Instance.RegisterMob(mobBase);
        }

        _currentCount++;

        // 초기 체력 및 부활 (매우 중요: 풀에서 꺼낸 몹은 이전의 죽은 상태이므로 반드시 부활시켜야 함!)
        var health = mobObj.GetComponent<HealthSystem>();
        if (health != null)
        {
            health.SetMaxHp(_targetData.MaxHP, true);
            health.Resurrect(); // 0이었던 HP를 꽉 채우고 IsAlive를 true로 만듦

            Action onDiedHandler = null;
            onDiedHandler = () => 
            {
                health.OnDied -= onDiedHandler;
                _currentCount--;
                
                if (MobManager.Instance != null)
                    MobManager.Instance.UnregisterMob(mobBase);

                // 사망 처리: 드롭 등 다른 OnDied 이벤트 콜백 처리를 위해 딜레이를 줘서 풀 반환
                StartCoroutine(ReturnAndRespawnRoutine(mobObj, slotIndex));
            };
            
            health.OnDied += onDiedHandler;
        }
    }

    // 골렘 구역 안이라 스폰을 미룬 슬롯을 일정 시간 뒤 다시 시도
    private IEnumerator RetrySpawnLater()
    {
        yield return new WaitForSeconds(_respawnDelay);
        SpawnOne();
    }

    private IEnumerator ReturnAndRespawnRoutine(GameObject mobObj, int slotIdx)
    {
        // 빈자리를 슬롯 큐에 반환
        _availableIndices.Enqueue(slotIdx);

        // 사망 애니메이션/페이드아웃 + 아이템 드롭 등 여타 로직이 완료되도록 대기
        yield return new WaitForSeconds(_deathReturnDelay);
        SimpleObjectPool.Instance.Release(mobObj);

        // 재스폰 대기
        yield return new WaitForSeconds(_respawnDelay);

        // 한 마리씩 빈 칸이 생겼으므로 다시 채우기
        SpawnOne();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        int columns = Mathf.CeilToInt(Mathf.Sqrt(_maxSpawnCount));
        
        for (int i = 0; i < _maxSpawnCount; i++)
        {
            int row = i / columns;
            int col = i % columns;
            float x = col * _gridSpacing;
            float y = row * _gridSpacing;
            
            Vector3 pos = transform.position + new Vector3(x, y, 0f);
            Gizmos.DrawWireCube(pos, Vector3.one * 1f); // 스폰 예정지점들을 작은 박스로 표시
        }
    }
}
