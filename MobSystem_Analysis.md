# Mob System Comprehensive Analysis

This document provides a technical breakdown of the modular Monster System implemented in the project, following SOLID principles and performance-optimized Unity patterns.

## 1. Core Architecture: Data-Driven Design
The system is built on a **Data-Logic Separation** model using Unity's `ScriptableObject`.

### 1.1 Data Layer (`Assets/Scripts/Monster/Data`)
- **`MonsterData.cs`**: `Assets/Scripts/Monster/Data/MonsterData.cs` - The source of truth for all monster types.
- **`MonsterRuntimeData.cs`**: `Assets/Scripts/Monster/Data/MonsterRuntimeData.cs` - holds the current state.
- **`AttackShapeData.cs`**: `Assets/Scripts/Monster/Data/AttackShapeData.cs` - Defines attack boundaries.
- **`DropEntry.cs`**: `Assets/Scripts/Monster/Data/DropEntry.cs` - Defines individual items in a droptable.

### 1.2 Base Layer (`Assets/Scripts/Monster/Base`)
- **`MonsterBase.cs`**: `Assets/Scripts/Monster/Base/MonsterBase.cs` - The abstract core lifecycle.
- **Sub-Types**: 
  - `NeutralMonster.cs`: `Assets/Scripts/Monster/Base/NeutralMonster.cs`
  - `HostileMonster.cs`: `Assets/Scripts/Monster/Base/HostileMonster.cs`
  - `BossMonster.cs`: `Assets/Scripts/Monster/Base/BossMonster.cs`

## 2. Artificial Intelligence: Behavior Trees (BT)
- **Core Node**: `Assets/Scripts/Monster/BT/BTNode.cs` - Lightweight, GC-free implementation.
- **AI Logic**: Logic branching using Selector/Sequence patterns found in the base monster classes.

## 3. High-Performance Management (`Assets/Scripts/Monster/Manager`)
- **`MobManager.cs`**: `Assets/Scripts/Monster/Manager/MobManager.cs` - Global tracking singleton.
- **`MobSpawner.cs`**: `Assets/Scripts/Monster/Manager/MobSpawner.cs` - Procedural/Targeted instantiation.
- **`FirstKillRegistry.cs`**: `Assets/Scripts/Monster/Manager/FirstKillRegistry.cs` - Tracks first-time kills.
- **`FirstKillBonusApplier.cs`**: `Assets/Scripts/Monster/Manager/FirstKillBonusApplier.cs` - Applies permanent stat bonuses.

## 4. Specialized Systems (`Assets/Scripts/Monster/Systems`)
- **`DetectionSystem.cs`**: `Assets/Scripts/Monster/Systems/DetectionSystem.cs` - Optimized player detection.
- **`MonsterNavigator.cs`**: `Assets/Scripts/Monster/Systems/MonsterNavigator.cs` - Encapsulates movement logic.
- **`MonsterLootDropper.cs`**: `Assets/Scripts/Monster/Systems/MonsterLootDropper.cs` - Probability-based drop logic.
- **`MonsterAttackHandler.cs`**: `Assets/Scripts/Monster/Systems/MonsterAttackHandler.cs` - Hitbox validation.
- **`WanderSystem.cs`**: `Assets/Scripts/Monster/Systems/WanderSystem.cs` - Idle/Patrol behaviors.
- **`MonsterAnimatorController.cs`**: `Assets/Scripts/Monster/Systems/MonsterAnimatorController.cs` - Directional animation logic.
- **`HitStaggerHandler.cs`**: `Assets/Scripts/Monster/Systems/HitStaggerHandler.cs` - Visual feedback on hit.

## 5. Performance Optimizations
- **No LINQ**: All loops use `for` or `foreach` to prevent runtime Garbage Collection.
- **sqrMagnitude**: Distance comparisons use squared magnitude to avoid expensive square root calculations.
- **Decoupled Events**: Uses event-based triggers for damage and death to minimize `Update()` loop overhead.
