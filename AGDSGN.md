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
