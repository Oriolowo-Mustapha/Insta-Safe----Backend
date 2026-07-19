using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using InstaSafe.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace InstaSafe.Infrastructure.ExternalServices.Cloudinary;

public class CloudinaryImageUploadService : IImageUploadService
{
    private readonly CloudinaryDotNet.Cloudinary _cloudinary;

    public CloudinaryImageUploadService(IOptions<CloudinaryOptions> options)
    {
        var account = new Account(
            options.Value.CloudName,
            options.Value.ApiKey,
            options.Value.ApiSecret);

        _cloudinary = new CloudinaryDotNet.Cloudinary(account);
    }

    public async Task<string> UploadImageAsync(Stream imageStream, string fileName, CancellationToken cancellationToken = default)
    {
        var uploadParams = new ImageUploadParams()
        {
            File = new FileDescription(fileName, imageStream),
            Folder = "instasafe_disputes",
            UseFilename = true,
            UniqueFilename = true,
            Overwrite = false
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams, cancellationToken);

        if (uploadResult.Error != null)
        {
            throw new Exception($"Cloudinary Upload Error: {uploadResult.Error.Message}");
        }

        return uploadResult.SecureUrl.ToString();
    }
}
