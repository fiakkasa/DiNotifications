using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiNotifications.Tests;

public class NotificationsServiceTests
{
    private readonly NotificationsService _notificationsService;
    private readonly IOptionsMonitor<NotificationsConfig> _optionsMonitor = Substitute.For<IOptionsMonitor<NotificationsConfig>>();
    private readonly ISenderService _senderService = Substitute.For<ISenderService>();
    private readonly IHostApplicationLifetime _hostApplicationLifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly ILogger<NotificationsService> _logger = Substitute.For<ILogger<NotificationsService>>();

    public NotificationsServiceTests()
    {
        _optionsMonitor.CurrentValue.Returns(new NotificationsConfig { Window = TimeSpan.FromMilliseconds(1000) });

        _notificationsService = new NotificationsService(
            _optionsMonitor,
            _senderService,
            _hostApplicationLifetime,
            _logger
        );
    }

    [Fact]
    public async Task Send_Should_Abort_When_Cancellation_Requested_On_Single()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _notificationsService.Send("subject", "message", cts.Token);

        Assert.True(result.IsT1);
        Assert.IsType<OperationCanceledException>(result.AsT1);
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<OperationCanceledException>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task Send_Should_Abort_When_Cancellation_Requested_On_Multiple()
    {
        await _notificationsService.Send("subject", "message");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _notificationsService.Send("subject", "message", cts.Token);

        await Task.Delay(1100);

        Assert.True(result.IsT1);
        Assert.IsType<OperationCanceledException>(result.AsT1);
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<OperationCanceledException>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task Send_Should_Send_Single_Schedule_Single_Message_Batch_And_Abort_On_Sending_Batch_On_ApplicationStopping()
    {
        using var cts = new CancellationTokenSource();
        _hostApplicationLifetime.ApplicationStopping.Returns(cts.Token);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 2).Select(x => _notificationsService.Send("subject", "message"))
        );

        cts.Cancel();

        var receivedCallsBefore = _senderService.ReceivedCalls().ToArray();

        await Task.Delay(1100);

        var receivedCallsAfter = _senderService.ReceivedCalls().ToArray();

        Assert.Single(receivedCallsBefore);
        Assert.Single(receivedCallsAfter);
        Assert.All(
            results,
            x =>
            {
                Assert.True(x.IsT0);
                Assert.True(x.AsT0);
            }
        );
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<OperationCanceledException>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task Send_Should_Send_Single_Schedule_Multi_Message_Batch_And_Abort_On_Sending_Batch_On_ApplicationStopping()
    {
        using var cts = new CancellationTokenSource();
        _hostApplicationLifetime.ApplicationStopping.Returns(cts.Token);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(x => _notificationsService.Send("subject", "message"))
        );

        cts.Cancel();

        var receivedCallsBefore = _senderService.ReceivedCalls().ToArray();

        await Task.Delay(1100);

        var receivedCallsAfter = _senderService.ReceivedCalls().ToArray();

        Assert.Single(receivedCallsBefore);
        Assert.Single(receivedCallsAfter);
        Assert.All(
            results,
            x =>
            {
                Assert.True(x.IsT0);
                Assert.True(x.AsT0);
            }
        );
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<OperationCanceledException>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task Send_Should_Send_Single_Message_Within_Window()
    {
        var messages = new[]
        {
            "Hello",
            "World",
            "Test"
        };

        foreach (var message in messages)
        {
            await _notificationsService.Send("subject", message);
            await Task.Delay(TimeSpan.FromMilliseconds(1100));
        }

        var receivedMessages =
            _senderService
                .ReceivedCalls()
                .Select(x => x.GetArguments()[2]?.ToString())
                .ToArray();

        Assert.Equal(3, receivedMessages.Length);
        Assert.All(
            messages,
            (expectedMessage, index) => Assert.Equal(expectedMessage, receivedMessages[index])
        );
    }

    [Fact]
    public async Task Send_Should_Send_Single_And_Batched_Message_Combinations_Within_Window()
    {
        var messages = new[]
        {
            "Hello",
            "World"
        };
        var additionalMessage = "Test";

        for (var i = 0; i < 4; i++)
        {
            foreach (var message in messages)
            {
                await _notificationsService.Send("subject", message);
            }

            if (i >= 2)
            {
                await _notificationsService.Send("subject", additionalMessage);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(1100));
        }

        var receivedArguments =
            _senderService.ReceivedCalls()
                .Select(x => x.GetArguments())
                .ToArray();
        var receivedInitialMessages =
            receivedArguments
                .Select((item, index) => (item, index))
                .Where(z => z.index % 2 == 0)
                .Select(z => z.item[2]?.ToString())
                .ToArray();
        var receivedSubsequentMessages =
            receivedArguments
                .Select((item, index) => (item, index))
                .Where(z => z.index % 2 == 1)
                .Select(z => new
                {
                    Subject = z.item[1]?.ToString(),
                    Body = z.item[2]?.ToString()
                })
                .ToArray();

        // 4 x single initial + 4 x batched
        Assert.Equal(4 + 4, receivedArguments.Length);
        Assert.All(
            receivedInitialMessages,
            value => Assert.Equal(messages[0], value)
        );
        Assert.All(
            receivedSubsequentMessages.Take(2),
            value =>
            {
                Assert.Equal("subject", value.Subject);
                Assert.Equal(messages[1], value.Body);
            }
        );
        Assert.All(
            receivedSubsequentMessages.Skip(2),
            value =>
            {
                Assert.Equal("2 Notifications Received", value.Subject);
                Assert.Contains(messages[1], value.Body);
                Assert.Contains(additionalMessage, value.Body);
                Assert.Contains("---", value.Body);
            }
        );
    }

    [Fact]
    public async Task Send_Should_Abort_When_Disposed()
    {
        _notificationsService.Dispose();

        var result = await _notificationsService.Send("subject", "message");

        _notificationsService.Dispose();

        Assert.True(result.IsT0);
        Assert.False(result.AsT0);
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(x => x.ToString() == "Operation cannot commence as the service is disposed."),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
    }
}
