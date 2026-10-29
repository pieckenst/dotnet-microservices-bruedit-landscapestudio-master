using FluentValidation;
using MediatR;

namespace Shared.ServiceKit
{
    /// <summary>Runs all validators for a request before its handler executes.</summary>
    public sealed class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IReadOnlyCollection<IValidator<TRequest>> _validators;

        public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = (validators ?? throw new ArgumentNullException(nameof(validators))).ToArray();
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (_validators.Count == 0)
                return await next();

            var context = new ValidationContext<TRequest>(request);
            var results = await Task.WhenAll(
                _validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

            var failures = results
                .SelectMany(result => result.Errors)
                .Where(failure => failure is not null)
                .ToArray();

            if (failures.Length > 0)
                throw new ValidationException(failures);

            return await next(cancellationToken);
        }
    }
}