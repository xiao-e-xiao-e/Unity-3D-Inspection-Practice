# 接口约定（实施基准）

本文件描述预期接口，运行验证记录见 TEST_PLAN.md。基础地址 `http://localhost:8080`，JSON 使用 UTF-8。Unity 与管理端必须连接同一服务。

## 数据结构

```json
{
  "id": "后端生成的 UUID",
  "title": "1 号机柜温度异常",
  "description": "机柜右侧温度明显偏高",
  "priority": "high",
  "status": "open",
  "position": { "x": 1.25, "y": 0.8, "z": 2.4 },
  "createdAt": "2026-11-01T08:00:00Z",
  "updatedAt": "2026-11-01T08:00:00Z"
}
```

- `position` 为 Unity 世界坐标；三轴必须显式给出有限数值，0 是有效值。
- `priority` 仅允许 `low`、`medium`、`high`。
- `status` 仅允许 `open`、`in_progress`、`resolved`。
- 时间由服务端生成，UTC RFC 3339 格式。
- 标题去除首尾空格后必须非空；描述可省略，保存为空字符串。

## GET /api/health

服务及数据库可用时返回 `200`：`{"data":{"status":"ok"}}`。数据库不可用返回 `503`。

## POST /api/issues

请求头 `Content-Type: application/json`。Unity 为每次新建草稿生成一个 `Idempotency-Key`（UUID），同一提交失败后的重试沿用该值。

```json
{
  "title": "1 号机柜温度异常",
  "description": "机柜右侧温度明显偏高",
  "priority": "high",
  "position": { "x": 1.25, "y": 0.8, "z": 2.4 }
}
```

首次创建返回 `201`、`{"data":完整问题对象}`。默认状态 `open`，不接受客户端指定 ID、状态或时间。

同一个幂等键及相同创建内容重放返回 `200` 和原问题，不新增记录；相同键搭配不同内容返回 `409`。幂等记录与问题在同一事务持久化，服务重启后仍有效。不带幂等键的普通接口调用也可创建问题，但不保证重复请求去重。

## GET /api/issues

返回 `200`、`{"data":[完整问题对象]}`，按创建时间倒序（相同时间按 ID 排序），空列表为 `{"data":[]}`，不能为 null。

## PATCH /api/issues/{id}/status

请求 `{"status":"in_progress"}`。三种有效状态之间允许互相切换。

返回 `200`、`{"data":更新后的完整问题对象}`。保留创建时间和其他字段，更新 `updatedAt`。不存在的 ID 返回 `404`。

## 错误响应

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "标题不能为空",
    "field": "title"
  }
}
```

`field` 可省略。错误 JSON、空标题、非法枚举、缺失/非法坐标返回 `400`；未知问题返回 `404`；不支持的方法返回 `405`；错误媒体类型返回 `415`；请求过大返回 `413`；数据库错误返回 `500`。错误不得包含数据库内部信息。

## 跨域

开发时允许 `http://localhost:5173` 和 `http://127.0.0.1:5173`，来源可配置。处理 OPTIONS 预检，允许 GET、POST、PATCH、OPTIONS 和 Content-Type、Idempotency-Key 请求头。没有 Origin 的桌面 Unity 请求正常处理。
