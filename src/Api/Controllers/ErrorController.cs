using ErrorOr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api.Controllers;

[ApiController]
public class ErrorController : ControllerBase
{
    private readonly ILogger? _logger;

    public ErrorController(ILogger logger)
    {
        _logger = logger;
    }

    public ErrorController() { }

    protected ActionResult<TValue> MatchOk<TValue>(ErrorOr<TValue> result) where TValue : class
    {
        if (result.IsError)
            return Problem(result.Errors);

        return result.Value;
    }

    protected ActionResult MatchNoContent<TValue>(ErrorOr<TValue> result)
    {
        if (result.IsError)
            return Problem(result.Errors);

        return NoContent();
    }

    protected ActionResult Problem(List<Error> errors)
    {
        if (errors.Count is 0)
            return Problem();

        _logger?.LogError("Errors found in the request {ERRORS}", errors.Select(e => $"[{e.Code}] {e.Description}"));

        if (errors.All(error => error.Type == ErrorType.Validation))
            return ValidationProblem(errors);

        HttpContext.Items[Configuration.CommonKeys.HttpContextItems.ERRORS] = errors;

        return Problem(errors[0]);
    }

    protected ActionResult Problem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Problem(statusCode: statusCode, title: error.Description);
    }

    private ActionResult ValidationProblem(List<Error> errors)
    {
        var modelStateDictionary = new ModelStateDictionary();

        foreach (var error in errors)
            modelStateDictionary.AddModelError(error.Code, error.Description);

        return ValidationProblem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            modelStateDictionary: modelStateDictionary
        );
    }
}
