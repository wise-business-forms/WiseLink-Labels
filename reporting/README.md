# Power BI GitHub Issues Reporting

This reporting solution provides direct Power BI integration with GitHub to visualize issues closed per user on a weekly and monthly basis for the WiseLink-Labels project.

## Overview

- **Data Source**: GitHub API (GraphQL)
- **Repository**: wise-business-forms/WiseLink-Labels
- **Metrics**: Issues closed per user (weekly & monthly aggregations)
- **Tools**: Power BI Desktop/Service, Power Query (M), Python (optional helper)

---

## Quick Start

### 1. Create a GitHub Personal Access Token

1. Go to [GitHub Settings → Personal Access Tokens](https://github.com/settings/tokens)
2. Click **Generate new token** (classic)
3. Give it a name: `PowerBI-Report`
4. Select scopes:
   - `repo` (full repository access)
   - `read:project` (read project data)
5. Click **Generate token** and copy it (you won't see it again!)
6. Store securely (use environment variables or Power BI parameter)

### 2. Option A: Direct Power BI Connection (Recommended)

#### Using Power Query (M Script)

1. **Open Power BI Desktop**
2. Go to **Home** → **Get Data** → **Other** → **Web**
3. Enter: `https://api.github.com/graphql`
4. Click **OK**
5. In the Web dialog, select **Advanced** and paste this header:
   ```
   Authorization: Bearer YOUR_GITHUB_TOKEN
   ```
6. Click **OK** and authenticate

#### Alternative: Get Data from Web

If the above doesn't work:

1. **New Query** → **Blank Query**
2. In the formula bar, paste the contents of `powerbi/github_api_connector.m`
3. Replace `YOUR_GITHUB_TOKEN_HERE` with your actual token
4. Click **Invoke**

### 3. Option B: Using Python Helper Script (For Data Preparation)

#### Setup

1. Install Python dependencies:
   ```bash
   pip install -r reporting/python/requirements.txt
   ```

2. Set up environment variable:
   ```bash
   export GITHUB_TOKEN="your_token_here"
   ```

3. Run the fetcher:
   ```bash
   python reporting/python/github_issues_fetcher.py
   ```

   This generates:
   - `reporting/data/github_issues_closed.csv`
   - `reporting/data/github_issues_closed.json`

4. **In Power BI:**
   - **Get Data** → **CSV** or **JSON**
   - Select the generated file
   - Load and refresh as needed

---

## Power BI Setup: Creating Visualizations

### Data Model

After connecting your data source, you'll have these columns:

| Column | Type | Description |
|--------|------|-------------|
| `issue_number` | Integer | Unique issue ID |
| `issue_title` | Text | Issue title |
| `closed_date` | Date | Date issue was closed |
| `closed_by_user` | Text | GitHub username who closed it |
| `issue_state` | Text | State (closed) |
| `year_week` | Text | Aggregation: YYYY-Www |
| `year_month` | Text | Aggregation: YYYY-MM |
| `week_start` | Date | Start date of the week |
| `month_start` | Date | Start date of the month |

### Recommended Visualizations

#### 1. Line Chart: Issues Closed Over Time
- **X-axis**: `year_week` or `year_month`
- **Y-axis**: Count of `issue_number`
- **Legend**: `closed_by_user`
- Shows trends and contributor activity over time

#### 2. Clustered Bar Chart: Top Contributors
- **X-axis**: `closed_by_user`
- **Y-axis**: Count of `issue_number`
- **Sort**: Descending by count
- Quick view of most active contributors

#### 3. Matrix/Table: Detailed View
- **Rows**: `closed_by_user`
- **Columns**: `year_month`
- **Values**: Count of `issue_number`
- Pivot table showing monthly breakdown by contributor

#### 4. Card Visuals: Key Metrics
- Total issues closed: `COUNT(issue_number)`
- Total contributors: `DISTINCTCOUNT(closed_by_user)`
- Date range: Min/Max of `closed_date`

#### 5. Slicers: Interactivity
- Add slicers for `closed_by_user` and `closed_date`
- Allows drilling down by contributor or time period

### Example DAX Measures

```dax
// Total Issues Closed
Total Issues = COUNTA([issue_number])

// Issues by Month (for matrix)
Issues by Month = CALCULATE(
    COUNTA([issue_number]),
    FILTER(ALL([year_month]), [year_month] IN VALUES([year_month]))
)

// Cumulative Issues (running total)
Cumulative Issues = CALCULATE(
    COUNTA([issue_number]),
    FILTER(ALL([closed_date]), [closed_date] <= MAX([closed_date]))
)
```

---

## Refreshing Data

### Automatic Refresh (Power BI Service - Pro License Required)

1. Publish report to Power BI Service
2. Go to **Datasets** → Select your dataset
3. Click **⚙️ Settings**
4. Enable **Scheduled refresh**
5. Set frequency (daily, weekly, etc.)
6. Configure gateway for on-premises refresh if needed

### Manual Refresh (Power BI Desktop)

1. Open report
2. **Home** → **Refresh** (or press **F5**)

### Scripted Refresh (Automation)

Create a scheduled task (Windows) or cron job (Mac/Linux):

```bash
#!/bin/bash
export GITHUB_TOKEN="your_token_here"
cd /path/to/WiseLink-Labels
python reporting/python/github_issues_fetcher.py
# Then trigger Power BI refresh via Power BI REST API or export to shared location
```

---

## Security Best Practices

⚠️ **Important**: Never commit your GitHub token to the repository!

1. **Use Environment Variables**:
   ```bash
   export GITHUB_TOKEN="ghp_xxxxxxxxxxxx"
   ```

2. **Power BI Parameters** (Cloud):
   - Set up as a parameter in Power BI Service
   - Don't hardcode in query

3. **Token Rotation**:
   - Regularly rotate your GitHub token
   - Use short-lived tokens if available

4. **.gitignore** (if storing locally):
   ```
   # Sensitive files
   .env
   *.token
   secrets/
   ```

---

## Troubleshooting

### "Authentication Failed"
- Verify GitHub token is valid and hasn't expired
- Check token scopes include `repo` and `read:project`
- Ensure Authorization header format: `Bearer <token>`

### "No Data Returned"
- Confirm repository has closed issues
- Check GraphQL query syntax in `github_api_connector.m`
- Verify `closed_by` field exists (some old issues may not have it)

### "Rate Limit Exceeded"
- GitHub API has rate limits (60 requests/hour unauthenticated, 5,000/hour authenticated)
- Reduce refresh frequency or implement caching
- Use GraphQL batching for multiple queries

### "Column Not Found"
- Ensure Power Query script expanded all nested columns
- Check that field names match exactly (case-sensitive in GraphQL)

---

## Files in This Directory

```
reporting/
├── README.md                          (This file)
├── powerbi/
│   └── github_api_connector.m        (Power Query M script for direct connection)
└── python/
    ├── github_issues_fetcher.py      (Python helper for data export)
    └── requirements.txt              (Python dependencies)
```

---

## Next Steps

1. ✅ Create GitHub Personal Access Token
2. ✅ Choose connection method (Power Query or Python)
3. ✅ Test data connection in Power BI Desktop
4. ✅ Create visualizations using the recommended layouts
5. ✅ Publish to Power BI Service (optional, requires Pro license)
6. ✅ Set up automated refresh
7. ✅ Share dashboard with team

---

## Support & Feedback

For issues or questions:
- Check this README troubleshooting section
- Review GitHub API documentation: https://docs.github.com/en/graphql
- Open an issue in the repository

---

## References

- [GitHub GraphQL API Docs](https://docs.github.com/en/graphql)
- [GitHub REST API Docs](https://docs.github.com/en/rest)
- [Power Query M Language Reference](https://learn.microsoft.com/en-us/powerquery-m/)
- [Power BI Documentation](https://learn.microsoft.com/en-us/power-bi/)