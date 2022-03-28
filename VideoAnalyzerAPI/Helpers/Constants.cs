namespace VideoAnalyzerAPI.Helpers
{
    public class Constants
    {
        public static readonly string SubscriptionKey = "";
        public static readonly string AccountId = "";
        public static readonly string Location = "trial";

        public static readonly string VideoIndexerBaseUrl = $"https://api.videoindexer.ai/{Location}/Accounts";
        public static readonly string AuthBaseUrl = $"https://api.videoindexer.ai/Auth/{Location}/Accounts/";
        public static readonly string TokenService = "AccessToken?allowEdit=False";
        public static readonly string SearchVideos = "Videos/Search?language=en-US&pageSize=25&skip=0&accessToken=";
        public static readonly string QueryParameter = "&query=";
        public static string VideoIndexerAccessToken = "";
    }
}
