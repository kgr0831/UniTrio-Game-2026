using UnityEngine;

/// <summary>
/// 업그레이드 대시 잔상 VFX 풀. Player GameObject에 부착합니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DashAfterimagePool : MonoBehaviour
{
    [Header("Pool Settings")]
    [SerializeField] private int   _poolSize      = 30;
    [SerializeField] private float _spawnInterval = 0.006f;

    private AfterimageGhost[] _pool;
    private int               _nextIndex;
    private SpriteRenderer    _playerSr;
    private bool              _isSpawning;
    private float             _spawnTimer;

    private void Awake()
    {
        _playerSr = GetComponent<SpriteRenderer>();

        _pool = new AfterimageGhost[_poolSize];
        for (int i = 0; i < _poolSize; i++)
        {
            var go = new GameObject($"[Ghost_{i}]");
            // 씬 루트에 배치 → 플레이어 transform 영향 없이 월드 좌표 독립 유지
            go.transform.SetParent(null);

            var ghostSr = go.AddComponent<SpriteRenderer>();
            // 플레이어와 동일한 Material 사용 → URP 호환 보장
            ghostSr.sharedMaterial = _playerSr.sharedMaterial;

            _pool[i] = go.AddComponent<AfterimageGhost>();
            go.SetActive(false);
        }

    }

    public void StartSpawning()
    {
        _isSpawning = true;
        _spawnTimer = 0f;

        SpawnOne();
    }

    public void StopSpawning()
    {
        _isSpawning = false;
    }

    private void Update()
    {
        if (!_isSpawning) return;

        _spawnTimer += Time.deltaTime;
        if (_spawnTimer >= _spawnInterval)
        {
            _spawnTimer -= _spawnInterval;
            SpawnOne();
        }
    }

    private void SpawnOne()
    {
        AfterimageGhost ghost = _pool[_nextIndex];
        _nextIndex = (_nextIndex + 1) % _poolSize;

        ghost.Setup(
            _playerSr.sprite,
            transform.position,
            transform.lossyScale,
            Color.white,
            _playerSr.sortingLayerID,
            _playerSr.sortingOrder);
    }
}
