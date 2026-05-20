#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

COMPOSE=(docker compose)
if ! docker compose version >/dev/null 2>&1; then
  COMPOSE=(docker-compose)
fi

AUTHOR_NAME="${GIT_AUTHOR_NAME:-felix}"
AUTHOR_EMAIL="${GIT_AUTHOR_EMAIL:-felix.kras@gmail.com}"
PARENT_MESSAGE="${1:-Deploy FunnyPot updates}"

ensure_clean_git_state() {
  local repo_dir="$1"
  if [ -d "$repo_dir/.git/rebase-merge" ] || [ -d "$repo_dir/.git/rebase-apply" ] || [ -f "$repo_dir/.git/MERGE_HEAD" ]; then
    printf 'Error: %s has an unfinished merge or rebase. Resolve it before deploying.\n' "$repo_dir" >&2
    exit 1
  fi
}

commit_submodule_if_needed() {
  local path="$1"
  local branch="$2"
  local message="$3"

  if [ ! -d "$path/.git" ]; then
    return
  fi

  ensure_clean_git_state "$path"

  if [ -n "$(git -C "$path" status --porcelain)" ]; then
    printf 'Committing %s changes...\n' "$path"
    git -C "$path" add -A
    git -C "$path" -c user.name="$AUTHOR_NAME" -c user.email="$AUTHOR_EMAIL" commit -m "$message"
  else
    printf '%s has no local changes.\n' "$path"
  fi

  printf 'Pushing %s %s...\n' "$path" "$branch"
  git -C "$path" push "git@github.com:FelixKras/FunnyPot.ai.git" "$branch"
}

commit_parent_if_needed() {
  ensure_clean_git_state "$ROOT_DIR"

  if [ -n "$(git status --porcelain)" ]; then
    printf 'Committing parent repository changes...\n'
    git add -A
    git commit -m "$PARENT_MESSAGE"
    git push
  else
    printf 'Parent repository has no local changes.\n'
  fi
}

printf 'Updating graphify...\n'
graphify update .

commit_submodule_if_needed "frontend-main" "main" "Update dashboard UI"
commit_submodule_if_needed "frontend" "data" "Update dashboard data"

commit_parent_if_needed

printf 'Redeploying Docker Compose service...\n'
"${COMPOSE[@]}" up -d --build

printf 'Service status:\n'
"${COMPOSE[@]}" ps

printf 'Deploy complete.\n'
