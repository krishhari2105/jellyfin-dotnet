# Branch strategy

- `main` is the stable branch.
- `development` is the active integration branch.
- Feature and fix branches should normally start from `development` and target `development`.
- After validation, the repository owner periodically merges `development` into `main`.
- `tailscale` is a frozen legacy branch retained for historical reference and must not receive normal development commits.
- The annotated `tailscale-legacy-final` tag identifies the final archived state of that legacy branch.
