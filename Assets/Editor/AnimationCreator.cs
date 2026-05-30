using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class AnimationCreator : EditorWindow
{
    [MenuItem("Tools/Update Player In Scene")]
    public static void UpdatePlayerInScene()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            player = GameObject.FindWithTag("Player");
        }

        if (player == null)
        {
            Debug.LogError("Player not found in scene!");
            return;
        }

        // Revert scale to 1x (we will scale the image itself instead)
        player.transform.localScale = Vector3.one;

        // Change sprite to new player image
        SpriteRenderer sr = player.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            string spritePath = "Assets/Resources/Player/Player.png";
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(spritePath);
            Sprite newSprite = null;
            foreach (var obj in allAssets)
            {
                if (obj is Sprite s && s.name == "Player_Down_Idle_0")
                {
                    newSprite = s;
                    break;
                }
            }

            // Update Pixels Per Unit to make the image 5x larger without affecting child scales
            TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer != null)
            {
                importer.spritePixelsPerUnit = 20f; // 100 -> 20 makes it 5x larger
                importer.SaveAndReimport();
                Debug.Log("Successfully updated Player Sprite Pixels Per Unit to 20.");
            }

            if (newSprite != null)
            {
                sr.sprite = newSprite;
                EditorUtility.SetDirty(player);
                Debug.Log($"Successfully updated Player sprite! (Player scale is reverted to 1, image size changed via PPU)");
            }
            else
            {
                Debug.LogError("Could not find Sprite 'Player_Down_Idle_0'");
            }
        }
        else
        {
            Debug.LogError("Player has no SpriteRenderer component");
        }
    }

    [MenuItem("Tools/Setup Player Animations")]
    public static void SetupPlayerAnimations()
    {
        CreateClips();
        SetupAnimatorController();
        Debug.Log("Successfully setup Player Animations and Animator Controller!");
    }

    private static void CreateClips()
    {
        string spritePath = "Assets/Resources/Player/Player.png";
        Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(spritePath);
        
        if (allAssets == null || allAssets.Length == 0)
        {
            Debug.LogError($"Failed to load sprites at {spritePath}");
            return;
        }

        var spriteDict = new Dictionary<string, Sprite>();
        foreach (var obj in allAssets)
        {
            if (obj is Sprite s) spriteDict[s.name] = s;
        }

        // 1. Idle
        CreateClip(spriteDict, "Idle/PlayerIdle_Up", GetSpriteNames("Player_Up_Idle_", 6), 6f, true);
        CreateClip(spriteDict, "Idle/PlayerIdle_Right", GetSpriteNames("Player_Right_Idle_", 6), 6f, true);
        CreateClip(spriteDict, "Idle/PlayerIdle_Down", GetSpriteNames("Player_Down_Idle_", 6), 6f, true);

        // 2. Walk
        CreateClip(spriteDict, "Walk/PlayerWalk_Up", GetSpriteNames("Player_Up_Walk_", 10), 10f, true);
        CreateClip(spriteDict, "Walk/PlayerWalk_Right", GetSpriteNames("Player_Right_Walk_", 10), 10f, true);
        CreateClip(spriteDict, "Walk/PlayerWalk_Down", GetSpriteNames("Player_Down_Walk_", 10), 10f, true);

        // 3. Dash
        CreateClip(spriteDict, "Dash/PlayerDash_Up", GetSpriteNames("Player_Up_Dash_", 2), 10f, false);
        CreateClip(spriteDict, "Dash/PlayerDash_Right", GetSpriteNames("Player_Right_Dash_", 2), 10f, false);
        CreateClip(spriteDict, "Dash/PlayerDash_Down", GetSpriteNames("Player_Down_Dash_", 2), 10f, false);

        // 4. Attack
        CreateClip(spriteDict, "Attack/PlayerAttack_Up", GetSpriteNames("Player_Up_Attack_", 8), 15f, false);
        CreateClip(spriteDict, "Attack/PlayerAttack_Right", GetSpriteNames("Player_Right_Attack_", 8), 15f, false);
        CreateClip(spriteDict, "Attack/PlayerAttack_Down", GetSpriteNames("Player_Down_Attack_", 8), 15f, false);

        // Attack2
        CreateClip(spriteDict, "Attack/PlayerAttack2_Right", GetSpriteNames("Player_Right_Attack2_", 8), 15f, false);

        AssetDatabase.SaveAssets();
    }

    private static string[] GetSpriteNames(string prefix, int count)
    {
        string[] names = new string[count];
        for (int i = 0; i < count; i++) names[i] = prefix + i;
        return names;
    }

    private static void CreateClip(Dictionary<string, Sprite> spriteDict, string relativePath, string[] spriteNames, float sampleRate, bool loop)
    {
        AnimationClip clip = new AnimationClip();
        clip.frameRate = sampleRate;
        
        if (loop)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }
        
        EditorCurveBinding spriteBinding = new EditorCurveBinding();
        spriteBinding.type = typeof(SpriteRenderer);
        spriteBinding.path = "";
        spriteBinding.propertyName = "m_Sprite";
        
        ObjectReferenceKeyframe[] keyFrames = new ObjectReferenceKeyframe[spriteNames.Length];
        for (int i = 0; i < spriteNames.Length; i++)
        {
            keyFrames[i] = new ObjectReferenceKeyframe();
            keyFrames[i].time = i / sampleRate;
            if (spriteDict.TryGetValue(spriteNames[i], out Sprite s)) keyFrames[i].value = s;
        }
        
        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyFrames);
        string path = "Assets/Resources/Animations/Player/" + relativePath + ".anim";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        AssetDatabase.CreateAsset(clip, path);
    }

    private static void SetupAnimatorController()
    {
        string path = "Assets/Resources/AnimationController/Player/PlayerAnimController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        
        if (controller == null)
        {
            Debug.LogError("Could not find PlayerAnimController");
            return;
        }

        // Clear existing states and parameters
        var rootStateMachine = controller.layers[0].stateMachine;
        rootStateMachine.states = new ChildAnimatorState[0];
        rootStateMachine.anyStateTransitions = new AnimatorStateTransition[0];
        controller.parameters = new AnimatorControllerParameter[0];

        // Add Parameters
        controller.AddParameter("DirX", AnimatorControllerParameterType.Float);
        controller.AddParameter("DirY", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsDashing", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("AttackCombo", AnimatorControllerParameterType.Int);

        // Create States
        var idleState = rootStateMachine.AddState("Idle");
        var walkState = rootStateMachine.AddState("Walk");
        var dashState = rootStateMachine.AddState("Dash");
        var attack1State = rootStateMachine.AddState("Attack1");
        var attack2State = rootStateMachine.AddState("Attack2");

        rootStateMachine.defaultState = idleState;

        // Setup BlendTrees
        idleState.motion = CreateDirectionalBlendTree("Idle", "Idle/PlayerIdle_Up", "Idle/PlayerIdle_Down", "Idle/PlayerIdle_Right");
        walkState.motion = CreateDirectionalBlendTree("Walk", "Walk/PlayerWalk_Up", "Walk/PlayerWalk_Down", "Walk/PlayerWalk_Right");
        dashState.motion = CreateDirectionalBlendTree("Dash", "Dash/PlayerDash_Up", "Dash/PlayerDash_Down", "Dash/PlayerDash_Right");
        
        // Attack1 uses Attack2 (Left Hand / 1타)
        attack1State.motion = CreateDirectionalBlendTree("Attack1", "Attack/PlayerAttack_Up", "Attack/PlayerAttack_Down", "Attack/PlayerAttack2_Right");
        
        // Attack2 uses Attack (Right Hand / 2타)
        attack2State.motion = CreateDirectionalBlendTree("Attack2", "Attack/PlayerAttack_Up", "Attack/PlayerAttack_Down", "Attack/PlayerAttack_Right");

        // Set up Transitions
        
        // Idle <-> Walk
        var idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        idleToWalk.hasExitTime = false;
        idleToWalk.duration = 0f;

        var walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        walkToIdle.hasExitTime = false;
        walkToIdle.duration = 0f;

        // Any -> Dash
        var anyToDash = rootStateMachine.AddAnyStateTransition(dashState);
        anyToDash.AddCondition(AnimatorConditionMode.If, 0, "IsDashing");
        anyToDash.hasExitTime = false;
        anyToDash.duration = 0f;
        
        var dashToIdle = dashState.AddTransition(idleState);
        dashToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing");
        dashToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        dashToIdle.hasExitTime = false;
        dashToIdle.duration = 0f;

        var dashToWalk = dashState.AddTransition(walkState);
        dashToWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing");
        dashToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        dashToWalk.hasExitTime = false;
        dashToWalk.duration = 0f;

        // Any -> Attack1
        var anyToAttack1 = rootStateMachine.AddAnyStateTransition(attack1State);
        anyToAttack1.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        anyToAttack1.AddCondition(AnimatorConditionMode.Equals, 1, "AttackCombo");
        anyToAttack1.hasExitTime = false;
        anyToAttack1.duration = 0f;

        // Any -> Attack2
        var anyToAttack2 = rootStateMachine.AddAnyStateTransition(attack2State);
        anyToAttack2.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        anyToAttack2.AddCondition(AnimatorConditionMode.Equals, 2, "AttackCombo");
        anyToAttack2.hasExitTime = false;
        anyToAttack2.duration = 0f;

        // Attack -> Idle/Walk
        var attack1ToIdle = attack1State.AddTransition(idleState);
        attack1ToIdle.hasExitTime = true;
        attack1ToIdle.exitTime = 1f;
        attack1ToIdle.duration = 0f;
        
        var attack2ToIdle = attack2State.AddTransition(idleState);
        attack2ToIdle.hasExitTime = true;
        attack2ToIdle.exitTime = 1f;
        attack2ToIdle.duration = 0f;

        AssetDatabase.SaveAssets();
    }

    private static BlendTree CreateDirectionalBlendTree(string name, string upClip, string downClip, string rightClip)
    {
        BlendTree tree = new BlendTree();
        tree.name = name;
        tree.blendType = BlendTreeType.SimpleDirectional2D;
        tree.blendParameter = "DirX";
        tree.blendParameterY = "DirY";
        
        AnimationClip up = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Resources/Animations/Player/{upClip}.anim");
        AnimationClip down = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Resources/Animations/Player/{downClip}.anim");
        AnimationClip right = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Resources/Animations/Player/{rightClip}.anim");

        // Left uses Right clip (we will flip it in code)
        tree.AddChild(right, new Vector2(1, 0));
        tree.AddChild(right, new Vector2(-1, 0));
        tree.AddChild(up, new Vector2(0, 1));
        tree.AddChild(down, new Vector2(0, -1));

        return tree;
    }
}
