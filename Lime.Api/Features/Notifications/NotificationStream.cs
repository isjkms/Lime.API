using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using StackExchange.Redis;

namespace Lime.Api.Features.Notifications;

/// <summary>
/// Redis Pub/Sub 기반 알림 스트림. 다중 노드에서 발생한 이벤트를
/// 다른 노드에 연결된 사용자의 SSE로도 전달한다.
/// 채널: "lime:notifications:{userId}".
/// </summary>
public class NotificationStream
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IConnectionMultiplexer _redis;

    public NotificationStream(IConnectionMultiplexer redis) => _redis = redis;

    public async IAsyncEnumerable<NotificationDto> SubscribeAsync(
        Guid userId, [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<NotificationDto>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        var sub = _redis.GetSubscriber();
        var redisChannel = new RedisChannel(ChannelName(userId), RedisChannel.PatternMode.Literal);

        Action<RedisChannel, RedisValue> handler = (_, msg) =>
        {
            if (msg.IsNullOrEmpty) return;
            try
            {
                var dto = JsonSerializer.Deserialize<NotificationDto>((string)msg!, JsonOpts);
                if (dto is not null) channel.Writer.TryWrite(dto);
            }
            catch { /* malformed message — drop */ }
        };

        await sub.SubscribeAsync(redisChannel, handler);
        try
        {
            await foreach (var dto in channel.Reader.ReadAllAsync(ct))
                yield return dto;
        }
        finally
        {
            await sub.UnsubscribeAsync(redisChannel, handler);
            channel.Writer.TryComplete();
        }
    }

    public void Publish(Guid userId, NotificationDto dto)
    {
        var json = JsonSerializer.Serialize(dto, JsonOpts);
        _redis.GetSubscriber().Publish(
            new RedisChannel(ChannelName(userId), RedisChannel.PatternMode.Literal),
            json,
            CommandFlags.FireAndForget);
    }

    private static string ChannelName(Guid userId) => $"lime:notifications:{userId:N}";
}

public record NotificationDto(
    Guid Id,
    string Kind,
    Actor? Actor,
    string? Link,
    string Message,
    bool Read,
    DateTime CreatedAt);

public record Actor(Guid Id, string DisplayName, string? AvatarUrl);
