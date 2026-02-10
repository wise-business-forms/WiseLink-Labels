namespace WiseLabels.Services
{
    /// <summary>
    /// Obtains OAuth access tokens for the CERM API.
    /// </summary>
    public interface ICermAuthService
    {
        Task<string?> GetAccessTokenAsync();
    }
}
