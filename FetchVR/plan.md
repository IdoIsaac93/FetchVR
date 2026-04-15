# FetchVR Plan

## 项目目标

在 VR 场景中实现一只可交互的狗，完成以下流程：

1. 玩家持球并抛出
2. 狗找到球并叼回玩家附近
3. 玩家继续交互，进入下一轮

当前项目包含两部分：

- `BasicScene`：VR 游玩场景
- `Level1`：ML-Agents 训练场景

## 关键系统

- `FetchAgent`
  - 狗的 ML-Agent 行为
  - 负责观察、移动、捡球、回到目标点
- `FetchBall`
  - 球的物理、持球、抛球、附着到狗嘴等逻辑
- `FetchArea`
  - 训练区域、出生点、目标点、课程学习区域限制
- `FetchGameController`
  - 游戏模式下的流程控制
  - 管理持球、抛球、命令狗去捡、重置回合
- `GoalFollowCamera`
  - 游戏模式下让目标点跟随玩家

## 场景约定

### 训练场景

- 使用 `Level1`
- 多个 `TrainingArea` prefab 并行采样
- 基于 `spawnAreas` 做课程学习
- 训练时 `FetchArea.isGameMode = false`
- 训练时 `FetchBall.isGameMode = false`

### 游玩场景

- 使用 `BasicScene`
- 玩家通过 `FetchGameController` 抛球
- 狗使用导出的 `.onnx` 模型推理
- 游玩时 `FetchArea.isGameMode = true`
- 游玩时 `FetchBall.isGameMode = true`

## 模型方案

训练部分只保留三个最终模型：

### M1

- 目标：半数以上情况下完成取回
- 用途：基础可玩版本，允许偶发失败
- 特点：能完成核心流程，但在复杂路径下可能丢球或绕路

### M2

- 目标：绝大部分情况下完成取回
- 用途：主提交版本，供 VR 同学接入场景和交互
- 特点：在 8 字地形中也能较稳定地找到球并快速带回

### M3

- 目标：又快又准地完成取回
- 用途：高质量版本 / 最终优化版本
- 特点：在成功率保持较高的前提下，进一步优化完成时间、路线效率和回收精度

## 当前交付建议

- `BasicScene` 中保留两个可切换模型：
  - `M1-final.onnx`
  - `M2-final.onnx`
- `M2` 作为默认游玩模型
- `M1` 作为较弱版本，供分级体验或调试使用

## 测试与评估

- 使用固定测试集评估模型
- 测试集分为三档：
  - 简单：4 area
  - 中等：6 area
  - 困难：8 area
- 通过 `FetchEvaluationRunner` 运行固定 case

建议验收标准：

- `M1`：总通过率约 `50%+`
- `M2`：总通过率约 `85%+`
- `M3`：高通过率，同时平均完成时间进一步下降

## 近期工作

1. 提交 `M1-final` 和 `M2-final` 供 VR 部分接入
2. 在 `BasicScene` 中完成模型切换与流程联调
3. 继续从 `M3-base` 精修速度与精度

## 后续开发

### VR 交互接入

- 将 `M1` 和 `M2` 接入 `BasicScene`
- 与玩家抛球、捡回、宠物互动流程联调
- 确认 XR Rig、球、狗、目标点之间没有异常物理冲突

### 游戏流程完善

- 完善回合重置、异常恢复和卡住处理
- 根据游玩反馈调整抛球手感、拾取距离和回收判定
- 增加不同等级模型的切换入口，支持分级体验

### 模型迭代

- 基于 `M2` 继续训练 `M3`
- 重点优化复杂路径下的路线效率与完成时间
- 在固定测试集和真实游玩场景中同时验证泛化表现

### 工程整理

- 整理训练配置、模型文件和场景引用
- 保持训练场景与游玩场景职责分离
- 为后续同学补充模型使用说明和场景配置说明
## 2026-04-13 Update

### FetchGameController

- Removed the pet-to-complete step from `FetchGameController`.
- A fetch round now completes automatically when the dog reaches the goal and `OnFetchSuccess` fires.
- Training progress is granted immediately on successful return via `dogStatus.AddTraining(1)`.
- The dog run animation is now driven by Animator bool `Run`.
- `Run = true` when fetch starts.
- `Run = false` when the dog returns successfully or when the round is reset.

### VR Throw And Input

- Kept keyboard controls `T / F / R` for editor testing.
- Added VR controller input support to `FetchGameController`.
- Default VR mapping is:
- `TriggerButton` = throw ball
- `PrimaryButton` = command dog to fetch
- `SecondaryButton` = reset round
- Ball hold position now prefers the right XR controller and is placed in front of it using `xrHandOffset`.
- Added auto-assignment attempt for right controller transform by searching `XR Controller Right` in scene.
- Added fallback to XR device position/rotation if the controller transform is not assigned.

### VR Throw Physics

- VR throw direction now uses right controller orientation instead of the old keyboard-style throw angle.
- VR throw velocity now reads controller `deviceVelocity`.
- Added `xrThrowVelocityMultiplier` for tuning controller throw strength.
- Added `xrMinThrowSpeed` threshold so tiny/no hand motion does not produce a strong throw.
- Disabled fallback forward throw speed by default.
- If needed, fallback speed can be re-enabled with `useXrFallbackThrowSpeed`.

### XR Input Asset

- Updated `Assets/Samples/XR Interaction Toolkit/3.3.0/Starter Assets/XRI Default Input Actions.inputactions`.
- Moved right-hand `Jump` off `PrimaryButton`.
- Right-hand `Jump` is currently bound to `Primary2DAxisClick` so `PrimaryButton` can be used for fetch gameplay input.

## 2026-04-14 Update

### Goal Follow Fix

- Investigated the issue where `Goal` moved with `XR Origin (XR Rig)` in edit mode but stayed behind after entering play mode.
- Confirmed the problem was not caused by `TitleScreen` scene loading logic; `TitleScreen` only calls `SceneManager.LoadScene(1)`.
- Traced the runtime `Goal` in `BasicScene` back to `Assets/Scenes/Level1/XR Origin (XR Rig).prefab`.
- Found that this XR rig prefab's `Goal` object only had `Transform + BoxCollider` and was missing `GoalFollowCamera`.
- Reattached `GoalFollowCamera` to the prefab `Goal` and bound `xrCamera` to the rig's `Main Camera`.
- Added a fallback in `GoalFollowCamera` so it auto-resolves `Camera.main` if `xrCamera` is not assigned in the Inspector.

### Debug Overlay Cleanup

- Investigated the `Fetch Game - Round 0` text shown at the top-left during play mode.
- Confirmed it was not coming from a scene `Canvas` or TMP object.
- Found the overlay was drawn by `FetchGameController.OnGUI()` using Unity IMGUI.
- Commented out `OnGUI()` in `FetchGameController` to remove the temporary debug HUD without changing fetch gameplay logic.

### UI Binding Support

- Added `Assets/Scripts/UI/LevelTextBinder.cs`.
- `LevelTextBinder` exposes `SetLevelText(int level)` for binding `DogStatusController.onLevelChanged` to a TMP text object in the Inspector.
- Documented the existing event-based UI hookup path:
- `onMoodPercentChanged` can drive a `Slider.value` directly when the slider range is `0..1`.
- `onLevelChanged` should use a small binder script to convert the `int` level into display text.
