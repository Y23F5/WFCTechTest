# WFC Map Editor 参数说明

本文档用于说明 `Window > WFC > Map Editor` 面板中各个分区、字段和按钮的用途。  
文档采用说明书写法，只描述字段定义、影响范围、触发行为和相关依赖。

---

## 1. References

### Generation Runner
- 类型：`WfcGenerationRunner`
- 含义：地图生成主入口组件
- 作用：
  - 提供 `GenerationConfig`、`SemanticTileSet`、`PrefabRegistry` 的运行时绑定
  - 提供生成、清理、重建边界等入口
  - 提供与 `ObstacleSceneSpawner` 的连接
- 依赖关系：
  - `Generation` 分区和 `Export` 分区依赖该引用
  - 部分 `Selection Tools` 操作也依赖该引用

### Prefab Registry
- 类型：`PrefabRegistryAsset`
- 含义：Prefab 条目注册表
- 作用：
  - 定义每个 `Index` 对应的 Prefab
  - 定义每个条目的语义类、权重、密度偏好和摆放规则
  - 提供 cell 计算、注册、导入恢复、场景校验所需的数据源
- 依赖关系：
  - `Prefab Registry` 分区直接编辑该资产
  - `Selection Tools`、`Import JSON`、生成链和校验都依赖该资产

### Placeholder Cube
- 类型：`GameObject`
- 含义：缺省占位物
- 作用：
  - 用于默认 placeholder entry
  - 用于导入未知 index 时的占位显示
  - 用于缺少可用 prefab 时的 fallback 可视化

---

## 2. Map Config

该分区对应 `GenerationConfigAsset` 中与地图尺寸和边界有关的字段。

### Map Center
- 类型：`Vector3`
- 含义：地图中心点
- 作用：
  - 定义地图逻辑网格在世界空间中的中心
  - 定义吸附时 X/Z 对齐的参考原点
  - 定义边界、地板、障碍物的整体平移基准

### Width
- 类型：`int`
- 含义：地图在 X 方向的逻辑格数
- 作用：
  - 决定地图宽度
  - 决定边界范围
  - 影响 WFC 求解规模
  - 影响越界校验

### Depth
- 类型：`int`
- 含义：地图在 Z 方向的逻辑格数
- 作用：
  - 决定地图深度
  - 决定边界范围
  - 影响 WFC 求解规模
  - 影响越界校验

### Height
- 类型：`int`
- 含义：地图逻辑高度层数
- 作用：
  - 影响逻辑空间定义
  - 影响生成和校验中的高度相关规则
- 备注：
  - 当前版本障碍物平面占地固定为 `1x1`

### Boundary Wall Height
- 类型：`int`
- 含义：边界墙高度
- 作用：
  - 用于边界墙生成
  - 用于 `Rebuild Boundaries`

---

## 3. Setup

该分区用于初始化场景节点、初始化 registry 和创建新条目。

### Ensure Roots
- 类型：按钮
- 作用：确保场景中存在生成链所需根节点
- 典型结果：
  - `MapRoot`
  - `worldBoundaryRoot`
  - `obstacleRoot`

### Seed Default Registry Entries
- 类型：按钮
- 作用：向 `Prefab Registry` 注入默认 placeholder 条目
- 用途：
  - 快速建立基础 registry
  - 在未配置完整 prefab 库时先跑通流程

### Add New Prefab Entry
- 类型：按钮
- 作用：向 `Prefab Registry` 新增一条 entry
- 默认行为：
  - 分配新的 index
  - 写入默认 prefab
  - 写入默认语义参数
  - 自动推导逻辑高度
  - 自动推导 `Default Pos Y`

### Placement cell edge
- 类型：只读信息
- 含义：当前 cell 的边长
- 作用：
  - 影响地板和边界尺寸
  - 影响吸附间距
  - 影响逻辑高度自动推导
  - 影响 `Default Pos Y` 自动推导

### cubic cell
- 类型：只读信息
- 含义：当前 cell 的三维尺寸
- 作用：显示当前 cell 的尺寸结果
- 备注：
  - 当前版本 cell 统一为立方体，因此三轴尺寸相同

### Map center
- 类型：只读信息
- 含义：当前地图中心点的摘要显示

---

## 4. Prefab Registry

该分区用于维护 Prefab Registry 资产本身。  
每个 entry 都表示一个可被生成、导入恢复、手工注册或手工指定的 prefab 条目。

### Validate Registry
- 类型：按钮
- 作用：检查 registry 配置是否合法
- 典型检查项：
  - index 冲突
  - prefab 缺失
  - clearance 配置不合法
  - 某个语义类没有可用条目

### Save Registry
- 类型：按钮
- 作用：将当前 registry 改动写回资产并保存

### Remove Index
- 类型：`int`
- 含义：待删除 entry 的 index

### Remove Prefab Entry
- 类型：按钮
- 作用：删除 `Remove Index` 指定的 entry

---

## 5. Prefab Registry Entry 字段

以下字段对应每一条 registry entry。

### Entry N
- 类型：列表序号
- 含义：当前 entry 在编辑器列表中的位置
- 备注：
  - 该值不是导出用的 index

### Reset Defaults
- 类型：按钮
- 作用：将当前条目重置为该语义类的默认参数
- 典型影响范围：
  - 基础权重
  - 稀疏/密集权重
  - 摆放规则
  - 自动推导字段的锁定状态

### Remove
- 类型：按钮
- 作用：删除当前 entry

### Index
- 类型：`int`
- 含义：条目编号
- 作用：
  - 写入导出 JSON 的 `Type`
  - 写入 `ObstacleInstanceMetadata.Type`
  - 用于 `Assign Index To Selection`
  - 用于导入时查找 prefab
- 约束：
  - 同一 registry 内应保持唯一

### Name
- 类型：`string`
- 含义：条目显示名
- 作用：
  - 用于编辑器识别
  - 用于 metadata 中的显示名称

### Prefab
- 类型：`GameObject`
- 含义：当前 index 对应的 prefab
- 作用：
  - 决定实例化对象的模型
  - 参与 cell 计算
  - 参与逻辑高度推导
  - 参与 `Default Pos Y` 推导

### Semantic Class
- 类型：枚举
- 含义：当前 prefab 对应的主语义类
- 常见值：
  - `LowCover`
  - `HighCover`
  - `Tower`
  - `Blocker`
- 作用：
  - 供 planner 在语义到 prefab 映射时筛选条目
  - 供校验工具检查语义是否匹配

### Weight
- 类型：`float`
- 含义：基础权重
- 作用：
  - 决定同一语义类下该 prefab 的基础抽样倾向
- 备注：
  - 该值为相对权重，不是百分比

### Auto Generate
- 类型：`bool`
- 含义：是否允许自动生成使用该条目
- 作用：
  - 打开时，自动生成链可将该条目作为候选
  - 关闭时，该条目仍可用于导入恢复和手工注册

### Sparse Weight
- 类型：`float`
- 含义：稀疏语义变体下的额外权重
- 作用：
  - 当当前格子属于 sparse 变体时，提高或降低该 prefab 的选中倾向

### Dense Weight
- 类型：`float`
- 含义：密集语义变体下的额外权重
- 作用：
  - 当当前格子属于 dense 变体时，提高或降低该 prefab 的选中倾向

### Logical Height
- 类型：`int`
- 含义：逻辑占高
- 作用：
  - 参与 bake、validation、connectivity 等规则层逻辑
- 默认行为：
  - 按 prefab 尺寸和 cell 自动推导

### Height: Auto / Height: Manual
- 类型：状态标签
- 含义：
  - `Auto`：当前逻辑高度由系统自动推导
  - `Manual`：当前逻辑高度为手工覆盖值

### Recalc Height
- 类型：按钮
- 作用：重新自动计算 `Logical Height`
- 结果：
  - 解除手动锁定
  - 按当前 prefab 和 cell 重算

### Default Pos Y
- 类型：`float`
- 含义：默认世界坐标 Y
- 作用：
  - 决定生成、导入、注册、吸附时该 prefab 的最终世界空间 Y
- 默认行为：
  - 按 prefab 的合并 bounds 和当前地面顶面高度自动推导

### Default Pos Y: Auto / Default Pos Y: Manual
- 类型：状态标签
- 含义：
  - `Auto`：当前值由系统自动推导
  - `Manual`：当前值为手工覆盖值

### Recalc Y
- 类型：按钮
- 作用：重新自动计算 `Default Pos Y`
- 结果：
  - 解除手动锁定
  - 按当前 prefab 和 cell 重算

### Can Appear Near Boundary
- 类型：`bool`
- 含义：是否允许出现在边界附近
- 作用：
  - 参与生成时的候选筛选
  - 参与场景校验

### Can Appear In Center
- 类型：`bool`
- 含义：是否允许出现在地图中心区域
- 作用：
  - 参与生成时的候选筛选
  - 参与场景校验

### Requires Clearance
- 类型：`bool`
- 含义：是否要求周围留空
- 作用：
  - 打开后，系统会进一步检查 `Clearance Radius`

### Clearance Radius
- 类型：`int`
- 含义：留空半径
- 作用：
  - 参与生成时的候选筛选
  - 参与场景校验

---

## 6. Generation

### Generate Obstacles
- 类型：按钮
- 作用：执行完整生成链
- 处理内容：
  - 运行 WFC
  - 生成语义布局
  - 按 registry 选择 prefab
  - 在场景中实例化障碍物

### Clear Obstacles
- 类型：按钮
- 作用：清理当前已生成的障碍物

### Rebuild Boundaries
- 类型：按钮
- 作用：重建边界墙
- 影响因素：
  - 地图尺寸
  - `Boundary Wall Height`
  - cell 尺寸

---

## 7. Selection Tools

该分区作用于当前在 Scene 中选中的对象。

### Register Selection As Obstacles
- 类型：按钮
- 作用：将当前选中的对象注册为障碍物
- 处理内容：
  - 移到 `obstacleRoot`
  - 添加或更新 `ObstacleInstanceMetadata`
  - 写入 index、语义类、显示名
  - 按当前 registry 规则重新对齐位置

### Assign Index To Selection
- 类型：按钮
- 作用：将当前选中的已注册对象改成指定 index
- 处理内容：
  - 更新 metadata
  - 按目标 entry 的规则重新对齐位置

### Snap Selection To Grid
- 类型：按钮
- 作用：将当前选中的对象重新吸附到逻辑网格
- 处理内容：
  - X/Z 对齐到 grid
  - Yaw 归整到 90 度倍数
  - Y 轴按 entry 规则重新对齐

### Validate Scene Obstacles
- 类型：按钮
- 作用：检查当前场景中的障碍物是否存在配置或摆放问题
- 典型检查项：
  - 缺少 metadata
  - 未注册
  - index 未知
  - registry 缺项
  - 语义不匹配
  - 边界/中心/clearance 规则违规
  - 未吸附到网格
  - 旋转不是 90 度倍数
  - 越界

---

## 8. Export

### Import JSON
- 类型：按钮
- 作用：从 JSON 导入障碍物数据
- 处理规则：
  - 已知 index：恢复对应 prefab
  - 未知 index：使用 placeholder 占位
  - 导入后按当前规则重新对齐

### Export JSON
- 类型：按钮
- 作用：将当前障碍物数据导出为 JSON
- 当前导出字段：
  - `Type`
  - `Pos_X`
  - `Pos_Y`
  - `Pos_Z`
  - `Rot_Y`

---

## 9. Status

### Status
- 类型：状态信息框
- 作用：显示最近一次操作结果、错误提示或校验摘要

---

## 10. 使用顺序

推荐流程如下：

1. 在 `References` 中指定 `Generation Runner`、`Prefab Registry`、`Placeholder Cube`
2. 在 `Setup` 中执行 `Ensure Roots`
3. 根据需要执行 `Seed Default Registry Entries`
4. 在 `Map Config` 中设置地图尺寸和边界参数
5. 在 `Prefab Registry` 中维护所有 entry
6. 执行 `Validate Registry`
7. 执行 `Generate Obstacles`
8. 如需手工调整，使用 `Selection Tools`
9. 最后执行 `Export JSON`

---

## 11. 常见问题定位

### 生成结果大量使用 placeholder
- 优先检查：
  - 目标语义类是否存在真实 prefab
  - `Auto Generate` 是否关闭
  - `Validate Registry` 是否存在错误

### 模型悬空或埋地
- 优先检查：
  - `Default Pos Y`
  - `Recalc Y` 后的结果
  - 当前 prefab 的 bounds 是否异常

### 生成反复失败
- 优先检查：
  - 开放度目标是否极端
  - 连通性约束是否过高
  - 某语义类是否没有可用 entry
  - clearance / boundary / center 限制是否过严

### 手工放置的对象未参与导出
- 优先检查：
  - 是否执行过 `Register Selection As Obstacles`
  - `ObstacleInstanceMetadata` 是否存在
  - `Index` 是否为合法值
