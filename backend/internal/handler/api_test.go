package handler

import (
	"encoding/json"
	"fmt"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"strings"
	"sync"
	"testing"

	"inspection/internal/model"
	"inspection/internal/store"
)

const valid = `{"title":"  1 号机柜温度异常  ","description":"右侧温度偏高","priority":"high","position":{"x":1.25,"y":0,"z":-2.4}}`

func request(h http.Handler, method, path, body, key string) *httptest.ResponseRecorder {
	r := httptest.NewRequest(method, path, strings.NewReader(body))
	r.Header.Set("Content-Type", "application/json")
	if key != "" {
		r.Header.Set("Idempotency-Key", key)
	}
	w := httptest.NewRecorder()
	h.ServeHTTP(w, r)
	return w
}

func setup(t *testing.T) (*store.Store, http.Handler, string) {
	t.Helper()
	path := filepath.Join(t.TempDir(), "test.db")
	s, err := store.Open(path)
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { s.Close() })
	return s, New(s, []string{"http://localhost:5173"}), path
}
func issue(t *testing.T, w *httptest.ResponseRecorder, status int) model.Issue {
	t.Helper()
	if w.Code != status {
		t.Fatalf("status %d: %s (want %d)", w.Code, w.Body.String(), status)
	}
	var v struct {
		Data model.Issue `json:"data"`
	}
	if err := json.Unmarshal(w.Body.Bytes(), &v); err != nil {
		t.Fatal(err)
	}
	return v.Data
}

func TestLifecycleAndRestart(t *testing.T) {
	s, h, path := setup(t)
	if w := request(h, "GET", "/api/issues", "", ""); w.Code != 200 || strings.TrimSpace(w.Body.String()) != `{"data":[]}` {
		t.Fatal(w.Body.String())
	}
	if w := request(h, "GET", "/api/health", "", ""); w.Code != 200 {
		t.Fatal(w.Code)
	}
	created := issue(t, request(h, "POST", "/api/issues", valid, "draft-1"), 201)
	if created.ID == "" || created.Status != "open" || created.Title != "1 号机柜温度异常" || created.Position.Y != 0 || created.CreatedAt == "" || created.CreatedAt != created.UpdatedAt {
		t.Fatalf("bad create: %+v", created)
	}
	for _, status := range []string{"in_progress", "resolved", "open", "resolved"} {
		updated := issue(t, request(h, "PATCH", "/api/issues/"+created.ID+"/status", `{"status":"`+status+`"}`, ""), 200)
		if updated.Status != status || updated.CreatedAt != created.CreatedAt || updated.Position != created.Position || updated.Title != created.Title {
			t.Fatalf("bad update: %+v", updated)
		}
	}
	if err := s.Close(); err != nil {
		t.Fatal(err)
	}
	reopened, err := store.Open(path)
	if err != nil {
		t.Fatal(err)
	}
	defer reopened.Close()
	h = New(reopened, []string{"http://localhost:5173"})
	replay := issue(t, request(h, "POST", "/api/issues", valid, "draft-1"), 200)
	if replay.ID != created.ID || replay.Status != "resolved" {
		t.Fatal(replay)
	}
	var list struct {
		Data []model.Issue `json:"data"`
	}
	w := request(h, "GET", "/api/issues", "", "")
	if err := json.Unmarshal(w.Body.Bytes(), &list); err != nil {
		t.Fatal(err)
	}
	if len(list.Data) != 1 || list.Data[0].Status != "resolved" {
		t.Fatal(list)
	}
	if w := request(h, "POST", "/api/issues", strings.Replace(valid, "high", "low", 1), "draft-1"); w.Code != 409 {
		t.Fatal(w.Code, w.Body.String())
	}
}

func TestInvalidInput(t *testing.T) {
	_, h, _ := setup(t)
	cases := []struct{ name, body string }{
		{"empty title", strings.Replace(valid, "  1 号机柜温度异常  ", "   ", 1)},
		{"priority", strings.Replace(valid, "high", "urgent", 1)},
		{"missing axis", strings.Replace(valid, `"y":0,`, "", 1)},
		{"null axis", strings.Replace(valid, `"y":0`, `"y":null`, 1)},
		{"string axis", strings.Replace(valid, `"y":0`, `"y":"0"`, 1)},
		{"overflow", strings.Replace(valid, `"y":0`, `"y":1e99`, 1)},
		{"unknown field", strings.Replace(valid, `"title":`, `"id":"forged","title":`, 1)},
		{"missing position", `{"title":"test","priority":"low"}`},
		{"invalid JSON", "{"}, {"multiple JSON", valid + valid}, {"null body", "null"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			w := request(h, "POST", "/api/issues", c.body, "")
			if w.Code != 400 || !strings.Contains(w.Body.String(), `"error"`) {
				t.Fatal(w.Code, w.Body.String())
			}
		})
	}
	for _, c := range []struct {
		method, path, body string
		status             int
	}{
		{"PATCH", "/api/issues/missing/status", `{"status":"resolved"}`, 404},
		{"PATCH", "/api/issues/missing/status", `{"status":"done"}`, 400},
		{"DELETE", "/api/issues", "", 405}, {"GET", "/unknown", "", 404},
		{"POST", "/api/issues", `{"title":"` + strings.Repeat("a", 70000) + `"}`, 413},
	} {
		if w := request(h, c.method, c.path, c.body, ""); w.Code != c.status {
			t.Fatal(w.Code, w.Body.String(), c.status)
		}
	}
	r := httptest.NewRequest("POST", "/api/issues", strings.NewReader(valid))
	w := httptest.NewRecorder()
	h.ServeHTTP(w, r)
	if w.Code != 415 {
		t.Fatal(w.Code)
	}
}

func TestConcurrentIdempotencyAndUniqueIDs(t *testing.T) {
	_, h, _ := setup(t)
	var wg sync.WaitGroup
	for n := 0; n < 16; n++ {
		wg.Add(1)
		go func(n int) {
			defer wg.Done()
			key := "same-draft"
			if n >= 8 {
				key = fmt.Sprintf("draft-%d", n)
			}
			w := request(h, "POST", "/api/issues", valid, key)
			if w.Code != 200 && w.Code != 201 {
				t.Errorf("%d %s", w.Code, w.Body.String())
			}
		}(n)
	}
	wg.Wait()
	w := request(h, "GET", "/api/issues", "", "")
	var list struct {
		Data []model.Issue `json:"data"`
	}
	json.Unmarshal(w.Body.Bytes(), &list)
	if len(list.Data) != 9 {
		t.Fatalf("got %d issues, want 9", len(list.Data))
	}
	ids := map[string]bool{}
	for _, i := range list.Data {
		if ids[i.ID] {
			t.Fatal("duplicate id")
		}
		ids[i.ID] = true
	}
}

func TestCORSAndUnavailableStore(t *testing.T) {
	s, h, _ := setup(t)
	r := httptest.NewRequest("OPTIONS", "/api/issues/a/status", nil)
	r.Header.Set("Origin", "http://localhost:5173")
	r.Header.Set("Access-Control-Request-Method", "PATCH")
	r.Header.Set("Access-Control-Request-Headers", "content-type")
	w := httptest.NewRecorder()
	h.ServeHTTP(w, r)
	if w.Code != 204 || w.Header().Get("Access-Control-Allow-Origin") != "http://localhost:5173" || !strings.Contains(w.Header().Get("Access-Control-Allow-Methods"), "PATCH") {
		t.Fatal(w.Code, w.Header())
	}
	r.Header.Set("Origin", "http://unapproved.test")
	w = httptest.NewRecorder()
	h.ServeHTTP(w, r)
	if w.Code != 403 {
		t.Fatal(w.Code)
	}
	s.Close()
	if w = request(h, "POST", "/api/issues", valid, ""); w.Code != 500 {
		t.Fatal(w.Code)
	}
	if w = request(h, "GET", "/api/health", "", ""); w.Code != 503 {
		t.Fatal(w.Code)
	}
}

func TestDetailAndSoftDeleteAcrossRestart(t *testing.T) {
	s, h, path := setup(t)
	created := issue(t, request(h, "POST", "/api/issues", valid, "delete-draft"), 201)
	uri := "/api/issues/" + created.ID
	detail := issue(t, request(h, "GET", uri, "", ""), 200)
	if detail != created {
		t.Fatalf("detail mismatch: %+v", detail)
	}
	if w := request(h, "DELETE", uri, "", ""); w.Code != 200 || !strings.Contains(w.Body.String(), `"deleted":true`) {
		t.Fatal(w.Code, w.Body.String())
	}
	s.Close()
	reopened, err := store.Open(path)
	if err != nil {
		t.Fatal(err)
	}
	defer reopened.Close()
	h = New(reopened, []string{"http://localhost:5173"})
	for _, method := range []string{"GET", "DELETE"} {
		if w := request(h, method, uri, "", ""); w.Code != 404 {
			t.Fatal(method, w.Code, w.Body.String())
		}
	}
	if w := request(h, "PATCH", uri+"/status", `{"status":"resolved"}`, ""); w.Code != 404 {
		t.Fatal(w.Code)
	}
	if w := request(h, "POST", "/api/issues", valid, "delete-draft"); w.Code != 410 {
		t.Fatal(w.Code, w.Body.String())
	}
	if w := request(h, "GET", "/api/issues", "", ""); strings.TrimSpace(w.Body.String()) != `{"data":[]}` {
		t.Fatal(w.Body.String())
	}
	// A genuinely new draft remains independent from a deleted submission.
	if next := issue(t, request(h, "POST", "/api/issues", valid, "new-draft"), 201); next.ID == created.ID {
		t.Fatal("reused ID")
	}
	r := httptest.NewRequest("OPTIONS", uri, nil)
	r.Header.Set("Origin", "http://localhost:5173")
	r.Header.Set("Access-Control-Request-Method", "DELETE")
	w := httptest.NewRecorder()
	h.ServeHTTP(w, r)
	if w.Code != 204 || !strings.Contains(w.Header().Get("Access-Control-Allow-Methods"), "DELETE") {
		t.Fatal(w.Code, w.Header())
	}
}
