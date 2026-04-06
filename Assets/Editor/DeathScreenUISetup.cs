using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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

        // 버튼 onClick 이벤트 연결
        if (buttonGO != null)
        {
            var btn = buttonGO.GetComponent<Button>();
            var soBtn = new SerializedObject(btn);
            // onClick 퍼시스턴트 리스너 추가
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

            // PointerEnter
            var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
            };
            UnityEditor.Events.UnityEventTools.AddPersistentListener(
                enterEntry.callback,
                new UnityEngine.Events.UnityAction<UnityEngine.EventSystems.BaseEventData>(
                    _ => ui.OnButtonPointerEnter()));
            // 런타임 delegate는 persistent 등록 불가이므로 동적 리스너 방식 대신
            // 직접 AddPersistentListener의 오버로드가 없어 Action<BaseEventData> 불가.
            // 대신 DeathScreenUI에 래퍼 메서드를 쓰도록 persistent void 메서드 사용.
            et.triggers.Clear();

            var enterE = new UnityEngine.EventSystems.EventTrigger.Entry();
            enterE.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(enterE.callback, ui.OnButtonPointerEnter);
            et.triggers.Add(enterE);

            var exitE = new UnityEngine.EventSystems.EventTrigger.Entry();
            exitE.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(exitE.callback, ui.OnButtonPointerExit);
            et.triggers.Add(exitE);

            EditorUtility.SetDirty(et);
        }

        EditorUtility.SetDirty(ui);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvasGO.scene);

        Debug.Log("[DeathScreenUISetup] 참조 연결 완료!");
    }
}
