# Demeure frontend

React + TypeScript + Vite, Tailwind CSS 4, and locally owned shadcn/ui components.
See the [root README](../README.md) for the backend, architecture, tenant-isolation
requirements, environment configuration, and full setup.

From this directory, with Node.js 22.12+:

```powershell
Copy-Item .env.example .env.local
npm ci
npm run dev
```

Copy the template only on first setup. Start the API separately, then open
`http://localhost:5173`. Requests to `/api` are proxied to `http://localhost:5076`.

```powershell
npm run lint
npm test
npm run build
npm run preview
```

Preview uses port 4173 and still requires the real API. Production deployments
must reverse-proxy `/api`; the Vite proxy is development/preview-only.

## UI components

`components.json` configures shadcn/ui for Vite, TypeScript, the `@/` source alias,
and Tailwind 4 CSS variables. The existing refresh control uses
`src/components/ui/button.tsx`; `src/lib/utils.ts` merges conditional Tailwind classes.
Add only components needed by requested features, for example:

```powershell
npx shadcn@latest add card
```

Do not put secrets in `VITE_*` variables. These values become public browser code.
