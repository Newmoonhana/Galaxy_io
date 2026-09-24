---
name: "asset-inventory-automate-catalog-workflows"
description: "Use this skill whenever the user asks to 'automate Asset Inventory', call its Unity Pipeline or Unity AI Assistant operations, export CSV or HTML, manage actions, or run repeatable catalog queries. Use asset-inventory-find-assets-across-sources for ordinary interactive searching."
metadata:
  asset: "Asset Inventory 4"
  publisher: "Impossible Robert"
  asset-version: "4.8.0"
  skill-version: "1.0.0"
  unity: "2022.3"
  render-pipelines: "Not applicable"
  category: "Tools/Utilities"
  asset-store-url: "https://assetstore.unity.com/packages/tools/utilities/asset-inventory-4-349582"
  documentation-url: "https://www.wetzold.com/tools/assetinventory/docs/"
  support-url: "https://discord.com/invite/uzeHzEMM4B"
  last-verified: "2026-08-29"
---

# Automate Catalog Workflows

Invoke Asset Inventory's transport-neutral automation through an installed adapter, target the intended Unity project explicitly, and preserve dry-run confirmation for mutations.

## When to use this skill

- Running bounded catalog and project queries through Unity Pipeline, CLI, MCP, or Unity AI Assistant.
- Creating, editing, and running Asset Inventory actions.
- Exporting selected catalog data as CSV or HTML.

## Prerequisites

- Asset Inventory is ready in a live Unity Editor.
- The chosen adapter package is installed and automatically active.
- The absolute target project path is known when more than one Editor can exist.

## Quick start

1. Choose a read-only query or a mutation operation.
2. Pass an explicit project path for CLI or MCP routing.
3. Keep searches paginated and inspect the structured response.
4. For a mutation, run dry-run first and execute only with the matching token.

## Workflows

### Workflow: Run a bounded query

1. Choose the exact `asset_inventory_*` command.
2. Pass `--project-path` and narrow query parameters.
3. Request JSON and inspect success, error code, data, and pagination.
4. Handle `not_ready` by resolving live Editor state rather than retrying blindly.

### Workflow: Run a confirmed mutation

1. Invoke the operation with its default dry run.
2. Review the validated target and scope in the response.
3. Repeat the exact unchanged operation with execution enabled and its confirmation token.
4. Verify the resulting catalog, files, action, export, or scene state.

## Verification

- The response identifies the intended project and operation.
- Queries remain bounded and structured.
- Mutation scope matches its preview token exactly.
- Exports or action changes are verified through a follow-up read.

## API quick reference

- Example: `unity command asset_inventory_get_inventory_stats --project-path <path> --json`.
- Example: `unity command asset_inventory_search_packages --project-path <path> --search-phrase environment --max-results 10 --json`.
- The shared surface contains 31 operations spanning queries, tags, imports, downloads, exports, actions, and window lifecycle.
- Unity AI Assistant uses `AssetInventory_*` names; Unity Pipeline uses lower snake case `asset_inventory_*` names.

## Common issues

- **The operation returns `not_ready`:** Open the target Unity Editor and resolve catalog or UI readiness.
- **A mutation requests confirmation again:** Use the current token with unchanged validated arguments; changed scope requires a new preview.
- **The wrong Editor responds:** Always supply the absolute project path to direct CLI or MCP.

## Boundaries

- Do not force-enable or modify companion adapter settings.
- Do not support batch or headless behavior as a release contract.
- Do not bypass pagination, expert-query validation, or mutation confirmation.
- Keep adapter code transport-only; customer automation uses the existing operations.
