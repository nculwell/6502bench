# Change History

## 2026-11-06

- Backed out the in-progress migration of the plugin system from C# to TypeScript
  (commits f3c9d07 "stalled" and bff8378 "in progress"). The C# plugin system stays.
  That work is preserved on the `local/modern-dotnet-ts-plugins` branch.
- Fixed a SourceGen.App startup crash (`ArgumentException: Requested value ';' was not
  found`). In `SourceGen.App/Views/MainWindow.axaml`, the Edit Comment shortcut is now
  written as `Ctrl+OemSemicolon` instead of `Ctrl+;`, in both the `KeyBinding` and the
  `MenuItem.InputGesture`.
- Fixed SourceGen.App sizing and clipping. The layouts ported from WPF have fixed pixel
  widths sized for 12px text, but Avalonia's Fluent theme defaults to 14px text and
  large padding. `App.axaml` now uses compact density, a 12px base font, tighter
  button and DataGrid header/cell padding, and no reserved sort-icon space. It also
  adds a `GeneralMonoFont` resource, which is applied to the code list and data grids
  as in WPF. The launch panel clips its oversized title.
