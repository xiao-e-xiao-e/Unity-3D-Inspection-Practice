package handler

import (
	"encoding/json"
	"errors"
	"io"
	"log"
	"math"
	"mime"
	"net/http"
	"strings"

	"inspection/internal/model"
	"inspection/internal/store"
)

type API struct {
	store   *store.Store
	origins map[string]bool
}
type errorBody struct {
	Code    string `json:"code"`
	Message string `json:"message"`
	Field   string `json:"field,omitempty"`
}

func New(s *store.Store, origins []string) http.Handler {
	a := &API{store: s, origins: map[string]bool{}}
	for _, origin := range origins {
		a.origins[strings.TrimSpace(origin)] = true
	}
	return http.HandlerFunc(a.serve)
}

func send(w http.ResponseWriter, status int, data any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(data)
}
func fail(w http.ResponseWriter, status int, code, message, field string) {
	send(w, status, map[string]any{"error": errorBody{code, message, field}})
}
func success(w http.ResponseWriter, status int, data any) {
	send(w, status, map[string]any{"data": data})
}

func (a *API) serve(w http.ResponseWriter, r *http.Request) {
	w.Header().Add("Vary", "Origin")
	w.Header().Set("Cache-Control", "no-store")
	if origin := r.Header.Get("Origin"); origin != "" {
		if !a.origins[origin] {
			fail(w, 403, "ORIGIN_NOT_ALLOWED", "当前网页来源未被允许", "")
			return
		}
		w.Header().Set("Access-Control-Allow-Origin", origin)
		w.Header().Set("Access-Control-Allow-Methods", "GET, POST, PATCH, OPTIONS")
		w.Header().Set("Access-Control-Allow-Headers", "Content-Type, Idempotency-Key")
	}
	if r.Method == http.MethodOptions {
		w.WriteHeader(204)
		return
	}
	switch {
	case r.URL.Path == "/api/health":
		if !method(w, r, "GET") {
			return
		}
		if err := a.store.Ping(r.Context()); err != nil {
			log.Printf("health: %v", err)
			fail(w, 503, "UNAVAILABLE", "数据库暂不可用", "")
			return
		}
		success(w, 200, map[string]string{"status": "ok"})
	case r.URL.Path == "/api/issues":
		switch r.Method {
		case "GET":
			issues, err := a.store.List(r.Context())
			if a.storageError(w, err) {
				return
			}
			success(w, 200, issues)
		case "POST":
			a.create(w, r)
		default:
			method(w, r, "GET", "POST")
		}
	case strings.HasPrefix(r.URL.Path, "/api/issues/") && strings.HasSuffix(r.URL.Path, "/status"):
		id := strings.TrimSuffix(strings.TrimPrefix(r.URL.Path, "/api/issues/"), "/status")
		if id == "" || strings.Contains(id, "/") {
			fail(w, 404, "NOT_FOUND", "接口不存在", "")
			return
		}
		if !method(w, r, "PATCH") {
			return
		}
		a.update(w, r, id)
	default:
		fail(w, 404, "NOT_FOUND", "接口不存在", "")
	}
}

func method(w http.ResponseWriter, r *http.Request, allowed ...string) bool {
	for _, m := range allowed {
		if r.Method == m {
			return true
		}
	}
	w.Header().Set("Allow", strings.Join(allowed, ", "))
	fail(w, 405, "METHOD_NOT_ALLOWED", "请求方法不支持", "")
	return false
}

func decode(w http.ResponseWriter, r *http.Request, v any) bool {
	media, _, err := mime.ParseMediaType(r.Header.Get("Content-Type"))
	if err != nil || media != "application/json" {
		fail(w, 415, "UNSUPPORTED_MEDIA_TYPE", "请使用 application/json", "")
		return false
	}
	r.Body = http.MaxBytesReader(w, r.Body, 65536)
	d := json.NewDecoder(r.Body)
	d.DisallowUnknownFields()
	err = d.Decode(v)
	if err == nil {
		var extra any
		if next := d.Decode(&extra); next != io.EOF {
			if next == nil {
				err = errors.New("multiple JSON values")
			} else {
				err = next
			}
		}
	}
	if err != nil {
		var size *http.MaxBytesError
		if errors.As(err, &size) {
			fail(w, 413, "BODY_TOO_LARGE", "请求内容过大", "")
		} else {
			fail(w, 400, "INVALID_JSON", "JSON 格式、字段或字段类型错误", "")
		}
		return false
	}
	return true
}

func (a *API) create(w http.ResponseWriter, r *http.Request) {
	// Pointer coordinates distinguish a missing/null axis from the valid value zero.
	var input struct {
		Title       string `json:"title"`
		Description string `json:"description"`
		Priority    string `json:"priority"`
		Position    *struct {
			X *float64 `json:"x"`
			Y *float64 `json:"y"`
			Z *float64 `json:"z"`
		} `json:"position"`
	}
	if !decode(w, r, &input) {
		return
	}
	input.Title = strings.TrimSpace(input.Title)
	if input.Title == "" {
		fail(w, 400, "VALIDATION_ERROR", "标题不能为空", "title")
		return
	}
	if !model.ValidPriority(input.Priority) {
		fail(w, 400, "VALIDATION_ERROR", "优先级必须为 low、medium 或 high", "priority")
		return
	}
	if input.Position == nil || input.Position.X == nil || input.Position.Y == nil || input.Position.Z == nil {
		fail(w, 400, "VALIDATION_ERROR", "位置必须包含 x、y、z 数值", "position")
		return
	}
	p := model.Position{X: *input.Position.X, Y: *input.Position.Y, Z: *input.Position.Z}
	for _, v := range []float64{p.X, p.Y, p.Z} {
		if math.IsNaN(v) || math.IsInf(v, 0) || math.Abs(v) > math.MaxFloat32 {
			fail(w, 400, "VALIDATION_ERROR", "位置必须为 Unity 可表示的有限数值", "position")
			return
		}
	}
	key := r.Header.Get("Idempotency-Key")
	if len(key) > 128 || strings.TrimSpace(key) != key {
		fail(w, 400, "VALIDATION_ERROR", "幂等键格式不正确", "Idempotency-Key")
		return
	}
	i, replay, err := a.store.Create(r.Context(), model.CreateIssue{Title: input.Title, Description: input.Description, Priority: input.Priority, Position: p}, key)
	if a.storageError(w, err) {
		return
	}
	status := 201
	if replay {
		status = 200
	}
	success(w, status, i)
}

func (a *API) update(w http.ResponseWriter, r *http.Request, id string) {
	var input struct {
		Status string `json:"status"`
	}
	if !decode(w, r, &input) {
		return
	}
	if !model.ValidStatus(input.Status) {
		fail(w, 400, "VALIDATION_ERROR", "状态必须为 open、in_progress 或 resolved", "status")
		return
	}
	i, err := a.store.UpdateStatus(r.Context(), id, input.Status)
	if a.storageError(w, err) {
		return
	}
	success(w, 200, i)
}

func (a *API) storageError(w http.ResponseWriter, err error) bool {
	if err == nil {
		return false
	}
	switch {
	case errors.Is(err, store.ErrNotFound):
		fail(w, 404, "NOT_FOUND", "问题不存在", "")
	case errors.Is(err, store.ErrConflict):
		fail(w, 409, "IDEMPOTENCY_CONFLICT", "同一个幂等键已用于不同的提交内容", "")
	default:
		log.Printf("storage: %v", err)
		fail(w, 500, "STORAGE_ERROR", "数据保存或读取失败，请稍后重试", "")
	}
	return true
}
