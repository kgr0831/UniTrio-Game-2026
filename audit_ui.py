import os

path = r'Assets/Scenes/Unitro2.unity'

target_names = ["InventoryPanel", "QuickSlotManager", "DragManager", "DragIcon"]

with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

def get_gameobject_id(name):
    for i, line in enumerate(lines):
        if f'm_Name: {name}' in line:
            # Look backwards for the GameObject ID
            for j in range(i, i-20, -1):
                if line.startswith('--- !u!1'): # GameObject
                    return line.split('&')[1].strip()
                if lines[j].startswith('--- !u!1'):
                    return lines[j].split('&')[1].strip()
    return None

results = []

for name in target_names:
    for i, line in enumerate(lines):
        if f'm_Name: {name}' in line:
            # Find layer and active state
            layer = "Unknown"
            active = "Unknown"
            for j in range(i-10, i+15):
                if 0 <= j < len(lines):
                    if 'm_Layer:' in lines[j]: layer = lines[j].strip()
                    if 'm_IsActive:' in lines[j]: active = lines[j].strip()
            results.append(f"Object: {name} | {layer} | {active}")

print("\n".join(results))

# Now check Raycast Targets broadly
raycast_off = []
for i, line in enumerate(lines):
    if 'm_RaycastTarget: 0' in line:
        # Check if it's an Image component (u!114)
        for j in range(i, i-15, -1):
            if '--- !u!114 &' in lines[j]:
                raycast_off.append(f"Line {i+1}: Raycast Target OFF on Component {lines[j].strip()}")
                break

print(f"\nFound {len(raycast_off)} components with Raycast Target OFF.")
if len(raycast_off) > 0:
    print("Example: " + raycast_off[0])
