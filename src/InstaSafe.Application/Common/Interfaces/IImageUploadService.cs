namespace InstaSafe.Application.Common.Interfaces;

public interface IImageUploadService
{
    Task<string> UploadImageAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default);
}
