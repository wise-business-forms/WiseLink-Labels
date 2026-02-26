# CERM Price List Items (stdfpl__) Implementation

**Status:** ✅ Complete - Ready for Integration  
**Created:** 2025  
**Purpose:** Support itemized line items for custom dies and additional charges in quotes

## Overview

Added full support for querying the CERM `stdfpl__` (Standard Price List) table. This enables retrieval of additional line items like custom die fabrication charges, setup costs, and other itemized charges for quotes.

---

## 📁 Files Created

### 1. Entity Model
**`../CERM.DataAccess/Models/PriceListItem.cs`**
- Complete C# entity model with 70+ properties
- Maps to all columns in `stdfpl__` table
- Composite primary key: `CustomerRef` + `ItemRef`

### 2. Repository Interface
**`../CERM.DataAccess/Repositories/PriceList/IPriceListItemRepository.cs`**
- Defines READ-ONLY repository contract
- Four main query methods

### 3. Entity Framework Implementation
**`../CERM.DataAccess/Repositories/PriceList/PriceListItemRepositoryEF.cs`**
- LINQ-based queries
- Uses `AsNoTracking()` for read-only access
- Good for complex queries

### 4. Dapper Implementation
**`../CERM.DataAccess/Repositories/PriceList/PriceListItemRepositoryDapper.cs`**
- Direct SQL for performance
- Optimized for large datasets
- Returns essential columns

---

## 📝 Files Updated

### 1. Schema Constants
**`../CERM.DataAccess/CermSchema.cs`**
```csharp
Tables.PriceListItems = "stdfpl__"
PriceListItemColumns.ItemRef = "fpl__ref"
PriceListItemColumns.CustomerRef = "ktrk_ref"
// ... additional constants
```

### 2. Database Context
**`../CERM.DataAccess/CermDbContext.cs`**
- Added `DbSet<PriceListItem>`
- Configured entity mapping with composite key

### 3. Dependency Injection
**`../CERM.DataAccess/ServiceCollectionExtensions.cs`**
- Registered in all three setup methods
- Supports EF, Dapper, or both

---

## 🔧 Repository Methods

### GetByCustomerAsync
```csharp
Task<List<PriceListItem>> GetByCustomerAsync(string customerRef)
```
Returns all price list items for a specific customer.

### GetByIdAsync
```csharp
Task<PriceListItem?> GetByIdAsync(string customerRef, string itemRef)
```
Returns a specific item by customer + item reference.

### SearchByKeywordAsync
```csharp
Task<List<PriceListItem>> SearchByKeywordAsync(string customerRef, string keyword)
```
Searches by keyword and description fields.

### GetActiveItemsAsync
```csharp
Task<List<PriceListItem>> GetActiveItemsAsync(string customerRef)
```
Returns only non-blocked items (where `Blocked != "1"`).

---

## 💻 Usage Examples

### Example 1: Get Custom Die Charge
```csharp
private readonly IPriceListItemRepository _priceListRepo;

public async Task<decimal?> GetCustomDiePriceAsync(string customerRef)
{
    var items = await _priceListRepo.SearchByKeywordAsync(customerRef, "custom die");
    return items.FirstOrDefault()?.PriceExcludingTax;
}
```

### Example 2: Display on Quote
```csharp
public async Task<List<QuoteLineItem>> GetAdditionalChargesAsync(
    string customerRef, 
    bool isCustomDie)
{
    var lineItems = new List<QuoteLineItem>();
    
    if (isCustomDie)
    {
        var dieItems = await _priceListRepo.SearchByKeywordAsync(customerRef, "custom die");
        if (dieItems.Any())
        {
            var item = dieItems.First();
            lineItems.Add(new QuoteLineItem
            {
                Description = item.InvoiceText11,
                Price = (decimal)item.PriceExcludingTax,
                Currency = item.CurrencyRef
            });
        }
    }
    
    return lineItems;
}
```

### Example 3: Get All Setup Charges
```csharp
public async Task<List<PriceListItem>> GetSetupChargesAsync(string customerRef)
{
    var allItems = await _priceListRepo.GetActiveItemsAsync(customerRef);
    
    return allItems
        .Where(i => i.Keyword.Contains("SETUP") || 
                    i.InvoiceText11.Contains("Setup"))
        .OrderBy(i => i.Keyword)
        .ToList();
}
```

---

## 📊 Database Structure

### Table: `stdfpl__`

**Primary Key:** Composite
- `ktrk_ref` (Customer Reference) - nvarchar(6)
- `fpl__ref` (Item Reference) - nvarchar(6)

**Key Columns:**
- `fpl__rpn` - Keyword (searchable)
- `fkttxt11` / `fkttxt21` - Description (lang 1)
- `fkttxt12` / `fkttxt22` - Description (lang 2)
- `prijs_bm` - Price excluding tax ⭐ **Use this**
- `prijs_vm` - Price before tax
- `prijs_om` - Price including tax
- `munt_ref` - Currency code
- `geblokk_` - Blocked flag (`'1'` = inactive)

### Common Item Keywords
- `CSTDIE` - Custom die fabrication
- `SETUP` - Setup charges
- `PLAT` - Plate charges
- Search descriptions for more

---

## ⚠️ Important Notes

### READ-ONLY Operations
- ✅ This implementation is **READ-ONLY** by design
- ❌ **DO NOT** use `SaveChanges()` or modify CERM data
- ✅ All methods are for querying/display only

### Composite Keys
- Same item can have different prices per customer
- Always query with both `customerRef` and `itemRef`

### Price Fields
- **Use `PriceExcludingTax`** (`prijs_bm`) for most quotes
- Respects currency in `CurrencyRef` (`munt_ref`)
- Apply tax calculation in application layer

### Active Items
- Check `Blocked != "1"` to filter out inactive items
- Use `GetActiveItemsAsync()` for convenience

### Performance
- **Dapper:** Best for high-frequency queries, large datasets
- **EF Core:** Best for complex LINQ, joining with other entities
- Both support async/await

---

## 🔗 Integration with Quote System

### Current State
The quote confirmation page (`Pages/Confirm.cshtml`) already displays a custom die line item when `IsCustomDie == true`.

### Next Integration Steps

1. **Inject Repository in QuoteService**
```csharp
private readonly IPriceListItemRepository _priceListRepo;

public QuoteService(IPriceListItemRepository priceListRepo, ...)
{
    _priceListRepo = priceListRepo;
}
```

2. **Fetch Custom Die Price**
```csharp
if (quoteRequest.IsCustomDie)
{
    var dieItem = await _priceListRepo.SearchByKeywordAsync(
        quoteRequest.CustomerId, 
        "custom die");
    
    quoteRequest.CustomDiePrice = dieItem.FirstOrDefault()?.PriceExcludingTax;
}
```

3. **Update QuoteRequest Model**
```csharp
public class QuoteRequest
{
    // ... existing properties
    public decimal? CustomDiePrice { get; set; }
}
```

4. **Display in Confirm Page**
```razor
@if (Model.QuoteRequest?.IsCustomDie == true)
{
    <tr style="background: #fef3c7;">
        <td colspan="2">⚠️ Custom Die Fabrication</td>
        <td class="text-right">@FormatCurrency(Model.QuoteRequest.CustomDiePrice)</td>
    </tr>
}
```

---

## ✅ Testing Checklist

- [ ] Verify `stdfpl__` table exists in CERM database
- [ ] Test query with valid customer reference
- [ ] Search for "custom die" keywords
- [ ] Validate price fields return correct values
- [ ] Test active items filter (excludes blocked)
- [ ] Check performance with large price lists
- [ ] Integrate with quote confirmation page
- [ ] Test currency handling
- [ ] Document common item codes for your organization

---

## 📚 Additional Resources

### SQL Schema
Located at: `../CERM.Schema/dbo/Tables/stdfpl__.sql`

### Related Models
- `QuoteRequest.cs` - Quote data model
- `QuotePriceBreakdown.cs` - Line item breakdown

### Related Pages
- `Pages/Confirm.cshtml` - Quote confirmation
- `Services/QuoteService.cs` - Quote business logic

---

## 🎯 Future Enhancements

1. **Caching:** Cache frequently-used items per customer
2. **Item Codes:** Document standard item codes organization-wide
3. **Multi-Currency:** Add currency conversion support
4. **Quantity Breaks:** Support quantity-based pricing tiers
5. **Custom Descriptions:** Allow override of standard descriptions
6. **Audit Trail:** Log which items were included in each quote

---

**Implementation Status:** ✅ Complete  
**Ready for:** Integration and Testing  
**Contact:** Development Team for CERM access and item code documentation
