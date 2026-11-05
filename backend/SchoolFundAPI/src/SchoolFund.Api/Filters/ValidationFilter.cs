using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SchoolFund.Api.ErrorHandling;

namespace SchoolFund.Api.Filters;

/// <summary>Chạy FluentValidation cho tham số action; lỗi trả 400 validation_failed kèm errors theo từng ô.</summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public const string FailedCode = "validation_failed";
    private const string Title = "Dữ liệu không hợp lệ.";
    private const string InvalidValueMessage = "Giá trị không hợp lệ.";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var argument in context.ActionArguments.Values.OfType<object>())
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is IValidator validator)
            {
                var result = await validator.ValidateAsync(
                    new ValidationContext<object>(argument),
                    context.HttpContext.RequestAborted);
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count == 0)
        {
            await next();
            return;
        }

        var errors = failures
            .GroupBy(failure => JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray());
        context.Result = ToResult(context.HttpContext, errors);
    }

    /// <summary>Lỗi model binding (ví dụ JSON sai) trả cùng định dạng, không kèm thông báo kỹ thuật của parser.</summary>
    public static IActionResult ToResult(HttpContext httpContext, ModelStateDictionary modelState)
    {
        var errors = modelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
                _ => new[] { InvalidValueMessage });
        return ToResult(httpContext, errors);
    }

    private static BadRequestObjectResult ToResult(HttpContext httpContext, IDictionary<string, string[]> errors)
    {
        var problem = ApiProblems.Create(httpContext, StatusCodes.Status400BadRequest, FailedCode, Title, errors: errors);
        return new BadRequestObjectResult(problem) { ContentTypes = { ApiProblems.ContentType } };
    }
}
