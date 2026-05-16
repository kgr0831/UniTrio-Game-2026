using UnityEngine;

public class SkillTreePanel : MonoBehaviour
{
    [Header("Skill Node Slots")]
    [SerializeField] private InventorySlot[] _skillNodes; // 3개 (Bash, AimedShot, ManaSpear)
    
    [Header("Skill Data")]
    [SerializeField] private SkillData[] _skillDataList; // 노드에 배치할 스킬 데이터
    
    [Header("Visual Settings")]
    [SerializeField] private float _emptyAlpha = 0.3f;  // 스킬이 빠졌을 때 alpha
    
    void Start()
    {
        InitializeNodes();
    }
    
    /// <summary>각 노드에 스킬 데이터 배치</summary>
    public void InitializeNodes()
    {
        if (_skillNodes == null || _skillDataList == null) return;
        
        for (int i = 0; i < _skillNodes.Length && i < _skillDataList.Length; i++)
        {
            if (_skillNodes[i] != null && _skillDataList[i] != null)
            {
                _skillNodes[i].slotType = SlotType.SkillTreeNode;
                _skillNodes[i].RefreshSlot(_skillDataList[i], 1);
            }
        }
    }
    
    /// <summary>특정 노드의 스킬이 빠졌음을 표시 (alpha 감소)</summary>
    public void MarkNodeAsEmpty(int nodeIndex)
    {
        if (_skillNodes == null || nodeIndex < 0 || nodeIndex >= _skillNodes.Length || _skillNodes[nodeIndex] == null) return;
        var icon = _skillNodes[nodeIndex].iconImage;
        if (icon != null)
        {
            var c = icon.color;
            c.a = _emptyAlpha;
            icon.color = c;
        }
    }
    
    /// <summary>특정 노드의 스킬이 돌아왔음을 표시 (alpha 복원)</summary>
    public void MarkNodeAsFilled(int nodeIndex)
    {
        if (_skillNodes == null || nodeIndex < 0 || nodeIndex >= _skillNodes.Length || _skillNodes[nodeIndex] == null) return;
        var icon = _skillNodes[nodeIndex].iconImage;
        if (icon != null)
        {
            var c = icon.color;
            c.a = 1f;
            icon.color = c;
        }
    }
    
    /// <summary>SkillData로 해당 노드 인덱스를 찾는 헬퍼</summary>
    public int FindNodeIndex(SkillData skill)
    {
        if (_skillDataList == null) return -1;
        for (int i = 0; i < _skillDataList.Length; i++)
        {
            if (_skillDataList[i] == skill) return i;
        }
        return -1;
    }
}
