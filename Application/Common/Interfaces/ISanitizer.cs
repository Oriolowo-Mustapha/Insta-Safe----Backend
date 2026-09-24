namespace InstaSafe.Application.Common.Interfaces;

public interface ISanitizer
{
    string Clean(string? input, int maxLength = 2000);
}
