# CERM Schema Reference

## Source of truth

**`CERM_tables_columns.json`** is the single source of truth for the CERM database schema.

- Use this JSON for building and validating queries.
- Contains: `TABLE_SCHEMA`, `TABLE_NAME`, `COLUMN_NAME`, `DATA_TYPE`, `LengthOrPrecision`, `IS_NULLABLE`, `COLUMN_DEFAULT`, `ENG_TABLE`, `ENG_COLUMN`
- `ENG_TABLE` and `ENG_COLUMN` are English translations of the original Dutch table/column names.

## Updating translations

Run the translation script to refresh `ENG_TABLE` and `ENG_COLUMN`:

```powershell
cd Documentation
.\translate-cerm-schema.ps1
```

Edit `translate-cerm-schema.ps1` to add or adjust mappings in `$tableMap`, `$columnPartMap`, or `$columnFullMap`.
