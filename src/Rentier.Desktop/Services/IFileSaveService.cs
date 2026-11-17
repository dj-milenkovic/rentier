namespace Rentier.Desktop.Services;

/// <summary>Describes a file the user is asked to save: dialog texts, suggested name and content.</summary>
public sealed record FileSaveRequest(
    string Title,
    string SuggestedFileName,
    byte[] Content,
    string FileTypeName,
    IReadOnlyList<string> Patterns);

/// <summary>Lets the user pick a destination and writes the content there.</summary>
public interface IFileSaveService
{
    /// <summary>Returns <c>true</c> when the file was written; <c>false</c> when the user cancelled.</summary>
    Task<bool> SaveAsync(FileSaveRequest request);
}
