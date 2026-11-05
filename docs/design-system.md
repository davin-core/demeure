# Demeure design system

## Direction and scope

Demeure is modern, professional and trustworthy without feeling institutional.
The existing developer workspace supplied the direction: warm ivory, forest
greens, a lime monogram, fine architectural lines and editorial serif headings.
The name remains **Demeure**; "Denmeure" was a typo.

Open `/design-system` on the frontend for the interactive component gallery.
The developer workspace remains at `/`, including its real API health check and
refresh/error handling. Gallery forms, dialog confirmations and notification
actions are explicitly labeled **local-only examples**. They do not call the API,
create accounts or save property data. Its table contains a component inventory,
not fictional properties or residents. No authentication screens or management
workflows were introduced.

## Foundations

Colors are semantic CSS custom properties exposed through Tailwind's inline
theme in [index.css](../frontend/src/index.css). Prefer tokens rather than literal
colors in features.

| Role | Value | Tailwind examples |
| --- | --- | --- |
| Background / ivory | `#f4f4ed` | `bg-background` |
| Foreground / forest | `#26352e` | `text-foreground` |
| Primary / evergreen | `#34745d` | `bg-primary text-primary-foreground` |
| Accent / lime | `#d7ed76` | `bg-accent text-accent-foreground` |
| Card / porcelain | `#fffffc` | `bg-card text-card-foreground` |
| Muted surface | `#eef0e7` | `bg-muted text-muted-foreground` |
| Muted text | `#59645d` | `text-muted-foreground` |
| Decorative border / stone | `#d6d8ce` | `border-border` |
| Control boundary | `#808d83` | `border-input` |
| Focus ring | `#34745d` | `ring-ring` |
| Success | `#25634c` on `#eaf3ed` | `text-success bg-success-subtle` |
| Warning | `#855513` on `#fbf2df` | `text-warning bg-warning-subtle` |
| Information | `#315c76` on `#ecf2f6` | `text-info bg-info-subtle` |
| Destructive | `#b42318` on `#fceeea` | `text-destructive bg-destructive-subtle` |

Decorative borders deliberately stay quiet. Controls use the stronger `input`
token so their boundaries remain distinguishable. Text/surface token pairs are
tested against 4.5:1 contrast; control boundaries and focus rings against 3:1.
These tests do not constitute a complete accessibility certification.

- **Interface:** `font-sans`, Aptos / Segoe UI / sans-serif. Body 14-16px,
  secondary guidance 12px; small uppercase metadata is used sparingly.
- **Display:** `font-display`, Georgia / Times New Roman / serif. Reserved for
  editorial page openings and dialog headings; the gallery hero scales 44-72px.
- **Metadata:** `font-mono`, Cascadia Code / Consolas / monospace.
- **Spacing:** a 4px base unit (`p-1`). Typical steps: 4, 8, 16, 24, 32, 48px.
- **Radii:** `rounded-sm` 2px, `rounded-md` 4px, `rounded-lg` 6px,
  `rounded-xl` 10px. Circular shapes are reserved for the monogram.
- **Depth:** border-first surfaces, very subtle card shadow, stronger depth only
  for portaled overlays and notifications.

Fonts are installed-system fallbacks, with no external font service, network
requests or font-license dependency. Reduced-motion preferences disable
nonessential animation and transitions.

## Reusable components

The subsequent [integration stage](frontend-integration.md) adds a reusable
Pagination component and a paged component-inventory example, without business pages.

The [UI directory](../frontend/src/components/ui) uses the existing shadcn/ui
configuration and locally owned React/TypeScript components. Radix provides
accessible interactive primitives; Tailwind, CVA and `cn` provide styling.
No icon package, router, form library or notification dependency was added.
The existing Button implementation was extended rather than overwritten.

| Component | Contract |
| --- | --- |
| `Button` | Primary, accent, outline, secondary, ghost, destructive, link; small/default/large/icon sizes. Defaults to `type="button"`; use explicit submit and accessible icon labels. `asChild` supports actual anchors. |
| `Input`, `Textarea` | Native semantics; focus, disabled and invalid styles; forward native props and React 19 refs. |
| `Field` | Render-prop wiring for `id`, label, description and error; `aria-invalid` and error announcement. `required` marks the label; also set `required` on the actual control. |
| `Checkbox` | Radix checked/unchecked/mixed state; enlarged pointer hit area and keyboard operation. Pair with a visible label. |
| `Select` | Radix combobox with portaled options, keyboard selection and disabled states. Connect the trigger to `Field`. |
| `Card` | Header/title/description/content/footer composition; titles use `h3`. Choose heading levels appropriate to the containing page. |
| `Badge` | Default/accent/success/warning/destructive/info text labels. Never encode status only in color. |
| `Table` | Native table/head/body/row/cell/caption semantics. Keyboard-focusable overflow region, customizable `scrollLabel`, column scope by default. Sorting is controlled by the consuming feature, not simulated by the table component. |
| `Dialog` | Radix portal, overlay, labeled title/description, scrollable viewport-bounded content, Escape, focus trapping/restoration and explicit close. |
| `Alert` | Info/success/warning/destructive text and icon. Polite status by default; destructive defaults to alert. Override `role` for static explanatory examples when appropriate. |
| `NotificationProvider` | Persistent, dismissible in-memory notifications; polite status or urgent error. Scrolls instead of evicting unread messages; no automatic timeout. |
| `Brand` | Shared home link and lime D monogram. |
| `Navigation`, `AppShell` | Labeled landmarks, current item, skip link, desktop sidebar and modal mobile navigation. Items are real links supplied by the caller. |

### Forms

```tsx
<Field label="Email address" required description="Where we can reach you."
  error={emailError}>
  {(props) => <Input {...props} required type="email"
    value={email} onChange={(event) => setEmail(event.target.value)} />}
</Field>
```

Field does not own validation or persistence. Features must validate their
request, show specific errors, focus the first invalid control, call the real API
and announce success only after a successful response. The gallery's simple email
validation demonstrates presentation only, not account-registration rules.

### Notifications

```tsx
import { NotificationProvider } from '@/components/ui/notifications'
import { useNotification } from '@/components/ui/use-notification'

// Mount NotificationProvider above consumers.
const notify = useNotification()
notify({ title: 'Changes saved', description: 'Your update is complete.',
  variant: 'success' })
```

The hook throws explicitly if the provider is missing. Call notifications in
event handlers or effects, never during rendering. The example above belongs
after a successful real save, not before it.

## Responsive and accessibility conventions

- The shell uses a desktop sidebar at 1024px and a mobile Radix dialog drawer
  below that width. Selecting a section closes the drawer.
- Card grids collapse to one column; palette swatches retain two columns on
  small screens. Tables scroll within their own labeled region.
- Dialogs remain inside the viewport; long content scrolls rather than cropping
  controls. Close/menu icon actions are 44px.
- Labels are persistent, placeholders supplementary. Errors include text.
- All interactive components use visible focus styling. Provide names for icon
  actions and titles/descriptions for dialogs.
- Browser verification covered 320, 375, 768, 1024 and 1440px without horizontal
  page overflow, form validation, dropdown selection, table sorting, notifications,
  dialog/drawer keyboard traps, Escape and focus restoration.
- Test with actual screen readers and supported mobile browsers before release;
  browser DOM checks alone cannot prove assistive-technology behavior.

## Routing and deployment

[AppRouter](../frontend/src/app-router.tsx) dispatches `/design-system` (including
a trailing slash) to a lazy-loaded gallery, and the five
[application layout routes](layouts.md) to session-aware layouts. `/` and unknown
paths preserve the workspace behavior. Navigation uses normal anchors; these
layout-only routes do not justify adding a client routing library. Introduce
nested routing when real business screens require it.

Production hosting must rewrite non-API frontend routes such as `/design-system`
to `index.html`; do not rewrite `/api` requests to the frontend. The gallery has
no backend dependency. Vite's development and preview servers support this
fallback automatically.

## Validation and change inventory

```powershell
npm run lint --prefix frontend
npm test --prefix frontend
npm run build --prefix frontend
```

Verified for this stage: lint passed without warnings, all 43 frontend tests
passed (including 12 new component contracts and 15 contrast cases), and the
production TypeScript/Vite build passed. Browser checks also verified the real
workspace health request/refresh and the gallery link, with no JavaScript runtime
errors. An unavailable API is still shown as an error, not replaced with mock
health data.

New files:

- [app-router.tsx](../frontend/src/app-router.tsx)
- [design-system.tsx](../frontend/src/pages/design-system.tsx)
- [brand.tsx](../frontend/src/components/layout/brand.tsx)
- [navigation.tsx](../frontend/src/components/layout/navigation.tsx)
- UI primitives: [icon](../frontend/src/components/ui/icon.tsx),
  [card](../frontend/src/components/ui/card.tsx),
  [badge](../frontend/src/components/ui/badge.tsx),
  [input](../frontend/src/components/ui/input.tsx),
  [field](../frontend/src/components/ui/field.tsx),
  [checkbox](../frontend/src/components/ui/checkbox.tsx),
  [select](../frontend/src/components/ui/select.tsx),
  [dialog](../frontend/src/components/ui/dialog.tsx),
  [alert](../frontend/src/components/ui/alert.tsx),
  [table](../frontend/src/components/ui/table.tsx),
  [notifications](../frontend/src/components/ui/notifications.tsx),
  [notification hook](../frontend/src/components/ui/use-notification.ts).
- [Component contracts](../frontend/src/components/ui/components.test.tsx) and
  [token contrast tests](../frontend/src/components/ui/tokens.test.ts).
- This guide.

Modified files:

- [package.json](../frontend/package.json) and
  [package-lock.json](../frontend/package-lock.json): Radix checkbox/dialog/select.
- [index.css](../frontend/src/index.css): semantic design tokens and shared states.
- [App.css](../frontend/src/App.css): scoped workspace heading styles.
- [Button](../frontend/src/components/ui/button.tsx): accent and focus/icon styles.
- [App.tsx](../frontend/src/App.tsx): shared brand and gallery link.
- [main.tsx](../frontend/src/main.tsx): route entry point.
- [README](../README.md): gallery and documentation discovery.
