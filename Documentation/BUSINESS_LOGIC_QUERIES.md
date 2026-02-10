# Business Logic Queries Catalog

Catalog of SQL queries and database operations used in the WiseLabels application. Use `CERM_tables_columns.json` to resolve table/column names and their English translations.

---

## BLQ-001: Cutting Die options

**Purpose:** Load cutting die options for the form dropdown, filtered by printing type (flexo/rotary vs digital).

**Location:** `Pages/Api/CuttingDie.cshtml.cs` → `OnGetAsync`

**Business rules:** BR-DIE-001, BR-DIE-002, BR-DIE-004, BR-DIE-005, BR-DIE-006

**Table:** `stnspr__` (ENG_TABLE: DieStation)

**Columns used:**

| COLUMN_NAME | ENG_COLUMN   | Usage                    |
|-------------|--------------|--------------------------|
| stns_ref    | DieReference | ValueId (form submission)|
| stns_oms    | DieDescription | DisplayText            |
| etiket_b    | —            | Label width              |
| etiket_h    | —            | Label height             |
| radius__    | —            | Radius                   |
| omtrek__    | —            | Perimeter                |
| weblabel    | —            | Filter (must be 'Y')     |
| materie_    | Material     | Filter parameter         |

**Parameters:**

| Parameter | Type   | Source           | Description                    |
|-----------|--------|------------------|--------------------------------|
| @materie  | string | Printing selection | `"1"` = flexo/rotary, `"2"` = digital |

**SQL:**

```sql
SELECT stns_ref, stns_oms, etiket_b, etiket_h, radius__, omtrek__, weblabel
FROM stnspr__
WHERE materie_ = @materie
  AND weblabel = 'Y'
  AND stns_oms IS NOT NULL
  AND stns_oms <> ''
  AND stns_oms <> 'UNKOWN'
ORDER BY stns_oms DESC
```

---

## BLQ-002: Update contact info for estimate

**Purpose:** Save customer contact information (name, email, phone) on the order/estimate after CERM submission.

**Location:** `Services/QuoteService.cs` → `UpdateContactInfoAsync`

**Table:** `v1bon___` (ENG_TABLE: OrderHeader)

**Columns used:**

| COLUMN_NAME | ENG_COLUMN   | Usage         |
|-------------|--------------|---------------|
| komment1    | Comment1     | Customer name |
| komment2    | Comment2     | Email         |
| komment3    | Comment3     | Phone         |
| bon__ref    | OrderReference | Filter (estimate ID) |

**Parameters:**

| Parameter   | Type   | Source     |
|-------------|--------|------------|
| @estimateId | string | CERM EstimateId from API |
| @name       | string | Quote form contact name |
| @email      | string | Quote form contact email |
| @phone      | string | Quote form contact phone |

**SQL:**

```sql
UPDATE v1bon___
SET komment1 = @name,
    komment2 = @email,
    komment3 = @phone
WHERE bon__ref = @estimateId
```

---

## Future queries

Add new business logic queries below using the same format:

- **BLQ-003:** (reserved)
- …
