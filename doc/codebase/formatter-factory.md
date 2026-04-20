# Formatter/Writer Factory Pattern

## Intent
Use **Factory** objects to centralize construction decisions for output handling so orchestration code remains independent of concrete classes.

## Class/interface map
- Formatter side
  - `IOutputFormatterRegistry` (factory-like resolver interface)
  - `OutputFormatterRegistry` (selects formatter by extension)
- Writer side
  - `IOutputWriterFactory` (factory contract)
  - `OutputWriterFactory` (creates `ConsoleOutputWriter` or `FileOutputWriter`)
  - `IOutputPathPolicy` + `OutputPathPolicy` (path validation concern extracted)

## Why Factory over alternatives
- **Over `new` calls in `RunService`:** avoids coupling run orchestration to construction and validation details.
- **Over service locator access everywhere:** makes creation choices explicit and local to composition/factory classes.
- **Over one large conditional constructor:** preserves single responsibility between selection and usage.

## Extension example
### Add a compressed file writer for `.gz`
1. Add `GzipFileOutputWriter : IOutputWriter`.
2. Extend `OutputWriterFactory.Create` to select `GzipFileOutputWriter` for `.gz` output path.
3. Keep `RunService` unchanged (still asks factory for `IOutputWriter`).

### Add a new CLI option controlling append behavior
1. Add CLI option `--append` in `CommandLineParser`.
2. Extend `RunOptions` with append semantics.
3. Update `OutputWriterFactory` to map options to writer mode.
