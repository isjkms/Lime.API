namespace Lime.Api.Features.Reviews;

public record CreateReviewRequest(string Target, string SpotifyId, decimal Rating, string Body);
public record UpdateReviewRequest(decimal? Rating, string? Body);
public record ReactionRequest(string Kind);
