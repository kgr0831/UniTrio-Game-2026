using UnityEngine;
using TMPro; // 🌟 텍스트매쉬 프로(TMP) 전용 네임스페이스 추가!

/// <summary>
/// 적 타격 시 머리 위로 데미지가 떠오르고 사라지는 UI 처리 스크립트.
/// 프리팹 최상단에 부착하여 사용합니다. (TextMeshPro 기반)
/// </summary>
public class DamageText : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float _lifetime = 1f;
    [SerializeField] private float _floatSpeed = 2f;
    [SerializeField] private Vector3 _randomScatter = new Vector3(0.5f, 0.5f, 0f);
    [SerializeField] private float _fontSize = 8f;
    
    // 기존 TextMesh 대신 압도적인 화질의 TMP를 사용합니다!
    private TextMeshPro _textMesh;
    private Color _originalColor;

    public void Setup(int damage)
    {
        if (_textMesh == null) _textMesh = GetComponent<TextMeshPro>();
        
        if (_textMesh != null)
        {
            // TMP는 SortingOrder 설정이 컴포넌트 내부에 직관적으로 들어있습니다.
            _textMesh.sortingLayerID = SortingLayer.NameToID("Default");
            _textMesh.sortingOrder = 9999; // Z-가림 완벽 방지
            
            _textMesh.fontSize = _fontSize;
            _textMesh.text = damage.ToString();
            _originalColor = _textMesh.color;
            _originalColor.a = 1f; // 풀링 재사용 시 알파값 초기화
            _textMesh.color = _originalColor;
        }

        // 생성 시 약간 무작위로 위치 비틀어주기
        transform.position += new Vector3(
            Random.Range(-_randomScatter.x, _randomScatter.x),
            Random.Range(-_randomScatter.y, _randomScatter.y),
            -2f // Z축을 앞으로 당겨 충돌 방지
        );

        // 이전 예약 취소 후 새로 예약
        CancelInvoke(nameof(ReturnToPool));
        Invoke(nameof(ReturnToPool), _lifetime);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(ReturnToPool));
    }

    private void ReturnToPool()
    {
        if (gameObject.activeSelf)
        {
            SimpleObjectPool.Instance.Release(gameObject);
        }
    }

    private void Update()
    {
        transform.position += Vector3.up * (_floatSpeed * Time.deltaTime);

        // 부드러운 투명도 알파 페이드 아웃 연출
        if (_textMesh != null)
        {
            _originalColor.a -= Time.deltaTime / _lifetime;
            _textMesh.color = _originalColor;
        }
    }
}
