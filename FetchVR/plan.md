# FetchVR ML-Agents Fetch Training Plan

## 1. 目标

在 `Level1` 场景的 9 个并行模块中训练 1 个统一行为的宠物 agent，使其完成完整的 `fetch` 流程：

1. 在随机位置找到 tennis ball
2. 接触 ball 后切换任务阶段
3. 找到随机生成的 `goal`
4. 回到 `goal` 并完成回收流程

最终导出 3 个可替换的模型版本：

- `Pet_v1_Novice`
- `Pet_v2_Intermediate`
- `Pet_v3_Smart`

这 3 个模型必须共享同一套 observation 和 action 定义，只通过训练难度和训练时长区分智能水平。

## 2. 当前项目状态总结

当前代码位于：

- `Assets/Scripts/MachineLearning/FetchAgent.cs`
- `Assets/Scripts/MachineLearning/FetchBall.cs`
- `Assets/Scripts/MachineLearning/FetchArea.cs`

当前行为存在以下问题：

1. `FetchAgent` 没有明确的阶段状态，缺少“找球”和“回目标”两个阶段的切换逻辑。
2. `CollectObservations()` 目前只有极少量向量信息，不足以支撑稳定训练。
3. 当前成功条件过于宽松，只要碰到 `goal` 就结束，没有校验是否已经拿到 ball。
4. 奖励过于稀疏，容易导致训练缓慢或学到投机策略。
5. 预设自带 3 个 `RayPerceptionSensor3D`，但参数来自示例场景，并不完全适合 fetch 任务。

## 3. 总体修改策略

采用以下训练思路：

1. 保留现有 3 个 `RayPerceptionSensor3D`
2. 新增必要的向量观察，不只依赖 ray
3. 把环境改成明确的两阶段任务
4. 把奖励改为分阶段 shaping reward
5. 用课程式训练导出 3 个模型

原则：

- 不改变三版模型的 observation/action 结构
- 不给 v1/v2/v3 分别写不同 agent 代码
- 所有并行模块共用同一个 `Behavior Name`

## 4. 代码修改计划

### 4.1 修改 `FetchAgent.cs`

目标：让 agent 真正学会完整 fetch 任务，而不是只学“碰 goal”。

计划修改项：

1. 新增阶段状态字段
   - 增加 `bool hasBall`
   - 或定义枚举：
     - `SearchingBall`
     - `ReturningGoal`

2. 新增训练辅助字段
   - 上一帧到 ball 的距离
   - 上一帧到 goal 的距离
   - 卡住检测计时器
   - 最低移动速度阈值

3. 重写 `CollectObservations()`
   建议加入：
   - `hasBall`
   - ball 相对 agent 的局部坐标或方向
   - goal 相对 agent 的局部坐标或方向
   - agent 当前局部速度
   - agent 朝向前向量
   - 到 ball 的归一化距离
   - 到 goal 的归一化距离

4. 重写 `OnActionReceived()`
   除了执行移动，还要：
   - 添加每步惩罚
   - 根据阶段计算距离差分奖励
   - 处理卡住惩罚

5. 修改成功条件
   - 只有在 `hasBall == true` 时接触 `goal` 才算成功
   - 未拿球时接触 `goal` 不结束，必要时给轻微惩罚

6. 调整 `OnEpisodeBegin()`
   - 重置 `hasBall`
   - 重置速度、旋转、距离缓存
   - 清除旧 goal
   - 随机放置 agent、ball
   - 如果训练阶段需要，可按课程策略控制 goal 的生成方式

7. 保留 `Heuristic()`
   用于调试行为逻辑，确认基础环境可玩。

### 4.2 修改 `FetchBall.cs`

目标：明确 ball 被“拾取/接触”后的状态切换。

计划修改项：

1. 把 `m_State` 命名含义改清楚
   - 可改为 `m_IsCollected`

2. 增加只触发一次的收集逻辑
   - 首次被 agent 接触时：
     - 设置 collected 状态
     - 通知 area 生成 goal
     - 通知 agent 切换到返回阶段

3. 避免重复生成 goal
   - 如果 ball 已经被收集，不允许再次触发生成

4. 视需求增加引用
   - 如果由 ball 主动通知 agent，增加对 `FetchAgent` 的引用
   - 或者由 agent 在碰撞中识别 ball，统一由 agent 驱动阶段切换

建议：

- 更推荐让 agent 自己感知“碰到 ball”，由 agent 维护主状态
- `FetchBall` 只负责自身被收集和重置

### 4.3 修改 `FetchArea.cs`

目标：让 area 更明确地支持训练环境重置与对象管理。

计划修改项：

1. 保留并完善 `CreateGoal()`
   - 只在 ball 已被收集后生成 goal

2. 保留 `PlaceObject()`
   - 后续如需课程训练，可允许传入更受限的随机区域

3. 扩展 `CleanFetchArea()`
   - 清除旧 goal
   - 如后续加入临时训练辅助物体，也由这里统一清理

4. 可选扩展
   - 增加训练难度参数
   - 增加不同阶段的 spawn 规则

## 5. Inspector / Prefab 修改计划

主要对象：`Assets/ML-Agents/Examples/Pyramids/Prefabs/AreaPB.prefab`

### 5.1 Behavior Parameters

需要修改：

1. `Behavior Name`
   - 从 `Fetch` 改成统一名称，例如 `FetchPet`

2. `Max Step`
   - 当前 `5000` 偏长
   - 建议先调到 `1000` 或 `1500`
   - 如果场景较大可升到 `2000`

3. `Behavior Type`
   - 训练时使用默认训练模式
   - 推理时切换到模型推理

4. 确认 `Use Child Sensors`
   - 保持开启

### 5.2 Decision Requester

建议：

- 保留 `Decision Period = 5` 作为初始值
- 如果移动反应太慢，可试 `3`
- 如果训练不稳定或动作震荡，再回调

### 5.3 RayPerceptionSensor3D

当前项目中有 3 个 ray sensor，应做以下调整。

#### 需要保留的标签

建议只保留：

- `ball`
- `goal`
- `wall`

如果训练场景没有真正参与任务的 `block`，从 detectable tags 中移除 `block`。

#### 射线层设计建议

当前高度偏高，建议改为更贴近 fetch 任务的探测高度。

推荐思路：

1. 近地水平层
   - 用于看见球和近处墙体
2. 中低高度层
   - 用于补足遮挡
3. 近距离避障层或略向下层
   - 用于防止撞墙和漏掉脚边球

推荐参数方向：

- Sensor A
  - `Start Vertical Offset = 0.3`
  - `End Vertical Offset = 0.3`
  - `Ray Length = 12~15`
  - `Max Ray Degrees = 70~90`

- Sensor B
  - `Start Vertical Offset = 0.3`
  - `End Vertical Offset = 1.0`
  - `Ray Length = 10~14`
  - `Max Ray Degrees = 50~70`

- Sensor C
  - `Start Vertical Offset = 0.3`
  - `End Vertical Offset = 0.0` 或 `-0.2`
  - `Ray Length = 6~10`
  - `Max Ray Degrees = 35~50`

目的：

- 一个管搜索
- 一个管补盲
- 一个管近距离避障

避免三个 sensor 的参数完全相似，否则信息冗余较大。

## 6. 观察空间设计

推荐采用“Ray + Vector 混合观察”。

### 6.1 Ray 负责的信息

- ball 是否在视野中
- goal 是否在视野中
- wall 是否在视野中
- 粗略方位和遮挡关系

### 6.2 Vector 负责的信息

- 当前是否已经拿到球
- 当前阶段目标是什么
- ball 的相对方向/位置
- goal 的相对方向/位置
- 自身速度
- 距离变化趋势

### 6.3 为什么不能只靠 ray

只靠 ray 时，agent 很难稳定理解：

- 当前应该追 ball 还是追 goal
- 当前距离目标是否在变近
- 场景任务是否已经切阶段

因此必须保留一个最小但足够清晰的 vector observation。

## 7. 动作空间计划

当前离散动作先保留，减少变量。

建议保持：

- `0` 不动
- `1` 前进
- `2` 后退
- `3` 左转
- `4` 右转

后续如果希望宠物移动更自然，再考虑：

- 改为连续动作
- 分别控制前进速度和转向速度

第一轮训练不建议同时改任务逻辑和动作空间。

## 8. 奖励函数设计

推荐奖励结构如下：

### 8.1 通用惩罚

- 每步惩罚：`-0.0005` 到 `-0.001`
- 超时结束：`-0.5` 到 `-1.0`
- 长时间卡住：小惩罚
- 撞墙：小惩罚

### 8.2 找球阶段奖励

在 `hasBall == false` 时：

- 比上一帧更接近 ball：给小正奖励
- 远离 ball：给小负奖励或不给奖励
- 第一次碰到 ball：`+1.0`

### 8.3 回程阶段奖励

在 `hasBall == true` 时：

- 比上一帧更接近 goal：给小正奖励
- 远离 goal：给小负奖励或不给奖励
- 成功回到 goal：`+2.0` 到 `+3.0`

### 8.4 非法或无效行为

- 没拿球先碰 goal：`-0.05` 到 `-0.1`
- 原地抖动太久：小惩罚

## 9. 成功条件定义

训练任务成功必须满足：

1. agent 先接触 ball
2. 系统生成或激活 goal
3. agent 在拿球状态下接触 goal
4. 然后 `EndEpisode()`

以下情况不算成功：

- 未接触 ball 就进入 goal
- 卡住直到超时
- 一直绕圈但未完成任务

## 10. 课程式训练计划

三版模型不分别写三套逻辑，而是在相同结构上逐步提高难度。

### 10.1 模型一：`Pet_v1_Novice`

目标：

- 让宠物学会基本 fetch 流程

环境设置：

- ball 与 goal 生成范围较小
- agent 起点离 ball 不远
- 无复杂障碍
- ray 和 vector 奖励引导较强

训练结束标准：

- 成功率稳定在约 `70%` 以上
- 平均 episode reward 明显上升且趋于稳定

### 10.2 模型二：`Pet_v2_Intermediate`

目标：

- 让宠物在更随机环境下稳定完成 fetch

环境设置：

- 扩大随机生成范围
- ball 与 goal 完全随机
- 适度减少 shaping reward 比重

训练方式：

- 从 `Pet_v1_Novice` 继续训练

训练结束标准：

- 成功率稳定在 `80%~85%`
- 完成路径比 v1 更短、更稳定

### 10.3 模型三：`Pet_v3_Smart`

目标：

- 让宠物更快、更稳定、更少绕路

环境设置：

- 使用完整随机化
- 可加少量路径干扰
- 更强调效率和鲁棒性

训练方式：

- 从 `Pet_v2_Intermediate` 继续训练

训练结束标准：

- 成功率接近稳定上限
- 平均完成步数继续下降
- 对随机初始位置更稳

## 11. 训练配置文件计划

建议新增目录：

- `config/fetch_v1.yaml`
- `config/fetch_v2.yaml`
- `config/fetch_v3.yaml`

推荐使用 PPO。

基础参数建议：

- `trainer_type: ppo`
- `normalize: true`
- `hidden_units: 256`
- `num_layers: 2`
- `learning_rate: 3e-4`
- `batch_size: 1024`
- `buffer_size: 10240`
- `time_horizon: 128`

训练命令建议：

```bash
mlagents-learn config/fetch_v1.yaml --run-id=fetch_v1
mlagents-learn config/fetch_v2.yaml --run-id=fetch_v2 --initialize-from=fetch_v1
mlagents-learn config/fetch_v3.yaml --run-id=fetch_v3 --initialize-from=fetch_v2
```

## 12. Unity 场景执行步骤

### 阶段一：环境改造

1. 修改 `FetchAgent.cs`
2. 修改 `FetchBall.cs`
3. 如有必要修改 `FetchArea.cs`
4. 调整 prefab 上的 `Behavior Parameters`
5. 调整 3 个 ray sensor

### 阶段二：功能验证

1. 用 `Heuristic()` 手动控制 agent
2. 验证 ball 接触后能否正确切阶段
3. 验证未拿球时进 goal 不会成功
4. 验证拿球后进 goal 会结束 episode
5. 验证 9 个模块都能正常 reset

### 阶段三：训练 v1

1. 先在简化场景下训练
2. 看 TensorBoard 或训练日志
3. 如果 reward 不上升，先查奖励和观察，不要急着调超参数

### 阶段四：训练 v2 和 v3

1. 逐级增大随机性
2. 使用前一版 checkpoint 继续训练
3. 导出对应 onnx 模型

### 阶段五：游戏接入

1. 宠物初始使用 `Pet_v1_Novice`
2. 玩家喂食/抚摸累计到阈值后切换 `Pet_v2_Intermediate`
3. 再累计切换 `Pet_v3_Smart`

## 13. 运行时模型切换计划

在正式游戏逻辑中，不改变 agent 结构，只切换 `ModelAsset`。

要求：

1. 三个模型必须使用相同的 observation 定义
2. 三个模型必须使用相同的 action 定义
3. 行为名保持一致

预期体验：

- `v1`：能完成任务，但慢、会绕路、偶尔失败
- `v2`：大多数时候可以顺利完成
- `v3`：路径更直接，成功率更高，反应更稳定

## 14. 验证与验收标准

修改完成后，应逐项验证：

1. 9 个并行区域都能正常 reset
2. ball 与 goal 的随机生成没有穿模或刷到非法位置
3. agent 可以稳定识别 ball / goal / wall
4. 未拿球进入 goal 不算成功
5. 拿球后进入 goal 正确结束
6. reward 曲线随训练逐步上升
7. v1/v2/v3 三版模型能力有明显差异

## 15. 风险与注意事项

1. 不要同时大改动作空间、观察空间、奖励函数和场景结构，否则很难定位训练失败原因。
2. 如果训练没有起色，优先检查：
   - 成功条件是否可达
   - reward 是否方向正确
   - observation 是否包含必要信息
   - ray 是否真的能打到目标对象
3. 如果 ball 很小，ray 可能不稳定，需要：
   - 增大 ball collider
   - 或增加向量观察的权重
4. 如果 goal 是后来生成的，必须保证生成后 tag、collider、layer 都正确。
5. 三版模型要保留同一行为结构，否则运行时切换模型会出问题。

## 16. 推荐实施顺序

建议按以下顺序执行，不要跳步：

1. 先改任务状态与成功条件
2. 再改奖励
3. 再补充向量观察
4. 再调 ray sensor
5. 用 heuristic 验证
6. 训练 v1
7. 基于 v1 训练 v2
8. 基于 v2 训练 v3

## 17. 下一步实际执行项

下一轮代码修改建议直接做这些：

1. 重构 `FetchAgent.cs`
   - 增加阶段状态
   - 增加观察
   - 增加 shaping reward
   - 修正成功条件

2. 调整 `FetchBall.cs`
   - 避免重复触发
   - 明确收集状态

3. 生成 3 份训练配置 YAML

4. 最后再统一调整 prefab 的 Inspector 参数

