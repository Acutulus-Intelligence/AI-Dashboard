using Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Application.Common.Mapping
{
    public class ExceptionMapper : IExceptionMapper
    {
        private readonly Dictionary<Type, ExceptionMapResult> _map;

        public ExceptionMapper()
        {
            _map = new()
        {
            { typeof(UnauthorizedAccessException), new ExceptionMapResult {
                StatusCode = (int)HttpStatusCode.Unauthorized,
                Title = "Unauthorized",
                ErrorCode = "unauthorized"
            }},

            { typeof(KeyNotFoundException), new ExceptionMapResult {
                StatusCode = (int)HttpStatusCode.NotFound,
                Title = "Not Found",
                ErrorCode = "not_found"
            }},

            { typeof(ConflictException), new ExceptionMapResult {
                StatusCode = (int)HttpStatusCode.Conflict,
                Title = "Conflict",
                ErrorCode = "conflict"
            }},

            { typeof(DbUpdateException), new ExceptionMapResult {
                StatusCode = (int)HttpStatusCode.Conflict,
                Title = "Database Error",
                ErrorCode = "db_update_error"
            }},

            { typeof(DbUpdateConcurrencyException), new ExceptionMapResult {
                StatusCode = (int)HttpStatusCode.Conflict,
                Title = "Concurrency Conflict",
                ErrorCode = "concurrency_conflict"
            }},

            { typeof(SecurityTokenExpiredException), new ExceptionMapResult {
                StatusCode = (int)HttpStatusCode.Unauthorized,
                Title = "Token Expired",
                ErrorCode = "token_expired"
            }},

            { typeof(SecurityTokenInvalidSignatureException), new ExceptionMapResult {
                StatusCode = (int)HttpStatusCode.Unauthorized,
                Title = "Invalid Token Signature",
                ErrorCode = "invalid_token_signature"
            }},

            { typeof(SecurityTokenException), new ExceptionMapResult {
                StatusCode = (int)HttpStatusCode.Forbidden,
                Title = "Token Error",
                ErrorCode = "token_error"
            }},

            { typeof(FluentValidation.ValidationException), new ExceptionMapResult{

                StatusCode = (int)HttpStatusCode.BadRequest,
                Title = "Validation Failed",
                ErrorCode = "validation_failed"
            }},


            { typeof(ArgumentException), new ExceptionMapResult {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Title = "Bad Request",
                ErrorCode = "bad_request"
            }},

            { typeof(InvalidOperationException), new ExceptionMapResult {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Title = "Invalid Operation",
                ErrorCode = "invalid_operation"
            }},
        };
        }

        public ExceptionMapResult Map(Exception ex)
        {
            var type = ex.GetType();

            if (_map.TryGetValue(type, out var result))
                return result;

            return new ExceptionMapResult
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Title = "Internal Server Error",
                ErrorCode = "server_error"
            };
        }
    }
}
