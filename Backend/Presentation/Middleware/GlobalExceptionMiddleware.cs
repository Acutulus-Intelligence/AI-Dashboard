using Application.Common.Exceptions;
using Application.Common.Mapping;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Presentation.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IExceptionMapper _mapper;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IWebHostEnvironment env,
        IExceptionMapper mapper)
    {
        _next = next;
        _logger = logger;
        _env = env;
        _mapper = mapper;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            _logger.LogError(ex,
                "Unhandled exception {Method} {Path} TraceId={TraceId} User={UserId}",
                context.Request.Method,
                context.Request.Path,
                traceId,
                context.User?.FindFirst("userId")?.Value ?? "anonymous");

            await WriteResponse(context, ex, traceId);
        }
    }

    private async Task WriteResponse(HttpContext context, Exception ex, string traceId)
    {
        var mapped = _mapper.Map(ex);

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7807",
            Title = mapped.Title,
            Status = mapped.StatusCode,
            Instance = context.Request.Path,
            Detail = _env.IsDevelopment()
                ? ex.Message
                : "An unexpected error occurred."
        };

        problem.Extensions["traceId"] = traceId;
        problem.Extensions["errorCode"] = mapped.ErrorCode;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = mapped.StatusCode;

        await context.Response.WriteAsJsonAsync(problem);
    }



}
