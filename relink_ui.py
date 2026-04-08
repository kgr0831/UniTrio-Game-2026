import os

path = r'Assets/Scenes/Unitro2.unity'

# IDs we found/identified
target_gameobject_ids = [
    '1526437123', # InventoryPanel
    '1530210928', # QuickSlotManager
    '856308689',  # DragManager
    '1930110838', # DragIcon
]

# We also need to find the 8 Slots GameObject IDs
# I'll use a dynamic search for the slots
slot_names = ["QuickSlot_Test (0)", "QuickSlot_Test (1)", "QuickSlot_Test (2)", "QuickSlot_Test (3)", 
              "QuickSlot_Test (4)", "QuickSlot_Test (5)", "QuickSlot_Test (6)", "QuickSlot_Test (7)"]

with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

for name in slot_names:
    for i, line in enumerate(lines):
        if f'm_Name: {name}' in line:
            for j in range(i, i-20, -1):
                if lines[j].startswith('--- !u!1'):
                    target_gameobject_ids.append(lines[j].split('&')[1].strip())
                    break

print(f"Total GameObjects to fix layer: {len(target_gameobject_ids)}")

# Apply Layer 5 (UI) to these GameObjects and RaycastTarget to Images
new_lines = []
for i in range(len(lines)):
    line = lines[i]
    is_affected = False
    
    # fix Layer on GameObjects
    if line.startswith('--- !u!1'):
        gid = line.split('&')[1].strip()
        if gid in target_gameobject_ids:
            # Look ahead for m_Layer
            for k in range(i+1, i+15):
                if k < len(lines) and 'm_Layer: 0' in lines[k]:
                    lines[k] = lines[k].replace('m_Layer: 0', 'm_Layer: 5')
                    print(f"Fixed Layer to 5 for GameObject ID {gid}")
                    break
    
    # fix RaycastTarget on MonoBehaviours (Images) - broadly for safety
    if 'm_RaycastTarget: 0' in line and 'm_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image' in lines[i-3] or 'm_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image' in lines[i-5]:
        # Don't fix DragIcon's raycast target, it should stay 0
        is_drag_icon_comp = False
        for k in range(i, i-15, -1):
            if '&1930110839' in lines[k]: # Known DragIcon Image ID
                is_drag_icon_comp = True
                break
        
        if not is_drag_icon_comp:
            line = line.replace('m_RaycastTarget: 0', 'm_RaycastTarget: 1')
            print(f"Fixed RaycastTarget to 1 at line {i+1}")

    new_lines.append(line)

with open(path, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print("Surgery completed successfully.")
