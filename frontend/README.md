# Frontend (Angular 22)

The supported way to run the whole system is Docker; see the [root README](../README.md). The commands below are only for working on the frontend directly and need Node.js.

```bash
npm ci          # install the pinned dependencies from package-lock.json
npm start       # ng serve on http://localhost:4200 (development build)
npm test        # unit tests (Vitest + jsdom), single run
npm run build   # production build into dist/
```

App code calls the API through relative `/api` paths only. Dependency versions are exact (`.npmrc` sets `save-exact=true`).

## Mock API (development build only)

`npm start` uses the development build, where `src/environments/environment.development.ts` sets `useMocks = true`. `/api` calls are then answered in the browser by `src/app/core/mock-api.interceptor.ts` (one organisation, sites "Head Office" and "Warehouse North", threshold 5,000). Mock users, all with the password `password`:

| Email | Role |
|---|---|
| `bob@acme.example` | Requester |
| `alice@acme.example` | Approver |
| `carol@acme.example` | Approver |

Mock data resets when the page reloads. Set `useMocks = false` to send `/api` to the real API on `http://localhost:5080` through `proxy.conf.json`. The production build (`npm run build`, used by the web container) never includes the mock.
