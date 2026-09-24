# Asset Inventory Unity Pipeline Integration

This adapter is bundled with Asset Inventory and requires no separate activation step.

When Unity Pipeline `0.6.0-exp.1` or newer is installed, the adapter assembly compiles automatically and Pipeline discovers all 31 lower snake case `asset_inventory_*` commands. Pipeline owns its Editor server lifecycle and starts it by default when no settings asset opts out through `AutoStart`.

When Pipeline is absent, the package version define excludes this assembly and the base Asset Inventory package remains compilable.

Use an explicit project path when more than one Unity Editor may be open:

```powershell
unity command asset_inventory_get_inventory_stats --project-path "D:\Unity\AssetInventory" --json
```

All operations delegate to `AssetInventory.Automation`; this adapter assembly contains no independent business behavior.
