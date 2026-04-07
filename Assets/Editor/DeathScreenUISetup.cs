using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>
/// DeathScreenCanvas의 SerializedField 참조를 자동으로 연결하는 에디터 유틸리티.
/// </summary>
public static class DeathScreenUISetup
{
    [MenuItem("Tools/Setup DeathScreenUI References")]
    public static void SetupReferences()
    {
        // DeathScreenCanvas 찾기
        var canvasGO = GameObject.Find("DeathScreenCanvas");
        if (canvasGO == null) { Debug.LogError("[DeathScreenUISetup] DeathScreenCanvas를 찾을 수 없습니다."); return; }

        var ui = canvasGO.GetComponent<DeathScreenUI>();
        if (ui == null) { Debug.LogError("[DeathScreenUISetup] DeathScreenUI 컴포넌트가 없습니다."); return; }

        var so = new SerializedObject(ui);

        // _rootCanvasGroup — 자기 자신에 있는 CanvasGroup
        so.FindProperty("_rootCanvasGroup").objectReferenceValue = canvasGO.GetComponent<CanvasGroup>();

        // _blackPanel
        var blackPanelGO = canvasGO.transform.Find("BlackPanel");
        if (blackPanelGO != null)
            so.FindProperty("_blackPanel").objectReferenceValue = blackPanelGO.GetComponent<Image>();

        // _deadText
        var deadTextGO = canvasGO.transform.Find("DeadText");
        if (deadTextGO != null)
            so.FindProperty("_deadText").objectReferenceValue = deadTextGO.GetComponent<TextMeshProUGUI>();

        // _resurrectButton
        var buttonGO = canvasGO.transform.Find("ResurrectButton");
        if (buttonGO != null)
        {
            so.FindProperty("_resurrectButton").objectReferenceValue = buttonGO.GetComponent<Button>();

            // _resurrectButtonText
            var buttonTextGO = buttonGO.Find("ResurrectButtonText");
            if (buttonTextGO != null)
                so.FindProperty("_resurrectButtonText").objectReferenceValue = buttonTextGO.GetComponent<TextMeshProUGUI>();
        }

        so.ApplyModifiedProperties();

        // 버튼 onClick 이벤트 연결 (중복 방지: 기존 리스너 전부 제거 후 1개 추가)
        if (buttonGO != null)
        {
            var btn   = buttonGO.GetComponent<Button>();
            var soBtn = new SerializedObject(btn);
            // 퍼시스턴트 리스너 배열 초기화 (이전 실행에서 누적된 중복 제거)
            var calls = soBtn.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            calls.ClearArray();
            soBtn.ApplyModifiedProperties();
            // 정확히 1개만 등록
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                btn.onClick,
                ui.OnResurrectButtonClicked);
            EditorUtility.SetDirty(btn);
        }

        // EventTrigger로 호버 효과 연결
        if (buttonGO != null)
        {
            var et = buttonGO.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (et == null) et = buttonGO.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            et.triggers.Clear();

            // PointerEnter → OnButtonPointerEnter (void persistent listener)
            var enterE = new UnityEngine.EventSystems.EventTrigger.Entry();
            enterE.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(enterE.callback, ui.OnButtonPointerEnter);
            et.triggers.Add(enterE);

            // PointerExit → OnButtonPointerExit
            var exitE = new UnityEngine.EventSystems.EventTrigger.Entry();
            exitE.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(exitE.callback, ui.OnButtonPointerExit);
            et.triggers.Add(exitE);

            EditorUtility.SetDirty(et);
        }

        EditorUtility.SetDirty(ui);

        // ── DeathCutsceneController 참조 연결 ────────────────────────────
        var playerGO = GameObject.Find("Player");
        if (playerGO != null)
        {
            var ctrl = playerGO.GetComponent<DeathCutsceneController>();
            if (ctrl != null)
            {
                var soCtrl = new SerializedObject(ctrl);
                soCtrl.FindProperty("_deathScreenUI").objectReferenceValue       = ui;
                soCtrl.FindProperty("_playerDeathEffect").objectReferenceValue   = playerGO.GetComponent<PlayerDeathEffect>();
                soCtrl.FindProperty("_karmaHandler").objectReferenceValue        = playerGO.GetComponent<KarmaHandler>();
                soCtrl.FindProperty("_livingEntity").objectReferenceValue        = playerGO.GetComponent<LivingEntity>();

                // KarmaNotifyText
                var karmaTextGO = canvasGO.transform.Find("KarmaNotifyText");
                if (karmaTextGO != null)
                    soCtrl.FindProperty("_karmaNotifyText").objectReferenceValue = karmaTextGO.GetComponent<TMPro.TextMeshProUGUI>();

                // PlayerInput (PlayerMovement로 입력 차단)
                var playerInput = playerGO.GetComponent<PlayerMovement>();
                if (playerInput != null)
                    soCtrl.FindProperty("_playerInput").objectReferenceValue = playerInput;

                soCtrl.ApplyModifiedProperties();
                EditorUtility.SetDirty(ctrl);
            }
            else
            {
                Debug.LogWarning("[DeathScreenUISetup] DeathCutsceneController를 Player에서 찾을 수 없습니다.");
            }
        }

        // ── FX Volume + ImpulseSource 참조 연결 ──────────────────────────
        if (playerGO != null)
        {
            var ctrl = playerGO.GetComponent<DeathCutsceneController>();
            if (ctrl != null)
            {
                var soCtrl2 = new SerializedObject(ctrl);

                // ResurrectionFXVolume
                var fxVolumeGO = GameObject.Find("ResurrectionFXVolume");
                if (fxVolumeGO != null)
                    soCtrl2.FindProperty("_fxVolume").objectReferenceValue = fxVolumeGO.GetComponent<Volume>();

                // CinemachineImpulseSource (Main Camera에 붙임)
                var mainCam2 = GameObject.Find("Main Camera");
                if (mainCam2 != null)
                    soCtrl2.FindProperty("_impulseSource").objectReferenceValue = mainCam2.GetComponent<CinemachineImpulseSource>();

                // PlayerReverseDissolveController
                var reverseCtrl = playerGO.GetComponent<PlayerReverseDissolveController>();
                if (reverseCtrl != null)
                {
                    soCtrl2.FindProperty("_reverseDissolveCtrl").objectReferenceValue = reverseCtrl;

                    // PlayerReverseDissolveController 내부 참조 연결
                    var soReverse = new SerializedObject(reverseCtrl);
                    soReverse.FindProperty("_spriteRenderer").objectReferenceValue =
                        playerGO.GetComponent<SpriteRenderer>();
                    soReverse.FindProperty("_resurrectMaterial").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Materials/PlayerResurrectMat.mat");
                    soReverse.ApplyModifiedProperties();
                    EditorUtility.SetDirty(reverseCtrl);
                }

                // ResurrectionTrailPool
                var trailPool = playerGO.GetComponent<ResurrectionTrailPool>();
                if (trailPool != null)
                {
                    soCtrl2.FindProperty("_trailPool").objectReferenceValue = trailPool;

                    var soTrail = new SerializedObject(trailPool);
                    soTrail.FindProperty("_playerSprite").objectReferenceValue =
                        playerGO.GetComponent<SpriteRenderer>();
                    soTrail.ApplyModifiedProperties();
                    EditorUtility.SetDirty(trailPool);
                }

                soCtrl2.ApplyModifiedProperties();
                EditorUtility.SetDirty(ctrl);
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvasGO.scene);

        Debug.Log("[DeathScreenUISetup] 참조 연결 완료!");
    }

    // ── 렌더러 피처 + 씬 오브젝트 셋업 ─────────────────────────────────────

    [MenuItem("Tools/Setup Phase1 Scene Objects")]
    public static void SetupPhase1SceneObjects()
    {
        // 1. ResurrectionFXVolume 생성 (없으면)
        var fxVolumeGO = GameObject.Find("ResurrectionFXVolume");
        if (fxVolumeGO == null)
        {
            fxVolumeGO = new GameObject("ResurrectionFXVolume");
            var vol = fxVolumeGO.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.weight   = 1f;
            vol.priority = 10;

            // VolumeProfile 생성 및 저장
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "ResurrectionFXProfile";
            AssetDatabase.CreateAsset(profile, "Assets/Settings/ResurrectionFXProfile.asset");

            // ChromaticAberration 추가 (강도 0으로 시작)
            var ca = profile.Add<ChromaticAberration>(true);
            ca.intensity.Override(0f);

            vol.profile = profile;
            AssetDatabase.SaveAssets();

            Debug.Log("[Phase1Setup] ResurrectionFXVolume 생성 완료.");
        }

        // 1b. Volume Profile에 없는 오버라이드 추가 (sharedProfile 사용)
        if (fxVolumeGO != null)
        {
            var vol = fxVolumeGO.GetComponent<Volume>();
            var profile = vol != null ? vol.sharedProfile : null;
            if (profile == null && vol != null)
                profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/ResurrectionFXProfile.asset");

            if (profile != null)
            {
                bool dirty = false;

                if (!profile.Has<ChromaticAberration>())
                {
                    var ca2 = profile.Add<ChromaticAberration>(true);
                    ca2.intensity.Override(0f);
                    AssetDatabase.AddObjectToAsset(ca2, profile);
                    dirty = true;
                    Debug.Log("[Phase1Setup] ChromaticAberration → ResurrectionFXProfile 추가 완료.");
                }

                if (!profile.Has<Vignette>())
                {
                    var vig = profile.Add<Vignette>(true);
                    vig.color.Override(new Color(0.55f, 0f, 0f));
                    vig.intensity.Override(0f);
                    vig.smoothness.Override(0.5f);
                    AssetDatabase.AddObjectToAsset(vig, profile);
                    dirty = true;
                    Debug.Log("[Phase1Setup] Vignette → ResurrectionFXProfile 추가 완료.");
                }

                if (!profile.Has<ColorAdjustments>())
                {
                    var ca = profile.Add<ColorAdjustments>(true);
                    ca.saturation.Override(0f);
                    AssetDatabase.AddObjectToAsset(ca, profile);
                    dirty = true;
                    Debug.Log("[Phase1Setup] ColorAdjustments → ResurrectionFXProfile 추가 완료.");
                }

                if (!profile.Has<FilmGrain>())
                {
                    var fg = profile.Add<FilmGrain>(true);
                    fg.intensity.Override(0f);
                    fg.response.Override(0.8f);
                    AssetDatabase.AddObjectToAsset(fg, profile);
                    dirty = true;
                    Debug.Log("[Phase1Setup] FilmGrain → ResurrectionFXProfile 추가 완료.");
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(profile);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Debug.Log("[Phase1Setup] ResurrectionFXProfile 저장 완료.");
                }
            }
            else
            {
                Debug.LogWarning("[Phase1Setup] ResurrectionFXProfile을 찾을 수 없습니다.");
            }
        }

        // 2. Main Camera에 CinemachineImpulseSource 추가
        var mainCam = GameObject.Find("Main Camera");
        if (mainCam != null && mainCam.GetComponent<CinemachineImpulseSource>() == null)
        {
            mainCam.AddComponent<CinemachineImpulseSource>();
            Debug.Log("[Phase1Setup] CinemachineImpulseSource → Main Camera 추가 완료.");
        }

        // 3. CinemachineCamera에 CinemachineImpulseListener 추가
        var cinemachineCam = GameObject.Find("CinemachineCamera");
        if (cinemachineCam != null && cinemachineCam.GetComponent<CinemachineImpulseListener>() == null)
        {
            cinemachineCam.AddComponent<CinemachineImpulseListener>();
            Debug.Log("[Phase1Setup] CinemachineImpulseListener → CinemachineCamera 추가 완료.");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        // 4. 참조 재연결
        SetupReferences();

        Debug.Log("[Phase1Setup] Phase 1 씬 오브젝트 셋업 완료!");
    }

    [MenuItem("Tools/Add ColorInvert Renderer Feature")]
    public static void AddColorInvertFeature()
    {
        const string rendererPath = "Assets/Settings/PC_Renderer.asset";
        var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (rendererData == null)
        {
            Debug.LogError($"[RendererFeature] {rendererPath} 를 찾을 수 없습니다.");
            return;
        }

        // 이미 있으면 skip
        foreach (var f in rendererData.rendererFeatures)
        {
            if (f is ColorInvertRendererFeature)
            {
                Debug.Log("[RendererFeature] ColorInvertRendererFeature 이미 등록되어 있습니다.");
                return;
            }
        }

        // 생성 & 에셋에 추가
        var feature = ScriptableObject.CreateInstance<ColorInvertRendererFeature>();
        feature.name = "ColorInvertRendererFeature";
        feature.Intensity = 0f;

        var so = new SerializedObject(rendererData);
        var featuresProp = so.FindProperty("m_RendererFeatures");
        featuresProp.arraySize++;
        var newElem = featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1);
        newElem.objectReferenceValue = feature;

        // m_RendererFeatureMap 재생성 (GUID 기반 맵)
        AssetDatabase.AddObjectToAsset(feature, rendererData);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(rendererData);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[RendererFeature] ColorInvertRendererFeature → PC_Renderer 등록 완료!");
    }
}
