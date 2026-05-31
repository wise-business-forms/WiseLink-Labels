// Power Query (M) Script for Power BI: GitHub Issues Closed Per User
// This script fetches closed issues from the WiseLink-Labels project
// and aggregates them by user and date (weekly/monthly)

let
    // Configuration
    GitHubToken = "YOUR_GITHUB_TOKEN_HERE", // Replace with your GitHub Personal Access Token
    Owner = "wise-business-forms",
    Repo = "WiseLink-Labels",
    
    // GraphQL Query to fetch closed issues with closed_by information
    GraphQLQuery = "query {
  repository(owner: \"wise-business-forms\", name: \"WiseLink-Labels\") {
    issues(first: 100, states: CLOSED, orderBy: {field: UPDATED_AT, direction: DESC}) {
      nodes {
        number
        title
        closedAt
        closedBy {
          login
        }
        state
      }
      pageInfo {
        endCursor
        hasNextPage
      }
    }
  }
}",

    // Function to make GraphQL request
    GitHubGraphQLRequest = (query) =>
        let
            Url = "https://api.github.com/graphql",
            Headers = [
                #"Authorization" = "Bearer " & GitHubToken,
                #"Content-Type" = "application/json",
                #"Accept" = "application/vnd.github.v3+json",
                #"User-Agent" = "PowerBI"
            ],
            Body = Json.FromText("{\"query\": \"" & Text.Replace(query, "\"", "\\"") & "\"}"),
            Response = Json.Document(Web.Contents(Url, [Headers=Headers, Content=Body])),
            Data = Response[data][repository][issues][nodes]
        in
            Data,

    // Fetch issues from GitHub
    Issues = GitHubGraphQLRequest(GraphQLQuery),
    
    // Convert to table
    IssuesTable = Table.FromList(Issues, Splitter.SplitByNothing(), null, null, ExtraValues.Error),
    ExpandedIssues = Table.ExpandRecordColumn(IssuesTable, "Column1", 
        {"number", "title", "closedAt", "closedBy", "state"}, 
        {"number", "title", "closedAt", "closedBy", "state"}),
    
    // Expand closedBy column to get login
    ExpandedClosedBy = Table.ExpandRecordColumn(ExpandedIssues, "closedBy",
        {"login"},
        {"closed_by_user"}),
    
    // Filter out null closed_by values
    FilteredIssues = Table.SelectRows(ExpandedClosedBy, each [closed_by_user] <> null),
    
    // Convert closedAt to date
    WithDateOnly = Table.TransformColumns(FilteredIssues,
        {{"closedAt", each Date.From(DateTime.From(_)), type date}}),
    
    // Rename columns for clarity
    RenamedColumns = Table.RenameColumns(WithDateOnly,
        {{"number", "issue_number"}, {"title", "issue_title"}, {"closedAt", "closed_date"}, {"state", "issue_state"}}),
    
    // Add week/month aggregation columns
    WithAggregations = Table.AddColumns(RenamedColumns,
        {
            {"week_start", each Date.StartOfWeek([closed_date]), type date},
            {"month_start", each Date.StartOfMonth([closed_date]), type date},
            {"year_week", each Text.From(Date.Year([closed_date])) & "-W" & Text.PadStart(Text.From(Date.WeekOfYear([closed_date])), 2, "0"), type text},
            {"year_month", each Text.From(Date.Year([closed_date])) & "-" & Text.PadStart(Text.From(Date.Month([closed_date])), 2, "0"), type text}
        }),
    
    // Reorder columns for final output
    FinalColumns = Table.ReorderColumns(WithAggregations,
        {"issue_number", "issue_title", "closed_date", "closed_by_user", "issue_state", "week_start", "month_start", "year_week", "year_month"})
in
    FinalColumns