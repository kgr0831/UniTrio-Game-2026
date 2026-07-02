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
    [Range(0f, 1f)] [SerializeField] private float _justDodgeVolume    = 1f;

    [Header("Element / Wand Volume (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float _elementSwitchVolume    = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float _elementHitVolume       = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float _wandFireVolume         = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float _wandExplosionVolume    = 1f;
    [Range(0f, 1f)] [SerializeField] private float _wandChargeCastVolume   = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float _wandChargeLoopVolume   = 0.7f;

    [Header("Gather / Craft / Cook Volume (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float _treeChopVolume     = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float _craftSuccessVolume = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float _craftFailVolume    = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float _cookCompleteVolume = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float _cookLoopVolume     = 0.5f;

    [Header("Monster Volume (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float _monsterAttackVolume = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float _monsterHitVolume    = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float _monsterDeathVolume  = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float _monsterAlertVolume  = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float _necroSpellVolume    = 0.9f;

    [Header("UI / Item Volume (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float _uiHoverVolume  = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float _uiClickVolume  = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float _uiDeniedVolume = 0.7f;
    [Range(0f, 1f)] [SerializeField] private float _panelVolume    = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float _itemVolume     = 0.7f;

    [Header("Player / Misc Volume (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float _playerHitVolume   = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float _playerDeathVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float _gaugeFullVolume   = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float _buildVolume       = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float _resurrectVolume   = 1f;

    [Header("Weapon Charge Skill Volume (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float _chargeLoopVolume     = 0.55f;
    [Range(0f, 1f)] [SerializeField] private float _chargeReadyVolume    = 0.8f;
    [Range(0f, 1f)] [SerializeField] private float _chargeUltimateVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float _bowChargeShotVolume  = 0.9f;
    [Range(0f, 1f)] [SerializeField] private float _spearChargeHitVolume = 0.9f;

    [Header("Attack Sound Offset")]
    [Tooltip("창 공격 사운드 파일의 앞부분 무음 구간을 건너뛸 시간(초).")]
    [SerializeField] private float _spearAttackStartOffset  = 0.15f;
    [Tooltip("활 발사 사운드 파일의 앞부분 무음 구간을 건너뛸 시간(초).")]
    [SerializeField] private float _bowReleaseStartOffset   = 0.15f;

    private AudioSource _source;
    private AudioSource _spearSource;      // 창 공격 전용 (오프셋 재생용)
    private AudioSource _bowReleaseSource; // 활 발사 전용 (오프셋 재생용)
    private AudioSource _wandChargeLoopSource; // 완드 차지3 집중 루프 전용
    private AudioSource _cookLoopSource;        // 모닥불 조리 진행 루프 전용
    private AudioSource _chargeLoopSource;      // 무기 차징(우클릭 홀드) 루프 전용

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

    // 회피 저스트 발동(슬로우다운)
    private AudioClip _justDodgeSlowdown;

    // 걷기 (Footstep_Grass_a~f 순환)
    private AudioClip[] _footsteps;
    private int _footstepIndex;

    // ── 원소 / 완드 마법 (Minifantasy Magic & Sorcery) ──────────────
    // 모든 배열 인덱스 = ElementType 값 (Earth=0, Fire=1, Ice=2)
    private AudioClip[] _elemSwitch;      // D-1 속성 전환 (Cast/Surge)
    private AudioClip[] _elemHit;         // D-2·4·6 원소 적중(디버프 부착)
    private AudioClip[] _wandThrow;       // C-4 완드 발사 (Throw)
    private AudioClip[] _wandExplosion;   // C-4·C-5 완드 착탄/폭발 (Explosion)
    private AudioClip[] _wandChargeHold;  // C-5 차지3 집중 루프 (Hold/Levitate)

    // ── 채집 / 제작 / 요리 ──────────────────────────────────────────
    private AudioClip[] _treeChops;       // H-1 나무 베기 타격 (2종 교차)
    private int         _treeChopIndex;
    private AudioClip   _craftSuccess;    // I-7·8 제작 완료 / I-10 요리 완료
    private AudioClip   _craftFail;       // 제작 실패(재료 부족)
    private AudioClip   _cookLoop;        // I-9 모닥불 조리 진행 루프

    // ── 몬스터 (Minifantasy Creatures) ──────────────────────────────
    // 배열 인덱스 = MonsterSfxKind 값 (None=0, Skeleton=1, Bear=2, Necromancer=3)
    private AudioClip[] _monAttack;   // F-7·13 공격
    private AudioClip[] _monHit;      // F-2 피격
    private AudioClip[] _monDeath;    // F-3 사망
    private AudioClip[] _monAlert;    // F-1 발견/경계

    // ── UI / 아이템 (RPG_Essentials) ────────────────────────────────
    private AudioClip _uiHover;       // J-1 호버
    private AudioClip _uiConfirm;     // J-2 클릭/확인, J-8 대화 넘김
    private AudioClip _uiCancel;      // J-3 취소/뒤로
    private AudioClip _uiDenied;      // J-4 거부/불가
    private AudioClip _panelOpen;     // I-4 열기 / J-9 일시정지
    private AudioClip _panelClose;    // I-4 닫기 / J-9 해제
    private AudioClip _itemEquip;     // I-6 장착
    private AudioClip _itemUnequip;   // I-6 해제
    private AudioClip _itemUse;       // I-11 소비 아이템 사용
    private AudioClip _itemAcquire;   // I-3 아이템 획득

    // ── 플레이어 / 기타 ─────────────────────────────────────────────
    private AudioClip _playerHit;     // E-4 플레이어 피격
    private AudioClip _playerDeath;   // E-5 플레이어 사망
    private AudioClip _gaugeFull;     // D-7 원소 게이지 만충
    private AudioClip _resurrect;     // E-6 부활

    // ── 무기 차징 스킬 (Minifantasy Weapons) ────────────────────────
    private AudioClip _chargeLoop;      // C-6 차징 충전 루프
    private AudioClip _chargeReady;     // C-7 단계 도달 신호
    private AudioClip _chargeUltimate;  // 3단계 궁극기 임팩트 (Special_Attack_1)
    private AudioClip _bowChargeShot;   // C-3 활 차지 발사
    private AudioClip _spearChargeHit;  // C-2 창 차지 타격 보강 (Thrust_2)
    private AudioClip _chargeSlashHeavy1; // 검 차지 1단계 강화 슬래시
    private AudioClip _chargeSlashHeavy2; // 검 차지 2단계 강화 슬래시
    private AudioClip _chargeSpecial2;    // 창/활 차지 2단계 특수 임팩트

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

        // 완드 차지3 집중 루프 전용 소스 (Start/Stop 제어)
        _wandChargeLoopSource = gameObject.AddComponent<AudioSource>();
        _wandChargeLoopSource.playOnAwake = false;
        _wandChargeLoopSource.spatialBlend = 0f;
        _wandChargeLoopSource.loop = true;

        // 모닥불 조리 진행 루프 전용 소스 (Start/Stop 제어)
        _cookLoopSource = gameObject.AddComponent<AudioSource>();
        _cookLoopSource.playOnAwake = false;
        _cookLoopSource.spatialBlend = 0f;
        _cookLoopSource.loop = true;

        // 무기 차징(우클릭 홀드) 루프 전용 소스
        _chargeLoopSource = gameObject.AddComponent<AudioSource>();
        _chargeLoopSource.playOnAwake = false;
        _chargeLoopSource.spatialBlend = 0f;
        _chargeLoopSource.loop = true;

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
        _justDodgeSlowdown = Resources.Load<AudioClip>("Sound/JustDodge/JustDodge_Slowdown");

        _footsteps = new AudioClip[]
        {
            Resources.Load<AudioClip>("Sound/Walk2/Footstep_Grass_a"),
            Resources.Load<AudioClip>("Sound/Walk2/Footstep_Grass_b"),
            Resources.Load<AudioClip>("Sound/Walk2/Footstep_Grass_c"),
            Resources.Load<AudioClip>("Sound/Walk2/Footstep_Grass_d"),
            Resources.Load<AudioClip>("Sound/Walk2/Footstep_Grass_e"),
            Resources.Load<AudioClip>("Sound/Walk2/Footstep_Grass_f"),
        };

        LoadElementClips();
    }

    /// <summary>
    /// 원소/완드 마법 클립을 로드합니다. 배열 인덱스는 ElementType 값(Earth=0, Fire=1, Ice=2)을 따릅니다.
    /// 파일별로 접두 번호가 달라(예: Earth는 Surge/Levitate) 명시적으로 로드합니다.
    /// </summary>
    private void LoadElementClips()
    {
        const string root = "Sound/Minifantasy_MagicAndSorcery_SFX/Minifantasy_MagicAndSorcery_SFX/Spells";

        // D-1 속성 전환: Earth=Surge, Fire/Ice=Cast
        _elemSwitch = new AudioClip[]
        {
            Resources.Load<AudioClip>($"{root}/Earth/01_Earth_Surge"),
            Resources.Load<AudioClip>($"{root}/Fire/01_Fire_Cast"),
            Resources.Load<AudioClip>($"{root}/Ice/01_Ice_Cast"),
        };

        // D-2·4·6 원소 적중(디버프 부착): *_Hit
        _elemHit = new AudioClip[]
        {
            Resources.Load<AudioClip>($"{root}/Earth/04_Earth_Hit"),
            Resources.Load<AudioClip>($"{root}/Fire/04_Fire_Hit"),
            Resources.Load<AudioClip>($"{root}/Ice/05_Ice_Hit"),
        };

        // C-4 완드 발사: *_Throw
        _wandThrow = new AudioClip[]
        {
            Resources.Load<AudioClip>($"{root}/Earth/03_Earth_Throw"),
            Resources.Load<AudioClip>($"{root}/Fire/03_Fire_Throw"),
            Resources.Load<AudioClip>($"{root}/Ice/04_Ice_Throw"),
        };

        // C-4·C-5 완드 착탄/폭발: *_Explosion
        _wandExplosion = new AudioClip[]
        {
            Resources.Load<AudioClip>($"{root}/Earth/05_Earth_Explosion"),
            Resources.Load<AudioClip>($"{root}/Fire/05_Fire_Explosion"),
            Resources.Load<AudioClip>($"{root}/Ice/03_Ice_Explosion"),
        };

        // C-5 차지3 집중 루프: Earth=Levitate, Fire/Ice=Hold
        _wandChargeHold = new AudioClip[]
        {
            Resources.Load<AudioClip>($"{root}/Earth/02_Earth_Levitate"),
            Resources.Load<AudioClip>($"{root}/Fire/02_Fire_Hold"),
            Resources.Load<AudioClip>($"{root}/Ice/02_Ice_Hold"),
        };

        LoadGatherCraftClips();
    }

    /// <summary>채집(나무)·제작·요리 클립을 로드합니다.</summary>
    private void LoadGatherCraftClips()
    {
        // H-1 나무 베기 타격 (Forgotten Plains, 2종 교차)
        _treeChops = new AudioClip[]
        {
            Resources.Load<AudioClip>("Sound/Minifantasy_Forgotten_Plains_SFX/15_Hit_on_wood_1"),
            Resources.Load<AudioClip>("Sound/Minifantasy_Forgotten_Plains_SFX/15_Hit_on_wood_2"),
        };

        const string craftRoot = "Sound/Minifantasy_CraftingAndProfessions2_SFX/Minifantasy_CraftingAndProfessions2_SFX";
        _craftSuccess = Resources.Load<AudioClip>($"{craftRoot}/Misc/Success");
        _craftFail    = Resources.Load<AudioClip>($"{craftRoot}/Misc/Fail");
        _cookLoop     = Resources.Load<AudioClip>($"{craftRoot}/Crafting_Professions/Cooking/Kitchen_Station_Loop");

        LoadMonsterClips();
    }

    /// <summary>
    /// 몬스터 종별 클립을 로드합니다. 배열 인덱스는 MonsterSfxKind 값을 따릅니다.
    /// 곰=Wolf(피격/사망)+Minotaur(공격/포효), 네크로맨서=Zombie(음성)+Fire(파이어볼) 조합.
    /// </summary>
    private void LoadMonsterClips()
    {
        const string root = "Sound/Minifantasy_Creatures_SFX_v3.0/Minifantasy_Creatures_SFX_v3.0";

        // 인덱스: None=0, Skeleton=1, Bear=2, Necromancer=3
        // 곰: 진짜 곰 음원이 팩에 없어 큰 야수 Yeti로 통일(임시). 전용 곰/동물 팩은 리서치 참고.
        _monAttack = new AudioClip[]
        {
            null,
            Resources.Load<AudioClip>($"{root}/Monsters/Skeleton/01_Skeleton_attack"),
            Resources.Load<AudioClip>($"{root}/Big_Guys/Yeti/01_Yeti_Attack_01"),
            null, // 네크로맨서는 스펠(파이어볼) 사운드를 별도 재생
        };

        _monHit = new AudioClip[]
        {
            null,
            Resources.Load<AudioClip>($"{root}/Monsters/Skeleton/02_Skeleton_damage"),
            Resources.Load<AudioClip>($"{root}/Big_Guys/Yeti/03_Yeti_Damage_01"),
            Resources.Load<AudioClip>($"{root}/Monsters/Zombie/03_Zombie_damage_1"),
        };

        _monDeath = new AudioClip[]
        {
            null,
            Resources.Load<AudioClip>($"{root}/Monsters/Skeleton/03_Skeleton_death"),
            Resources.Load<AudioClip>($"{root}/Big_Guys/Yeti/05_Yeti_Death"),
            Resources.Load<AudioClip>($"{root}/Monsters/Zombie/05_Zombie_death"),
        };

        _monAlert = new AudioClip[]
        {
            null,
            Resources.Load<AudioClip>($"{root}/Monsters/Skeleton/04_Skeleton_idle_1"),
            Resources.Load<AudioClip>($"{root}/Big_Guys/Yeti/06_Yeti_Idle_Loop"),
            Resources.Load<AudioClip>($"{root}/Monsters/Zombie/06_Zombie_idle_1"),
        };

        LoadUIItemClips();
    }

    /// <summary>UI/아이템 클립을 로드합니다. (RPG_Essentials Free)</summary>
    private void LoadUIItemClips()
    {
        const string ui = "Sound/RPG_Essentials_Free/10_UI_Menu_SFX";
        _uiHover     = Resources.Load<AudioClip>($"{ui}/001_Hover_01");
        _uiConfirm   = Resources.Load<AudioClip>($"{ui}/013_Confirm_03");
        _uiCancel    = Resources.Load<AudioClip>($"{ui}/029_Decline_09");
        _uiDenied    = Resources.Load<AudioClip>($"{ui}/033_Denied_03");
        _itemEquip   = Resources.Load<AudioClip>($"{ui}/070_Equip_10");
        _itemUnequip = Resources.Load<AudioClip>($"{ui}/071_Unequip_01");
        _itemUse     = Resources.Load<AudioClip>($"{ui}/051_use_item_01");

        // UI 열기/닫기: 보유 에셋(상자/비프) 모두 톤이 안 맞아 임시 비활성화.
        //   → 전용 UI 팩(Dustyroom/ Cute UI 등) 도입 후 여기서 클립 지정.
        _panelOpen   = null;
        _panelClose  = null;

        // 아이템 획득: 동전 찰랑/Confirm 블립 모두 튀어 임시 비활성화.
        //   → 전용 픽업 칩(Dustyroom 무료팩 등) 도입 후 여기서 클립 지정.
        _itemAcquire = null;

        // 플레이어 피격/사망 (Minifantasy Dungeon human)
        _playerHit   = Resources.Load<AudioClip>("Sound/Minifantasy_Dungeon_SFX/11_human_damage_1");
        _playerDeath = Resources.Load<AudioClip>("Sound/Minifantasy_Dungeon_SFX/14_human_death_spin");

        // D-7 원소 게이지 만충 (Magic and Sorcery Buff_Start)
        _gaugeFull   = Resources.Load<AudioClip>("Sound/Minifantasy_MagicAndSorcery_SFX/Minifantasy_MagicAndSorcery_SFX/Buffs/Buff_Start");

        // E-6 부활 (Magic and Sorcery Holy_Pillar — 상승하는 신성한 광휘)
        _resurrect   = Resources.Load<AudioClip>("Sound/Minifantasy_MagicAndSorcery_SFX/Minifantasy_MagicAndSorcery_SFX/Spells/Holy/02_Holy_Pillar");

        // C. 무기 차징 스킬 (Minifantasy Weapons)
        const string wepRoot = "Sound/Minifantasy_Weapons_SFX/Minifantasy_Weapons_SFX";
        _chargeLoop     = Resources.Load<AudioClip>($"{wepRoot}/Charged_Attacks/Charging_Loop");
        _chargeReady    = Resources.Load<AudioClip>($"{wepRoot}/Charged_Attacks/Charge_2");
        _chargeUltimate = Resources.Load<AudioClip>($"{wepRoot}/Charged_Attacks/Special_Attack_1");
        _bowChargeShot  = Resources.Load<AudioClip>($"{wepRoot}/Ranged_Attacks/Bow_Release");
        _spearChargeHit = Resources.Load<AudioClip>($"{wepRoot}/Thrust_Attacks/Thrust_2");
        _chargeSlashHeavy1 = Resources.Load<AudioClip>($"{wepRoot}/Slash_Attacks/Slash_Attack_Heavy_1");
        _chargeSlashHeavy2 = Resources.Load<AudioClip>($"{wepRoot}/Slash_Attacks/Slash_Attack_Heavy_2");
        _chargeSpecial2    = Resources.Load<AudioClip>($"{wepRoot}/Charged_Attacks/Special_Attack_2");
    }

    /// <summary>MonsterSfxKind를 0~3 배열 인덱스로 안전 변환합니다.</summary>
    private static int MonIndex(MonsterSfxKind kind)
    {
        int i = (int)kind;
        return (i < 0 || i > 3) ? 0 : i;
    }

    /// <summary>ElementType을 0~2 배열 인덱스로 안전 변환합니다.</summary>
    private static int ElemIndex(ElementType element)
    {
        int i = (int)element;
        return (i < 0 || i > 2) ? 0 : i;
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

    /// <summary>회피 저스트 발동 시 슬로우다운 효과음.</summary>
    public void PlayJustDodgeSlowdown()
    {
        Play(_justDodgeSlowdown, _justDodgeVolume);
    }

    /// <summary>발걸음음. 호출할 때마다 Footstep_Grass a→b→c→d→e→f→a… 순으로 순환 재생합니다.</summary>
    public void PlayFootstep()
    {
        if (_footsteps == null || _footsteps.Length == 0) return;

        AudioClip clip = _footsteps[_footstepIndex];
        _footstepIndex = (_footstepIndex + 1) % _footsteps.Length;
        Play(clip, _footstepVolume);
    }

    // ── 원소 / 완드 마법 API ────────────────────────────────────────

    /// <summary>D-1 속성 전환음. 전환된 속성(Earth/Fire/Ice)에 맞는 캐스트음을 재생합니다.</summary>
    public void PlayElementSwitch(ElementType element)
    {
        if (_elemSwitch == null) return;
        Play(_elemSwitch[ElemIndex(element)], _elementSwitchVolume);
    }

    /// <summary>D-2·4·6 원소 적중음. 속성 디버프가 새로 부착될 때 재생합니다.</summary>
    public void PlayElementHit(ElementType element)
    {
        if (_elemHit == null) return;
        Play(_elemHit[ElemIndex(element)], _elementHitVolume);
    }

    /// <summary>C-4 완드 발사음. 현재 속성에 맞는 Throw음을 재생합니다.</summary>
    public void PlayWandFire(ElementType element)
    {
        if (_wandThrow == null) return;
        Play(_wandThrow[ElemIndex(element)], _wandFireVolume);
    }

    /// <summary>C-4·C-5 완드 착탄/폭발음. 속성에 맞는 Explosion음을 재생합니다.</summary>
    public void PlayWandExplosion(ElementType element)
    {
        if (_wandExplosion == null) return;
        Play(_wandExplosion[ElemIndex(element)], _wandExplosionVolume);
    }

    /// <summary>C-5 완드 차지 시전음. 차징 스킬 발동 시 속성 캐스트음을 재생합니다.</summary>
    public void PlayWandChargeCast(ElementType element)
    {
        if (_elemSwitch == null) return;
        Play(_elemSwitch[ElemIndex(element)], _wandChargeCastVolume);
    }

    /// <summary>C-5 차지3 집중 루프 시작. 속성에 맞는 Hold음을 반복 재생합니다.</summary>
    public void StartWandChargeLoop(ElementType element)
    {
        if (_wandChargeHold == null || _wandChargeLoopSource == null) return;
        AudioClip clip = _wandChargeHold[ElemIndex(element)];
        if (clip == null) return;

        _wandChargeLoopSource.clip   = clip;
        _wandChargeLoopSource.volume = _wandChargeLoopVolume;
        _wandChargeLoopSource.Play();
    }

    /// <summary>C-5 차지3 집중 루프 정지.</summary>
    public void StopWandChargeLoop()
    {
        if (_wandChargeLoopSource == null) return;
        _wandChargeLoopSource.Stop();
        _wandChargeLoopSource.clip = null;
    }

    // ── 채집 / 제작 / 요리 API ──────────────────────────────────────

    /// <summary>H-1 나무 베기 타격음. 호출마다 2종을 교차 재생합니다.</summary>
    public void PlayTreeChop()
    {
        if (_treeChops == null || _treeChops.Length == 0) return;
        AudioClip clip = _treeChops[_treeChopIndex];
        _treeChopIndex = (_treeChopIndex + 1) % _treeChops.Length;
        Play(clip, _treeChopVolume);
    }

    /// <summary>I-7·8 제작 완료음.</summary>
    public void PlayCraftSuccess()
    {
        Play(_craftSuccess, _craftSuccessVolume);
    }

    /// <summary>제작 실패(재료 부족)음.</summary>
    public void PlayCraftFail()
    {
        Play(_craftFail, _craftFailVolume);
    }

    /// <summary>I-10 모닥불 요리 완료음.</summary>
    public void PlayCookComplete()
    {
        Play(_craftSuccess, _cookCompleteVolume);
    }

    /// <summary>I-9 모닥불 조리 진행 루프 시작. (이미 재생 중이면 무시)</summary>
    public void StartCookingLoop()
    {
        if (_cookLoopSource == null || _cookLoop == null) return;
        if (_cookLoopSource.isPlaying) return;

        _cookLoopSource.clip   = _cookLoop;
        _cookLoopSource.volume = _cookLoopVolume;
        _cookLoopSource.Play();
    }

    /// <summary>I-9 모닥불 조리 진행 루프 정지.</summary>
    public void StopCookingLoop()
    {
        if (_cookLoopSource == null) return;
        _cookLoopSource.Stop();
        _cookLoopSource.clip = null;
    }

    // ── 몬스터 API ──────────────────────────────────────────────────

    /// <summary>F-7·13 몬스터 공격음.</summary>
    public void PlayMonsterAttack(MonsterSfxKind kind)
    {
        if (_monAttack == null) return;
        Play(_monAttack[MonIndex(kind)], _monsterAttackVolume);
    }

    /// <summary>F-2 몬스터 피격음.</summary>
    public void PlayMonsterHit(MonsterSfxKind kind)
    {
        if (_monHit == null) return;
        Play(_monHit[MonIndex(kind)], _monsterHitVolume);
    }

    /// <summary>F-3 몬스터 사망음.</summary>
    public void PlayMonsterDeath(MonsterSfxKind kind)
    {
        if (_monDeath == null) return;
        Play(_monDeath[MonIndex(kind)], _monsterDeathVolume);
    }

    /// <summary>F-1 몬스터 발견/경계음.</summary>
    public void PlayMonsterAlert(MonsterSfxKind kind)
    {
        if (_monAlert == null) return;
        Play(_monAlert[MonIndex(kind)], _monsterAlertVolume);
    }

    /// <summary>F-9 네크로맨서 파이어볼 시전(영창)음. (Fire_Cast 재사용)</summary>
    public void PlayNecromancerCast()
    {
        if (_elemSwitch == null) return;
        Play(_elemSwitch[(int)ElementType.Fire], _necroSpellVolume);
    }

    /// <summary>F-10 네크로맨서 파이어볼 발사음. (Fire_Throw 재사용)</summary>
    public void PlayNecromancerFireball()
    {
        if (_wandThrow == null) return;
        Play(_wandThrow[(int)ElementType.Fire], _necroSpellVolume);
    }

    /// <summary>F-11 네크로맨서 파이어볼 착탄/폭발음. (Fire_Explosion 재사용)</summary>
    public void PlayNecromancerFireballExplosion()
    {
        if (_wandExplosion == null) return;
        Play(_wandExplosion[(int)ElementType.Fire], _necroSpellVolume);
    }

    // ── UI / 아이템 API ─────────────────────────────────────────────

    /// <summary>J-1 버튼 호버음.</summary>
    public void PlayUIHover()   => Play(_uiHover, _uiHoverVolume);
    /// <summary>J-2 버튼 클릭/확인음.</summary>
    public void PlayUIConfirm() => Play(_uiConfirm, _uiClickVolume);
    /// <summary>J-3 취소/뒤로가기음.</summary>
    public void PlayUICancel()  => Play(_uiCancel, _uiClickVolume);
    /// <summary>J-4 거부/불가 입력음.</summary>
    public void PlayUIDenied()  => Play(_uiDenied, _uiDeniedVolume);

    /// <summary>I-4 패널 열기 / J-9 일시정지음.</summary>
    public void PlayPanelOpen()  => Play(_panelOpen, _panelVolume);
    /// <summary>I-4 패널 닫기 / J-9 해제음.</summary>
    public void PlayPanelClose() => Play(_panelClose, _panelVolume);

    /// <summary>J-8 대화 진행(넘김)음.</summary>
    public void PlayDialogueAdvance() => Play(_uiConfirm, _uiClickVolume);

    /// <summary>I-6 장비 장착음.</summary>
    public void PlayItemEquip()   => Play(_itemEquip, _itemVolume);
    /// <summary>I-6 장비 해제음.</summary>
    public void PlayItemUnequip() => Play(_itemUnequip, _itemVolume);
    /// <summary>I-11 소비 아이템 사용음.</summary>
    public void PlayItemUse()     => Play(_itemUse, _itemVolume);
    /// <summary>I-3 아이템 획득음.</summary>
    public void PlayItemAcquire() => Play(_itemAcquire, _itemVolume);

    /// <summary>E-4 플레이어 피격음.</summary>
    public void PlayPlayerHit()   => Play(_playerHit, _playerHitVolume);
    /// <summary>E-5 플레이어 사망음.</summary>
    public void PlayPlayerDeath() => Play(_playerDeath, _playerDeathVolume);
    /// <summary>D-7 원소 게이지 만충음.</summary>
    public void PlayGaugeFull()   => Play(_gaugeFull, _gaugeFullVolume);
    /// <summary>H-8 건물 배치 확정음.</summary>
    public void PlayBuildPlace()  => Play(_uiConfirm, _buildVolume);
    /// <summary>E-6 플레이어 부활음.</summary>
    public void PlayResurrect()   => Play(_resurrect, _resurrectVolume);

    // ── 무기 차징 스킬 API ──────────────────────────────────────────

    /// <summary>C-6 차징 충전 루프 시작 (우클릭 홀드).</summary>
    public void StartChargeLoop()
    {
        if (_chargeLoopSource == null || _chargeLoop == null) return;
        if (_chargeLoopSource.isPlaying) return;
        _chargeLoopSource.clip   = _chargeLoop;
        _chargeLoopSource.volume = _chargeLoopVolume;
        _chargeLoopSource.Play();
    }

    /// <summary>C-6 차징 충전 루프 정지 (릴리즈/캔슬).</summary>
    public void StopChargeLoop()
    {
        if (_chargeLoopSource == null) return;
        _chargeLoopSource.Stop();
        _chargeLoopSource.clip = null;
    }

    /// <summary>C-7 차징 단계 도달 신호음.</summary>
    public void PlayChargeReady()    => Play(_chargeReady, _chargeReadyVolume);

    /// <summary>3단계 궁극 차징 스킬 임팩트음.</summary>
    public void PlayChargeUltimate() => Play(_chargeUltimate, _chargeUltimateVolume);

    /// <summary>C-3 활 차지 발사음 (관통 화살).</summary>
    public void PlayBowChargeShot()  => Play(_bowChargeShot, _bowChargeShotVolume);

    /// <summary>C-2 창 차지 타격 보강음.</summary>
    public void PlaySpearChargeHit() => Play(_spearChargeHit, _spearChargeHitVolume);

    /// <summary>
    /// 차지 스킬 발동 시 무기·단계별 "무거운 강화 릴리즈"음을 재생합니다.
    /// 일반 공격과 확실히 구분되도록 단계가 오를수록 강한 클립을 사용합니다. (Staff 제외)
    /// </summary>
    public void PlayChargeRelease(WeaponType weaponType, int stage)
    {
        AudioClip clip = null;
        switch (weaponType)
        {
            case WeaponType.Sword:
                clip = stage >= 3 ? _chargeUltimate : (stage == 2 ? _chargeSlashHeavy2 : _chargeSlashHeavy1);
                break;
            case WeaponType.Spear:
                clip = stage >= 3 ? _chargeUltimate : (stage == 2 ? _chargeSpecial2 : _spearChargeHit);
                break;
            case WeaponType.Bow:
                clip = stage >= 3 ? _chargeUltimate : _chargeSpecial2;
                break;
            default:
                return; // 완드(Staff)는 자체 마법 폭발음 사용
        }
        Play(clip, _chargeUltimateVolume);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || _source == null) return;
        _source.PlayOneShot(clip, volume);
    }
}
