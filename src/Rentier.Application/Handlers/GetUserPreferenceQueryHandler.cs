using Rentier.Application.Common;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

public sealed class GetUserPreferenceQueryHandler
    : IQueryHandler<GetUserPreferenceQuery, Result<string?, Error>>
{
    private readonly IUserPreferenceRepository _repository;

    public GetUserPreferenceQueryHandler(IUserPreferenceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<string?, Error>> HandleAsync(
        GetUserPreferenceQuery query, CancellationToken ct = default)
    {
        try
        {
            var preference = await _repository.GetAsync(query.Key, ct);
            return Result<string?, Error>.Success(preference?.Value);
        }
        catch (Exception ex)
        {
            return Result<string?, Error>.Failure(Error.Infrastructure(ex.Message));
        }
    }
}
