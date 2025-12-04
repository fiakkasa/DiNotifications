using System.ComponentModel.DataAnnotations;

namespace DiNotifications;

public record NotificationsConfig : IValidatableObject
{
    private const long _minWindow = 500;
    private const long _maxWindow = 60_000;

    public TimeSpan ImmediateCallsThresholdWindow { get; init; } = TimeSpan.FromMilliseconds(500);

    public TimeSpan BatchedCallsRetentionPeriod { get; init; } = TimeSpan.FromMilliseconds(1000);

    [Range(1, 256)]
    public int MaxImmediateCalls { get; init; } = 1;

    [StringLength(256)]
    public string BatchedItemsSeparator { get; init; } = string.Empty;

    // 0 to disable
    [Range(0, 1_000)]
    public int MaxBatchedItems { get; init; } = 100;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ImmediateCallsThresholdWindow is not { TotalMilliseconds: >= _minWindow and <= _maxWindow })
        {
            yield return new(
                $"The specified period needs to be between {_minWindow} and {_maxWindow} milliseconds, inclusive.",
                [nameof(ImmediateCallsThresholdWindow)]
            );
        }

        if (BatchedCallsRetentionPeriod is not { TotalMilliseconds: >= _minWindow and <= _maxWindow })
        {
            yield return new(
                $"The specified period needs to be between {_minWindow} and {_maxWindow} milliseconds, inclusive.",
                [nameof(BatchedCallsRetentionPeriod)]
            );
        }
    }
}
