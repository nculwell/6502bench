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
centred content. Fluent's `DataGridCell` sets its own `FontSize` (15) and `MinHeight`
(32) instead of inheriting, so `DataGridCell` and `DataGridRow` are restyled too. New UI should be designed against these metrics.

`GeneralMonoFont` is a fallback list (Consolas, Menlo, DejaVu Sans Mono, ...), so it
works on Windows, macOS and Linux.

### Code list clipping

The code list is a fixed-width header `Grid` above a `ListBox` (Avalonia has no
ListView/GridView). Avalonia doesn't clip children to their layout slot by default, so
`codeListGrid` and `launchPanel` set `ClipToBounds="True"` to keep wide content from
drawing over the neighboring panes.

### Code list horizontal scrolling

Avalonia's `ListBox` disables horizontal scrolling by default, which hid the rightmost
columns in a narrow pane. It is turned on (`ScrollViewer.HorizontalScrollBarVisibility`).
Because the header row is a separate control, it is wrapped in its own `ScrollViewer`
with a hidden scroll bar. `MainWindow.SetupCodeListScrollSync` hooks the list's
`PART_ScrollViewer.ScrollChanged`, sets the header's `MinWidth` to the list's extent so
both can scroll the same distance, and copies the X offset to the header. The offset is
posted to the dispatcher because the header's extent only grows after layout, and
setting the offset sooner would clamp it.
