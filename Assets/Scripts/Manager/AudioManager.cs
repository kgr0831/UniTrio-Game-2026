using UnityEngine;

/// <summary>
/// 게임 전역 효과음(SFX) 재생을 전담하는 싱글톤 매니저.
///
/// 특징:
/// - [RuntimeInitializeOnLoadMethod]로 첫 씬 로드 시 자동 생성되므로 씬에 수동 배치할 필요가 없습니다.
/// - 클립은 Resources/Sound 아래에서 로드합니다.
/// - PlayOneShot 기반이라 동일/다른 효과음이 겹쳐 재생될 수 있습니다.
///
/// 호출 예:
///   AudioManager.Instance.PlaySwordAttack(comboStep);
///   AudioManager.Instance.PlayHit();
///   AudioManager.Instance.PlayDash();
///   AudioManager.Instance.PlayFootstep();
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Volume (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float _attackVolume   = 1f;
    [Range(0f, 1f)] [SerializeField] private float _hitVolume      = 1f;
    [Range(0f, 1f)] [SerializeField] private float _dashVolume     = 1f;
    [Range(0f, 1f)] [SerializeField] private float _footstepVolume = 0.6f;

    private AudioSource _source;

    // 공격 (콤보 1·2타 / 3타)
    private AudioClip _swordAttack1;
    private AudioClip _swordAttack2;

    // 피격
    private AudioClip _hit1;

    // 대시
    private AudioClip _dash;

    // 걷기 (Walk 1~5 순환)
    private AudioClip[] _footsteps;
    private int _footstepIndex;

    /// <summary>첫 씬 진입 시 매니저를 자동 생성합니다. (씬 수동 배치 불필요)</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("[AudioManager]");
        go.AddComponent<AudioManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 0f; // 2D 사운드

        LoadClips();
    }

    private void LoadClips()
    {
        _swordAttack1 = Resources.Load<AudioClip>("Sound/Attack/SwordAttack_1");
        _swordAttack2 = Resources.Load<AudioClip>("Sound/Attack/SwordAttack_2");
        _hit1         = Resources.Load<AudioClip>("Sound/Hit/Hit_1");
        _dash         = Resources.Load<AudioClip>("Sound/Dash/Dalsh_1");

        _footsteps = new AudioClip[]
        {
            Resources.Load<AudioClip>("Sound/Walk/Walk 1"),
            Resources.Load<AudioClip>("Sound/Walk/Walk 2"),
            Resources.Load<AudioClip>("Sound/Walk/Walk 3"),
            Resources.Load<AudioClip>("Sound/Walk/Walk 4"),
            Resources.Load<AudioClip>("Sound/Walk/Walk 5"),
        };
    }

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>검 공격음. 콤보 1·2타 → SwordAttack_1, 3타 이상 → SwordAttack_2.</summary>
    public void PlaySwordAttack(int comboStep)
    {
        AudioClip clip = comboStep >= 3 ? _swordAttack2 : _swordAttack1;
        Play(clip, _attackVolume);
    }

    /// <summary>적 피격음(Hit_1).</summary>
    public void PlayHit()
    {
        Play(_hit1, _hitVolume);
    }

    /// <summary>대시음(Dalsh_1).</summary>
    public void PlayDash()
    {
        Play(_dash, _dashVolume);
    }

    /// <summary>발걸음음. 호출할 때마다 Walk 1→2→3→4→5→1… 순으로 순환 재생합니다.</summary>
    public void PlayFootstep()
    {
        if (_footsteps == null || _footsteps.Length == 0) return;

        AudioClip clip = _footsteps[_footstepIndex];
        _footstepIndex = (_footstepIndex + 1) % _footsteps.Length;
        Play(clip, _footstepVolume);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || _source == null) return;
        _source.PlayOneShot(clip, volume);
    }
}
