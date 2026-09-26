# Frontend (Angular 22)

The supported way to run the whole system is Docker; see the [root README](../README.md). The commands below are only for working on the frontend directly and need Node.js.

```bash
npm ci          # install the pinned dependencies from package-lock.json
npm start       # ng serve on http://localhost:4200; /api is proxied to http://localhost:5080 (proxy.conf.json)
npm test        # unit tests (Vitest + jsdom), single run
npm run build   # production build into dist/
```

App code calls the API through relative `/api` paths only. Dependency versions are exact (`.npmrc` sets `save-exact=true`).
