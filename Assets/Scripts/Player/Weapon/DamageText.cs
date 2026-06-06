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

    [Header("Scale by Damage")]
    [Tooltip("이 데미지에서 최소 배율(_minScale)이 적용됩니다.")]
    [SerializeField] private float _minDamage = 1f;
    [Tooltip("이 데미지 이상에서 최대 배율(_maxScale)이 적용됩니다.")]
    [SerializeField] private float _maxDamage = 200f;
    [SerializeField] private float _minScale = 0.7f;
    [SerializeField] private float _maxScale = 1.7f;

    [Header("Outline")]
    [SerializeField] private Color _outlineColor = Color.black;
    [Range(0f, 1f)]
    [SerializeField] private float _outlineWidth = 0.12f;

    // 프리팹 TMP에 제작된 원본 폰트 크기(첫 Setup에서 1회 캡처). 배율은 이 값을 기준으로 적용.
    private float _baseFontSize = -1f;

    /// <summary>속성별 데미지 텍스트 전용 밝은 색상 (Earth=0, Fire=1, Ice=2 순서). 공용 aura 색과 분리되어 무기 이펙트에 영향을 주지 않습니다.</summary>
    private static readonly Color[] _elementTextColors = new Color[]
    {
        new Color(1.0f, 0.88f, 0.35f, 1f),  // Earth: 선명한 황금빛
        new Color(1.0f, 0.55f, 0.15f, 1f),  // Fire:  밝은 주황
        new Color(0.45f, 0.92f, 1.0f, 1f)   // Ice:   밝은 시안
    };

    // 기존 TextMesh 대신 압도적인 화질의 TMP를 사용합니다!
    private TextMeshPro _textMesh;
    private Color _originalColor;

    /// <summary>색을 지정하지 않으면 현재 무기 속성 색으로 표시한다(고정 데미지 등은 색을 직접 넘긴다).</summary>
    public void Setup(int damage)
    {
        Color c = Color.white;
        if (ElementalWeaponSystem.Instance != null)
        {
            int idx = (int)ElementalWeaponSystem.Instance.CurrentElement;
            c = (idx >= 0 && idx < _elementTextColors.Length)
                ? _elementTextColors[idx]
                : ElementalWeaponSystem.Instance.GetCurrentAuraColor();
        }
        Setup(damage, c);
    }

    public void Setup(int damage, Color color)
    {
        if (_textMesh == null) _textMesh = GetComponent<TextMeshPro>();

        if (_textMesh != null)
        {
            // 프리팹에 제작된 원본 폰트 크기를 1회 캡처 → 이 값을 기준으로 배율 적용(하드코딩 크기로 덮어쓰지 않음)
            if (_baseFontSize < 0f) _baseFontSize = _textMesh.fontSize;

            // TMP는 SortingOrder 설정이 컴포넌트 내부에 직관적으로 들어있습니다.
            _textMesh.sortingLayerID = SortingLayer.NameToID("Default");
            _textMesh.sortingOrder = 9999; // Z-가림 완벽 방지
            _textMesh.enableWordWrapping = false; // 자동 줄바꿈(Wrap) 비활성화

            // 데미지에 비례한 크기 배율 (_minDamage→_minScale ~ _maxDamage→_maxScale, 범위 밖은 고정)
            float t = Mathf.InverseLerp(_minDamage, _maxDamage, damage);
            float scale = Mathf.Lerp(_minScale, _maxScale, t);
            _textMesh.fontSize = _baseFontSize * scale;

            // 가독성을 위한 검은 외곽선
            _textMesh.outlineColor = _outlineColor;
            _textMesh.outlineWidth = _outlineWidth;

            _textMesh.text = damage.ToString();
            color.a = 1f; // 풀링 재사용 시 알파값 초기화
            _originalColor = color;
            _textMesh.color = color;
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
