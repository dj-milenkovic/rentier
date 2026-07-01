---
paths:
  - "src/Rentier.Desktop/**"
---

# Rentier.Desktop (Avalonia UI) rules

- Calls **`Rentier.Application` use cases only** — never touches Infrastructure or
  EF Core directly, even for "quick" reads.
- **`ReactiveUserControl<TViewModel>`** for every view.
- Bind to ViewModel observables only — **no event handlers in code-behind.**
- **No UI-thread blocking.** All commands use `ReactiveCommand.CreateFromTask`; never
  block with `.Result`/`.Wait()`.
- Standard ViewModel state properties: `IsLoading`, `ErrorMessage`, `HasItems` (or
  `HasData`). Every view that loads data binds to these three states.
- Use `DataGrid` for lists. Use `ContentDialog` for dialogs — always invoked async.
- Navigation goes through `INavigationService`; ViewModels request a page type and
  never reference Views directly.
- See `.claude/skills/clean-architecture` for the full ViewModel pattern, navigation
  shell layout, and page-specific UX contracts (Filings grouping, Sync log format).
- See `.claude/skills/rentier-ui-design` before adding/changing any AXAML, style,
  color, icon, or layout — it defines the semantic token system and theme rules.
- See `.claude/skills/rentier-ui-tests` when adding or changing a ViewModel.
