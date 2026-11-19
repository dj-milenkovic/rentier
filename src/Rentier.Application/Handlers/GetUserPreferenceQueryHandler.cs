using Microsoft.Extensions.Logging;
using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

public sealed class GetUserPreferenceQueryHandler
    : IQueryHandler<GetUserPreferenceQuery, Result<string?, Error>>
{
    private readonly IUserPreferenceRepository _repository;
    private readonly ILogger<GetUserPreferenceQueryHandler>? _logger;

    public GetUserPreferenceQueryHandler(IUserPreferenceRepository repository, ILogger<GetUserPreferenceQueryHandler>? logger = null)
    {
        _logger = logger;
        _repository = repository;
    }

    public Task<Result<string?, Error>> HandleAsync(
        GetUserPreferenceQuery query, CancellationToken ct = default) =>
        HandlerHelper.ExecuteAsync<string?>(
            async () =>
            {
                var preference = await _repository.GetAsync(query.Key, ct);
                return Result<string?, Error>.Success(preference?.Value);
            },
            ErrorCodes.INFRASTRUCTURE_ERROR,
            _logger);
}
