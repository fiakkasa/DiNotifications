namespace DiNotifications;

[ExcludeFromCodeCoverage]
public sealed class SenderService : ISenderService
{
    public async Task<OneOf<bool, Exception>> Send(
        DateTimeOffset timestamp, 
        string subject,
        string body,
        CancellationToken cancellationToken = default
    )
    {
        Console.WriteLine(
$"""
[{timestamp:u}] {subject}

{body}

""");

        return await Task.FromResult(true);
    }
}
