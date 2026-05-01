namespace Lime.Data;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;

    public string BuildConnectionString()
    {
        if (string.IsNullOrWhiteSpace(Host)) throw new InvalidOperationException("Database:Host is not configured.");
        if (Port == 0) throw new InvalidOperationException("Database:Port is not configured.");
        if (string.IsNullOrWhiteSpace(Username)) throw new InvalidOperationException("Database:Username is not configured.");
        if (string.IsNullOrWhiteSpace(Password)) throw new InvalidOperationException("Database:Password is not configured.");
        if (string.IsNullOrWhiteSpace(Database)) throw new InvalidOperationException("Database:Database is not configured.");

        return $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password}";
    }
}
