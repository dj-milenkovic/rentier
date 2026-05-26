namespace Rentier.Application.Commands;

public sealed record SetUserPreferenceCommand(string Key, string Value);
