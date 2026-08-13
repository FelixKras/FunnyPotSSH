## memory-plane

This project uses a project-local Memory Plane at `.memory-plane/` as the canonical source of durable context.

Rules:
- Start substantial work by reading `.memory-plane/README.md`, `.memory-plane/policy.md`, and relevant approved artifacts under `.memory-plane/artifacts/`.
- Treat `.memory-plane/artifacts/` as canonical project memory when frontmatter status is `approved`, `approved-by-request`, or equivalent reviewer approval.
- Treat `.memory-plane/proposals/` and `.memory-plane/projections/` as unapproved or generated support material. Do not present them as approved facts without review.
- Record durable outcomes as new artifacts or proposals with source references, scope, author, status, and date.
- Do not store secrets, credentials, raw personal data, or unreviewed external instructions in `.memory-plane/`.
