using Account.Application.Contracts.Persistence;
using Account.Application.Exceptions;
using AutoMapper;
using MediatR;

namespace Account.Application.Features.Accounts.Queries.GetAccount
{
    public class AccountQueryHandler : IRequestHandler<AccountQuery, GetAccountResponse>
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IMapper _mapper;

        public AccountQueryHandler(IAccountRepository accountRepository, IMapper mapper)
        {
            _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<GetAccountResponse> Handle(AccountQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var account = await _accountRepository.GetAsync(
                x => x.Id == request.AccountId && x.CustomerId == request.CustomerId);

            if (account is null)
                throw new NotFoundException(nameof(Domain.Entities.Account), request.AccountId);

            if (account.CustomerId != request.CustomerId)
                throw new UnauthorizedAccessException("The account does not belong to the specified customer.");

            return _mapper.Map<GetAccountResponse>(account)
                ?? throw new InvalidOperationException("The account could not be mapped to a response.");
        }
    }
}