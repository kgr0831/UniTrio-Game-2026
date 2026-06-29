using UnityEngine;

/// <summary>
/// 조작법(키 바인딩) 안내 창. 표시 후 아무 키/클릭으로 닫습니다.
/// 튜토리얼 마지막(곰 처치 후)에 1회 표시됩니다.
/// </summary>
public class KeybindingPanel : MonoBehaviour
{
    [Tooltip("표시/숨김 대상 루트 패널")]
    [SerializeField] private GameObject _root;

    [Tooltip("표시 직후 입력을 무시하는 유예 시간 (직전 클릭으로 즉시 닫히는 것 방지)")]
    [SerializeField] private float _graceSeconds = 0.4f;

    public bool IsOpen { get; private set; }
    private float _openTime;

    public void Show()
    {
        if (_root != null) _root.SetActive(true);
        IsOpen = true;
        _openTime = Time.unscaledTime;
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (Time.unscaledTime - _openTime < _graceSeconds) return;

        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            IsOpen = false;
            if (_root != null) _root.SetActive(false);
        }
    }
}
