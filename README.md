# WFC Tech Test — Unity 3D 程序化地图生成器

基于 Unity 3D 的**语义 Wave Function Collapse（WFC）**管线，用于生成可玩的 blockout 风格多人战斗地图。

## 项目概述

本系统不只是一个普通的 WFC 求解器，而是采用四层架构来确保每张生成的地图在结构上合理且可供玩家通行：

| 层级 | 职责 |
|---|---|
| **Core** | 移动规则、网格基本类型、占用图建模 |
| **Semantic** | 高层语义原型约束（Floor、Wall、Portal、Stair……） |
| **Compile** | 语义网格 → 体素网格转译 |
| **Runtime** | Unity MonoBehaviour 接入、Prefab 生成、批量运行器 |

生成器支持：
- 通过种子确定性复现
- 多层楼层布局（最多 3 层）
- 多样化障碍物（1–3 格高度、可变宽度）
- 门洞 / 拱门结构
- 楼梯与坡道连接
- 生成后连通性自动验证
- 编辑器内参数调节窗口与 Fuzz 压力测试工具

## 目录结构

```
Assets/
├── _Project/
│   └── WFC/
│       ├── Core/           # 网格类型、移动规则、求解器接口
│       ├── Semantic/       # 语义网格与邻接规则
│       ├── Compile/        # SemanticToVoxelCompiler
│       ├── Validation/     # ConnectivityValidator、GenerationValidator
│       ├── Runtime/        # WfcGenerationPipeline、WfcGenerationRunner、BatchRunner
│       ├── Data/           # ScriptableObject 资产（TileSet、GenerationConfig）
│       ├── Diagnostics/    # GenerationReport、BatchGenerationReport
│       └── Editor/         # WfcTuningWindow、WfcFuzzTestWindow、WfcAssetFactory
├── Tests/
│   └── Editor/             # EditMode 单元测试（NUnit + Unity Test Framework）
├── Scenes/
├── Prefabs/
└── Settings/               # URP 渲染管线设置
Packages/
ProjectSettings/
```

## 环境要求

- **Unity** 2023.2.x（URP）
- **依赖包**：Universal Render Pipeline 16、Unity Test Framework 1.3、AI Navigation 2.0、Timeline 1.8

## 快速开始

1. 克隆仓库，用 Unity 2023.2.x 打开项目。
2. 打开 `Assets/Scenes/` 并加载主场景。
3. 在 Unity 菜单中选择 **WFC → Tuning Window** 调整生成参数。
4. 点击 **Generate**（或直接运行场景）生成地图。
5. 使用 **WFC → Fuzz Test** 进行批量生成并查看通过 / 失败报告。

## 运行测试

打开 **Window → General → Test Runner**，选择 **EditMode**，点击 **Run All**。

测试覆盖范围：
- `SemanticWfcSolverTests` — 求解器约束传播
- `SemanticToVoxelCompilerTests` — 语义到体素转译
- `ConnectivityValidatorTests` — 图可达性检查
- `WfcGenerationPipelineTests` — 端到端管线集成

## 架构说明

核心求解器（`IWfcSolver`）是纯 C# 接口，不依赖任何 Unity API，可独立进行单元测试。所有 Unity 相关逻辑（MonoBehaviour、Prefab 生成、ScriptableObject）均隔离在 `Unity/Runtime` 和 `Editor` 子命名空间中。
