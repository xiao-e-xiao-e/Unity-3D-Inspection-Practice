# Unity 3D 虚拟巡检

使用 Unity 基础对象构建设备间，在实际射线命中位置上报问题；React 管理端通过同一个 Go 后端查看记录和修改状态，SQLite 保存数据。

```text
Unity ── 上报问题 / 读取标记 ──┐
                               ├── Go HTTP API ── SQLite
React ── 查询问题 / 修改状态 ──┘
```

## 1. 使用的版本

| 部分 | 技术与版本 |
| --- | --- |
| Unity 客户端 | Unity 2022.3.62f3c1 LTS、C# |
| React 管理端 | React 19.3.1、TypeScript 7.0.2、Vite 8.3.5 |
| 后端 | Go 1.27.3、SQLite（modernc.org/sqlite 1.58.0） |
| 前端运行环境 | Node.js 24.21.0、npm 11.19.0 |

## 2. 三个部分如何启动

先安装上述环境，在项目根目录分别打开两个 PowerShell 终端。

**Go 后端：**

```powershell
.\scripts\Start-Backend.ps1
```

Go 未加入 PATH 时，可通过 `-GoPath` 指定 go.exe 路径。数据保存在 `backend/data/inspection.db`，服务重启后保留。

**React 管理端：**

```powershell
.\scripts\Start-Admin.ps1
```

脚本自动检查并安装依赖。启动后访问 http://127.0.0.1:5173。

**Unity 客户端：**

使用 Unity Hub 打开 `unity` 文件夹，进入 `Assets/Scenes/InspectionSite.unity`，点击 Play。

WASD 移动，按住右键转动视角；左键点击表面上报问题，点击已保存标记查看详情。取消填写会移除未保存标记。

## 3. 已完成、未完成和已知问题


**已完成：**

- Unity 基础场景、移动、射线放置、问题提交、标记详情及 UI 防误触。
- React 列表、状态修改、搜索、筛选、排序、详情和软删除。
- Go 参数校验、唯一 ID、持久化、跨域和错误提示。
- 三端联调及本轮用户测试。

**未完成：** 真实 AR、跨启动空间定位、登录权限、分页和删除恢复界面。

**已知问题：**

- 状态和删除结果需在 Unity 手动刷新后同步。
- 标记重新加载后可能部分嵌入表面；改变场景布局可能使旧标记偏离设备。
- 请求超时可能已经保存，应重试原草稿或刷新确认。
- 默认端口 8080、5173 被占用时需要先释放。
## 4. 后端地址和接口设计

默认地址：`http://localhost:8080`，Unity 和 React 必须连接同一后端。

Unity 在 `Inspection Api → Base Url` 配置地址；React 在 `admin/.env.local` 中配置 `VITE_API_BASE_URL`，修改后重启管理端。后端默认允许 localhost 和 127.0.0.1 的 5173 端口跨域访问。

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| GET | `/api/health` | 健康检查 |
| POST | `/api/issues` | 创建问题 |
| GET | `/api/issues` | 获取未删除问题列表 |
| GET | `/api/issues/{id}` | 获取详情 |
| PATCH | `/api/issues/{id}/status` | 修改状态 |
| DELETE | `/api/issues/{id}` | 软删除，数据库保留记录 |

创建请求示例：

```json
{
  "title": "1 号机柜温度异常",
  "description": "机柜右侧温度偏高",
  "priority": "high",
  "position": { "x": 1.25, "y": 0.8, "z": 2.4 }
}
```

标题必填；优先级为 `low / medium / high`。后端生成 `id`、`createdAt`、`updatedAt`，默认状态为 `open`。

修改状态请求：`{"status":"in_progress"}`，支持 `open / in_progress / resolved`。

成功响应为 `{"data":...}`，失败为 `{"error":{"code":"...","message":"..."}}`。参数错误返回 400，不存在或已删除返回 404。完整约定见 `docs/API.md`。

## 5. AR 基础问题


### 5.1 如果把虚拟场景标注改成真实 AR，平面检测和点击放置通常需要使用哪些 AR Foundation 能力？

用AR Session 和 XR Origin 管理AR会话，相机以及设备位姿的跟踪；采用ARPlaneManager识别切跟踪真实环境中的水平或者垂直平面；通过ARRaycastManager将用户点击的屏幕位置转换为AR射线，用来检测
是否命中已识别平面，从而取得命中位置和朝向；用ARAnchorManager或者ARAAnchor为放置的标记建立空间锚点，帮助它在当前会话中稳定跟随真实的位置。
基本流程：扫描环境 → 识别平面 → 过滤 UI 点击 → 射线命中 → 放置标记 → 填写问题。


### 5.2 为什么只保存 Unity 中的 x/y/z 坐标，通常不能保证应用下次启动后仍对应同一个真实位置？
因为坐标依赖会话原点与跟踪结果，重启后原点可能改变；三个数字不包含与现实环境的绑定。跨会话恢复需要平台支持的持久化锚点/地图及重定位，或利用已知图像、设备标识重新校准，并保存关联 ID 和相对姿态。
普通运行时锚点不等于跨启动持久化。

### 5.3 当 AR 初始化失败、设备不支持或 Tracking 状态异常时，应用应该如何提示用户？
检查可用性并监听 ARSession.stateChanged，结合跟踪状态显示原因。初始化显示进度；不支持时，明确提示并允许退出或进入普通 3D。跟踪不稳时暂停放置，提示缓慢移动、改善光照或对准有纹理区域；恢复后开放操作，保留输入，不假报成功。
