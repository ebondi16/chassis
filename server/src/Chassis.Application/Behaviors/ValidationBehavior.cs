using FluentValidation;
using MediatR;

namespace Chassis.Application.Behaviors;

/// <summary>
/// MediatR pipeline step: runs every registered FluentValidation validator for
/// the incoming request before the handler sees it, and throws a single
/// <see cref="ValidationException"/> if any rule fails. The API layer translates
/// that into an RFC 7807 problem response.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var validatorList = validators as IReadOnlyCollection<IValidator<TRequest>> ?? validators.ToList();
        if (validatorList.Count == 0)
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
                validatorList.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
