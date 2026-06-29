using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀘스트 목표 타입.
/// </summary>
public enum QuestObjectiveType
{
    /// <summary>아이템 수집 (나무 조각 30개 등)</summary>
    Collect,
    /// <summary>몬스터 처치 (곰 1마리 등)</summary>
    Kill,
    /// <summary>특정 위치 도달 (마을 발견 등)</summary>
    Reach,
    /// <summary>NPC와 대화</summary>
    Talk,
    /// <summary>아이템 제작</summary>
    Craft,
    /// <summary>던전 입장/탐사</summary>
    Explore,
    /// <summary>아이템 구매</summary>
    Buy,
    /// <summary>커스텀 (코드에서 직접 Complete 호출)</summary>
    Custom
}

/// <summary>
/// 퀘스트 카테고리.
/// </summary>
public enum QuestCategory
{
    Tutorial,
    Main,
    Npc
}

/// <summary>
/// 퀘스트 상태.
/// </summary>
public enum QuestState
{
    /// <summary>아직 해금되지 않음 (선행 퀘스트 미완료 등)</summary>
    Locked,
    /// <summary>수행 가능 (NPC 대화로 수락 가능, 또는 자동 시작)</summary>
    Available,
    /// <summary>진행 중</summary>
    Active,
    /// <summary>완료됨</summary>
    Completed
}

/// <summary>
/// 퀘스트 목표 하나를 정의합니다.
/// </summary>
[Serializable]
public class QuestObjectiveData
{
    [Tooltip("목표 타입")]
    public QuestObjectiveType Type;
    [Tooltip("목표 설명 (UI 표시용, 예: '곰 1마리 처치')")]
    public string Description;
    [Tooltip("필요 수량")]
    public int RequiredAmount = 1;
    [Tooltip("대상 ID (Kill: MonsterData.name, Collect: ItemData.Id, Reach: zone name 등)")]
    public string TargetId;
}

/// <summary>
/// 퀘스트 보상 정의.
/// </summary>
[Serializable]
public class QuestReward
{
    [Tooltip("보상 골드")]
    public int Gold;
    [Tooltip("보상 아이템 목록")]
    public List<QuestRewardItem> Items = new List<QuestRewardItem>();
    [Tooltip("완료 시 해금할 퀘스트 ID 목록")]
    public List<string> UnlockQuestIds = new List<string>();
}

[Serializable]
public class QuestRewardItem
{
    public ItemData Item;
    public int Count = 1;
}

/// <summary>
/// 퀘스트 데이터 ScriptableObject.
/// 모든 퀘스트(튜토리얼·메인·NPC)를 통일된 형식으로 정의합니다.
/// </summary>
[CreateAssetMenu(fileName = "NewQuest", menuName = "Game/Quest")]
public class QuestSO : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("퀘스트 고유 ID (예: TUT_01, MAIN_01, NPC_01)")]
    public string QuestId;
    [Tooltip("퀘스트 표시 이름")]
    public string QuestName;
    [Tooltip("카테고리")]
    public QuestCategory Category;
    [Tooltip("퀘스트 설명")]
    [TextArea(2, 4)]
    public string Description;

    [Header("NPC 연동")]
    [Tooltip("퀘스트를 부여하는 NPC 이름 (비어있으면 자동 시작)")]
    public string GiverNpcName;
    [Tooltip("퀘스트 부여 시 재생할 대화 ID (DialogueDatabase)")]
    public string StartDialogueId;
    [Tooltip("퀘스트 완료 시 재생할 대화 ID")]
    public string CompleteDialogueId;

    [Header("선행 조건")]
    [Tooltip("이 퀘스트가 Available이 되려면 완료되어야 하는 퀘스트 ID 목록")]
    public List<string> PrerequisiteQuestIds = new List<string>();
    [Tooltip("자동 시작 여부 (Available 되자마자 Active로 전환)")]
    public bool AutoStart;

    [Header("목표")]
    public List<QuestObjectiveData> Objectives = new List<QuestObjectiveData>();

    [Header("보상")]
    public QuestReward Reward = new QuestReward();

    [Header("UI")]
    [Tooltip("퀘스트 트래커에 표시할 순서 (낮을수록 위)")]
    public int SortOrder;
    [Tooltip("비고 (기획 메모용, 런타임 미사용)")]
    [TextArea(1, 3)]
    public string Notes;
}
