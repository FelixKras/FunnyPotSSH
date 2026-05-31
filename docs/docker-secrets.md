# Runtime Secrets

The Docker Compose deployment reads sensitive values from `.env` and passes them into the container environment.

Set these values in local `.env` before starting the stack:

```text
GITHUB_TOKEN=ghp_exampletoken
GITHUB_USER=example-user
OPENROUTER_API_KEY=sk-or-example
```

Start or recreate the container after creating or rotating secrets:

```bash
docker compose up --build --force-recreate
```

The app can also read Docker secrets from `/run/secrets/<name>` if they are mounted by another deployment system, but this Compose file uses `.env` pass-through. `.env` is ignored by Git and excluded from the Docker build context.

For static dashboard publication, configure one remote target through `.env` or the runtime environment:

```text
GITHUB_REPOSITORY=owner/repo
```

Alternatively use `STATIC_SITE_REMOTE_URL=https://github.com/owner/repo.git`, `GITHUB_REMOTE_URL=https://github.com/owner/repo.git`, or `GITHUB_REPO=repo` with `GITHUB_USER` as the owner. The data branch defaults to `data`; override it with `GITHUB_DATA_BRANCH`.
