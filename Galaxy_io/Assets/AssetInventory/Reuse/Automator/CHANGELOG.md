# Changelog

All notable changes to this project will be documented in this file.

## [1.0.0] - 2024-01-01

- Use shared window viewport spacing in the action editor and remove obsolete footer inset compensation.

### Improved

- Added Unity 6.7 lifecycle ownership for action registries and runner state while retaining deterministic subsystem resets on older supported Unity versions.
- Kept built-in automation steps available to the authoring workflow without presenting them as the primary scripting API.
- Require Unity 2022.3 or newer.
- Rebuilt action editing with a polished responsive UI Toolkit workflow, categorized step creation, clearer validation, native parameter controls, and safer step reordering.

### Added
- Initial release
- Core action framework with ActionStep base class
- Parameter system with string, int, bool, multiline string types
- Variable system with $varname syntax
- Built-in internal variable groups (Application, SystemInfo, Environment, DateTime, etc.)
- Extensible variable groups via VariableGroupRegistry
- JSON persistence (JsonActionRepository)
- SQLite persistence (SqliteActionRepository)
- Reflection-based step discovery
- Built-in action steps for files, folders, settings, packaging
- ActionEditorWindow for creating and editing actions
- ActionRunner for executing action sequences
