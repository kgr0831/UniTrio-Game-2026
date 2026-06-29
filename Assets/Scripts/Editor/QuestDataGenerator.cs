using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class QuestDataGenerator
{
    [MenuItem("Tools/Generate Quest Data")]
    public static void Execute()
    {
        // 1. 아이템 생성
        CreateItem<WeaponData>(1001, "나무 도구", "간단한 나무 도구", "WoodenTool_Item");
        CreateItem<GadgetData>(1002, "나무 갑옷", "나무로 만든 조잡한 갑옷", "WoodenArmor_Item");
        CreateItem<GadgetData>(1003, "보스 추적기", "보스의 위치를 알려준다", "BossTracker_Item");
        CreateItem<ConsumableData>(1004, "이동 스크롤", "마을로 이동한다", "TeleportScroll_Item");
        CreateItem<IngredientData>(1005, "나무 조각", "장인의 재료", "WoodPiece_Item");
        CreateItem<IngredientData>(1006, "정령석", "정령술사의 돌", "SpiritStone_Item");
        CreateItem<IngredientData>(1007, "열쇠", "어딘가의 문을 연다", "DungeonKey_Item");

        // 2. 퀘스트 생성
        CreateQuest("TUT_01", "돌 검 만들기", QuestCategory.Tutorial, "튜토리얼: 돌 검을 만드세요.",
            new QuestObjectiveData[] { new QuestObjectiveData { Type = QuestObjectiveType.Craft, Description = "돌 검 만들기", RequiredAmount = 1, TargetId = "Stone Sword" } },
            new QuestReward { Items = new List<QuestRewardItem> { new QuestRewardItem { Item = LoadItem("WoodenTool_Item"), Count = 1 } } },
            new string[] { }, true, 10, "튜토리얼 1", null, null, null);

        CreateQuest("TUT_02", "곰 3마리 잡기", QuestCategory.Tutorial, "튜토리얼: 곰을 잡으세요.",
            new QuestObjectiveData[] { new QuestObjectiveData { Type = QuestObjectiveType.Kill, Description = "곰 처치", RequiredAmount = 1, TargetId = "Bear" } },
            new QuestReward { Items = new List<QuestRewardItem> { new QuestRewardItem { Item = LoadItem("WoodenArmor_Item"), Count = 1 } }, UnlockQuestIds = new List<string> { "MAIN_01" } },
            new string[] { "TUT_01" }, false, 20, "튜토리얼 2", null, null, null);

        CreateQuest("MAIN_01", "마을 찾기", QuestCategory.Main, "마을을 찾으세요.",
            new QuestObjectiveData[] { new QuestObjectiveData { Type = QuestObjectiveType.Reach, Description = "마을 발견", RequiredAmount = 1, TargetId = "Village" } },
            new QuestReward { Items = new List<QuestRewardItem> { new QuestRewardItem { Item = LoadItem("TeleportScroll_Item"), Count = 1 } }, UnlockQuestIds = new List<string> { "MAIN_02", "MAIN_03" } },
            new string[] { "TUT_02" }, true, 30, "메인 1", null, null, null);

        CreateQuest("MAIN_02", "던전 탐사", QuestCategory.Main, "던전을 탐사하세요.",
            new QuestObjectiveData[] { new QuestObjectiveData { Type = QuestObjectiveType.Explore, Description = "던전 입장", RequiredAmount = 1, TargetId = "Dungeon" } },
            new QuestReward { Gold = 600 },
            new string[] { "MAIN_01" }, true, 40, "메인 2", null, null, null);

        CreateQuest("MAIN_03", "골렘 처치", QuestCategory.Main, "보스 골렘을 처치하세요.",
            new QuestObjectiveData[] { new QuestObjectiveData { Type = QuestObjectiveType.Kill, Description = "골렘 처치", RequiredAmount = 1, TargetId = "Golem" } },
            new QuestReward { Gold = 1000 },
            new string[] { "MAIN_01" }, true, 50, "메인 3", null, null, null);

        CreateQuest("NPC_01", "열쇠 찾기", QuestCategory.Npc, "상인의 부탁으로 열쇠를 찾으세요.",
            new QuestObjectiveData[] { new QuestObjectiveData { Type = QuestObjectiveType.Collect, Description = "열쇠 찾기", RequiredAmount = 1, TargetId = "열쇠" } },
            new QuestReward { },
            new string[] { "MAIN_01" }, false, 60, "NPC 1", "Merchant", null, null);

        CreateQuest("NPC_02", "나무 조각 모으기", QuestCategory.Npc, "장인에게 나무 조각 30개를 가져다주세요.",
            new QuestObjectiveData[] { new QuestObjectiveData { Type = QuestObjectiveType.Collect, Description = "나무 조각 수집", RequiredAmount = 30, TargetId = "나무 조각" } },
            new QuestReward { Gold = 1500 },
            new string[] { "MAIN_01" }, false, 70, "NPC 2", "Artisan", null, null);

        CreateQuest("NPC_03", "정령 소환", QuestCategory.Npc, "정령술사에게 정령석을 가져다주세요.",
            new QuestObjectiveData[] { new QuestObjectiveData { Type = QuestObjectiveType.Collect, Description = "정령석 구매", RequiredAmount = 1, TargetId = "정령석" } },
            new QuestReward { Items = new List<QuestRewardItem> { new QuestRewardItem { Item = LoadItem("SpiritStone_Item"), Count = 3 } } },
            new string[] { "MAIN_01" }, false, 80, "NPC 3", "Summoner", null, null);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[QuestDataGenerator] Assets created successfully.");
    }

    private static void CreateItem<T>(int id, string name, string desc, string fileName) where T : ItemData
    {
        string path = $"Assets/Data/Items/{fileName}.asset";
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
        if (!AssetDatabase.IsValidFolder("Assets/Data/Items"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            AssetDatabase.CreateFolder("Assets/Data", "Items");
        }
        
        T item = ScriptableObject.CreateInstance<T>();
        item.Id = id;
        item.Name = name;
        item.Description = desc;
        
        AssetDatabase.CreateAsset(item, path);
    }

    private static ItemData LoadItem(string fileName)
    {
        string path = $"Assets/Data/Items/{fileName}.asset";
        return AssetDatabase.LoadAssetAtPath<ItemData>(path);
    }

    private static void CreateQuest(string id, string name, QuestCategory cat, string desc, QuestObjectiveData[] objectives, QuestReward reward, string[] prereqs, bool autoStart, int sortOrder, string notes, string giverNpc, string startDial, string compDial)
    {
        string path = $"Assets/Data/Quests/{id}.asset";
        if (AssetDatabase.LoadAssetAtPath<QuestSO>(path) != null) return;
        if (!AssetDatabase.IsValidFolder("Assets/Data/Quests"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
            AssetDatabase.CreateFolder("Assets/Data", "Quests");
        }

        QuestSO quest = ScriptableObject.CreateInstance<QuestSO>();
        quest.QuestId = id;
        quest.QuestName = name;
        quest.Category = cat;
        quest.Description = desc;
        quest.Objectives = new List<QuestObjectiveData>(objectives);
        quest.Reward = reward;
        quest.PrerequisiteQuestIds = new List<string>(prereqs);
        quest.AutoStart = autoStart;
        quest.SortOrder = sortOrder;
        quest.Notes = notes;
        quest.GiverNpcName = giverNpc;
        quest.StartDialogueId = startDial;
        quest.CompleteDialogueId = compDial;

        AssetDatabase.CreateAsset(quest, path);
    }
}
