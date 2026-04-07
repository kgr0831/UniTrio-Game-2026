using UnityEngine;

/// <summary>
/// 부활 모션 트레일 — 오브젝트 풀링 기반 혈적색 애프터이미지.
///
/// DeathCutsceneController.OnCutsceneEnd 에서 Activate()를 호출하면
/// _activeDuration 초 동안 플레이어 이동 잔상을 생성한 뒤 자동 종료됩니다.
///
/// 퍼포먼스 주의사항:
///  - 풀 GO는 Awake에서 한 번만 생성 (Instantiate 런타임 호출 없음)
///  - 매 프레임 GC 없음: Color 값 캐싱, for 루프 사용
///  - SpriteRenderer.color는 MaterialPropertyBlock보다 단순하지만
///    잔상 수가 적고 SRP Batch 대상이 아니라 허용 가능
/// </summary>
[DisallowMultipleComponent]
public class ResurrectionTrailPool : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("잔상을 복사할 플레이어 SpriteRenderer.")]
    [SerializeField] private SpriteRenderer _playerSprite;

    [Header("Pool Settings")]
    [Tooltip("사전 할당할 잔상 GO 수. emitInterval / ghostLifetime 으로 계산: 20이면 충분.")]
    [SerializeField] private int   _poolSize     = 20;

    [Tooltip("잔상 생성 간격(초). 작을수록 촘촘한 트레일.")]
    [SerializeField] private float _emitInterval = 0.06f;

    [Tooltip("잔상 한 개의 수명(초). 이 시간 동안 알파가 1→0으로 감소.")]
    [SerializeField] private float _ghostLifetime = 0.38f;

    [Tooltip("트레일 전체 활성 시간(초). 이후 자동 비활성화.")]
    [SerializeField] private float _activeDuration = 2.0f;

    [Header("Visual")]
    [Tooltip("혈적색 잔상 색 (알파는 최대 알파로 사용됨).")]
    [SerializeField] private Color _ghostColor = new Color(0.55f, 0f, 0f, 0.7f);

    // ── 내부 상태 ─────────────────────────────────────────────────────────

    private SpriteRenderer[] _pool;
    private float[]          _poolTimers;
    private int              _nextIdx     = 0;
    private bool             _isActive    = false;
    private float            _emitTimer   = 0f;
    private float            _activeTimer = 0f;

    // ── 생명주기 ──────────────────────────────────────────────────────────

    private void Awake()
    {
        _pool       = new SpriteRenderer[_poolSize];
        _poolTimers = new float[_poolSize];

        for (int i = 0; i < _poolSize; i++)
        {
            var go = new GameObject($"ResurrectTrailGhost_{i}");
            // 씬 루트에 배치 → 플레이어 이동/스케일 영향 없음
            go.transform.SetParent(null, false);

            var sr = go.AddComponent<SpriteRenderer>();
            // 플레이어 레이어와 동일, 플레이어 아래로 정렬
            sr.sortingLayerID = _playerSprite != null ? _playerSprite.sortingLayerID : 0;
            sr.sortingOrder   = _playerSprite != null ? _playerSprite.sortingOrder - 1 : 0;

            go.SetActive(false);
            _pool[i]       = sr;
            _poolTimers[i] = 0f;
        }
    }

    private void Update()
    {
        if (!_isActive) return;

        float dt = Time.deltaTime; // timeScale=1 이후 호출됨

        // ── 잔상 생성 ───────────────────────────────────────────────────
        _emitTimer += dt;
        if (_emitTimer >= _emitInterval)
        {
            _emitTimer -= _emitInterval;
            EmitGhost();
        }

        // ── 활성 시간 카운트 ────────────────────────────────────────────
        _activeTimer += dt;
        if (_activeTimer >= _activeDuration)
        {
            Deactivate();
            return;
        }

        // ── 풀 수명/알파 업데이트 ───────────────────────────────────────
        for (int i = 0; i < _poolSize; i++)
        {
            var sr = _pool[i];
            if (!sr.gameObject.activeSelf) continue;

            _poolTimers[i] -= dt;
            if (_poolTimers[i] <= 0f)
            {
                sr.gameObject.SetActive(false);
                continue;
            }

            // 선형 알파 페이드아웃
            float alpha = Mathf.Clamp01(_poolTimers[i] / _ghostLifetime) * _ghostColor.a;
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>트레일을 활성화합니다. timeScale=1 복원 후 호출하세요.</summary>
    public void Activate()
    {
        _isActive    = true;
        _activeTimer = 0f;
        _emitTimer   = 0f;
    }

    /// <summary>트레일을 즉시 비활성화하고 모든 잔상을 숨깁니다.</summary>
    public void Deactivate()
    {
        _isActive = false;
        for (int i = 0; i < _poolSize; i++)
        {
            if (_pool[i].gameObject.activeSelf)
                _pool[i].gameObject.SetActive(false);
        }
    }

    // ── 잔상 방출 ─────────────────────────────────────────────────────────

    private void EmitGhost()
    {
        if (_playerSprite == null) return;

        var sr = _pool[_nextIdx];

        // 현재 스프라이트 복사 (atlas 대응)
        sr.sprite           = _playerSprite.sprite;
        sr.flipX            = _playerSprite.flipX;
        sr.flipY            = _playerSprite.flipY;

        // 월드 Transform 복사
        Transform pt = _playerSprite.transform;
        sr.transform.position   = pt.position;
        sr.transform.rotation   = pt.rotation;
        sr.transform.localScale = pt.lossyScale;   // 월드 스케일 → lossyScale

        // 초기 색상 (최대 알파)
        sr.color = _ghostColor;

        sr.gameObject.SetActive(true);
        _poolTimers[_nextIdx] = _ghostLifetime;

        _nextIdx = (_nextIdx + 1) % _poolSize;
    }
}
