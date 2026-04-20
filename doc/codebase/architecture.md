# Codebase Structure Design

## Intent
Create a responsibility-driven structure where each layer has a focused purpose and boundary interfaces make data/control flow explicit.

## Folder map
- `src/Sloth/Cli/`
  - CLI parsing, argument validation, run orchestration, and progress reporting.
- `src/Sloth/HttpFileParsing/`
  - Parsing `.http` source files into request definitions.
- `src/Sloth/Execution/`
  - Request execution via `HttpClient`, plus execution result/progress models.
- `src/Sloth/Output/`
  - Formatter resolution/formatting and output writer selection/persistence.
- `src/Sloth/Domain/`
  - Cross-boundary input models (currently `RunOptions`) shared by CLI and output writer selection.

## Boundary interface map
- Parser boundary: `IHttpFileParser` (`HttpFileParsing`) used by `RunService` (`Cli`).
- Executor boundary: `IRequestExecutor` (`Execution`) used by `RunService` (`Cli`).
- Formatter boundary: `IOutputFormatter` + `IOutputFormatterRegistry` (`Output`) used by `RunService` (`Cli`).
- Writer boundary: `IOutputWriter` + `IOutputWriterFactory` (`Output`) used by `RunService` (`Cli`).
- CLI boundary: `ICommandLineParser` and `IRunService` (`Cli`) used by composition root in `Program.cs`.

## Why this structure over alternatives
- **Over a flat `Program.cs`:** keeps orchestration logic testable and easier to evolve.
- **Over feature-by-file-only organization:** explicit responsibility folders reduce coupling and make ownership clear.
- **Over concrete-class coupling:** interfaces at boundaries prevent parser/executor/output implementations from leaking into CLI command flow.

## Extension example
### Add a new CLI option (`--timeout-ms`)
1. Add option definition in `CompositionRoot` registry.
2. Parse and validate in `CommandLineParser`.
3. Extend `RunOptions` in `Domain` with `TimeoutMs`.
4. Map `RunOptions.TimeoutMs` to `RequestExecutionOptions` in `RunService`.
5. No required changes to parser/output modules because boundaries remain interface-based.
