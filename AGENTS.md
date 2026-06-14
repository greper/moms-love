# AGENTS.md — 妈妈的爱 (MomsLove) 项目开发指南

## 1. 项目概述

本仓库是一个基于 .NET 8 / WPF 构建的 Windows 桌面应用程序，帮助家长管理孩子的屏幕使用时间（使用-冷却模式），并通过密码保护设置入口。

| 信息项 | 内容 |
| --- | --- |
| 解决方案文件 | `MomsLoveApp.sln` |
| 目标框架 | `net8.0-windows`（WPF 桌面应用） |
| 运行环境 | Windows 10 及以上 |
| 构建工具 | .NET 8 SDK（`dotnet` CLI） |
| 测试框架 | xUnit（通过 `dotnet test` 运行） |

## 2. 代码结构

```
moms-love2/
├── MomsLoveApp.sln              # 解决方案
├── MomsLove/                    # WPF 主程序（UI 层）
│   ├── App.xaml / App.xaml.cs   # 应用入口
│   ├── MainWindow.xaml          # 主窗口（孩子使用界面）
│   ├── TimeUpWindow             # 时间到弹窗
│   ├── TimeOverlayWindow        # 倒计时覆盖层
│   ├── SettingsWindow           # 家长设置窗口
│   ├── PasswordWindow           # 密码输入窗口
│   ├── ConfirmPasswordWindow    # 确认密码窗口
│   └── Services/                # 服务：托盘图标、进程守护、启动、图标等
├── MomsLove.Core/               # 核心业务逻辑（无 UI 依赖）
│   ├── Models.cs                # 领域模型
│   ├── AppDataStore.cs          # 配置数据持久化
│   ├── PlaySessionManager.cs    # 会话管理核心
│   └── PasswordHasher.cs        # 密码哈希
└── MomsLove.Tests/              # 单元测试项目
    ├── AppDataStoreTests.cs
    ├── PasswordHasherTests.cs
    └── PlaySessionManagerTests.cs
```

## 3. 工作规则（核心约束）

1. **变更最小化原则**：改动严格限定在用户请求的范围内。不做"顺便"优化，不引入不必要的抽象层。
2. **遵循现有模式**：优先遵循项目已有的代码风格、命名约定和架构模式，而不是引入新的模式或第三方库。
3. **尊重用户改动**：不得回退用户已提交/已做出的变更，除非用户明确要求。
4. **中文字符策略**：新增的代码与文本使用 ASCII；只有在已有中文/非 ASCII 内容的文件中，或任务明确要求中文时才使用中文字符（本文件本身为中文说明文档，不在此限）。
5. **禁止凭空生成**：不要在代码、路径、命令、输出中编造不存在的文件或内容。
6. **搜索优先**：浏览/检索代码时优先使用 `rg`（ripgrep）或 `rg --files`，保持高效。
7. **手动文件编辑**：对已有文件做精确行内编辑时使用 `apply_patch` 风格的改动（即 `Edit` 工具），避免整文件覆盖导致风格/上下文丢失。

## 4. 默认完成工作流（构建 → 测试 → 启动 → 通知用户测试）

> **默认行为，无需用户开口**：任何代码或 UI 需求修改完成后，都必须按以下顺序执行完整流程。不要等待用户说"编译一下""跑个测试"或"打开看看"。
> **最终目标**：让用户看到应用实际效果并进行人工测试。

### 第 1 步：构建解决方案

```powershell
dotnet build .\MomsLoveApp.sln
```

- **构建失败**：必须先修复问题，再继续后续步骤。不得在构建失败状态下打开应用或汇报"已完成"。
- 仅当用户明确要求跳过时可以不构建（极为罕见）。

### 第 2 步：运行测试

当改动可能影响行为、逻辑、模型、服务或共享代码时（即改动 `MomsLove.Core/` 或 `MomsLove.Tests/` 时），必须运行：

```powershell
dotnet test .\MomsLoveApp.sln
```

- **测试不通过**：定位失败原因，修正实现代码。
- **禁止**：为了让测试通过而去弱化或删除既有测试用例。除非有充分理由证明测试本身过时或不正确，且已向用户说明理由。

### 第 3 步：启动桌面应用

构建与测试通过后，启动应用，让用户可以直接看到效果：

```powershell
Start-Process -FilePath ".\MomsLove\bin\Debug\net8.0-windows\MomsLove.exe"
```

- 如果应用无法启动，向用户报告**具体原因和尝试的命令**，不要沉默跳过。

### 第 4 步：通知用户来测试

完成上述三步后，在最终回复中明确告知用户：

1. **构建状态**：构建成功或失败的简要结果。
2. **测试状态**：有多少测试通过/失败。
3. **应用已启动**：确认应用已打开，用户可直接在桌面上看到界面。
4. **可以开始测试**：提示用户进行人工测试，验证需求是否已按预期实现。

> **注意**：不要用 TODO 工具来汇报最终结果。最终回复应当是清晰的纯文本总结。

## 5. 代码风格与约定

- 遵循 [Microsoft C# 命名规范](https://learn.microsoft.com/zh-cn/dotnet/csharp/fundamentals/coding-style/identifier-names)：
  - 类 / 接口 / 方法 / 属性：`PascalCase`（如 `PlaySessionManager`、`GetRemainingTime`）。
  - 局部变量 / 方法参数：`camelCase`。
  - 私有字段：`_camelCase`（以下划线开头）。
  - 常量：`PascalCase`。
- XAML 中元素命名保持语义化，避免 `button1`、`label2` 之类无意义的名称。
- 新增类/方法时，先看 `MomsLove.Core/` 是否已有类似实现，避免重复造轮子。

## 6. 安全注意事项

- **不要硬编码密钥或密码**：任何敏感信息不得写入源代码。
- 用户密码哈希已在 `PasswordHasher.cs` 中处理，新增密码相关逻辑时必须沿用该实现，不得自行发明哈希方案。
- 不得将 `output/` 或 `bin/` 中的产物提交到版本控制（已由 `.gitignore` 处理）。

## 7. 快速参考速查表

| 目标 | 命令 |
| --- | --- |
| 构建 | `dotnet build .\MomsLoveApp.sln` |
| 运行测试 | `dotnet test .\MomsLoveApp.sln` |
| 清理 | `dotnet clean .\MomsLoveApp.sln` |
| 启动已构建的应用 | `Start-Process -FilePath ".\MomsLove\bin\Debug\net8.0-windows\MomsLove.exe"` |
| 发布 Release | `.\scripts\publish.ps1` |
| 查看项目文件结构 | `rg --files` |

## 8. 发布打包规则

### 版本号管理

- 版本号在 `MomsLove\MomsLove.csproj` 的 `<Version>` 元素中维护，格式 `主版本.次版本.内部版本`（如 `0.2.0`）。
- 版本号运行时显示在应用标题栏右侧（灰色小字 `v0.2.0`），从 `Assembly.GetExecutingAssembly().GetName().Version` 读取。
- **每次打包前必须自增版本号**（至少递增内部版本号）。

### 打包方式

- **非自包含（`--self-contained false`）**：依赖用户系统已安装 .NET 8 运行时，产物体积小（约 3–5 MB）。
- 使用发布配置文件 `MomsLove\Properties\PublishProfiles\FolderProfile.pubxml`。
- 输出目录：`publish\MomsLove\`。

### 发布命令（推荐）

一键发布（自动递增内部版本号并发布到 `publish\MomsLove\`）：

```powershell
.\scripts\publish.ps1
```

手动递增其他版本段：

```powershell
.\scripts\publish.ps1 -Bump minor   # 递增次版本号
.\scripts\publish.ps1 -Bump major   # 递增主版本号
```

直接使用 dotnet CLI：

```powershell
dotnet publish .\MomsLove\MomsLove.csproj -c Release /p:PublishProfile=FolderProfile
```

## 9. 禁止事项（红线）

1. 不要在用户未明确要求的情况下新增文档类文件（`*.md`、`README.md` 等）。本指南文件是唯一例外。
2. 不要引入未经过验证的第三方 NuGet 包。如需新增依赖，先向用户确认。
3. 不要弱化或删除既有测试以绕过失败。
4. 不要在用户明确否定时仍"代为"构建/启动应用。

---

*记住：用户期望默认行为是"改完 → 构建 → 测试 → 启动应用 → 通知用户测试"。*
