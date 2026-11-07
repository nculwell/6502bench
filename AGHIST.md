# Change History

## 2026-11-06

- Backed out the in-progress migration of the plugin system from C# to TypeScript
  (commits f3c9d07 "stalled" and bff8378 "in progress"). The C# plugin system stays.
  That work is preserved on the `local/modern-dotnet-ts-plugins` branch.
- Fixed a SourceGen.App startup crash (`ArgumentException: Requested value ';' was not
  found`). In `SourceGen.App/Views/MainWindow.axaml`, the Edit Comment shortcut is now
  written as `Ctrl+OemSemicolon` instead of `Ctrl+;`, in both the `KeyBinding` and the
  `MenuItem.InputGesture`.
