using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 대화 UI 바인더.
/// - 좌클릭·F·엔터·스페이스로 진행
/// - 대화 중 상호작용 입력 차단 (PlayerInteractionDetector 비활성화)
/// - 선택지 버튼 동적 생성
/// - 비주얼 노벨식 초상화 연출 (활성 화자 강조, 비활성 어둡게+축소)
/// </summary>
public class DialogueUIBinder : MonoBehaviour
{
    [Header("Dialogue Player")]
    [SerializeField] private DialoguePlayer _dialoguePlayer;

    [Header("Dialogue Database (선택지 다음 대화 조회용)")]
    [SerializeField] private DialogueDatabase _dialogueDb;

    [Header("Panel")]
    [SerializeField] private GameObject _dialoguePanel;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI _characterNameText;
    [SerializeField] private TextMeshProUGUI _dialogueText;

    [Header("비주얼 노벨 초상화 (선택)")]
    [Tooltip("왼쪽 초상화 (Participants[0])")]
    [SerializeField] private Image _portraitLeft;
    [Tooltip("오른쪽 초상화 (Participants[1]). 없으면 연출 비활성화")]
    [SerializeField] private Image _portraitRight;

    [Header("선택지")]
    [Tooltip("선택지 버튼 프리팹 (없으면 코드로 생성)")]
    [SerializeField] private Button _choiceButtonPrefab;
    [Tooltip("선택지 버튼들이 붙을 컨테이너")]
    [SerializeField] private Transform _choiceContainer;

    [Header("Player Input Control")]
    [Tooltip("대화 중 비활성화할 플레이어 상호작용 감지기")]
    [SerializeField] private PlayerInteractionDetector _interactionDetector;

    // 비주얼 노벨 초상화 설정
    private static readonly Color _activeSpeakerColor   = Color.white;
    private static readonly Color _inactiveSpeakerColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    private static readonly Vector3 _activeScale   = Vector3.one;
    private static readonly Vector3 _inactiveScale = new Vector3(0.9f, 0.9f, 1f);

    private IDialoguePlayer _player;
    private CharacterSO _participant0; // Participants[0] → 왼쪽
    private CharacterSO _participant1; // Participants[1] → 오른쪽

    private void Awake()
    {
        if (_dialoguePlayer != null)
            _player = _dialoguePlayer;
        else
            Debug.LogError("[DialogueUIBinder] DialoguePlayer 참조 미설정.");

        if (_dialoguePanel != null) _dialoguePanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (_player == null) return;
        _player.OnDialogueStarted  += HandleDialogueStarted;
        _player.OnDialogueEnded    += HandleDialogueEnded;
        _player.OnLineChanged      += HandleLineChanged;
        _player.OnTextUpdated      += HandleTextUpdated;
        _player.OnChoicesRequired  += HandleChoicesRequired;
    }

    private void OnDisable()
    {
        if (_player == null) return;
        _player.OnDialogueStarted  -= HandleDialogueStarted;
        _player.OnDialogueEnded    -= HandleDialogueEnded;
        _player.OnLineChanged      -= HandleLineChanged;
        _player.OnTextUpdated      -= HandleTextUpdated;
        _player.OnChoicesRequired  -= HandleChoicesRequired;
    }

    private void Update()
    {
        if (_player == null || !_player.IsPlaying || _player.IsWaitingForChoice) return;

        if (Input.GetMouseButtonDown(0)
            || Input.GetKeyDown(KeyCode.F)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.Space))
        {
            _player.Advance();
        }
    }

    // ── 이벤트 핸들러 ──────────────────────────────────────────────

    private void HandleDialogueStarted()
    {
        if (_dialoguePanel != null) _dialoguePanel.SetActive(true);
        if (_interactionDetector != null) _interactionDetector.enabled = false;

        // 이번 대화의 참여자 초상화 배치 준비
        var dialogue = _dialoguePlayer != null ? _dialoguePlayer.CurrentDialogue : null;
        _participant0 = dialogue != null && dialogue.Participants?.Length > 0 ? dialogue.Participants[0] : null;
        _participant1 = dialogue != null && dialogue.Participants?.Length > 1 ? dialogue.Participants[1] : null;

        // 초기 상태: 양쪽 다 비활성 색상
        SetPortraitState(_portraitLeft,  null, false);
        SetPortraitState(_portraitRight, null, false);
    }

    private void HandleDialogueEnded()
    {
        if (_dialoguePanel != null) _dialoguePanel.SetActive(false);
        if (_interactionDetector != null) _interactionDetector.enabled = true;

        ClearChoiceButtons();

        if (_dialogueText != null) _dialogueText.text = string.Empty;
        if (_characterNameText != null) _characterNameText.text = string.Empty;

        // 초상화 원상복구
        SetPortraitState(_portraitLeft,  null, false);
        SetPortraitState(_portraitRight, null, false);
    }

    private void HandleLineChanged(CharacterSO speaker, string fullText)
    {
        ClearChoiceButtons();

        if (_characterNameText != null)
            _characterNameText.text = speaker != null ? speaker.CharacterName : string.Empty;

        if (_dialogueText != null) _dialogueText.text = string.Empty;

        // 비주얼 노벨 초상화 연출
        if (_portraitLeft != null && _portraitRight != null)
        {
            bool speakerIsLeft = (speaker == _participant0);
            bool speakerIsRight = (speaker == _participant1);

            SetPortraitState(_portraitLeft,  _participant0, speakerIsLeft);
            SetPortraitState(_portraitRight, _participant1, speakerIsRight);
        }
        else if (_portraitLeft != null)
        {
            // 단일 초상화: 현재 화자 이미지만 표시
            if (speaker != null && speaker.Portrait != null)
            {
                _portraitLeft.sprite  = speaker.Portrait;
                _portraitLeft.enabled = true;
                _portraitLeft.color   = _activeSpeakerColor;
            }
            else
            {
                _portraitLeft.enabled = false;
            }
        }
    }

    private void HandleTextUpdated(string currentText)
    {
        if (_dialogueText != null) _dialogueText.text = currentText;
    }

    private void HandleChoicesRequired(DialogueChoice[] choices)
    {
        if (choices == null || choices.Length == 0) return;
        BuildChoiceButtons(choices);
    }

    // ── 선택지 버튼 ───────────────────────────────────────────────

    private void BuildChoiceButtons(DialogueChoice[] choices)
    {
        if (_choiceContainer == null)
        {
            Debug.LogWarning("[DialogueUIBinder] _choiceContainer 미설정 — 선택지 표시 불가.");
            return;
        }

        ClearChoiceButtons();

        for (int i = 0; i < choices.Length; i++)
        {
            int capturedIdx = i;
            DialogueChoice choice = choices[i];

            Button btn = _choiceButtonPrefab != null
                ? Instantiate(_choiceButtonPrefab, _choiceContainer)
                : CreateFallbackButton(choice.Text, _choiceContainer);

            // 텍스트 설정
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = choice.Text;
            else
            {
                var legacyLabel = btn.GetComponentInChildren<Text>();
                if (legacyLabel != null) legacyLabel.text = choice.Text;
            }

            btn.onClick.AddListener(() => OnChoiceClicked(choice));
        }
    }

    private void OnChoiceClicked(DialogueChoice choice)
    {
        ClearChoiceButtons();

        DialogueSO next = null;
        if (!string.IsNullOrEmpty(choice.NextDialogueId) && _dialogueDb != null)
            next = _dialogueDb.Get(choice.NextDialogueId);

        _player?.SelectChoice(next);
    }

    private void ClearChoiceButtons()
    {
        if (_choiceContainer == null) return;
        foreach (Transform child in _choiceContainer)
            Destroy(child.gameObject);
    }

    // ── 초상화 연출 헬퍼 ─────────────────────────────────────────

    private static void SetPortraitState(Image portrait, CharacterSO character, bool active)
    {
        if (portrait == null) return;

        if (character != null && character.Portrait != null)
        {
            portrait.sprite  = character.Portrait;
            portrait.enabled = true;
        }
        else
        {
            portrait.enabled = false;
            return;
        }

        portrait.color = active ? _activeSpeakerColor : _inactiveSpeakerColor;

        var rt = portrait.GetComponent<RectTransform>();
        if (rt != null) rt.localScale = active ? _activeScale : _inactiveScale;
    }

    // ── 선택지 버튼 코드 생성 (프리팹 없을 때 폴백) ──────────────

    private static Button CreateFallbackButton(string label, Transform parent)
    {
        var go = new GameObject("ChoiceBtn_" + label);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300f, 40f);

        go.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        var btn = go.AddComponent<Button>();

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var textRt = textGO.AddComponent<RectTransform>();
        textRt.sizeDelta = new Vector2(290f, 40f);
        textRt.anchoredPosition = Vector2.zero;

        var text = textGO.AddComponent<Text>();
        text.text      = label;
        text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize  = 18;
        text.alignment = TextAnchor.MiddleCenter;
        text.color     = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;

        return btn;
    }
}
