import os

src_path = r'Assets/Scenes/Unitro.unity'
dst_path = r'Assets/Scenes/Unitro2.unity'

target_ids = [
    '1530210928', '1530210929', '1530210930', # Manager
    '1732515844', '2018348923', '740226020',  # Slots
    '1763866057', '1136699461', '1759815806',
    '321523035', '1879057685'
]

with open(src_path, 'r', encoding='utf-8') as f:
    src_lines = f.readlines()

with open(dst_path, 'r', encoding='utf-8') as f:
    dst_content = f.read()

to_append = []

def extract_block(lines, target_id):
    start = -1
    for i, line in enumerate(lines):
        if line.startswith('--- !u!') and f'&{target_id}' in line:
            start = i
            break
    if start == -1: return None
    
    block = [lines[start]]
    for i in range(start + 1, len(lines)):
        if lines[i].startswith('--- !u!'):
            break
        block.append(lines[i])
    return block

for tid in target_ids:
    # Check if ID already exists in destination
    if f'&{tid}' in dst_content:
        print(f"Skipping ID {tid}: Already exists in {dst_path}")
        continue
    
    block = extract_block(src_lines, tid)
    if block:
        to_append.extend(block)
        print(f"Prepared ID {tid} for injection")
    else:
        print(f"Warning: ID {tid} not found in {src_path}")

if to_append:
    with open(dst_path, 'a', encoding='utf-8') as f:
        f.write('\n')
        f.writelines(to_append)
    print(f"Successfully injected {len(to_append)} lines to {dst_path}")
else:
    print("Nothing to inject.")
