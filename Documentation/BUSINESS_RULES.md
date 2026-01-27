# Business rules (agent spec)

Primary context for implementing and validating behavior. Prefer **stable rule IDs**, explicit **inputs/outputs**, and **mapping functions**. Avoid prose unless it clarifies ambiguity.

## 0. Glossary / canonical terms

- **CERM Parameter Item**: object containing `Id` and `Descriptions[]` (or equivalent).
- **Descriptions[]**: array of `{ ISOLanguageCode, Description }` (case-insensitive keys).
- **DisplayText**: text shown to user in UI.
- **ValueId**: identifier submitted to server (form value).
- **Printing**: CERM Color Code/ColourBacking selection, shown as buttons.
- **Material**: CERM Substrate/Material selection, shown as listbox.
- **Finish**: CERM Finishing Type selection, shown as dropdown.
- **CuttingDie**: die selection sourced from DB `stnspr__`, shown as dropdown.

## 1. Global mapping functions

### BR-FN-001: `PickEnglishDescription(descriptions[]) -> string`

- Prefer item where `ISOLanguageCode` equals `"en-US"` (case-insensitive).
- If not found, use first entry in the array.
- If array missing/empty, return `"Unknown"`.

### BR-FN-002: `GetId(item) -> string`

- Return `item.Id` (case-insensitive).
- If missing, treat as invalid item (do not render/select).

### BR-FN-003: `FilterAllowRFQ(items[]) -> items[]`

- If items have an `AllowRFQ` flag, include only `AllowRFQ == true`.
- If a feed does not include `AllowRFQ`, do not filter by it.

## 2. Shared UI invariants

### BR-UI-001: Display vs submit

- **DisplayText** must be `PickEnglishDescription(item.Descriptions)` when available.
- **ValueId** must be `GetId(item)` when available.

### BR-UI-002: No client secrets

- Browser calls only server endpoints under `/Api/*`.
- OAuth/token handling and all SQL occur server-side.

## 3. Materials (Material/Substrate)

### BR-MAT-001: Source

- **Endpoint**: `/Api/Materials`
- **Config**: `Cerm:MaterialsUrl`

### BR-MAT-002: Dependency

- Loads on page load (no prerequisite).

### BR-MAT-003: Filtering

- Apply `FilterAllowRFQ` (current behavior).
- No printing-based filtering is applied at this time.

### BR-MAT-004: UI mapping

- **DisplayText**: `PickEnglishDescription(Descriptions[])`
- **ValueId**: `Id`

## 4. Printing (Color Codes / ColourBacking)

### BR-PRN-001: Source

- **Endpoint**: `/Api/ColorCodes`
- **Config**: `Cerm:ColorCodesUrl`

### BR-PRN-002: UI

- Render as a set of buttons.

### BR-PRN-003: Mapping

- Input shape may be nested:
  - `{ ColourBacking: { Id, Descriptions[] }, AllowRFQ?, Blocked? }`
  - or flat `{ Id, Descriptions[] }` (fallback shape)
- **DisplayText**: `PickEnglishDescription(ColourBacking.Descriptions[])` (or flat `Descriptions[]`)
- **ValueId**: `ColourBacking.Id` (or flat `Id`)

### BR-PRN-004: Deduplication

- Show a distinct list by **ValueId**.

## 5. Cutting Die

### BR-DIE-001: Source

- **Endpoint**: `/Api/CuttingDie`
- **DB**: `stnspr__` (server-side only)

### BR-DIE-002: Dependency

- Requires a Printing selection first.
- Not applicable when Shape is **circle**, **oval**, or **special** (hide the Cutting Die section and do not require a selection).

### BR-DIE-003: Query input

- Client must send `printing=<Printing.ValueId>` whenever possible.
- If only `Printing.DisplayText` exists, it may be sent as a fallback.

### BR-DIE-004: `materie_` mapping

Determine `materie_` from the `printing` query value:

- **materie_ = 1 (flexo/rotary)** if:
  - printing contains `"flexo"` or `"rotary"` (case-insensitive), OR
  - printing ends with `"F"` (case-insensitive; implies Printing Id like `2F`)
- **materie_ = 2 (digital)** if:
  - printing contains `"digital"` (case-insensitive), OR
  - printing ends with `"D"` (case-insensitive; implies Printing Id like `2D`)

### BR-DIE-005: UI mapping

- **DisplayText**: `stns_oms`
- **ValueId**: `stns_ref`

### BR-DIE-006: Exclusions

- Exclude rows where `stns_oms` is NULL/empty.
- Current implementation also restricts to `weblabel = 'Y'` and excludes `stns_oms = 'UNKOWN'`.

## 6. Finish

### BR-FIN-001: Source

- **Endpoint**: `/Api/FinishingTypes`
- **Config**: `Cerm:FinishingTypesUrl`

### BR-FIN-002: Dependency

- Requires a Printing selection first.
- Finish options load when a Printing button is selected.

### BR-FIN-003: Filtering

- Apply `FilterAllowRFQ` (current behavior).
- Do **not** apply text-based filtering for Inline/Offline unless a stable “Finish Type” field exists in the payload.

### BR-FIN-004: UI mapping

- **DisplayText**: `PickEnglishDescription(Descriptions[])`
- **ValueId**: `Id`

### BR-FIN-FUTURE-001: Inline/Offline filtering

When a Printing option is selected:

- If Printing display contains `"Flexo"` → **hide** Finish items whose DisplayText contains `"Digital"`.
- If Printing display contains `"Digital"` → **hide** Finish items whose DisplayText contains `"Flexo"`.

Implementation note (optional): If the API provides a stable `FinishingType` (1/2), it may be used as an additional constraint, but the primary rule is based on **button text** and **finish display text**.


