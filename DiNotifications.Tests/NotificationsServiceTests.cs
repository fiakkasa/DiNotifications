using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneOf;

namespace DiNotifications.Tests;

public class NotificationsServiceTests
{
    private readonly NotificationsService _notificationsService;
    private readonly IOptionsMonitor<NotificationsConfig> _optionsMonitor = Substitute.For<IOptionsMonitor<NotificationsConfig>>();
    private readonly ISenderService _senderService = Substitute.For<ISenderService>();
    private readonly IHostApplicationLifetime _hostApplicationLifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly ILogger<NotificationsService> _logger = Substitute.For<ILogger<NotificationsService>>();

    private readonly NotificationsConfig _defaultConfig = new()
    {
        ImmediateCallsThresholdWindow = TimeSpan.FromMilliseconds(500),
        BatchedCallsRetentionPeriod = TimeSpan.FromMilliseconds(500),
        MaxImmediateCalls = 1,
        BatchedItemsSeparator = "---",
        MaxBatchedItems = 100
    };

    public NotificationsServiceTests()
    {
        _optionsMonitor.CurrentValue.Returns(_defaultConfig);
        _senderService.Send(
            Arg.Any<DateTimeOffset>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        ).Returns(true);
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
        await cts.CancelAsync();

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
        await cts.CancelAsync();

        var result = await _notificationsService.Send("subject", "message", cts.Token);

        await Task.Delay(600);

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
    public async Task Send_Should_Send_Single_Message_Schedule_Single_Message_Batch_And_Abort_Sending_Batch_On_ApplicationStopping()
    {
        using var cts = new CancellationTokenSource();
        _hostApplicationLifetime.ApplicationStopping.Returns(cts.Token);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 2).Select(x => _notificationsService.Send("subject", "message"))
        );

        await cts.CancelAsync();

        var receivedCallsBefore = _senderService.ReceivedCalls().ToArray();

        await Task.Delay(600);

        var receivedCallsAfter = _senderService.ReceivedCalls().ToArray();

        Assert.Single(receivedCallsBefore);
        Assert.Single(receivedCallsAfter);
        Assert.All(results, x => Assert.True(x.IsT0 && x.AsT0));
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<OperationCanceledException>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Fact]
    public async Task Send_Should_Send_Single_Message_Schedule_Multi_Message_Batch_And_Abort_Sending_Batch_On_ApplicationStopping()
    {
        using var cts = new CancellationTokenSource();
        _hostApplicationLifetime.ApplicationStopping.Returns(cts.Token);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(x => _notificationsService.Send("subject", "message"))
        );

        await cts.CancelAsync();

        var receivedCallsBefore = _senderService.ReceivedCalls().ToArray();

        await Task.Delay(600);

        var receivedCallsAfter = _senderService.ReceivedCalls().ToArray();

        Assert.Single(receivedCallsBefore);
        Assert.Single(receivedCallsAfter);
        Assert.All(results, x => Assert.True(x.IsT0 && x.AsT0));
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<OperationCanceledException>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(9)]
    public async Task Send_Should_Send_Single_Message_Within_Window(int factor)
    {
        var messages =
            Enumerable.Range(0, factor)
                .Select((x, i) => $"{i}_Hello")
                .ToArray();

        var results = new List<OneOf<bool, Exception>>();

        foreach (var message in messages)
        {
            results.Add(await _notificationsService.Send("subject", message));
            await Task.Delay(TimeSpan.FromMilliseconds(600));
        }

        var receivedMessagesBody =
            _senderService
                .ReceivedCalls()
                .Select(x => x.GetArguments()[2]?.ToString())
                .ToArray();

        Assert.All(results, x => Assert.True(x.IsT0 && x.AsT0));
        Assert.Equal(messages.Length, receivedMessagesBody.Length);
        Assert.All(
            messages,
            (expectedMessage, index) =>
                Assert.Equal(expectedMessage, receivedMessagesBody[index])
        );
    }

    [Fact]
    public async Task Send_Should_Process_A_Limited_Amount_Of_Messages_Based_On_Configuration()
    {
        var messages =
            Enumerable.Range(0, 201)
                .Select((x, i) => $"{i}_Hello")
                .ToArray();

        var results = await Task.WhenAll(
            messages.Select(message => _notificationsService.Send("subject", message))
        );

        await Task.Delay(TimeSpan.FromMilliseconds(600));

        var receivedMessagesBody =
            _senderService
                .ReceivedCalls()
                .Select(x => x.GetArguments()[2]?.ToString())
                .ToArray();

        Assert.All(results, x => Assert.True(x.IsT0 && x.AsT0));
        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(x => x.ToString() == $"Only {_defaultConfig.MaxBatchedItems} out of {messages.Length - 1} will be processed."),
            null,
            Arg.Any<Func<object, Exception?, string>>()
        );
        Assert.Equal(2, receivedMessagesBody.Length);
        var batchedText = receivedMessagesBody[1]?.Split(_defaultConfig.BatchedItemsSeparator) ?? [];
        Assert.Contains("Note: Only ", batchedText[^1]);
        Assert.Contains("Check the logs for more details.", batchedText[^1]);
        // skip last element: note
        Assert.Equal(_defaultConfig.MaxBatchedItems, batchedText.Length -1);
        Assert.All(
            // first message sent immediately
            messages.Skip(1).Take(_defaultConfig.MaxBatchedItems),
            (expectedMessage, index) =>
                Assert.Contains(expectedMessage, batchedText[index])
        );
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task Send_Should_Send_Single_Message_And_Batched_Message_Combinations_Within_Window(int factor)
    {
        var messages = new[]
        {
            // single
            "Hello",
            // batched
            "World"
        };
        var additionalMessage = "Test";
        var results = new List<OneOf<bool, Exception>>();

        for (var i = 0; i < factor; i++)
        {
            var requests = new List<Task<OneOf<bool, Exception>>>(
                messages.Select(message =>
                    _notificationsService.Send("subject", message)
                )
            );

            if (i >= 2)
            {
                requests.Add(_notificationsService.Send("subject", additionalMessage));
            }

            results.AddRange(await Task.WhenAll(requests));

            await Task.Delay(TimeSpan.FromMilliseconds(600));
        }

        var receivedArguments =
            _senderService.ReceivedCalls()
                .Select(x => x.GetArguments())
                .ToArray();
        var receivedInitialMessagesBody =
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

        Assert.All(results, x => Assert.True(x.IsT0 && x.AsT0));
        // factor x single initial + factor x batched
        Assert.Equal(factor + factor, receivedArguments.Length);
        Assert.All(
            receivedInitialMessagesBody,
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
                var batchedText = value.Body?.Split(_defaultConfig.BatchedItemsSeparator) ?? [];
                Assert.Equal(2, batchedText.Length);
                Assert.Contains(messages[1], batchedText[0]);
                Assert.Contains(additionalMessage, batchedText[1]);
            }
        );
    }

    [Fact]
    public async Task Send_Should_Send_Multiple_Single_Messages_And_Batched_Message_Combinations_Within_Window()
    {
        _optionsMonitor.CurrentValue.Returns(
            _defaultConfig with { MaxImmediateCalls = 3 }
        );
        var notificationsService = new NotificationsService(
            _optionsMonitor,
            _senderService,
            _hostApplicationLifetime,
            _logger
        );

        var messages = new[]
        {
            // single
            "1_Hello",
            "2_World",
            "3_Test",
            // batched
            "4_Hello",
            "5_World",
            "6_Test",
            "7_Hello",
            "8_World",
            "9_Test"
        };

        var results = await Task.WhenAll(
            messages.Select(message => notificationsService.Send("subject", message))
        );
        await Task.Delay(TimeSpan.FromMilliseconds(600));

        var receivedMessages =
            _senderService
                .ReceivedCalls()
                .Select(x =>
                {
                    var arguments = x.GetArguments();

                    return new
                    {
                        Subject = arguments[1]?.ToString(),
                        Body = arguments[2]?.ToString()
                    };
                })
                .ToArray();

        Assert.All(results, x => Assert.True(x.IsT0 && x.AsT0));
        // 3 single + 1 batched
        Assert.Equal(4, receivedMessages.Length);
        Assert.All(
            receivedMessages.Take(3).Select((message, index) => (message, index)),
            item => Assert.Equal(messages[item.index], item.message.Body)
        );

        var batched = receivedMessages[3];
        Assert.Equal("6 Notifications Received", batched.Subject);
        var batchedText = batched.Body?.Split(_defaultConfig.BatchedItemsSeparator) ?? [];
        Assert.Equal(6, batchedText.Length); // 6 messages
        Assert.All(
            messages.Skip(3),
            message => Assert.Contains(message, batched.Body)
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
