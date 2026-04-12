using System.Security.Claims;
using Api.Providers;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Filters;

public sealed class ResolveCurrentUserFilter(
    IUserRepository userRepository,
    CurrentUserProvider currentUserProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<SkipUserResolutionAttribute>() is not null)
        {
            await next();
            return;
        }

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var externalId = context.HttpContext.User.FindFirstValue("user_id");

        if (string.IsNullOrEmpty(externalId))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = AuthErrors.MissingExternalId.Description
            })
            { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        var user = await userRepository.GetByExternalId(
            externalId,
            context.HttpContext.RequestAborted);

        if (user is null)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = AuthErrors.UserNotFound.Description
            })
            { StatusCode = StatusCodes.Status404NotFound };
            return;
        }

        currentUserProvider.UserId = user.Id;
        await next();
    }
}
