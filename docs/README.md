# 极性失控 / Polarity Unleashed

带立体感的 2D 斜俯视磁力实时解谜原型。玩家用有限的主动标记赋予敌人与箱子正负极性，设计目标是让接触传播与物理运动相互影响。

当前已完成步骤 1—5，包括基础白盒与斜俯视表现、极性状态与唯一模拟入口、磁力、玩家移动与有限标记，以及受磁力影响的追击敌人；下一项为步骤 6「接触采集与快照传播」。运行 Game 场景可用 WASD 移动、左/右键正负标记，在三次预算内干预六个 Neutral 追击敌人与一个负极箱子的磁力。简单通道与玩家走位会改变接触机会；传播和关卡流程尚未实现。当前进度与剩余工作见[实施步骤](./实施步骤.md)，实际执行与验收证据见[实施记录](./实施记录.md)。

## 项目文档

- [玩法设计](./玩法设计.md)
- [美术资源规划](./美术资源规划.md)
- [技术架构](./技术架构.md)
- [实施步骤](./实施步骤.md)
- [实施记录](./实施记录.md)

## 打开工程

使用团结引擎 **1.10.1 / 2022.3.62t13**，打开仓库中的 `Polarity/` 文件夹（其中包含 Assets、Packages、ProjectSettings），再打开 `Assets/Polarity/Scenes/Game.scene`。

首次打开时，编辑器会根据 `Packages/manifest.json` 和 `packages-lock.json` 安装依赖并重建 Library。Codely Unity Bridge 1.0.86 为已声明的包依赖；工程内的 codely-unity 技能位于 `.agents/skills/codely-unity/`。

## 仓库结构

```text
Polarity/                   仓库根目录
├─ docs/                    项目文档与验收记录
├─ .agents/skills/           项目技能
└─ Polarity/                实际团结工程
   ├─ Assets/               场景、Prefab、精灵与表现脚本
   ├─ Packages/             依赖清单与锁文件
   └─ ProjectSettings/      引擎与物理配置
```

Library、Temp、Logs、UserSettings、构建输出及生成的 IDE 工程文件属于本机可再生成的内容，由 `.gitignore` 排除。场景、Prefab 与目录的 `.meta` 文件保留在版本控制中。
