namespace DiNotifications;

[ExcludeFromCodeCoverage]
public sealed class SenderService : ISenderService
{
    public Task Send(DateTimeOffset timestamp, string subject, string body, CancellationToken cancellationToken = default)
    {
        Console.WriteLine(
$"""
[{timestamp:u}] {subject}

{body}

""");

        return Task.CompletedTask;
    }
}
