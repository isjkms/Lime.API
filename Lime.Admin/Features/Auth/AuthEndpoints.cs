namespace Lime.Admin.Features.Auth;

using Lime.Admin.Features.Auth.Services;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", async (HttpContext ctx, AuthService authService) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var email = form["email"].ToString();
            var password = form["password"].ToString();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return Results.Redirect("/login?error=1");
            }

            var ok = await authService.LoginAsync(ctx, email, password);

            return ok
                ? Results.Redirect("/")
                : Results.Redirect("/login?error=1");
        }).AllowAnonymous();

        app.MapPost("/auth/logout", async (HttpContext ctx, AuthService authService) =>
        {
            await authService.LogoutAsync(ctx);
            return Results.Redirect("/login");
        }).RequireAuthorization();

        return app;
    }
}
