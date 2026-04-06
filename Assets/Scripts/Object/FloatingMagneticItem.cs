using UnityEngine;

/// <summary>
/// 드랍 후 통통 튀며, 부유상태가 되고, 플레이어가 다가오면 자석처럼 끌려가는 역할을 전담합니다 (SRP).
/// </summary>
public class FloatingMagneticItem : MonoBehaviour
{
    private enum State { Dropping, Floating, Magnetic }
    private State _currentState = State.Dropping;

    [Header("Drop Settings")]
    [Tooltip("떨어질 때 중력값")]
    [SerializeField] private float _gravity = 15f;
    private Vector2 _velocity;
    private float _floorY;

    [Header("Float Settings")]
    [Tooltip("부유 상하 진폭")]
    [SerializeField] private float _floatAmplitude = 0.15f;
    [Tooltip("부유 상하 속도 진동수")]
    [SerializeField] private float _floatFrequency = 4f;
    private float _floatTimer;
    private Vector2 _floatBasePosition;

    [Header("Magnetic Settings")]
    [Tooltip("자석 반응 반경")]
    [SerializeField] private float _magneticRadius = 3f;
    [Tooltip("끌려갈 때 가속도")]
    [SerializeField] private float _magneticAcceleration = 25f;
    [Tooltip("플레이어 획득 판정 거리")]
    [SerializeField] private float _collectDistance = 0.4f;
    
    private float _currentMagneticSpeed;
    private Transform _playerTransform;

    public void InitDrop(Vector2 initialVelocity)
    {
        _velocity = initialVelocity;
        // 현재 위치에서 Y로 조금 내려간 지점을 가상의 '땅'으로 잡아줍니다.
        // 포물선을 그리며 땅에 도달하게 하는 용도입니다.
        _floorY = transform.position.y - Random.Range(0.2f, 0.4f); 
        _currentState = State.Dropping;
    }

    private void Update()
    {
        // 플레이어를 매번 찾지 않고 한 번 캐싱해둠
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }

        switch (_currentState)
        {
            case State.Dropping:
                UpdateDropping();
                break;
            case State.Floating:
                UpdateFloating();
                CheckMagneticDistance();
                break;
            case State.Magnetic:
                UpdateMagnetic();
                break;
        }
    }

    private void UpdateDropping()
    {
        // 속도에 중력을 적용하여 떨어지는 모션 구현
        _velocity.y -= _gravity * Time.deltaTime;
        transform.position += (Vector3)_velocity * Time.deltaTime;

        // 바닥에 닿았다면 부유 상태(Floating)로 전환
        if (transform.position.y <= _floorY && _velocity.y < 0)
        {
            Vector3 pos = transform.position;
            pos.y = _floorY;
            transform.position = pos;

            _floatBasePosition = transform.position; // 이 위치를 기준으로 둥둥거림
            _currentState = State.Floating;
        }
    }

    private void UpdateFloating()
    {
        // 사인파를 이용해 위아래로 부드럽게 움직임
        _floatTimer += Time.deltaTime * _floatFrequency;
        float yOffset = Mathf.Sin(_floatTimer) * _floatAmplitude;
        
        transform.position = _floatBasePosition + new Vector2(0f, yOffset);
    }

    private void CheckMagneticDistance()
    {
        if (_playerTransform == null) return;

        // 플레이어 거리 계산
        float dist = Vector2.Distance(transform.position, _playerTransform.position);
        if (dist <= _magneticRadius)
        {
            _currentState = State.Magnetic;
            _currentMagneticSpeed = 0f;
        }
    }

    private void UpdateMagnetic()
    {
        if (_playerTransform == null) return;

        _currentMagneticSpeed += _magneticAcceleration * Time.deltaTime;
        
        // 플레이어 중앙 즈음(예: 발밑보다 조금 위)을 타겟으로 함
        Vector3 targetPos = _playerTransform.position + Vector3.up * 0.5f;
        Vector3 dir = (targetPos - transform.position).normalized;
        
        transform.position += dir * _currentMagneticSpeed * Time.deltaTime;

        // 도착 판정
        float dist = Vector2.Distance(transform.position, targetPos);
        if (dist <= _collectDistance)
        {
            CollectItem();
        }
    }

    private void CollectItem()
    {
        // TODO: 향후 플레이어의 인벤토리에 나무를 더하는 로직 등을 이곳에 연결할 수 있습니다.
        Destroy(gameObject);
    }
}
