namespace Rentier.Application.DTOs;

public sealed record TaxpayerProfileDto(
    Guid Id,
    string Jmbg,
    string FullName,
    string Address,
    string OpstinaCode,
    string? PhoneNumber,
    string? Email);
