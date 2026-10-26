using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Recruitment.Application.Abstractions;

namespace Recruitment.Application.Common.Behaviors;

internal sealed class ValidationBehavior(IServiceProvider services) : IPipelineBehavior
{
    public async Task<Result<TResponse>> Handle<TResponse>(
        RequestContext context,
        Func<Task<Result<TResponse>>> next,
        CancellationToken ct)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(context.Request.GetType());
        var failures = new List<ValidationFailure>();

        foreach (var validator in services.GetServices(validatorType).Cast<IValidator>())
        {
            var result = await validator.ValidateAsync(new ValidationContext<object>(context.Request), ct);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
            return await next();

        var fieldErrors = failures
            .GroupBy(f => ToCamelCasePath(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

        return Error.Validation(fieldErrors);
    }

    // "Educations[0].School" → "educations[0].school": istemcideki form alan adlarıyla eşleşsin.
    private static string ToCamelCasePath(string path) =>
        string.Join('.', path.Split('.').Select(part =>
            part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
}
