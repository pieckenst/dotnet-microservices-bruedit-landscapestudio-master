using Account.Application.Contracts.Persistence;
using Account.Application.Exceptions;
using MediatR;

namespace Account.Application.Features.Accounts.Commands.Adding
{
    public sealed class AddingHandler : IRequestHandler<AddingCommand>
    {
        private readonly IAccountRepository _accountRepository;

        public AddingHandler(IAccountRepository accountRepository)
        {
            _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        }

        public async Task Handle(AddingCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            if (request.Amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(request.Amount), "Deposit amount must be greater than zero.");

            var account = await _accountRepository.GetByIdAsync(request.AccountId);
            if (account is null)
                throw new NotFoundException(nameof(Domain.Entities.Account), request.AccountId);

            if (account.CustomerId != request.CustomerId)
                throw new UnauthorizedAccessException("The account does not belong to the specified customer.");

            account.Balance += request.Amount;
            await _accountRepository.UpdateAsync(account);
        }
    }
}