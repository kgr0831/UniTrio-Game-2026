using UnityEngine;
using System.Collections;

/// <summary>
/// 차징 VFX(에너지 수렴 셰이더)를 관리하는 컴포넌트.
/// Player에 부착되어 ChargeSystem에서 호출됩니다.
/// Start 시 자식으로 Quad를 생성하고, ChargeGather 셰이더를 적용합니다.
/// </summary>
public class ChargeVFXController : MonoBehaviour
{
    [Header("VFX Settings")]
    [Tooltip("VFX Quad의 월드 스케일 (에너지 수렴 범위)")]
    [SerializeField] private float _vfxScale = 6.5f;
    [Tooltip("소팅 레이어")]
    [SerializeField] private string _sortingLayer = "Default";
    [Tooltip("소팅 오더 (플레이어 위에 렌더링)")]
    [SerializeField] private int _sortingOrder = 10;

    // 런타임 생성 오브젝트
    private GameObject _quadObj;
    private MeshRenderer _meshRenderer;
    private Material _matInstance;

    // Shader property ID 캐시 (GC 방지)
    private static readonly int _ID_ChargeColor    = Shader.PropertyToID("_ChargeColor");
    private static readonly int _ID_ChargeProgress = Shader.PropertyToID("_ChargeProgress");
    private static readonly int _ID_Burst          = Shader.PropertyToID("_Burst");

    private bool _isActive;
    private Coroutine _burstCoroutine;

    private void Start()
    {
        CreateQuad();
        _quadObj.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_matInstance != null)
            Destroy(_matInstance);
        if (_quadObj != null)
            Destroy(_quadObj);
    }

    /// <summary>
    /// VFX용 Quad를 동적 생성합니다. Player의 자식으로 배치됩니다.
    /// </summary>
    private void CreateQuad()
    {
        _quadObj = new GameObject("ChargeGatherVFX");
        _quadObj.transform.SetParent(transform, false);
        _quadObj.transform.localPosition = Vector3.zero;
        _quadObj.transform.localRotation = Quaternion.identity;
        _quadObj.transform.localScale = new Vector3(_vfxScale, _vfxScale, 1f);

        // MeshFilter + Quad 메쉬
        MeshFilter mf = _quadObj.AddComponent<MeshFilter>();
        mf.mesh = CreateQuadMesh();

        // MeshRenderer + 머티리얼
        _meshRenderer = _quadObj.AddComponent<MeshRenderer>();
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;

        // ChargeGather 셰이더 찾기
        Shader shader = Shader.Find("Custom/ChargeGather");
        if (shader == null)
        {
            Debug.LogWarning("[ChargeVFXController] Custom/ChargeGather 셰이더를 찾을 수 없습니다.");
            return;
        }

        _matInstance = new Material(shader);
        _meshRenderer.material = _matInstance;

        // 소팅 설정
        _meshRenderer.sortingLayerName = _sortingLayer;
        _meshRenderer.sortingOrder = _sortingOrder;
    }

    /// <summary>단순 Quad 메쉬를 절차적으로 생성합니다.</summary>
    private Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "ChargeQuad";

        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f)
        };

        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.normals = new Vector3[]
        {
            -Vector3.forward,
            -Vector3.forward,
            -Vector3.forward,
            -Vector3.forward
        };

        return mesh;
    }

    // ── Public API ──────────────────────────────────────────────

    /// <summary>
    /// 차징 VFX를 시작합니다.
    /// </summary>
    /// <param name="elementColor">현재 속성의 대표 색상</param>
    public void StartCharge(Color elementColor)
    {
        if (_matInstance == null) return;
        if (_burstCoroutine != null)
        {
            StopCoroutine(_burstCoroutine);
            _burstCoroutine = null;
        }

        _matInstance.SetColor(_ID_ChargeColor, elementColor);
        _matInstance.SetFloat(_ID_ChargeProgress, 0f);
        _matInstance.SetFloat(_ID_Burst, 0f);

        _quadObj.SetActive(true);
        _isActive = true;
    }

    /// <summary>
    /// 차징 진행도를 업데이트합니다.
    /// </summary>
    /// <param name="progress">0 ~ 1 사이 값</param>
    public void UpdateProgress(float progress)
    {
        if (!_isActive || _matInstance == null) return;
        _matInstance.SetFloat(_ID_ChargeProgress, Mathf.Clamp01(progress));
    }

    /// <summary>
    /// 차징 VFX 색상을 실시간 업데이트합니다.
    /// </summary>
    public void UpdateColor(Color elementColor)
    {
        if (!_isActive || _matInstance == null) return;
        _matInstance.SetColor(_ID_ChargeColor, elementColor);
    }

    /// <summary>
    /// 차징 VFX를 폭발시키며 중단합니다.
    /// </summary>
    public void StopCharge(bool success)
    {
        _isActive = false;
        
        if (success && gameObject.activeInHierarchy)
        {
            if (_burstCoroutine != null) StopCoroutine(_burstCoroutine);
            _burstCoroutine = StartCoroutine(BurstRoutine());
        }
        else
        {
            if (_quadObj != null) _quadObj.SetActive(false);
        }
    }

    private IEnumerator BurstRoutine()
    {
        float duration = 0.3f; // 조금 더 길게 터지도록 시간 연장
        float elapsed = 0f;
        
        Vector3 initialScale = new Vector3(_vfxScale, _vfxScale, 1f);
        Vector3 targetScale = initialScale * 1.5f; // 터질 때 반경을 더 크게 팽창

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Ease out cubic (처음에 빠르게, 나중에 천천히)
            float easeT = 1f - Mathf.Pow(1f - t, 3f);
            
            // _Burst 파라미터를 0에서 1로 
            if (_matInstance != null)
                _matInstance.SetFloat(_ID_Burst, easeT);
                
            // 쿼드 스케일 동적 팽창
            if (_quadObj != null)
                _quadObj.transform.localScale = Vector3.Lerp(initialScale, targetScale, easeT);
                
            yield return null;
        }

        if (_quadObj != null)
        {
            _quadObj.SetActive(false);
            _quadObj.transform.localScale = initialScale; // 원상 복구
        }
    }
}
