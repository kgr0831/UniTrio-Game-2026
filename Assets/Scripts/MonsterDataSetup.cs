#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[ExecuteInEditMode]
public class MonsterDataSetup : MonoBehaviour
{
    private void Awake()
    {
        SetupData();
    }

    public static void SetupData()
    {
        string assetPath = "Assets/Resources/Data/Monster/Cow.asset";
        MonsterData data = AssetDatabase.LoadAssetAtPath<MonsterData>(assetPath);
        if (data == null)
        {
            Debug.LogError("Monster asset not found at " + assetPath);
            return;
        }

        data.MonsterName = "Cow";
        data.Type = MonsterType.Neutral; // Assuming MonsterType.Neutral exists
        data.MaxHP = 150f;
        data.ATK = 0f;
        data.DEF = 20f;
        data.Speed = 65f;
        data.DetectionRadius = 6f;

        data.AnimController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Resources/Animations/Cow/Cow_AnimatorController.controller");

        data.DropTable = new DropEntry[3];

        // 1. AnimalMeat
        data.DropTable[0] = new DropEntry
        {
            Item = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Data/ItemData/FoodData/AnimalMeat.asset"),
            DropChance = 1f,
            MaxCount = 3,
            CountWeights = new float[] { 0f, 0.7f, 0.3f } // 1개: 0%, 2개: 70%, 3개: 30%
        };

        // 2. Leather
        data.DropTable[1] = new DropEntry
        {
            Item = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Data/ItemData/IngredientData/Leather.asset"),
            DropChance = 1f,
            MaxCount = 2,
            CountWeights = new float[] { 0.5f, 0.5f } // 1개: 50%, 2개: 50%
        };

        // 3. Bone
        data.DropTable[2] = new DropEntry
        {
            Item = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Resources/Data/ItemData/IngredientData/Bone.asset"),
            DropChance = 1f,
            MaxCount = 2,
            CountWeights = new float[] { 0.5f, 0.5f } // 1개: 50%, 2개: 50%
        };

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log("Monster Data Setup Complete!");
    }
}
#endif
