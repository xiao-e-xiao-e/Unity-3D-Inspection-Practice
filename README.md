# Unity 3D 虚拟巡检练习

使用 Unity 基础对象构建设备间，在实际射线命中位置上报问题；React 管理端通过同一个 Go 后端查看记录和修改状态，SQLite 保存数据。

当前版本为 **Windows 本地虚拟场景演示**。用户已于 2026-11-01 完成初步测试并确认基本功能实现。真实 AR 尚未接入，第 5 节仅说明后续改造思路。

```text
Unity ── 上报问题 / 读取标记 ──┐
                               ├── Go HTTP API ── SQLite
React ── 查询问题 / 修改状态 ──┘
```

## 1. 使用的版本

| 工具 | 本机验证版本 |
| --- | --- |
| Unity | 2022.3.62f3c1 LTS，内置渲染管线 |
| Go | 本机 go1.27.3 windows/amd64；backend/go.mod 声明 1.25.0 |
| Node.js / npm | v24.21.0 / 11.19.0 |
| React / Vite / TypeScript | 19.3.1 / 8.3.5 / 7.0.2，完整版本见 package-lock.json |
| React DOM / @vitejs/plugin-react | 19.3.1 / 6.1.2 |
| Unity UI | com.unity.ugui 1.0.0；客户端使用 C# |
| SQLite Go 驱动 | modernc.org/sqlite v1.58.0，无需另装数据库服务或 C 编译器 |

目录：`unity/` 为 Unity 项目，`admin/` 为管理端，`backend/` 为 Go 服务，`docs/` 为接口、验收和核心代码说明。

Unity 版本以 [ProjectVersion.txt](unity/ProjectSettings/ProjectVersion.txt) 为准，前端依赖以 [package-lock.json](admin/package-lock.json) 为准，后端依赖以 [go.mod](backend/go.mod) 和 [go.sum](backend/go.sum) 为准。上述版本是本项目的使用记录。当前未安装 AR Foundation。

## 2. 三个部分如何启动

推荐顺序：**Go 后端 → React 管理端 → Unity 客户端**。以下 PowerShell 命令从仓库根目录执行；其他电脑请替换为自己的项目路径。两个终端分别运行后端和管理端，停止服务使用 `Ctrl+C`。

```powershell
cd E:\Project\Unity-3D-Inspection-Practice
```

### 2.1 Go 后端

```powershell
.\scripts\Start-Backend.ps1
```

脚本先找 PATH 中的 Go，再尝试本机路径 `D:\environment\Go\bin\go.exe`。其他安装路径可以显式传入：

```powershell
.\scripts\Start-Backend.ps1 -GoPath 'D:\environment\Go\bin\go.exe'
```

启动成功后输出 API 地址和数据库绝对路径。默认仅监听 `127.0.0.1:8080`；数据库固定为仓库的 `backend/data/inspection.db`。首次启动自动建表，重新启动不清空数据。

```powershell
Invoke-RestMethod http://localhost:8080/api/health
```

也可在 `backend/` 内手动执行 `go run ./cmd/server`；手动运行的 `-db` 相对路径以当前工作目录为准。首次需要联网下载 Go 依赖，不应关闭校验。本项目未修改全局 GOPROXY；本次开发曾按用户许可临时使用 `https://goproxy.cn`。

若脚本无法运行，可从仓库根目录手动启动：

```powershell
cd backend
& 'D:\environment\Go\bin\go.exe' run ./cmd/server -addr 127.0.0.1:8080 -db data/inspection.db
```

联调使用过的 `qa.db` 是独立测试数据，不是默认演示数据库。

### 2.2 React 管理端

打开第二个终端，进入仓库根目录：

```powershell
.\scripts\Start-Admin.ps1
```

脚本检查依赖安装状态和 Windows 命令入口；首次启动或依赖不完整时自动执行 `npm ci --include=dev --bin-links=true`，然后启动 [管理端](http://127.0.0.1:5173)。也可在 `admin/` 内依次执行 `npm ci --include=dev --bin-links=true`、`npm run dev`。Vite、TypeScript 等开发依赖是运行开发服务器所必需的，不需要全局安装 Vite。

手动启动方式（从仓库根目录执行）：

```powershell
cd admin
npm.cmd ci --include=dev --bin-links=true
npm.cmd run dev
```

重新安装前先停止旧的本项目开发服务器，避免 Windows 文件占用。看到 Vite 输出 `Local: http://127.0.0.1:5173/` 后再打开页面。

默认 API 地址为 `http://localhost:8080`。需要修改时，将 `admin/.env.example` 复制为 `admin/.env.local`，调整 `VITE_API_BASE_URL` 后重启 Vite。

开发端口固定为 5173，若被占用会报错，不会自动换到未配置跨域的端口。后端默认允许 `http://localhost:5173` 和 `http://127.0.0.1:5173`；可通过后端 `-origins` 参数调整。

### 2.3 Unity 客户端

1. Unity Hub → 添加磁盘上的项目 → 选择仓库内的 `unity` 文件夹。
2. 使用 **2022.3.62f3c1** 打开项目。
3. 打开 `Assets/Scenes/InspectionSite.unity`，点击 Play。
4. Inspector 中 `Inspection System` → `Inspection Api` → `Base Url` 默认 `http://localhost:8080`。它必须与 React 连接同一个后端。

也可运行本机已经构建的 `unity/Builds/Windows/Inspection.exe`。构建产物被 Git 忽略，其他电脑需在 Unity 菜单 **Inspection → Build Windows demo** 重新构建。

| 操作 | 功能 |
| --- | --- |
| W/A/S/D | 移动，墙面和设备有碰撞 |
| 按住鼠标右键并移动 | 转动视角 |
| 鼠标左键点击设备、墙面或地面 | 在实际命中位置创建一个待提交标记 |
| 表单取消 / 编辑时 Esc | 移除未保存的本地标记 |
| 刷新已保存标记 | 读取已有问题及最新状态颜色 |

表单打开时停止移动、视角转动和场景射线点击。标题必填，描述可选，优先级可选择 low / medium / high。提交中按钮被禁用，不能重复提交或取消。

临时标记为黄色；保存后红色代表 open，蓝色代表 in_progress，绿色代表 resolved。Unity 启动及点击刷新时同步已有问题；管理端修改状态后，需要在 Unity 手动刷新。

### 2.4 完整演示流程

1. 启动 Go 和 React，打开 Unity 场景。
2. 点击 1 号机柜，填写标题“1 号机柜温度异常”、描述及 high 优先级。
3. 提交后确认成功提示和标记保留。
4. 刷新管理端，核对标题、优先级、open 状态和位置。
5. 修改为 in_progress，再修改为 resolved；确认页面显示保存成功。
6. 停止并重新启动 Go，刷新管理端，确认记录和状态保留。
7. 在 Unity 点击“刷新已保存标记”，确认标记变绿。

## 3. 已完成、未完成和已知问题

### 3.1 已完成

| 部分 | 已实现内容 |
| --- | --- |
| Unity 场景与操作 | 基础对象搭建地面、墙面、两个机柜、设备和管线；移动、视角转动及碰撞 |
| Unity 上报与交互保护 | 实际命中位置标记；标题、描述、优先级及坐标提交；成功/失败提示；UI 点击过滤；单个待提交标记；取消清理；防重复提交 |
| Unity 数据读取 | 启动或手动刷新时读取已保存的问题，按状态着色 |
| React 管理端 | 列表、详情、状态筛选和三种状态修改；加载、空列表、错误与重试提示 |
| Go 后端 | 健康检查、创建、列表、状态修改；唯一 ID、默认 open、服务端时间、参数校验与 CORS |
| 持久化与重试 | SQLite 事务保存、重启后数据保留；同一草稿的幂等重试 |
| 文档 | 启动脚本、接口约定、验收清单、核心代码说明及 AI 原文日志 |

已完成 Go 测试/静态检查/编译、React 类型检查/构建、Unity C# 编译/Windows 构建；浏览器实测过列表、状态保存、停服错误提示及重启后数据保留。用户反馈基本功能初步测试完成，此反馈不等同于所有异常用例逐项验收通过，详见 [TEST_PLAN.md](docs/TEST_PLAN.md)。

### 3.2 未完成 / 本版未覆盖

- **验收待补充：** 异常交互用例的逐项人工结果、不同电脑及分辨率下的完整验证。
- **真实 AR 未实现：** 手机相机背景、真实平面检测、跨会话锚点恢复及 AR 状态引导，设计说明见第 5 节。
- **本版范围外：** 登录权限、图片附件、删除问题、分页、实时推送、多场景坐标管理、WebGL/移动端发布。
- **后续交付流程：** 本次经用户授权发布已验收基线并建立 dev；后续改动测试确认后推送 dev，由用户合并到 main。

### 3.3 已知问题与使用限制

- **状态不实时同步：** 管理端修改后，Unity 需要手动刷新标记。
- **失败重试冻结输入：** 首次请求冻结内容，重试沿用同一幂等键和内容；需要修改时先取消再重新填写。
- **超时不等于未保存：** 服务端可能已经保存但响应丢失，应优先重试同一草稿或刷新确认。取消只删除本地临时标记，不撤回服务端记录；管理端状态修改超时后也应刷新确认。
- **恢复标记可能部分嵌入表面：** 当前只保存命中坐标，不保存表面法线；重新加载直接按坐标放置，不恢复首次显示的小幅法线偏移。数据坐标仍为原命中点。
- **场景布局需保持一致：** 当前只记录单场景世界坐标，没有场景版本或设备局部坐标，改变布局可能使旧标记偏离设备。
- **管理端依赖问题已修复：** 曾因 node_modules 残缺导致 vite 命令缺失，启动脚本已增加检测与恢复；若 npm ci 报 EPERM，先关闭占用原生模块的本项目旧 Vite 进程再重试。
- **端口固定：** 默认使用 8080 和 5173；Vite 使用 strictPort，端口占用时会报错，不自动换端口。

## 4. 后端地址和接口设计

### 4.1 地址配置

| 配置项 | 默认值 | 修改位置 |
| --- | --- | --- |
| Go 监听地址 | `127.0.0.1:8080` | 启动脚本 `-Address`，或 Go 参数 `-addr` |
| Unity API 地址 | `http://localhost:8080` | `Inspection System` → `Inspection Api` → `Base Url` |
| React API 地址 | `http://localhost:8080` | `admin/.env.local` 中的 `VITE_API_BASE_URL` |
| 浏览器允许来源 | `http://localhost:5173`、`http://127.0.0.1:5173` | 启动脚本 `-Origins`，或 Go 参数 `-origins`，逗号分隔 |
| 数据库 | `backend/data/inspection.db` | 启动脚本 `-Database`，或 Go 参数 `-db` |

修改 React 地址后需重启 Vite，生产构建需重新构建；修改 Unity 地址应保存场景，桌面客户端需重新构建。两端必须指向同一个 Go 服务，才会使用同一份数据。

后端处理 OPTIONS 预检，允许 GET、POST、PATCH、OPTIONS，以及 Content-Type、Idempotency-Key 请求头。桌面 Unity 不受浏览器 CORS 限制。

目前默认只供本机访问。若以后接入手机，手机上的 localhost 指手机自身，必须改为手机可访问的后端地址，并相应配置服务监听与网络访问；本版未验证该部署方式。

### 4.2 接口与数据格式

请求及响应均为 UTF-8 JSON，写请求设置 `Content-Type: application/json`。

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| GET | `/api/health` | 服务与数据库健康检查；正常返回 200、`{"data":{"status":"ok"}}` |
| POST | `/api/issues` | 创建问题，生成 UUID 和时间，默认 open；首次返回 201 |
| GET | `/api/issues` | 按创建时间倒序返回问题列表；返回 200 |
| PATCH | `/api/issues/{id}/status` | 请求如 `{"status":"in_progress"}`；可修改为 open / in_progress / resolved，返回 200 |

成功响应为 `{"data":...}`，列表为空时为 `{"data":[]}`。错误响应为 `{"error":{"code":"VALIDATION_ERROR","message":"标题不能为空","field":"title"}}`。

创建请求：

```json
{
  "title": "1 号机柜温度异常",
  "description": "机柜右侧温度明显偏高",
  "priority": "high",
  "position": { "x": 1.25, "y": 0.8, "z": 2.4 }
}
```

响应问题还包含 `id`、`status`、`createdAt`、`updatedAt`，三端名称完全一致。坐标是 Unity 世界坐标；时间是服务端生成的 UTC RFC 3339 时间，管理端按本地时区显示。完整错误码和幂等规则见 [API.md](docs/API.md)。

成功响应示例（ID 和时间仅作格式展示）：

```json
{
  "data": {
    "id": "a6c813c6-7f66-482c-9b49-7dfe5f756c82",
    "title": "1 号机柜温度异常",
    "description": "机柜右侧温度明显偏高",
    "priority": "high",
    "status": "open",
    "position": { "x": 1.25, "y": 0.8, "z": 2.4 },
    "createdAt": "2026-11-01T08:00:00.000000000Z",
    "updatedAt": "2026-11-01T08:00:00.000000000Z"
  }
}
```

### 4.3 校验、幂等和持久化

- 标题去除首尾空格后不能为空；描述可省略；优先级仅允许 low / medium / high；坐标三轴必须显式传入有限数值，0 合法。
- 空标题、错误枚举/坐标返回 400；问题不存在返回 404；数据库写入失败返回 500，不报告成功；数据库不可用时健康检查返回 503。
- Unity 每个草稿使用一个 `Idempotency-Key`；同键相同内容重试返回 200 和原问题，同键不同内容返回 409。不传幂等键也可创建，但不保证重复请求去重。
- 问题与幂等记录在同一 SQLite 事务内保存，重启后仍存在。状态更新保留 createdAt，只修改 status 和 updatedAt。

## 5. AR 基础问题

以下是未来扩展设计，使用 AR Foundation 5.x 的术语；当前项目没有接入这些能力。实际开发需选择与 Unity 及目标平台兼容的版本并在真机验证。

### 5.1 如果把虚拟场景标注改成真实 AR，平面检测和点击放置通常需要使用哪些 AR Foundation 能力？

| 能力 / 组件 | 用途 |
| --- | --- |
| AR Foundation 和平台 Provider | 统一 AR 接口；Android 通常配合 ARCore XR Plug-in，iOS 配合 ARKit XR Plug-in |
| `ARSession`、`ARInputManager` | 管理会话生命周期、启用设备追踪 |
| `XROrigin` 和 AR Camera | 将会话坐标转换为 Unity 世界坐标；相机配合 `ARCameraManager`、`ARCameraBackground` 展示真实画面 |
| `ARPlaneManager` | 检测并跟踪水平/垂直平面，如地面、桌面和墙面 |
| `ARRaycastManager` | 用屏幕触摸位置命中 AR 平面，获取位置和旋转组成的 Pose |
| `ARAnchorManager` / `ARAnchor` | 在命中位置建立锚点，标记作为子对象随空间追踪更新 |

放置流程：追踪正常 → 扫描并发现平面 → 过滤 UI 触摸 → 调用 `ARRaycastManager.Raycast`，使用 `TrackableType.PlaneWithinPolygon` 限制在检测到的平面边界内 → 通过命中 Pose 建立锚点并放置标记 → 打开问题表单。

仍需保留“一次触摸最多一个草稿”和“取消清理临时标记/临时锚点”。AR 可追踪对象不一定存在 Unity Collider，不能直接把现有 `Physics.Raycast` 当作真实环境检测。依据：[Unity AR Foundation 概览](https://docs.unity.cn/Packages/com.unity.xr.arfoundation@5.1/manual/index.html)、[平面检测](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@5.1/api/UnityEngine.XR.ARFoundation.ARPlaneManager.html)、[AR Raycast Manager](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@5.1/manual/features/raycasts.html)、[AR Anchor Manager](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@5.1/manual/features/anchors.html)。

### 5.2 为什么只保存 Unity 中的 x/y/z 坐标，通常不能保证应用下次启动后仍对应同一个真实位置？

`x/y/z` 是相对于坐标系的数值，不是现实世界中的永久地址。AR 会话启动时建立空间参考系；下次启动时设备位置、朝向或环境识别结果不同，相同数值可能落到另一个真实位置。`XROrigin` 的变换也影响会话坐标与 Unity 世界坐标的关系。依据：[Unity 会话空间与世界空间](https://docs.unity.cn/Packages/com.unity.xr.arfoundation@5.1/manual/features/device-tracking.html)。

例如，第一次站在门口标记前方两米的机柜；第二次站在房间另一侧启动，恢复同样的 `(0, 0, 2)`，只会指向新参考系中的前方两米，不能识别原机柜。

**普通运行时锚点不自动等于跨次启动持久化。** 需要支持重定位的空间参考，例如平台提供的地图保存/恢复、持久化锚点、ARKit World Map、ARCore Cloud Anchors，或重新识别已知图像建立参考系。保存可恢复的地图/锚点标识和相对位姿，下次先完成重定位再恢复标记；仅保存本次运行的 Trackable ID 或 Transform 坐标也不够。支持能力需按平台检查。依据：[Unity 锚点与 World Map](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@5.1/manual/features/anchors.html)、[ARCore Cloud Anchors 的保存与解析](https://developers.google.com/ar/develop/cloud-anchors)。

因此未来的数据设计还需考虑 `anchorId` / `mapId`、参考系类型、相对位置与旋转、重定位状态；当前三端 JSON 尚未增加这些字段。

### 5.3 当 AR 初始化失败、设备不支持或 Tracking 状态异常时，应用应该如何提示用户？

应在画面上明确展示状态及恢复操作，不能只写 Console 日志，也不能一直显示无法退出的加载动画。

实现时调用 `ARSession.CheckAvailability()` 检查支持情况，监听 `ARSession.stateChanged`，结合 `ARSession.notTrackingReason` 判断异常原因。`Ready` 只表示设备及所需软件就绪，不代表追踪成功；放置还需满足 `SessionTracking`、相关平面/锚点追踪有效且射线命中有效。依据：[Unity AR Session 状态](https://docs.unity.cn/Packages/com.unity.xr.arfoundation@5.1/manual/features/session.html)、[NotTrackingReason 枚举](https://docs.unity.cn/Packages/com.unity.xr.arfoundation@5.1/api/UnityEngine.XR.ARSubsystems.NotTrackingReason.html)。

下面是建议的交互设计，不是当前已实现功能：

| 情况 | 提示示例 | 应用行为 |
| --- | --- | --- |
| 初始化 / 扫描中 | “正在初始化，请缓慢移动设备，扫描地面或桌面。” | 显示状态，获得有效命中前禁用放置 |
| 初始化失败 / 长时间无进展 | “AR 暂时无法启动，请检查相机权限或重试。” | 提供重试、退出 AR 入口，保留填写内容 |
| 设备不支持 | “当前设备不支持 AR，可使用普通巡检模式。” | 不反复重试，允许返回或进入非 AR 模式 |
| 需要安装 / 更新 AR 服务 | “请安装或更新 AR 服务后继续。” | 按平台能力提供引导及取消入口 |
| 相机权限被拒 / 不可用 | “请允许相机访问，或关闭占用相机的应用后重试。” | 按实际原因提供设置或重试入口，不能只显示黑屏 |
| 光照不足 / 特征不足 | “请移到明亮位置，对准有纹理的表面。” | 暂停新标记放置，等待追踪恢复 |
| 移动过快 | “请减慢移动速度，保持设备稳定。” | 恢复追踪后再允许放置 |
| 追踪丢失 / 重定位中 | “定位暂时丢失，请回到刚才扫描的位置缓慢移动。” | 隐藏或弱化位置不可靠的标记，暂停位置提交；恢复后显示标记并提示“定位已恢复” |

异常时保留标题和描述，位置相关操作等追踪恢复并重新确认命中后继续。不能用过期坐标假装放置成功，也不能自动重置会话后静默把旧坐标当作新坐标使用。

## 6. 验证、代码说明与提交约定

```powershell
cd backend
& 'D:\environment\Go\bin\go.exe' test ./... -count=1
& 'D:\environment\Go\bin\go.exe' vet ./...
cd ..\admin
npm.cmd run build
```

后端测试使用临时数据库，覆盖校验、状态更新、重启持久化、幂等竞争和跨域。人工验收清单、已执行记录与待验证项目见 [TEST_PLAN.md](docs/TEST_PLAN.md)。核心代码说明见 [CORE_CODE_GUIDE.md](docs/CORE_CODE_GUIDE.md)。

Unity 菜单 **Inspection → Rebuild demo scene** 会重建示例场景并覆盖 `InspectionSite.unity`；手动修改场景后不要使用该菜单，除非确实希望恢复示例。通常直接打开已提交场景即可。

### 6.1 常见问题

- **Go 找不到**：使用启动脚本的 `-GoPath`，无需修改系统 PATH。
- **脚本不能执行**：按上述手动命令启动，无需更改 PowerShell 执行策略。
- **端口占用**：关闭此前启动的本项目服务，或同步修改两端地址和跨域配置。
- **提示 vite 不是命令**：表示本地依赖或 `node_modules/.bin/vite.cmd` 不完整；重新运行修复后的启动脚本，或进入 `admin` 执行 `npm ci --include=dev --bin-links=true`，再启动。用 `npm ls --depth=0 --include=dev` 检查是否仍有 missing / invalid 项。
- **数据库似乎丢失**：查看后端启动输出的绝对路径；不要从不同目录使用不同的相对数据库路径。
- **Unity 没有场景**：确认打开的是 `unity` 子目录，再打开 `InspectionSite.unity`。
- **中文显示**：Windows 客户端优先使用系统 Microsoft YaHei / SimHei 字体。

### 6.2 AI 记录与 Git 流程

[AI_LOG.md](AI_LOG.md) 按顺序保留用户原始指令、采用情况和真实验证记录。用户亲自检查、修改或放弃 AI 结果的记录必须基于用户实际反馈，不能用自动测试冒充。

分支约定：`main` 保存已验收基线，`dev` 用于后续开发。**后续修改经用户测试确认后，先推送 dev，由用户自行合并到 main；未经新的明确授权，不直接推送 main 或代用户合并。** 测试后改动需要补测受影响部分，不强制推送或覆盖远程历史。供后续协作遵循的规则见 [AGENTS.md](AGENTS.md)。

提交 Unity 的 Assets（含 .meta）、Packages 和 ProjectSettings；忽略 Library、Temp、Builds、node_modules、运行日志和本地数据库。

