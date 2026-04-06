namespace Rentier.Application.Commands;

public sealed record SaveTaxpayerProfileCommand(
    string Jmbg,
    string FullName,
    string Address,
    string OpstinaCode,
    string? PhoneNumber = null,
    string? Email = null);
