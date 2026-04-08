import os

path = r'Assets/Scenes/Unitro2.unity'

# SkillSlot Data from Unitro.unity
skill_slot_rect_transform = """--- !u!224 &1170640821 stripped
RectTransform:
  m_CorrespondingSourceObject: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
  m_PrefabInstance: {fileID: 1820404872}
  m_PrefabAsset: {fileID: 0}
"""

skill_slot_prefab_instance = """--- !u!1001 &1820404872
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {fileID: 1522120781}
    m_Modifications:
    - target: {fileID: 2814023477388444962, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: currentData
      value: 
      objectReference: {fileID: 11400000, guid: 3fb20573d3c2f1b48b8d05ed765cc787, type: 2}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_Pivot.x
      value: 0.5
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_Pivot.y
      value: 0.5
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_AnchorMax.x
      value: 0.5
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_AnchorMax.y
      value: 0.5
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_AnchorMin.x
      value: 0.5
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_AnchorMin.y
      value: 0.5
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_SizeDelta.x
      value: 100
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_SizeDelta.y
      value: 100
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_LocalPosition.x
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_LocalPosition.y
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_LocalPosition.z
      value: -12.6974
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_LocalRotation.w
      value: 1
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_LocalRotation.x
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_LocalRotation.y
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_LocalRotation.z
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_AnchoredPosition.x
      value: 672
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_AnchoredPosition.y
      value: 67.15875
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_LocalEulerAnglesHint.x
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_LocalEulerAnglesHint.y
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 7605474185703999354, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_LocalEulerAnglesHint.z
      value: 0
      objectReference: {fileID: 0}
    - target: {fileID: 8119696712580267461, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
      propertyPath: m_Name
      value: SkillSlot
      objectReference: {fileID: 0}
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {fileID: 100100000, guid: fe84177f30fc9324ebe71210d9094d47, type: 3}
"""

with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

# Check for duplicates before injection
if '--- !u!1001 &1820404872' in "".join(lines):
    print("SkillSlot already exists. Skipping injection.")
else:
    # Inject at the end
    lines.append(skill_slot_rect_transform)
    lines.append(skill_slot_prefab_instance)
    
    # Update Canvas m_Children
    # Canvas RectTransform ID in Unitro2 is 1522120781
    canvas_found = False
    for i in range(len(lines)):
        if '--- !u!224 &1522120781' in lines[i]:
            canvas_found = True
            for j in range(i+1, i+20):
                if 'm_Children:' in lines[j]:
                    # Build and insert the new child line
                    new_child = f"  - {{fileID: 1170640821}}\n"
                    lines.insert(j+1, new_child)
                    print("Added SkillSlot to Canvas m_Children.")
                    break
            break
            
    if not canvas_found:
        print("Error: Main Canvas RectTransform not found in Unitro2.unity.")

    with open(path, 'w', encoding='utf-8') as f:
        f.writelines(lines)
    print("Injection successful.")
