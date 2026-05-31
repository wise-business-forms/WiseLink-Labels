"""
GitHub Issues Fetcher for Power BI
Fetches closed issues from wise-business-forms/WiseLink-Labels repository
and exports data in formats suitable for Power BI (CSV, JSON)
"""

import requests
import json
import csv
from datetime import datetime
from collections import defaultdict
from typing import List, Dict, Optional
import os

class GitHubIssuesFetcher:
    """Fetches and processes GitHub issues data"""
    
    def __init__(self, token: str, owner: str, repo: str):
        """
        Initialize the fetcher
        
        Args:
            token: GitHub Personal Access Token
            owner: Repository owner (e.g., 'wise-business-forms')
            repo: Repository name (e.g., 'WiseLink-Labels')
        """
        self.token = token
        self.owner = owner
        self.repo = repo
        self.headers = {
            'Authorization': f'token {token}',
            'Accept': 'application/vnd.github.v3+json',
            'User-Agent': 'GitHub-Issues-Fetcher'
        }
        self.base_url = 'https://api.github.com'
        
    def fetch_closed_issues(self) -> List[Dict]:
        """
        Fetch all closed issues from the repository
        
        Returns:
            List of issue dictionaries
        """
        issues = []
        page = 1
        per_page = 100
        
        print(f"Fetching closed issues from {self.owner}/{self.repo}...")
        
        while True:
            url = f'{self.base_url}/repos/{self.owner}/{self.repo}/issues'
            params = {
                'state': 'closed',
                'per_page': per_page,
                'page': page,
                'sort': 'updated',
                'direction': 'desc'
            }
            
            response = requests.get(url, headers=self.headers, params=params)
            response.raise_for_status()
            
            data = response.json()
            if not data:
                break
            
            # Filter issues that have closed_by info
            for issue in data:
                if issue.get('closed_by'):
                    issues.append(issue)
            
            print(f"  Page {page}: fetched {len(data)} issues (total: {len(issues)} closed)")
            
            # Check if there are more pages
            if len(data) < per_page:
                break
            
            page += 1
        
        print(f"Total closed issues fetched: {len(issues)}\n")
        return issues
    
    def process_issues(self, issues: List[Dict]) -> List[Dict]:
        """
        Process raw issue data into structured format with aggregations
        
        Args:
            issues: List of raw issue dictionaries from GitHub API
            
        Returns:
            List of processed issue dictionaries
        """
        processed = []
        
        for issue in issues:
            closed_at = datetime.fromisoformat(issue['closed_at'].replace('Z', '+00:00'))
            closed_date = closed_at.date()
            
            # Calculate week and month info
            week_start = closed_date.isocalendar()[0]  # Year
            week_num = closed_date.isocalendar()[1]    # Week number
            month_num = closed_date.month
            year = closed_date.year
            
            processed_issue = {
                'issue_number': issue['number'],
                'issue_title': issue['title'],
                'closed_date': str(closed_date),
                'closed_by_user': issue['closed_by']['login'],
                'issue_state': issue['state'],
                'week_start': str(closed_date - __import__('datetime').timedelta(days=closed_date.weekday())),
                'month_start': str(closed_date.replace(day=1)),
                'year_week': f"{year}-W{week_num:02d}",
                'year_month': f"{year}-{month_num:02d}",
                'closed_at_time': issue['closed_at'],
                'labels': ','.join([label['name'] for label in issue.get('labels', [])])
            }
            processed.append(processed_issue)
        
        return processed
    
    def export_to_csv(self, issues: List[Dict], output_path: str = 'github_issues_closed.csv'):
        """
        Export processed issues to CSV file
        
        Args:
            issues: List of processed issue dictionaries
            output_path: Path to output CSV file
        """
        if not issues:
            print("No issues to export")
            return
        
        keys = issues[0].keys()
        
        with open(output_path, 'w', newline='', encoding='utf-8') as f:
            writer = csv.DictWriter(f, fieldnames=keys)
            writer.writeheader()
            writer.writerows(issues)
        
        print(f"✓ Exported {len(issues)} issues to {output_path}\n")
    
    def export_to_json(self, issues: List[Dict], output_path: str = 'github_issues_closed.json'):
        """
        Export processed issues to JSON file
        
        Args:
            issues: List of processed issue dictionaries
            output_path: Path to output JSON file
        """
        if not issues:
            print("No issues to export")
            return
        
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(issues, f, indent=2)
        
        print(f"✓ Exported {len(issues)} issues to {output_path}\n")
    
    def generate_summary(self, issues: List[Dict]):
        """
        Generate and print summary statistics
        
        Args:
            issues: List of processed issue dictionaries
        """
        if not issues:
            print("No issues to summarize")
            return
        
        # Summary by user
        by_user = defaultdict(int)
        for issue in issues:
            by_user[issue['closed_by_user']] += 1
        
        # Summary by month
        by_month = defaultdict(int)
        for issue in issues:
            by_month[issue['year_month']] += 1
        
        # Summary by week
        by_week = defaultdict(int)
        for issue in issues:
            by_week[issue['year_week']] += 1
        
        print("=" * 60)
        print("SUMMARY STATISTICS")
        print("=" * 60)
        
        print("\n📊 ISSUES CLOSED BY USER:")
        print("-" * 60)
        for user in sorted(by_user.keys()):
            print(f"  {user}: {by_user[user]} issues")
        
        print("\n📅 ISSUES CLOSED BY MONTH:")
        print("-" * 60)
        for month in sorted(by_month.keys()):
            print(f"  {month}: {by_month[month]} issues")
        
        print("\n📆 ISSUES CLOSED BY WEEK:")
        print("-" * 60)
        for week in sorted(by_week.keys()):
            print(f"  {week}: {by_week[week]} issues")
        
        print("\n" + "=" * 60)
        print(f"Total closed issues: {len(issues)}")
        print(f"Total unique contributors: {len(by_user)}")
        print("=" * 60 + "\n")

def main():
    """Main execution function"""
    
    # Configuration
    GITHUB_TOKEN = os.getenv('GITHUB_TOKEN', 'YOUR_GITHUB_TOKEN_HERE')
    OWNER = 'wise-business-forms'
    REPO = 'WiseLink-Labels'
    OUTPUT_DIR = 'reporting/data'
    
    # Create output directory if it doesn't exist
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    
    if GITHUB_TOKEN == 'YOUR_GITHUB_TOKEN_HERE':
        print("❌ Error: GitHub token not set!")
        print("Set the GITHUB_TOKEN environment variable or update the script directly.")
        return
    
    try:
        # Initialize fetcher
        fetcher = GitHubIssuesFetcher(GITHUB_TOKEN, OWNER, REPO)
        
        # Fetch and process issues
        raw_issues = fetcher.fetch_closed_issues()
        processed_issues = fetcher.process_issues(raw_issues)
        
        # Export data
        csv_path = os.path.join(OUTPUT_DIR, 'github_issues_closed.csv')
        json_path = os.path.join(OUTPUT_DIR, 'github_issues_closed.json')
        
        fetcher.export_to_csv(processed_issues, csv_path)
        fetcher.export_to_json(processed_issues, json_path)
        
        # Print summary
        fetcher.generate_summary(processed_issues)
        
        print(f"✅ Data export complete!")
        print(f"   CSV:  {csv_path}")
        print(f"   JSON: {json_path}")
        
    except requests.exceptions.RequestException as e:
        print(f"❌ API Error: {e}")
    except Exception as e:
        print(f"❌ Error: {e}")

if __name__ == '__main__':
    main()