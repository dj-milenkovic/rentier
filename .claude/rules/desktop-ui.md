---
paths:
  - "src/Rentier.Desktop/**"
---

# Rentier.Desktop (Avalonia UI) rules

- Calls **`Rentier.Application` use cases only** — never touches Infrastructure or
  EF Core directly, even for "quick" reads. The only exception is the composition root
  (`Composition/`, `Program.cs`, `App.axaml.cs`), which wires Infrastructure via
  `AddInfrastructureServicesAsync`; `LayerDependencyTests` enforces this per type.
- **`ReactiveUserControl<TViewModel>`** for every view.
- Bind to ViewModel observables only — **no event handlers in code-behind.**
- **No UI-thread blocking.** All commands use `ReactiveCommand.CreateFromTask`; never
  block with `.Result`/`.Wait()`.
- Standard ViewModel state properties: `IsLoading`, `ErrorMessage`, `HasItems` (or
  `HasData`). Every view that loads data binds to these three states.
- Use `DataGrid` for lists. Dialogs go through the shared helpers in `Dialogs/`
  (`ConfirmDialogHelper`, `ImportDialogHelper`) — always invoked async.
- Navigation is owned by `MainWindowViewModel`: sidebar `NavigationEntry` items swap
  `CurrentViewModel`. Content ViewModels never reference Views or other pages directly.
- **Logging lives in `Logging/` and the composition root only.** The file logger is started in
  `Program.Main` (never in `App`, which headless tests run too). ViewModels depend on
  `IDiagnosticsService`, never on Serilog types. Same content rule as every layer: ids, codes
  and counts only — no personal or financial data in a log message.
- See `.claude/skills/clean-architecture` for the full ViewModel pattern, navigation
  shell layout, and page-specific UX contracts (Filings grouping, Sync log format).
- See `.claude/skills/rentier-ui-design` before adding/changing any AXAML, style,
  color, icon, or layout — it defines the semantic token system and theme rules.
- See `.claude/skills/rentier-ui-tests` when adding or changing a ViewModel.
