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
///   AudioManager.Instance.PlaySpearAttack();
///   AudioManager.Instance.PlayBowCharge();
///   AudioManager.Instance.PlayBowRelease();
///   AudioManager.Instance.PlayHit();
///   AudioManager.Instance.PlayDash();
///   AudioManager.Instance.PlayFootstep();
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Volume (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float _attackVolume       = 1f;
    [Range(0f, 1f)] [SerializeField] private float _spearAttackVolume  = 1f;
    [Range(0f, 1f)] [SerializeField] private float _bowChargeVolume    = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float _bowReleaseVolume   = 1f;
    [Range(0f, 1f)] [SerializeField] private float _hitVolume          = 1f;
    [Range(0f, 1f)] [SerializeField] private float _dashVolume         = 1f;
    [Range(0f, 1f)] [SerializeField] private float _footstepVolume     = 0.6f;

    [Header("Attack Sound Offset")]
    [Tooltip("창 공격 사운드 파일의 앞부분 무음 구간을 건너뛸 시간(초).")]
    [SerializeField] private float _spearAttackStartOffset  = 0.15f;
    [Tooltip("활 발사 사운드 파일의 앞부분 무음 구간을 건너뛸 시간(초).")]
    [SerializeField] private float _bowReleaseStartOffset   = 0.15f;

    private AudioSource _source;
    private AudioSource _spearSource;      // 창 공격 전용 (오프셋 재생용)
    private AudioSource _bowReleaseSource; // 활 발사 전용 (오프셋 재생용)

    // 공격 (콤보 1·2타 / 3타)
    private AudioClip _swordAttack1;
    private AudioClip _swordAttack2;

    // 창 공격
    private AudioClip _spearAttack;

    // 활 (당기기 / 발사)
    private AudioClip _bowCharge;   // Slingshot_Stretch (차징 시)
    private AudioClip _bowRelease;  // Bow_Release (발사 시)

    // 피격
    private AudioClip _hit1;

    // 대시
    private AudioClip _dash;

    // 걷기 (Footstep_Grass_a~f 순환)
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

        // 창 공격 전용 소스 (오프셋 재생 시 이전 사운드를 끊고 새로 재생)
        _spearSource = gameObject.AddComponent<AudioSource>();
        _spearSource.playOnAwake = false;
        _spearSource.spatialBlend = 0f;

        // 활 발사 전용 소스 (오프셋 재생)
        _bowReleaseSource = gameObject.AddComponent<AudioSource>();
        _bowReleaseSource.playOnAwake = false;
        _bowReleaseSource.spatialBlend = 0f;

        LoadClips();
    }

    private void LoadClips()
    {
        _swordAttack1 = Resources.Load<AudioClip>("Sound/Attack/SwordAttack_1");
        _swordAttack2 = Resources.Load<AudioClip>("Sound/Attack/SwordAttack_2");
        _spearAttack  = Resources.Load<AudioClip>("Sound/Spear/Slash_Attack_Light_2");
        _bowCharge    = Resources.Load<AudioClip>("Sound/Bow/Slingshot_Stretch");
        _bowRelease   = Resources.Load<AudioClip>("Sound/Bow/Slash_Attack_Light_3");
        _hit1         = Resources.Load<AudioClip>("Sound/Hit/Hit_1");
        _dash         = Resources.Load<AudioClip>("Sound/Dash/Dalsh_1");

        _footsteps = new AudioClip[]
        {
            Resources.Load<AudioClip>("Sound/Walk2/Footstep_Grass_a"),
            Resources.Load<AudioClip>("Sound/Walk2/Footstep_Grass_b"),
            Resources.Load<AudioClip>("Sound/Walk2/Footstep_Grass_c"),
            Resources.Load<AudioClip>("Sound/Walk2/Footstep_Grass_d"),
            Resources.Load<AudioClip>("Sound/Walk2/Footstep_Grass_e"),
            Resources.Load<AudioClip>("Sound/Walk2/Footstep_Grass_f"),
        };
    }

    // ── Public API ──────────────────────────────────────────────────

    /// <summary>검 공격음. 콤보 1·2타 → SwordAttack_1, 3타 이상 → SwordAttack_2.</summary>
    public void PlaySwordAttack(int comboStep)
    {
        AudioClip clip = comboStep >= 3 ? _swordAttack2 : _swordAttack1;
        Play(clip, _attackVolume);
    }

    /// <summary>창 공격음 (Slash_Attack_Light_2). 오프셋 재생으로 앞부분 무음을 건너뜁니다.</summary>
    public void PlaySpearAttack()
    {
        if (_spearAttack == null || _spearSource == null) return;
        _spearSource.Stop();
        _spearSource.clip   = _spearAttack;
        _spearSource.volume = _spearAttackVolume;
        _spearSource.time   = Mathf.Clamp(_spearAttackStartOffset, 0f, _spearAttack.length - 0.01f);
        _spearSource.Play();
    }

    /// <summary>활 당기기(차징) 시작음 (Slingshot_Stretch).</summary>
    public void PlayBowCharge()
    {
        Play(_bowCharge, _bowChargeVolume);
    }

    /// <summary>활 발사음 (Slash_Attack_Light_3). 오프셋 재생으로 앞부분 무음을 건너뜁니다.</summary>
    public void PlayBowRelease()
    {
        if (_bowRelease == null || _bowReleaseSource == null) return;
        _bowReleaseSource.Stop();
        _bowReleaseSource.clip   = _bowRelease;
        _bowReleaseSource.volume = _bowReleaseVolume;
        _bowReleaseSource.time   = Mathf.Clamp(_bowReleaseStartOffset, 0f, _bowRelease.length - 0.01f);
        _bowReleaseSource.Play();
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

    /// <summary>발걸음음. 호출할 때마다 Footstep_Grass a→b→c→d→e→f→a… 순으로 순환 재생합니다.</summary>
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
