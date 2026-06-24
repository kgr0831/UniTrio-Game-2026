using UnityEngine;

[CreateAssetMenu(fileName = "MapObjectData", menuName = "Map/Object Data")]
public class MapObjectData : ScriptableObject
{
    public string objectName;
    public float maxHealth = 100f;

    [Header("비주얼 설정")]
    public Sprite visualSprite; // 프리팹 대신 스프라이트를 직접 할당

    [Header("인디케이터 설정")]
    public float indicatorWidth = 1f;
    public float indicatorLength = 1f;
    public float indicatorOffset = 0.5f;
    public Sprite indicatorSprite; 
}