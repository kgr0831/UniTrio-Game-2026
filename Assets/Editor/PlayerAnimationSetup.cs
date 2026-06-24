using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// [Tools > Setup Player Animation] 메뉴 실행 시:
/// 1. 플레이어 방향 전환 AnimationClip 4종 생성
/// 2. PlayerAnimController (2D 블랜드 트리), SwordAnimController, VFXAnimController 생성
/// 3. 씬에 WeaponPivot 생성 후 Sword/VFX 재부모화, 컴포넌트 할당
/// </summary>
public static class PlayerAnimationSetup
{
    private const string AnimPath   = "Assets/Resources/Animations/";
    private const string SwordClip  = "Sword_Attack1_R";
    private const string SlashClip  = "Sword_Attack1_R_Slash";

    // 스프라이트 GUID (PlayerDummy 폴더의 각 방향 스프라이트)
    private const string GuidR = "8852598b8ec038b4089ea5e19e56ec6f"; // Player_R
    private const string GuidL = "be8952178b6a530448f7737d71cec538"; // Player_L
    private const string GuidF = "a0392e7595b6dad439289bff6b59c81b"; // Player_F
    private const string GuidB = "f98404eda952722468b0079ab48f32f7"; // Player_B

    [MenuItem("Tools/Setup Player Animation")]
    public static void Run()
    {
        CreateFacingClips();
        CreatePlayerAnimController();
        CreateSwordAnimController();
        CreateVFXAnimController();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        SetupSceneObjects();

        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[PlayerAnimationSetup] 완료.");
    }

    // ─────────────────────────────────────────────
    // 1. 방향 전환 AnimationClip 생성 (단일 프레임, 스프라이트 교체)
    // ─────────────────────────────────────────────
    private static void CreateFacingClips()
    {
        CreateFacingClip("Player_Facing_R", GuidR);
        CreateFacingClip("Player_Facing_L", GuidL);
        CreateFacingClip("Player_Facing_F", GuidF);
        CreateFacingClip("Player_Facing_B", GuidB);
    }

    private static void CreateFacingClip(string clipName, string spriteGuid)
    {
        string clipPath = AnimPath + clipName + ".anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null) return;

        string   spritePath = AssetDatabase.GUIDToAssetPath(spriteGuid);
        Sprite   sprite     = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null) { Debug.LogWarning($"[Setup] 스프라이트 없음: {spritePath}"); return; }

        AnimationClip clip = new AnimationClip
        {
            name     = clipName,
            wrapMode = WrapMode.Loop
        };

        // 단일 keyframe으로 스프라이트를 고정 (SpriteRenderer.m_Sprite 바인딩)
        var binding = new EditorCurveBinding
        {
            type         = typeof(SpriteRenderer),
            path         = "",
            propertyName = "m_Sprite"
        };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, new[]
        {
            new ObjectReferenceKeyframe { time = 0f, value = sprite }
        });

        // 루프 설정 유지
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, clipPath);
    }

    // ─────────────────────────────────────────────
    // 2. PlayerAnimController: DirX/DirY 2D 블랜드 트리
    // ─────────────────────────────────────────────
    private static void CreatePlayerAnimController()
    {
        string path = AnimPath + "PlayerAnimController.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) return;

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("DirX", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("DirY", AnimatorControllerParameterType.Float);

        var rootSM = ctrl.layers[0].stateMachine;

        // BlendTree를 담을 State 생성
        ctrl.CreateBlendTreeInController("Facing", out BlendTree tree, 0);
        tree.blendType       = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter  = "DirX";
        tree.blendParameterY = "DirY";

        // 4방향 클립 추가: 커서가 각 방향일 때 해당 스프라이트로 전환
        tree.AddChild(LoadClip("Player_Facing_R"), new Vector2( 1f,  0f));
        tree.AddChild(LoadClip("Player_Facing_L"), new Vector2(-1f,  0f));
        tree.AddChild(LoadClip("Player_Facing_F"), new Vector2( 0f,  1f));
        tree.AddChild(LoadClip("Player_Facing_B"), new Vector2( 0f, -1f));

        EditorUtility.SetDirty(ctrl);
    }

    // ─────────────────────────────────────────────
    // 3. SwordAnimController: Idle ↔ Attack (Trigger)
    // ─────────────────────────────────────────────
    private static void CreateSwordAnimController()
    {
        string path = AnimPath + "SwordAnimController.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) return;

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var rootSM = ctrl.layers[0].stateMachine;

        var idleState   = rootSM.AddState("Idle");
        var attackState = rootSM.AddState("Attack");

        attackState.motion = LoadClip(SwordClip);
        // Sword 애니메이션은 0→45→-135→0 회전 델타; 루프 없이 1회 재생
        attackState.speed  = 1f;

        rootSM.defaultState = idleState;

        // Idle → Attack (즉시 전환, exitTime 없음)
        var toAttack = idleState.AddTransition(attackState);
        toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        toAttack.hasExitTime     = false;
        toAttack.duration        = 0f;
        toAttack.offset          = 0f;
        toAttack.canTransitionToSelf = false;

        // Attack → Idle (애니메이션 1회 완료 후 복귀)
        var toIdle = attackState.AddTransition(idleState);
        toIdle.hasExitTime      = true;
        toIdle.exitTime         = 1f;     // normalized time = 1.0 (1회 완료)
        toIdle.hasFixedDuration = false;
        toIdle.duration         = 0f;

        EditorUtility.SetDirty(ctrl);
    }

    // ─────────────────────────────────────────────
    // 4. VFXAnimController: Idle ↔ Attack (Trigger)
    // ─────────────────────────────────────────────
    private static void CreateVFXAnimController()
    {
        string path = AnimPath + "VFXAnimController.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) return;

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var rootSM = ctrl.layers[0].stateMachine;

        var idleState   = rootSM.AddState("Idle");
        var attackState = rootSM.AddState("Attack");

        attackState.motion = LoadClip(SlashClip);
        attackState.speed  = 1f;

        rootSM.defaultState = idleState;

        var toAttack = idleState.AddTransition(attackState);
        toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        toAttack.hasExitTime     = false;
        toAttack.duration        = 0f;
        toAttack.offset          = 0f;
        toAttack.canTransitionToSelf = false;

        var toIdle = attackState.AddTransition(idleState);
        toIdle.hasExitTime      = true;
        toIdle.exitTime         = 1f;
        toIdle.hasFixedDuration = false;
        toIdle.duration         = 0f;

        EditorUtility.SetDirty(ctrl);
    }

    // ─────────────────────────────────────────────
    // 5. 씬 오브젝트 설정
    // ─────────────────────────────────────────────
    private static void SetupSceneObjects()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null) { Debug.LogError("[Setup] Player 오브젝트를 찾을 수 없습니다."); return; }

        Transform swordTf = player.transform.Find("Sword");
        Transform vfxTf   = player.transform.Find("VFX");

        if (swordTf == null) { Debug.LogError("[Setup] Sword 오브젝트를 찾을 수 없습니다."); return; }
        if (vfxTf   == null) { Debug.LogError("[Setup] VFX 오브젝트를 찾을 수 없습니다.");   return; }

        // ── WeaponPivot 생성 (이미 있으면 재사용) ──────────────────────────
        Transform pivotTf = player.transform.Find("WeaponPivot");
        GameObject weaponPivot;
        if (pivotTf == null)
        {
            weaponPivot = new GameObject("WeaponPivot");
            weaponPivot.transform.SetParent(player.transform, false);
        }
        else
        {
            weaponPivot = pivotTf.gameObject;
        }
        weaponPivot.transform.localPosition    = Vector3.zero;
        weaponPivot.transform.localEulerAngles = Vector3.zero;
        weaponPivot.transform.localScale       = Vector3.one;

        // ── Sword / VFX 를 WeaponPivot 자식으로 재부모화 ──────────────────
        // worldPositionStays = false: 로컬 좌표 기준 이동 (Pivot이 Player 원점이므로 동일)
        swordTf.SetParent(weaponPivot.transform, false);
        vfxTf.SetParent(weaponPivot.transform, false);

        // 애니메이션의 로컬 좌표계 기준 초기 위치로 설정
        swordTf.localPosition    = new Vector3(0.15f,          -0.27f,  0f);
        vfxTf.localPosition      = new Vector3(0.194151878f,   -0.211f, 0f);
        swordTf.localEulerAngles = Vector3.zero;
        vfxTf.localEulerAngles   = Vector3.zero;
        swordTf.localScale       = Vector3.one;
        vfxTf.localScale         = new Vector3(1.0041f, 1f, 1f); // 원래 스케일 유지

        // ── Player Animator (방향 전환 블랜드 트리) ───────────────────────
        Animator playerAnim = player.GetComponent<Animator>();
        if (playerAnim == null) playerAnim = player.AddComponent<Animator>();
        playerAnim.runtimeAnimatorController =
            Load<RuntimeAnimatorController>("PlayerAnimController.controller");

        // ── Sword Animator ────────────────────────────────────────────────
        Animator swordAnim = swordTf.GetComponent<Animator>();
        if (swordAnim == null) swordAnim = swordTf.gameObject.AddComponent<Animator>();
        swordAnim.runtimeAnimatorController =
            Load<RuntimeAnimatorController>("SwordAnimController.controller");

        // ── VFX Animator ──────────────────────────────────────────────────
        Animator vfxAnim = vfxTf.GetComponent<Animator>();
        if (vfxAnim == null) vfxAnim = vfxTf.gameObject.AddComponent<Animator>();
        vfxAnim.runtimeAnimatorController =
            Load<RuntimeAnimatorController>("VFXAnimController.controller");

        // ── PlayerWeaponController 추가 및 SerializedField 할당 ───────────
        PlayerWeaponController ctrl = player.GetComponent<PlayerWeaponController>();
        if (ctrl == null) ctrl = player.AddComponent<PlayerWeaponController>();

        // private [SerializeField] 필드는 SerializedObject를 통해서만 안전하게 할당 가능
        var so = new SerializedObject(ctrl);
        so.FindProperty("_weaponPivot").objectReferenceValue    = weaponPivot.transform;
        so.FindProperty("_swordAnimator").objectReferenceValue  = swordAnim;
        so.FindProperty("_vfxAnimator").objectReferenceValue    = vfxAnim;
        so.FindProperty("_playerAnimator").objectReferenceValue = playerAnim;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(player.scene);

        Debug.Log("[PlayerAnimationSetup] 씬 오브젝트 설정 완료.");
    }

    // ─────────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────────
    private static AnimationClip LoadClip(string name) =>
        AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimPath + name + ".anim");

    private static T Load<T>(string fileName) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(AnimPath + fileName);
}
