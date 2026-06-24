using UnityEngine;

/// <summary>
/// 플레이어 주변의 IInteractable 오브젝트를 감지하고,
/// "Press F" 프롬프트를 표시하며 F키 입력 시 상호작용을 실행합니다.
/// OverlapCircleNonAlloc으로 GC 할당 없이 탐색합니다.
/// </summary>
public class PlayerInteractionDetector : MonoBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("상호작용 감지 반경")]
    [SerializeField] private float _detectionRadius = 2f;

    [Tooltip("감지 대상 레이어 마스크 (Building 레이어 등)")]
    [SerializeField] private LayerMask _interactableMask;

    [SerializeField] private KeyCode _interactKey = KeyCode.F;

    // NonAlloc 결과 배열 (GC 방지 — 프로젝트 규칙 준수)
    private readonly Collider2D[] _hitBuffer = new Collider2D[8];

    // 현재 가장 가까운 상호작용 대상 (캐싱)
    private IInteractable _currentTarget;
    private Transform _currentTargetTransform;

    private void Update()
    {
        // UI 패널이 열려있으면 상호작용 감지 중단
        if (InventoryToggle.Instance != null && InventoryToggle.Instance.IsAnyPanelOpen())
        {
            ClearTarget();
            return;
        }

        DetectNearestInteractable();

        // F키 입력 처리
        if (_currentTarget != null && Input.GetKeyDown(_interactKey))
        {
            if (_currentTarget.CanInteract(gameObject))
            {
                _currentTarget.Interact(gameObject);
            }
        }
    }

    /// <summary>
    /// OverlapCircleNonAlloc으로 주변의 IInteractable을 탐색하여
    /// 가장 가까운 대상을 선택합니다. (매 프레임 GC 0)
    /// </summary>
    private void DetectNearestInteractable()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position, _detectionRadius, _hitBuffer, _interactableMask);

        IInteractable nearest = null;
        Transform nearestTransform = null;
        float nearestSqrDist = float.MaxValue;
        Vector3 myPos = transform.position;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = _hitBuffer[i];
            if (col == null) continue;

            // GetComponent 호출은 감지된 콜라이더 수만큼만 발생 (최대 8)
            IInteractable interactable = col.GetComponent<IInteractable>();
            if (interactable == null || !interactable.CanInteract(gameObject)) continue;

            // sqrMagnitude로 거리 비교 (프로젝트 규칙: Distance 대신 sqrMagnitude)
            float sqrDist = (col.transform.position - myPos).sqrMagnitude;
            if (sqrDist < nearestSqrDist)
            {
                nearestSqrDist = sqrDist;
                nearest = interactable;
                nearestTransform = col.transform;
            }
        }

        // 대상이 바뀌었을 때만 UI 갱신 (불필요한 호출 방지)
        if (nearest != _currentTarget)
        {
            if (_currentTarget != null)
            {
                // 이전 타겟 정리 — 먼저 Hide 호출
                if (InteractionPromptUI.Instance != null)
                    InteractionPromptUI.Instance.Hide();
            }

            _currentTarget = nearest;
            _currentTargetTransform = nearestTransform;

            if (_currentTarget != null && InteractionPromptUI.Instance != null)
            {
                InteractionPromptUI.Instance.Show(
                    _currentTargetTransform, _currentTarget.InteractionPrompt);
            }
        }
        // 아무 대상도 감지되지 않았으면 확실하게 정리
        else if (nearest == null && _currentTarget == null)
        {
            if (InteractionPromptUI.Instance != null)
                InteractionPromptUI.Instance.Hide();
        }
    }

    private void ClearTarget()
    {
        _currentTarget = null;
        _currentTargetTransform = null;

        if (InteractionPromptUI.Instance != null)
        {
            InteractionPromptUI.Instance.Hide();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
    }
}
