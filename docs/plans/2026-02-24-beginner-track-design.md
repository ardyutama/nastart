# Design: Beginner Track — .NET 10 Minimal API → MediatR → Telegram Bot

**Date**: 2026-02-24  
**Status**: Approved

---

## Context

The existing lessons (`docs/lessons/`) assume prior knowledge of Vertical Slice Architecture and MediatR. A learner who knows Python/JS but is new to .NET 10 will get lost immediately.

This track is a separate, gentler on-ramp that builds the same Nastart application but starts from first principles.

---

## Decisions Made

| Decision | Choice | Reason |
|----------|--------|--------|
| Approach | Option A — one project, lesson checkpoints | Mirrors real development, no repetition |
| Per-lesson tone | Technical-first, no ELI5 | User knows programming (Python/JS) |
| Python/JS comparisons | Yes, where helpful | Fastest way to internalize new syntax |
| Exercises | No | Keep focus on building, not quizzes |

---

## Folder Structure

```
docs/beginner-track/
├── 00-overview.md
├── phase-1-crud/
│   ├── 01-first-dotnet-project.md
│   ├── 02-your-first-endpoint.md
│   ├── 03-crud-in-memory.md
│   ├── 04-ef-core-postgres.md
│   └── 05-validation-openapi.md
├── phase-2-mediatr/
│   ├── 06-what-is-mediatr.md
│   ├── 07-first-command.md
│   ├── 08-pipeline-behaviors.md
│   └── 09-refactor-all-features.md
└── phase-3-telegram/
    ├── 10-create-your-bot.md
    ├── 11-webhook-endpoint.md
    ├── 12-ingredients-command.md
    └── 13-cost-command.md
```

---

## Per-Lesson Template

```
## Lesson N: Title
### 🎯 What you'll build
### 🔧 The .NET way (with Python/JS comparison)
### Step 1 — ...
### Step 2 — ...
### ✅ Run it
### 🔑 Key concepts
### ➡️ Next
```

---

## Phase Summaries

### Phase 1 — Minimal API CRUD (5 lessons)
Build a raw, working CRUD API for Ingredients with no abstractions. The goal is to understand the platform before adding patterns. By lesson 5 the API has a database, validation, and Swagger docs.

### Phase 2 — MediatR (4 lessons)
Refactor the same project to use MediatR. The student sees concretely what problem it solves — tight coupling between endpoints and business logic. By lesson 9 all endpoints delegate to handlers via MediatR.

### Phase 3 — Telegram Bot (4 lessons)
Add a Telegram webhook endpoint to the same project. Bot commands talk to the existing MediatR handlers. By lesson 13 the user can type `/ingredients` and `/cost Nastar` in Telegram and get live data back.
