using Rentier.Application.Common;
using Rentier.Application.DTOs;
using Rentier.Application.Interfaces;
using Rentier.Application.Queries;
using Rentier.Application.Repositories;

namespace Rentier.Application.Handlers;

public sealed class GetTaxpayerProfileQueryHandler
    : IQueryHandler<GetTaxpayerProfileQuery, Result<TaxpayerProfileDto?, Error>>
{
    private readonly ITaxpayerProfileRepository _repository;

    public GetTaxpayerProfileQueryHandler(ITaxpayerProfileRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<TaxpayerProfileDto?, Error>> HandleAsync(
        GetTaxpayerProfileQuery query, CancellationToken ct = default)
    {
        var profile = await _repository.GetAsync(ct);
        if (profile is null)
            return Result<TaxpayerProfileDto?, Error>.Success(null);

        var dto = new TaxpayerProfileDto(
            profile.Id,
            profile.Jmbg,
            profile.FullName,
            profile.Address,
            profile.OpstinaCode,
            profile.PhoneNumber,
            profile.Email);

        return Result<TaxpayerProfileDto?, Error>.Success(dto);
    }
}
