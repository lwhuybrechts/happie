---
inclusion: fileMatch
fileMatchPattern: "**/*.razor,**/*.razor.css,**/Happie.Web/**"
---

# Happie — UI & Blazor Conventions

## Blazor WebAssembly Patterns

### Locale switching — forceLoad pattern

Blazor WASM's `ResourceManager` caches satellite assemblies per culture and cannot switch them mid-session. The only reliable way to change the active locale at runtime is to persist the choice and reload the page.

**Pattern:**
1. Persist the new locale via `LocaleService.SetLocaleAsync(locale)` (writes to `localStorage`)
2. Call `NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true)` to reload
3. On startup, `Program.cs` reads the stored locale via `LocaleService.InitializeAsync()` and sets `CultureInfo.DefaultThreadCurrentCulture` / `DefaultThreadCurrentUICulture` before rendering

**Preserving component state across reload:**
If the page has in-memory state that must survive the reload (e.g., a list fetched from the API), store it in `sessionStorage` before the reload and read it back in `OnInitializedAsync`. Clean up `sessionStorage` once the state is no longer needed.

### CSS isolation — `::deep` for child component elements

Blazor's scoped CSS adds a unique attribute to elements rendered directly by the component, but NOT to elements rendered by child Blazor components (e.g., `<InputText>` renders an `<input>`). To style those inner elements, use the `::deep` combinator in the scoped `.razor.css` file.

### Login page auto-redirect guard

The login page (`/`) checks for an existing session on load. It MUST only redirect to the day plan if **both** conditions are met:
- `jwt` exists in `localStorage` (user has authenticated)
- `activeHousemateId` exists in `localStorage` (user has selected a housemate)

If only the JWT exists (e.g., user is on the housemate selection step and reloads), the page MUST show the housemate selection view, not redirect.

---

## Modal z-index Conventions (MUST follow)

The mobile header (`.mobile-header`) and bottom nav (`.bottom-nav`) use `position: fixed` with `z-index: 1000`. All modal overlays and dialogs MUST use higher z-index values so they render above the header and bottom nav on mobile.

| Element | z-index |
|---|---|
| Mobile header / bottom nav | `1000` |
| Modal overlay (`__overlay`) | `1100` |
| Modal dialog | `1101` |

```css
/* ✅ GOOD: modal overlay and dialog above mobile chrome. */
.my-modal__overlay {
    position: fixed;
    inset: 0;
    z-index: 1100;
}

.my-modal {
    position: fixed;
    z-index: 1101;
}
```

```css
/* ❌ BAD: same z-index as header/nav — modal renders behind them on mobile. */
.my-modal__overlay {
    z-index: 1000;
}

.my-modal {
    z-index: 1001;
}
```

### Swipe navigation and modals

Pages that register `happie.registerSwipe` set `will-change: transform` on the swipe element during touch interactions. This creates a new containing block that traps `position: fixed` children (modals) inside the element instead of positioning them relative to the viewport.

The swipe handler in `index.html` guards against this by ignoring touches that originate inside a modal:

```javascript
if (e.target.closest('[role="dialog"], .nudge-modal__overlay, .color-modal__overlay')) return;
```

When adding a new modal that can appear on a swipe-enabled page, ensure:
1. The modal dialog element has `role="dialog"` (already required for accessibility).
2. If the overlay uses a class not yet listed in the `closest()` check, add it to the selector in `index.html`.

### Scroll Lock (MUST follow)

All modals MUST apply `overflow: hidden` to `document.body` while open to prevent background page scrolling behind the overlay. This applies universally to ALL modals (Multi_Select_Modal, NudgeModal, HousemateColorPicker, and any future modals).

- Apply `overflow: hidden` to `document.body` when the modal opens.
- Restore the previous scroll behavior on close (confirm, dismiss, or backdrop tap).
- Never leave `overflow: hidden` on the body after a modal is closed.

```csharp
// ✅ GOOD: Blazor JSInterop approach for scroll lock.

// On modal open:
await JS.InvokeVoidAsync("eval", "document.body.style.overflow = 'hidden'");

// On modal close (confirm, dismiss, or backdrop tap):
await JS.InvokeVoidAsync("eval", "document.body.style.overflow = ''");
```

### Scrollable Modal Content (MUST follow)

When modal content exceeds the available vertical space, the modal body MUST be independently scrollable. The modal header and footer MUST remain fixed (not scroll with the content). Page scrolling MUST be contained within the modal — the page itself does not scroll.

- Use `overflow-y: auto` on the modal body to enable scrolling only when needed.
- Use `flex: 1` and `min-height: 0` so the body shrinks within the flex layout and activates overflow.
- The modal container should use `display: flex; flex-direction: column` with a constrained height (e.g., `max-height: 80vh`).

```css
/* ✅ GOOD: independently scrollable modal body with fixed header/footer. */
.my-modal {
    display: flex;
    flex-direction: column;
    max-height: 80vh;
}

.my-modal__header {
    flex-shrink: 0;
}

.my-modal__body {
    overflow-y: auto;
    flex: 1;
    min-height: 0;
}

.my-modal__footer {
    flex-shrink: 0;
}
```

```css
/* ❌ BAD: entire modal scrolls, footer disappears off-screen. */
.my-modal {
    overflow-y: auto;
}
```


