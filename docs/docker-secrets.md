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

To receive ntfy notifications when an SSH authentication attempt is received, add a private topic URL to `.env`:

```text
NTFY_TOPIC_URL=https://ntfy.sh/funnypot-your-long-random-topic
```

Optional settings:

```text
NOTIFY_ENABLED=true
NTFY_PRIORITY=high
NTFY_TAGS=warning,computer
```

Use a long unguessable ntfy topic and keep notifications minimal. FunnyPot sends the remote endpoint, session key, username, auth method, attempt number, decision, and SSH client version, but not passwords or command logs. Raw TCP connections do not notify, which avoids Docker healthcheck noise.
