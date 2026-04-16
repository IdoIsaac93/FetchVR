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

## 2026-04-15 Update

### Title Screen Panels

- Added `Assets/Scripts/UI/ToturialScreen.cs` for paged tutorial content inside a Canvas panel.
- `ToturialScreen` supports `Show()`, `Hide()`, `Close()`, `NextPage()`, `PreviousPage()`, and `GoToPage(int)`.
- Tutorial content is not hardcoded in script; pages are configured from the Inspector via a serialized list.
- Added `Assets/Scripts/UI/SettingScreen.cs` as a minimal settings panel controller.
- `SettingScreen` currently supports `Show()`, `Hide()`, and `Close()` only.

### TitleScreen Integration

- Extended `Assets/Scripts/UI/TitleScreen.cs` with a `ShowTutorial()` flow that opens the scene's `ToturialScreen`.
- Extended `Assets/Scripts/UI/TitleScreen.cs` with a `ShowSetting()` flow that opens the scene's `SettingScreen`.
- Both panel entry points first use Inspector references and then fall back to `FindFirstObjectByType(..., FindObjectsInactive.Include)`.
- Added warning logs when the requested panel controller cannot be found in the scene.

### UI Wiring Notes

- No scene objects or bindings were auto-created by script changes.
- Tutorial and settings buttons still need to be wired in the Inspector to `TitleScreen.ShowTutorial()` and `TitleScreen.ShowSetting()`.
- Panel close buttons should be wired to `ToturialScreen.Close()` and `SettingScreen.Close()`.

## 2026-04-16 Update

### 音频系统新增

- 新增 `Assets/Scripts/Dog/DogAudioController.cs`，用于统一管理狗的音效播放。
- `DogAudioController` 将狗的音频分成两类：
- 循环声道：用于 `idle` 状态下的呼吸声。
- 单次声道：用于 `fetch` 开始时的狗叫、`pet` 时的撒娇声、`feed` 时的吃饭声。
- `fetch` 的狗叫支持从 `fetchBarkClips` 中随机选择，每次触发时随机播放一个可用音频。
- `idle` 呼吸声会在狗进入跑动或播放一次性音效时自动暂停，待单次音效播放完后再恢复。

### 已接入的触发点

- 在 `Assets/Scripts/Dog/FetchGameController.cs` 中：
- `CommandDogFetch()` 里已接入 `dogAudioController.PlayFetchBark()`。
- 这对应 `BasicScene` 游戏模式下，玩家命令狗开始去捡球时播放狗叫。
- 在 `Assets/Scripts/Dog/DogTrainingSession.cs` 中：
- `NotifyBallThrown()` 里已接入 `dogAudioController.PlayFetchBark()`。
- 这对应训练/投球流程里，狗开始执行 fetch 时播放狗叫。
- 在 `Assets/Scripts/Dog/DogPetInteraction.cs` 中：
- `PerformPet()` 成功执行后会调用 `dogAudioController.PlayPetSound()`。
- 这对应玩家抚摸成功后播放撒娇声。
- 在 `Assets/Scripts/Dog/DogFeedAction.cs` 中：
- `FeedDog()` 执行后会调用 `dogAudioController.PlayFeedSound()`。
- 这对应玩家点击喂食后播放吃饭声。

### BGM 系统新增

- 新增 `Assets/Scripts/UI/BgmController.cs`，用于管理主菜单和游戏场景的背景音乐。
- `BgmController` 使用 `DontDestroyOnLoad(gameObject)` 常驻，不会因为切场景而被销毁。
- 当前默认按场景名切换 BGM：
- `TitleScreen` 播放主菜单 BGM。
- `BasicScene` 播放游戏场景 BGM。
- 如果进入未单独配置的场景，则优先回退到游戏场景 BGM；若没有则回退到主菜单 BGM。
- `BgmController` 会在 `SceneManager.sceneLoaded` 时自动检查当前场景并切换正确的背景音乐。

### SettingScreen 功能扩展

- 已扩展 `Assets/Scripts/UI/SettingScreen.cs`，不再只是显示/隐藏设置面板。
- 目前 `SettingScreen` 已新增以下接口：
- `SetMasterVolume(float)`：设置全局总音量，直接作用于 `AudioListener.volume`。
- `GetMasterVolume()`：读取当前保存的总音量。
- `SetBgmVolume(float)`：设置背景音乐音量，只影响 `BgmController`。
- `GetBgmVolume()`：读取当前保存的 BGM 音量。
- 音量设置已使用 `PlayerPrefs` 保存：
- 总音量键：`Audio.MasterVolume`
- BGM 音量键：`Audio.BgmVolume`
- 下次进入游戏时会自动读取上次保存的音量。

### 当前资源情况说明

- 当前仓库内已经能确认存在 3 个狗叫音频，位于 `Assets/Audio/SE/`：
- `dragon-studio-barking-dog-cute-sound-463192.mp3`
- `dragon-studio-dog-bark-03-472362.mp3`
- `dragon-studio-dog-bark-04-472385.mp3`
- 当前代码已支持随机狗叫，可以直接把以上音频拖入 `DogAudioController.fetchBarkClips`。
- 当前仓库内尚未确认到“呼吸声”“撒娇声”“吃饭声”的现成素材，因此代码先保留了 Inspector 挂载入口，后续导入素材后可直接接入，无需再次改脚本。

### 之后如何使用今天写的代码

### 1. 狗音效的挂载方式

- 在狗对象上挂载 `DogAudioController`。
- 如果狗对象上没有现成的 `AudioSource`，脚本会在运行时自动补一个循环声道和一个单次声道。
- 在 Inspector 中配置：
- `idleBreathingClip`：拖入狗 idle 时要循环播放的呼吸声。
- `fetchBarkClips`：拖入多个狗叫声，脚本会随机播放。
- `petClips`：拖入撒娇声，可以一个或多个。
- `feedClips`：拖入吃饭声，可以一个或多个。
- 如果 `DogFeedAction`、`DogPetInteraction`、`FetchGameController`、`DogTrainingSession` 没有手动指定 `DogAudioController`，脚本会优先尝试从同对象或 `fetchAgent` 对象自动寻找。

### 2. BGM 的挂载方式

- 在 `TitleScreen` 场景中新建一个对象，例如 `BGM Manager`。
- 给这个对象挂载 `BgmController`。
- 在 `BgmController` Inspector 中配置：
- `titleBgm`：拖入主菜单 BGM。
- `gameplayBgm`：拖入游戏场景 BGM。
- `titleSceneName` 默认是 `TitleScreen`。
- `gameplaySceneName` 默认是 `BasicScene`。
- 如果以后你修改了场景名，需要同步改这里的两个字符串。

### 3. 设置界面音量滑条的绑定方式

- 在设置界面的总音量 Slider 上绑定 `SettingScreen.SetMasterVolume(float)`。
- 在设置界面的 BGM 音量 Slider 上绑定 `SettingScreen.SetBgmVolume(float)`。
- 建议两个 Slider 的取值范围都设为 `0 ~ 1`。
- 如果 UI 需要在打开设置面板时显示当前保存的值：
- 总音量 Slider 应读取 `SettingScreen.GetMasterVolume()`。
- BGM 音量 Slider 应读取 `SettingScreen.GetBgmVolume()`。
- 如果以后还要区分“音效音量”和“BGM 音量”，可以继续沿用当前结构，在 `SettingScreen` 和对应控制器里新增 `SE Volume` 的读写接口。

### 4. 后续扩展建议

- 如果后续要让按钮点击音效、UI 悬停音效、环境音效也进入统一管理，建议再拆一个专门的 `SfxController`。
- 如果后续需要在场景切换时淡入淡出 BGM，建议直接在 `BgmController` 里加入 `Coroutine` 做音量渐变，而不是把切歌逻辑分散到 `TitleScreen` 或其他 UI 脚本。
- 如果后续要让设置面板打开时自动刷新 Slider 的当前值，可以继续扩展 `SettingScreen`，在 `Show()` 时同步 UI 控件状态。
