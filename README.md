# WFC Map Generator & Editor

基于波函数坍缩（WFC）算法的 Unity 地图生成、编辑与导出工具。

> **Map Editor 中文参数说明**：见 [Docs/MapEditor参数说明.md](Docs/MapEditor参数说明.md)

## 核心机制

本工具将地图的逻辑生成与美术表现分离，通过两个阶段完成地图构建：

1. **语义生成层 (Semantic WFC)**
    
    优先生成包含语义信息的网格布局（如 `Open`、`LowCover`、`Tower`、`Boundary` 等），该阶段不涉及具体的 3D 资产。
    
2. **表现映射层 (Prefab Registry)**
    
    基于预设的权重、分布偏好及边界规则，将语义层的结果映射为具体的 Prefab（如特定的障碍物或建筑模型）。
    

> **核心入口**：`Window > WFC > Map Editor`。所有场景编辑、规则校验及导入导出操作均在此面板中统一。

## 工作流指南

### 1. 资产创建

在 `Project` 面板中，通过右键菜单依次创建核心配置文件（默认位于 `Assets/` 目录）：

- `Assets > Create > WFC > Create Default Generation Config`
    
- `Assets > Create > WFC > Create Default Semantic Tile Set`
    
- `Assets > Create > WFC > Create Prefab Registry`
    

### 2. 场景装配

1. 创建空 GameObject（如 `WFC_Generator`），挂载 `WfcGenerationRunner` 与 `ObstacleSceneSpawner` 组件。
    
2. 将步骤 1 中创建的三个配置文件拖入 `WfcGenerationRunner` 对应的槽位。
    
3. 将自身的 `ObstacleSceneSpawner` 赋给 `Prefab Spawner` 引用。
    
4. 为 `ObstacleSceneSpawner` 配置一个基础的 Cube Prefab（如 `Assets/Prefabs/Cube.prefab`），用于后续的边界可视化及缺失资产占位（Placeholder）。
    

### 3. 编辑器初始化

1. 打开 `Window > WFC > Map Editor`。
    
2. 将场景中的 `Generation Runner`、`Prefab Registry` 资产以及占位 Cube 分别拖入 `References` 区域。
    
3. 点击 `Ensure Roots` 和 `Seed Default Registry Entries`。此操作将构建场景层级（如 `MapRoot`）并注入默认的占位数据（0: LowCover, 1: HighCover 等）。
    

### 4. 配置与生成

1. **参数设定**：在 Map Editor 的 `Map Parameters` 区域设置地图中心点、长宽高及边界墙高度。
    
2. **注册表维护**：在 `Prefab Registry` 区域中，将实体 Prefab 与语义类关联，并设定其权重与间距规则（Clearance）。
    
3. **开放度调整**：打开 `Window > WFC > Tuning`。通过 `Overall Openness` 控制地图整体的可通行区域比例；通过各项 Weight 参数调整不同 Prefab 在稀疏（Sparse）或密集（Dense）区域的出现频率。
    
4. **生成执行**：回到 Map Editor，点击 `Generate Obstacles`。
    

### 5. 手工编辑

生成后，支持在场景中直接移动、删除或新增生成的 GameObject。

编辑后，可使用 Map Editor 中的 `Selection Tools` 执行吸附网格（Snap）、重新分配 Index 或注册为障碍物等操作。

## 空间计算与物理规则

- **网格尺寸 (Cell Size)**：默认逻辑占地为 `1x1`。物理网格的单边长度取决于 `Prefab Registry` 中最大 Prefab 的合并边界（Bounds）。若未注册任何 Prefab，则回退使用默认 Cube 的尺寸。
    
- **贴地逻辑**：障碍物实例会以其原始视觉尺寸生成（不进行拉伸形变），并根据其 Bounds 底部自动贴合至地板顶面。
    

## 导入导出与校验

- **导出格式**：导出的 JSON 数据采用精简结构，仅包含 `Type`、`Pos` (X, Y, Z) 及 `Rot` (Y轴)。
    
- **未知 Index 处理**：导入外部 JSON 时，若遇到本地未注册的 Index，系统会使用 Placeholder 占位，保留并标记其原 Index 值。再次导出时，原 Index 数据将保持不变，实现无损的 round-trip。
    
- **场景校验 (Validation)**：点击 `Validate Scene Obstacles` 可检测场景中是否存在未注册对象、未知导入项、语义不匹配、未对齐网格、非 90 度旋转及越界等异常。
    

## 测试验证

工程内包含覆盖核心逻辑的 EditMode 自动化测试。可通过 Unity Editor 内的 Test Runner 运行，或使用以下命令行进行构建与验证：

```
dotnet build WFCTechTest.sln -nologo
dotnet test WFC.Tests.Editor.csproj --no-build -nologo
```
