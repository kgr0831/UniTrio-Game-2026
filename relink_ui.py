import os

path = r'Assets/Scenes/Unitro2.unity'

# Target GameObject IDs to fix Layer (from 0 to 5)
target_gameobject_ids = [
    '1526437123', # InventoryPanel
    '1530210928', # QuickSlotManager
    '856308689',  # DragManager
    '1930110838', # DragIcon
    '1820404872', # SkillSlot PrefabInstance (Wait! I'll target the GameObject)
]

# We need the GameObject ID for SkillSlot (u!1)
# I'll search for it dynamically in the script
skill_slot_name = "SkillSlot"

with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

for i, line in enumerate(lines):
    if f'm_Name: {skill_slot_name}' in line:
        for j in range(i, i-20, -1):
            if lines[j].startswith('--- !u!1'):
                target_gameobject_ids.append(lines[j].split('&')[1].strip())
                break

print(f"Total GameObjects to check layer: {len(target_gameobject_ids)}")

new_lines = []
for i in range(len(lines)):
    line = lines[i]
    
    # fix Layer on specific GameObjects
    if line.startswith('--- !u!1'):
        gid = line.split('&')[1].strip()
        if gid in target_gameobject_ids:
            for k in range(i+1, i+15):
                if k < len(lines) and 'm_Layer: 0' in lines[k]:
                    lines[k] = lines[k].replace('m_Layer: 0', 'm_Layer: 5')
                    print(f"Update: Fixed Layer to 5 for GameObject ID {gid}")
                    break
    
    # fix RaycastTarget on all Images (excluding DragIcon)
    if 'm_RaycastTarget: 0' in line and ('UnityEngine.UI::UnityEngine.UI.Image' in lines[i-3] or 'UnityEngine.UI::UnityEngine.UI.Image' in lines[i-5]):
        is_drag_icon = False
        for k in range(i, i-15, -1):
            if '&1930110839' in lines[k]: # Known DragIcon Image ID
                is_drag_icon = True
                break
        
        if not is_drag_icon:
            line = line.replace('m_RaycastTarget: 0', 'm_RaycastTarget: 1')
            print(f"Update: Fixed RaycastTarget to 1 at line {i+1}")

    new_lines.append(line)

with open(path, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print("Final UI stabilization completed.")
