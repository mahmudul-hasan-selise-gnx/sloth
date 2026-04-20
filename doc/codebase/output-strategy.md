# Output Strategy Pattern

## Intent
Use **Strategy** to select output rendering behavior by destination format without branching throughout orchestration code.

## Class/interface map
- `IOutputFormatter` (strategy contract)
  - `TextOutputFormatter`
  - `JsonOutputFormatter`
- `OutputFormatterRegistry` (strategy resolver/selector)
- `RunService` (strategy client)

## Why Strategy over alternatives
- **Over switch/case in `RunService`:** avoids central conditional growth when adding formats.
- **Over inheritance from one formatter base class:** independent formatter implementations stay small and format-focused.
- **Over one “universal formatter”:** keeps each formatter cohesive and easier to test.

## Extension example (new output format)
Add YAML support:
1. Implement `YamlOutputFormatter : IOutputFormatter`.
2. Register `".yaml"` and `".yml"` in `CompositionRoot` formatter registry.
3. Keep `RunService` unchanged because it only depends on `IOutputFormatter` from the registry.
