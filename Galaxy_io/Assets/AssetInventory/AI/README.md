# Asset Inventory AI Skills

These skills teach a coding assistant to use Asset Inventory's indexed catalog, organization, import, analysis, export, and guarded automation workflows.

## Use the skills

1. Point a compatible coding assistant at this package's `AI/Skills` folder.
2. Ask it to read the skill descriptions and load `asset-inventory-setup-and-overview` first when setup or routing is unclear.
3. Load only the narrowest skill that matches the requested job.
4. Review every proposed project change before allowing the assistant to apply it.

These Markdown files are documentation only. They do not add an AI service, subscription, runtime dependency, or generated content to Asset Inventory.

## Included skills

- `asset-inventory-setup-and-overview`: Open Asset Inventory, confirm its catalog state and configured sources, and route the request without assuming where the package is installed.
- `asset-inventory-find-assets-across-sources`: Search Asset Inventory's indexed files and packages with bounded filters, verify source identity, and return actionable results rather than broad guesses.
- `asset-inventory-organize-assets-and-packages`: Organize verified catalog items with tags, groups, and saved searches while preserving stable package identity and existing user organization.
- `asset-inventory-import-assets-safely`: Preflight an exact catalog item, preview dependencies and destination scope, then import or place it with explicit confirmation and post-import verification.
- `asset-inventory-audit-dependencies-and-duplicates`: Run the narrowest dependency, usage, duplicate, or maintenance audit, inspect findings, and apply only supported fixes with explicit scope.
- `asset-inventory-automate-catalog-workflows`: Invoke Asset Inventory's transport-neutral automation through an installed adapter, target the intended Unity project explicitly, and preserve dry-run confirmation for mutations.

Documentation: https://www.wetzold.com/tools/assetinventory/docs/

Support: https://discord.com/invite/uzeHzEMM4B
