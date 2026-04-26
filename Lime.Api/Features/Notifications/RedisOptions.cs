using StackExchange.Redis;

namespace Lime.Api.Features.Notifications;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6379;
    public string Password { get; set; } = string.Empty;
    public int Database { get; set; } = 0;

    public ConfigurationOptions ToConfigurationOptions()
    {
        if (string.IsNullOrWhiteSpace(Host)) throw new InvalidOperationException("Redis:Host is not configured.");
        if (Port == 0) throw new InvalidOperationException("Redis:Port is not configured.");

        var opts = new ConfigurationOptions
        {
            EndPoints = { { Host, Port } },
            DefaultDatabase = Database,
            AbortOnConnectFail = false, // 시작 시 Redis 미가용이어도 앱 부팅
        };
        if (!string.IsNullOrEmpty(Password)) opts.Password = Password;
        return opts;
    }
}
