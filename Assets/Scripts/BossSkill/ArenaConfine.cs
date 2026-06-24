using UnityEngine;

/// <summary>
/// 대상(플레이어·골렘)을 원형 아레나 안에 가두는 가드.
/// 매 프레임 중심으로부터 반지름을 벗어나면 경계 안으로 위치를 되돌린다.
/// 물리 레이어 충돌 설정에 의존하지 않고 확실히 가둘 수 있다(돌진처럼 transform 이동도 차단).
/// 골렘이 죽으면 GolemZone이 이 컴포넌트를 제거해 다시 나갈 수 있게 한다.
/// </summary>
public class ArenaConfine : MonoBehaviour
{
    private Vector2 _center;
    private float _radius;
    private Rigidbody2D _rb;

    public void Init(Vector2 center, float radius)
    {
        _center = center;
        _radius = Mathf.Max(0.1f, radius);
        _rb = GetComponent<Rigidbody2D>();
    }

    private void LateUpdate()
    {
        Vector2 p = (_rb != null) ? _rb.position : (Vector2)transform.position;
        Vector2 d = p - _center;
        float r2 = _radius * _radius;
        if (d.sqrMagnitude <= r2) return; // 안에 있으면 그대로

        Vector2 clamped = _center + d.normalized * _radius;
        if (_rb != null)
            _rb.position = clamped;            // 물리 이동 차단
        else
            transform.position = new Vector3(clamped.x, clamped.y, transform.position.z);
    }
}
