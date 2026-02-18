namespace WiseLabels.Authorization
{
    public class AccessControlOptions
    {
        public string[] FullAccessGroups { get; set; } = Array.Empty<string>();
        public string[] LimitedAccessGroups { get; set; } = Array.Empty<string>();
        public string[] FullAccessUsers { get; set; } = Array.Empty<string>();
        public string[] LimitedAccessUsers { get; set; } = Array.Empty<string>();
    }
}
