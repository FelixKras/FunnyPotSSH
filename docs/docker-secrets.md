# Docker Secrets

The Docker Compose deployment reads sensitive values from local secret files instead of container environment variables.

Create these files locally before starting the stack:

```text
.secrets/github_token
.secrets/github_user
.secrets/openrouter_api_key
```

Each file should contain only the secret value, with no variable name. Example:

```text
ghp_exampletoken
```

Start or recreate the container after creating or rotating secrets:

```bash
docker compose up --build --force-recreate
```

The app reads Docker secrets from `/run/secrets/<name>` and falls back to environment variables for non-Docker development. Do not put real tokens in `.env`; Compose may use `.env` for non-secret interpolation, but secrets should live under `.secrets/`, which is ignored by Git.

For static dashboard publication, configure one remote target through `.env` or the runtime environment:

```text
GITHUB_REPOSITORY=owner/repo
```

Alternatively use `STATIC_SITE_REMOTE_URL=https://github.com/owner/repo.git`, `GITHUB_REMOTE_URL=https://github.com/owner/repo.git`, or `GITHUB_REPO=repo` with `GITHUB_USER` as the owner. The data branch defaults to `data`; override it with `GITHUB_DATA_BRANCH`.
