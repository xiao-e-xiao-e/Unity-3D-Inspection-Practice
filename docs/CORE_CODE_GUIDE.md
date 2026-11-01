# 核心代码说明

## 一次上报如何贯穿三端

1. `PlayerMovement.cs` 通过 CharacterController 处理移动和碰撞。只有未编辑表单时才读取移动/转向输入。
2. `InspectionController.cs` 在 LateUpdate 处理左键按下：检查当前状态、关闭表单后的帧保护、鼠标是否位于 UI、是否正按右键，然后对第 8 层发射射线。
3. 射线命中的 `hit.point` 被保留为真实世界坐标。小球只做显示，可沿法线偏移 0.055，绝不把显示偏移存入问题位置。
4. 草稿只能存在一个：创建后立即进入 Editing；Submitting 中拒绝再次提交；取消销毁草稿；成功把原草稿转为已保存标记。
5. `InspectionApi.cs` 使用 UnityWebRequest、UTF-8 JSON、10 秒超时。Unity JsonUtility 用可序列化的包装类读取 `data` 和 `error`。
6. Go `handler/api.go` 校验输入，`store/store.go` 用参数化 SQL 和事务保存，返回完整记录。React 和 Unity 都不自行生成正式 ID 或时间。
7. `admin/src/api/issues.ts` 请求同一个 API。React 保存状态后使用后端返回的对象替换本地记录；失败不乐观改写列表。

## 为什么不会点击 UI 时误放标记

- Canvas 使用 GraphicRaycaster，场景中建立 EventSystem。
- 射线前调用 `IsPointerOverGameObject()`。
- 表单有覆盖全屏的射线阻挡层，编辑/提交状态完全禁用场景点击。
- `CloseForm()` 将 `ignoreClicksThroughFrame` 设为当前帧 + 1。即使取消按钮的点击在本帧改变状态，也不会被同帧 LateUpdate 再用于场景放置。
- 标记在 Ignore Raycast 层并移除 Collider，射线只查询可巡检的第 8 层。

## 坐标中的 0 与字段缺失

Go 入参使用 `*float64` 接收坐标轴：缺少字段或 null 为 nil，显式 0 是非 nil 指针。避免把合法的地面 y=0 当作缺失，也避免把遗漏轴默认为 0。校验后再转换为模型的 float64。额外限制在 Unity float 可表示的范围内。

## 为什么重试不重复写入

- 每次新草稿产生一个 UUID 作为 Idempotency-Key。
- 首次发送时冻结完整 JSON，之后重试沿用同样的 JSON 和 key。
- 后端对规范化输入计算 SHA-256；幂等表存 key、请求指纹、问题 ID。
- 创建问题和保存幂等键在同一个事务。相同键相同内容返回原问题；不同内容返回 409。
- SQLite 连接池限制为一个连接，串行处理本演示的写事务；数据库主键进一步保证键唯一。
- 幂等表也在 SQLite 中，服务重启后仍可去重。

幂等只能识别同一个草稿。取消后重新填写是新草稿；如果前次请求响应丢失，应先刷新确认，避免把同一业务问题再次作为新问题上报。

## 持久化、时间和并发

`schema.sql` 通过 go:embed 内嵌，首次打开自动建表。优先级和状态同时受 Go 校验与 SQL CHECK 约束。创建使用事务，状态更新在事务中完成更新并读取完整记录，只有 Commit 成功后才报告成功。创建时间不随状态修改改变。

时间统一采用 UTC、固定九位小数的 RFC 3339 表示，保证文本排序与时间顺序一致。SQLite WAL 和 busy_timeout 支持正常的本地数据库访问；这是低并发练习项目，不宣称具备生产级多实例写入能力。

## React 网络失败处理

API 层统一处理 HTTP 错误、JSON 格式错误、连接失败和超时，并检查响应模型。页面保留最后成功数据，显示 role=alert 错误和重试按钮。同步 ref 锁保证刷新和保存不会重叠，避免旧列表覆盖刚保存的状态。

## 建议用户亲自解释的代码

- 找出 `hit.point` 到 `position.x/y/z` 的赋值路径。
- 解释 `GetMouseButtonDown`、状态机和 UI 判断各自解决什么问题。
- 说明为什么提交失败不立即删除草稿，以及成功时为什么复用原标记。
- 解释 POST、GET、PATCH 三条业务接口分别做什么。
- 解释 SQL 参数占位符、事务与幂等表的作用。
- 解释 React 为什么在服务器保存成功后才更新列表状态。
