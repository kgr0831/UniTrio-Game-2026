using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 대화 UI 요소를 IDialoguePlayer 이벤트에 바인딩하는 컴포넌트 (SRP: UI 표시만 담당).
/// DialoguePlayer의 이벤트를 구독하여 패널 활성화, 텍스트 갱신, 초상화 표시를 수행합니다.
/// DIP 준수: IDialoguePlayer 인터페이스에만 의존합니다.
/// </summary>
public class DialogueUIBinder : MonoBehaviour
{
    [Header("Dialogue Player Reference")]
    [Tooltip("IDialoguePlayer를 구현한 DialoguePlayer 컴포넌트")]
    [SerializeField] private DialoguePlayer _dialoguePlayer;

    [Header("Panel")]
    [Tooltip("대화창 전체 패널 (활성화/비활성화 대상)")]
    [SerializeField] private GameObject _dialoguePanel;

    [Header("UI Elements")]
    [Tooltip("캐릭터 이름을 표시할 TextMeshProUGUI")]
    [SerializeField] private TextMeshProUGUI _characterNameText;

    [Tooltip("대화 텍스트를 표시할 TextMeshProUGUI")]
    [SerializeField] private TextMeshProUGUI _dialogueText;

    [Tooltip("캐릭터 일러스트를 표시할 Image")]
    [SerializeField] private Image _portraitImage;

    [Header("Player Input Control (Optional)")]
    [Tooltip("대화 중 비활성화할 플레이어 입력 오브젝트 (선택 사항)")]
    [SerializeField] private GameObject _playerInputObject;

    /// <summary>
    /// IDialoguePlayer 인터페이스 참조 (DIP 준수).
    /// </summary>
    private IDialoguePlayer _player;

    private void Awake()
    {
        // 인터페이스로 캐스팅하여 의존
        if (_dialoguePlayer != null)
        {
            _player = _dialoguePlayer;
        }
        else
        {
            Debug.LogError("[DialogueUIBinder] DialoguePlayer 참조가 설정되지 않았습니다.");
        }

        // 시작 시 대화창 비활성화
        if (_dialoguePanel != null)
        {
            _dialoguePanel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (_player == null) return;

        _player.OnDialogueStarted += HandleDialogueStarted;
        _player.OnDialogueEnded += HandleDialogueEnded;
        _player.OnLineChanged += HandleLineChanged;
        _player.OnTextUpdated += HandleTextUpdated;
    }

    private void OnDisable()
    {
        if (_player == null) return;

        _player.OnDialogueStarted -= HandleDialogueStarted;
        _player.OnDialogueEnded -= HandleDialogueEnded;
        _player.OnLineChanged -= HandleLineChanged;
        _player.OnTextUpdated -= HandleTextUpdated;
    }

    private void Update()
    {
        // 대화 진행 중일 때 마우스 클릭으로 Advance 호출
        if (_player != null && _player.IsPlaying && Input.GetMouseButtonDown(0))
        {
            _player.Advance();
        }
    }

    // ── 이벤트 핸들러 ──

    /// <summary>
    /// 대화 시작 시: 패널 활성화, 플레이어 입력 비활성화
    /// </summary>
    private void HandleDialogueStarted()
    {
        if (_dialoguePanel != null)
        {
            _dialoguePanel.SetActive(true);
        }

        // 플레이어 입력 비활성화 (선택 사항)
        if (_playerInputObject != null)
        {
            _playerInputObject.SetActive(false);
        }
    }

    /// <summary>
    /// 대화 종료 시: 패널 비활성화, 플레이어 입력 활성화
    /// </summary>
    private void HandleDialogueEnded()
    {
        if (_dialoguePanel != null)
        {
            _dialoguePanel.SetActive(false);
        }

        // 플레이어 입력 활성화
        if (_playerInputObject != null)
        {
            _playerInputObject.SetActive(true);
        }

        // 텍스트 초기화
        if (_dialogueText != null)
        {
            _dialogueText.text = string.Empty;
        }
        if (_characterNameText != null)
        {
            _characterNameText.text = string.Empty;
        }
    }

    /// <summary>
    /// 줄 변경 시: 화자 이름과 초상화를 갱신합니다.
    /// </summary>
    /// <param name="speaker">현재 화자 데이터</param>
    /// <param name="fullText">전체 텍스트 (참고용, 실제 표시는 OnTextUpdated에서 처리)</param>
    private void HandleLineChanged(CharacterSO speaker, string fullText)
    {
        // 캐릭터 이름 갱신
        if (_characterNameText != null)
        {
            _characterNameText.text = speaker != null ? speaker.CharacterName : string.Empty;
        }

        // 초상화 이미지 갱신
        if (_portraitImage != null)
        {
            if (speaker != null && speaker.Portrait != null)
            {
                _portraitImage.sprite = speaker.Portrait;
                _portraitImage.enabled = true;
            }
            else
            {
                _portraitImage.enabled = false;
            }
        }

        // 텍스트 초기화 (타자기 효과 시작 전)
        if (_dialogueText != null)
        {
            _dialogueText.text = string.Empty;
        }
    }

    /// <summary>
    /// 타자기 효과로 텍스트가 갱신될 때: 대화 텍스트를 업데이트합니다.
    /// </summary>
    /// <param name="currentText">현재까지 출력된 부분 텍스트</param>
    private void HandleTextUpdated(string currentText)
    {
        if (_dialogueText != null)
        {
            _dialogueText.text = currentText;
        }
    }
}
