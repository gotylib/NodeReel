namespace NodeReel.Infrastructure.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string JwtKey { get; set; } = "NodeReel-dev-secret-change-me-32chars!";
    public string JwtIssuer { get; set; } = "NodeReel";
    public string JwtAudience { get; set; } = "NodeReel";
    public int TokenLifetimeHours { get; set; } = 72;
    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = "admin123";
}
