namespace InstaSafe.Application.Common.Interfaces;

/// <summary>
/// Maps a WhatsApp contact JID (e.g. an @lid privacy id) back to the
/// phone number. Best-effort: null when the gateway cannot map it.
/// </summary>
public interface IContactResolver
{
    Task<string?> ResolvePhoneAsync(string contactJid, CancellationToken ct);
}
