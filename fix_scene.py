import os

src_path = r'Assets/Scenes/Unitro.unity'
dst_path = r'Assets/Scenes/Unitro2.unity'

# Read golden block from source (Line 12500 to 12800)
# Line numbers are 1-based, so 12499 to 12800 in 0-based
with open(src_path, 'r', encoding='utf-8') as f:
    lines = f.readlines()
    golden_block = lines[12499:12800]

# Append to destination
with open(dst_path, 'a', encoding='utf-8') as f:
    f.write('\n')
    f.writelines(golden_block)

print(f"Successfully migrated {len(golden_block)} lines to {dst_path}")
