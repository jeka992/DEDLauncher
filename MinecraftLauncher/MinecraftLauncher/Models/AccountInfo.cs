namespace DedLauncher.Models;

public class AccountInfo
{
    public string Username { get; set; } = "";
    public string Uuid { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public string AccountType { get; set; } = "offline";
    public string SkinPath { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public bool IsLoggedIn => !string.IsNullOrEmpty(Username);
    public bool IsOffline => AccountType == "offline";
    public string AccountTypeLabel => IsOffline ? "cyr1" : "MS";
}
