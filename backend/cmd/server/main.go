package main

import (
	"context"
	"errors"
	"flag"
	"log"
	"net/http"
	"os"
	"os/signal"
	"path/filepath"
	"strings"
	"time"

	"inspection/internal/handler"
	"inspection/internal/store"
)

func main() {
	addr := flag.String("addr", "127.0.0.1:8080", "HTTP listening address")
	dbPath := flag.String("db", "data/inspection.db", "SQLite file path (relative to working directory)")
	origins := flag.String("origins", "http://localhost:5173,http://127.0.0.1:5173", "Comma-separated allowed browser origins")
	flag.Parse()
	absolute, err := filepath.Abs(*dbPath)
	if err != nil {
		log.Fatal(err)
	}
	s, err := store.Open(absolute)
	if err != nil {
		log.Fatal(err)
	}
	defer s.Close()
	server := &http.Server{Addr: *addr, Handler: handler.New(s, strings.Split(*origins, ",")), ReadHeaderTimeout: 5 * time.Second, ReadTimeout: 10 * time.Second, WriteTimeout: 15 * time.Second, IdleTimeout: 60 * time.Second}
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt)
	defer stop()
	go func() {
		<-ctx.Done()
		timeout, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		_ = server.Shutdown(timeout)
	}()
	log.Printf("API http://%s | SQLite %s", *addr, absolute)
	if err = server.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
		log.Fatal(err)
	}
}
