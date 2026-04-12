#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class CowDataSetup : MonoBehaviour
{
    private void Awake()
    {
        SetupData();
    }

    public static void SetupData()
    {
        string assetPath = "Assets/Resources/Data/Monster/Cow.asset";
        MonsterData cowData = AssetDatabase.LoadAssetAtPath<MonsterData>(assetPath);
        if (cowData == null)
        {
            Debug.LogError("Cow.asset not found at " + assetPath);
            return;
        }

        cowData.MonsterName = "Cow";
        cowData.Type = MonsterType.Neutral; // Assuming MonsterType.Neutral exists
        cowData.MaxHP = 150f;
        cowData.ATK = 0f;
        cowData.DEF = 20f;
        cowData.Speed = 65f;
        cowData.DetectionRadius = 6f;

        cowData.AnimController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Resources/Animations/Cow/Cow_AnimatorController.controller");

        cowData.DropTable = new DropEntry[3];

        // 1. AnimalMeat
        cowData.DropTable[0] = new DropEntry
        {
            Item = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Data/ItemData/FoodData/AnimalMeat.asset"),
            DropChance = 1f,
            MaxCount = 3,
            CountWeights = new float[] { 0f, 0.7f, 0.3f } // 1개: 0%, 2개: 70%, 3개: 30%
        };

        // 2. Leather
        cowData.DropTable[1] = new DropEntry
        {
            Item = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Data/ItemData/IngredientData/Leather.asset"),
            DropChance = 1f,
            MaxCount = 2,
            CountWeights = new float[] { 0.5f, 0.5f } // 1개: 50%, 2개: 50%
        };

        // 3. Bone
        cowData.DropTable[2] = new DropEntry
        {
            Item = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Data/ItemData/IngredientData/Bone.asset"),
            DropChance = 1f,
            MaxCount = 2,
            CountWeights = new float[] { 0.5f, 0.5f } // 1개: 50%, 2개: 50%
        };

        EditorUtility.SetDirty(cowData);
        AssetDatabase.SaveAssets();
        Debug.Log("Cow Data Setup Complete!");
    }
}
#endif
