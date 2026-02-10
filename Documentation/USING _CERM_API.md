# Using the CERM API

These are the endpoints that can be used with proper authentication.  All endpoints originate from the same base url: https://brandmark-api.cerm.be/quote-api/v1/

**Filtering** https://onlinehelp.cerm.net/Modules/RESTservices/API_filtering.htm

## Orders

### Calculation Price

Endpoint: calculations/price

Purpose: Calculate prices for the given quantities using an existing calculation

HTTP Method: POST

Request:

```json
{
  "CalculationId": "101514",
  "NumberOfProducts": 1,
  "Quantities": [
    1000,
    2000
  ]
}
```

Responses:
200

```json
{
  "Meta": {
    "Offset": 0,
    "Limit": 0,
    "TotalCount": 0,
    "Pages": 0
  },
  "Data": [
    {
      "Quantity": 0,
      "UnitPrice": 0,
      "TotalPrice": 0,
      "Currency": "string",
      "ValidQuantity": true
    }
  ]
}
```

default

```json
[
  {
    "Name": "string",
    "Value": "string",
    "ErrorCode": 0,
    "ErrorString": "string",
    "Description": "string",
    "Help": "string"
  }
]
```

### Quick Quote

Endpoint: calculations/narrow-web/quick-quote/price

Purpose: Calculate prices for a (narrow web) Quick Quote

HTTP Method: POST

Note: Does not create an estimate (calculation ID)

Request example:
```json
{
  "CustomerId": "100931",
  "ContactId": "004",
  "PressRuns": [
    {
      "ColourCodeIdFront": "1D",
      "ColourCodeIdAdhesive": "1D",
      "ColourCodeIdBack": "1D",
      "FinishingTypes": [
        "01"
      ]
    }
  ],
  "WindingId": "1",
  "Outline": "Rectangle",
  "DieSizeId": "001086",
  "SubstrateId": "800143",
  "Description": "My quote",
  "NumberOfProducts": 1,
  "Quantities": [
    1000,
    2000
  ],
  "Width": 100,
  "Height": 150,
  "Radius": 5,
  "PackingProcedureId": "000",
  "PackingPriority": "Diameter",
  "PackingNumber": 500
}
```
200
```json
{
  "Meta": {
    "Offset": 0,
    "Limit": 0,
    "TotalCount": 0,
    "Pages": 0
  },
  "Data": [
    {
      "Quantity": 0,
      "UnitPrice": 0,
      "TotalPrice": 0,
      "Currency": "string",
      "ValidQuantity": true
    }
  ]
}
```
default
```json
[
  {
    "Name": "string",
    "Value": "string",
    "ErrorCode": 0,
    "ErrorString": "string",
    "Description": "string",
    "Help": "string"
  }
]
```

### Calculations

Endpoint: calculations

Purpose: Create a new calculation

HTTP method: POST

Note: DOES NOT CREATE A PDF (quote letter)

Request example:

```json
{
  "CustomerId": "100931",
  "ContactId": "004",
  "PressRuns": [
    {
      "ColourCodeIdFront": "1D",
      "ColourCodeIdAdhesive": "1D",
      "ColourCodeIdBack": "1D",
      "FinishingTypes": [
        "01"
      ]
    }
  ],
  "WindingId": "1",
  "Outline": "Rectangle",
  "DieSizeId": "001086",
  "SubstrateId": "800143",
  "Description": "My quote",
  "NumberOfProducts": 1,
  "Quantities": [
    1000,
    2000
  ],
  "Width": 100,
  "Height": 150,
  "Radius": 5,
  "PackingProcedureId": "000",
  "PackingPriority": "Diameter",
  "PackingNumber": 500
}
```

Response example:

```json
{
    "Data": {
        "Id": "129071",
        "EstimateId": "113230",
        "Description": "Thursday 14 April 2022",
        "ReferenceAtCustomer": "",
        "Size": " 3 \"",
        "Substrate": "60# Orange Fluorescent /C4500/40#CK",
        "Colour": "Process Color - Digital",
        "LabelShape": "2",
        "LabelWidth": 3,
        "LabelHeight": 3,
        "LabelRadius": 3,
        "ProductsOnInternet": 0,
        "MinimumCount": 0,
        "MaximumCount": 0,
        "PriceInformation": true,
        "PriceType": "Per_1000",
        "PriceUnit": "pcs.",
        "Winding": "10",
        "PackingProcedureId": "152",
        "AllowInternet": true,
        "GroupingId": "",
        "QuoteLetterAvailable": false
    }
}
```

### Quick Quote

/quote-api/v1/calculations/narrow-web/quick-quote/price/calculation

DOES NOT CREATE A PDF (quote letter)

#### Example  model:

```json
{
    "CustomerId": "108620",
    "ContactId": "001",
    "PressRuns": [
        {
            "ColourCodeIdFront": "CMYKD", //Process Color - Digital
            "FinishingTypes": [
                "QQDIED" 
            ]
        }
    ],
    "WindingId": "10",
    "Outline": "Circle",
    "DieSizeId": "101809",
    "SubstrateId": "000500",
    "Description": "Thursday 14 April 2022",
    "NumberOfProducts": 1,
    "Quantities": [
        1000,
        2000
    ],
    "Width": 10.0,
    "Height": 15.0,
    "Radius": 5.0,
    "PackingProcedureId": "152",
    "PackingPriority": "Diameter",
    "PackingNumber": 500
}
```

#### Payload returned:

```json
{
    "Data": [
        {
            "Quantity": 1000,
            "UnitPrice": 407.68,
            "TotalPrice": 407.68,
            "Currency": "USD",
            "ValidQuantity": true,
            "ValidErrorCode": "Quantity"
        },
        {
            "Quantity": 2000,
            "UnitPrice": 269.045,
            "TotalPrice": 538.09,
            "Currency": "USD",
            "ValidQuantity": true,
            "ValidErrorCode": "Quantity"
        },
        {
            "Quantity": 10000,
            "UnitPrice": 161.342,
            "TotalPrice": 1613.42,
            "Currency": "USD",
            "ValidQuantity": true,
            "ValidErrorCode": "Quantity"
        }
    ]
}
```

### Calculation PDF

Return the quick quote for a given estimate

/quote-api/v1/calculations/{estimateID}/quote-letter/pdf

HTTP method: GET

Responses

201

```json
{
  "Data": {
    "Id": "string",
    "EstimateId": "string",
    "Description": "string",
    "ReferenceAtCustomer": "string",
    "Size": "string",
    "Substrate": "string",
    "Colour": "string",
    "ProductsOnInternet": 0,
    "MinimumCount": 0,
    "MaximumCount": 0,
    "PriceInformation": true,
    "PriceType": "Text",
    "PriceUnit": "string",
    "Winding": "string",
    "AllowInternet": true,
    "GroupingId": "string",
    "QuoteLetterAvailable": true
  }
}
```

default

```json
[
  {
    "Name": "string",
    "Value": "string",
    "ErrorCode": 0,
    "ErrorString": "string",
    "Description": "string",
    "Help": "string"
  }
]
```

## Customers

### Get Customers

Base URL: https://brandmark-api.cerm.be/customer-api/v1/

Endpoint: customers

Purpose: Retrieve customer records from CERM with optional OData filtering for name-based autocomplete and lookups

HTTP Method: GET

**Filterable Fields:** Id, Keyword, Name, PhoneNumber, FaxNumber, Website, Email, CodeCustomerOtherSoftware, Status, Department, Street, Country, PostalCode, City, District, County, State, RepresentativeId

**Filter Syntax:** Uses OData filtering with `Filter` query parameter. Filters are case-insensitive when using `tolower()`.

#### Query Examples:

**1. Get all customers (returns up to 8,837 records):**
```
/customers
```

**2. Filter customers by name starting with "ABC" (case-insensitive):**
```
/customers?Filter=startswith(tolower(Name), tolower('ABC'))
```

**3. Filter customers by country:**
```
/customers?Filter=Country eq 'US'
```

**4. Multiple conditions - customers in Belgium OR Great Britain, excluding a specific keyword:**
```
/customers?Filter=(Country eq 'BE' or Country eq 'GB') and Keyword ne 'ALLSEAL'
```

**5. Filter by email domain:**
```
/customers?Filter=endswith(tolower(Email), tolower('@example.com'))
```

#### Response Structure:

```json
{
    "Meta": {
        "TotalCount": 8837
    },
    "Data": [
        {
            "Id": "100001",
            "Keyword": "3SCO01",
            "Name": "3 S Corporation",
            "Site": "",
            "PhoneNumber": "0-0-",
            "FaxNumber": "0-0-",
            "Email": "john.kuykendall@3Sinternational.com",
            "Website": "",
            "CodeCustomerOtherSoftware": "10008",
            "CustomerGroupId": "200",
            "Status": "Customer",
            "Department": "",
            "Street": "1073 Neely Ferry Road",
            "Country": "US",
            "PostalCode": "29360",
            "City": "Laurens",
            "District": "",
            "County": "Laurens",
            "State": "SC",
            "JobController": "",
            "JobControllerEmail": "",
            "RepresentativeId": "100009",
            "VATNumber": "",
            "TradeNumber": "",
            "MannerOfPaymentId": "",
            "QuickQuoteLogging": true,
            "TaxHandling": {
                "Type": "SalesTax",
                "IsTaxable": false,
                "IsRemission": false
            },
            "CustomStatusStates": [],
            "CustomFieldsValues": []
        }
    ]
}
```

**Notes:**
- The API returns up to 8,837 customers total in the database
- Field values like Name, Email, and Street may contain trailing whitespace in the raw response (trimmed in application)
- Use the `Filter` parameter to narrow results for specific use cases like autocomplete functionality
- Results are typically returned in the order received from the database (sort client-side if needed)

