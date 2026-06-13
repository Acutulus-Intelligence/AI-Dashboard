namespace Presentation.CookieExtensions;

public static class AuthCookieExtensions
{
    private const string AccessTokenCookie = "access_token";
    private const string RefreshTokenCookie = "refresh_token";

    public static void SetAuthCookies(this HttpContext httpContext, string accessToken, string refreshToken, int expiresInSeconds, int refreshTokenExpirationDays = 7)
    {
        var secure = httpContext.Request.IsHttps;

        httpContext.Response.Cookies.Append(AccessTokenCookie, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromSeconds(expiresInSeconds),
            Path = "/",
        });

        httpContext.Response.Cookies.Append(RefreshTokenCookie, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(refreshTokenExpirationDays),
            Path = "/api/auth",
        });
    }

    public static void RemoveAuthCookies(this HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(AccessTokenCookie, new CookieOptions
        {
            Path = "/",
        });

        httpContext.Response.Cookies.Delete(RefreshTokenCookie, new CookieOptions
        {
            Path = "/api/auth",
        });
    }
}
