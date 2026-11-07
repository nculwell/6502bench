# Design Notes

Architecture and implementation decisions, and the reasons behind them. See also
README.md and SourceNotes.md.

## Plugins

The extension script (plugin) system stays C#-based. A migration to TypeScript plugins
running in a JS host was tried and then abandoned (see AGHIST.md). It is kept on the
`local/modern-dotnet-ts-plugins` branch for reference.

## Avalonia UI

### Key gestures with punctuation keys

Avalonia's `KeyGesture.Parse` (11.3.x) only understands `Key` enum names, plus synonyms
for a few characters such as `+` and `-`. A literal `;` makes it throw at XAML load
time, which crashes the app on startup. Punctuation keys must therefore use their
enum names, e.g. `Ctrl+OemSemicolon`. The WPF version had the same limitation
(it used `Oem1` and overrode the displayed text with `InputGestureText`). Avalonia's
`MenuItem` has no equivalent text override.

### Font size and density

Window layouts keep the WPF version's fixed pixel widths (columns, buttons). Fluent's
defaults (14px text, 12px DataGrid header/cell padding, 32px sort-icon column,
padded and left-aligned button content) make those widths too small. Rather than
resizing every control, `App.axaml` brings back WPF-like metrics globally:
`FluentTheme DensityStyle="Compact"`, `FontSize` 12 on `Window`, and
`ControlContentThemeFontSize` 12 (menus read this resource instead of the inherited
size). It also overrides `DataGridSortIconMinWidth` and
`DataGridTextColumnCellTextBlockMargin`, and sets Button/ToggleButton padding and
centred content. New UI should be designed against these metrics.

`GeneralMonoFont` is a fallback list (Consolas, Menlo, DejaVu Sans Mono, ...), so it
works on Windows, macOS and Linux.
