using System.ComponentModel.DataAnnotations;

namespace DiNotifications;

public record NotificationsConfig : IValidatableObject
{
    private const long _minWindow = 500;
    private const long _maxWindow = 60_000;

    public TimeSpan Window { get; init; } = TimeSpan.FromMilliseconds(500);

    [Range(1, 256)]
    public int MaxNonBatchedCalls { get; init; } = 1;

    [StringLength(256)]
    public string BatchedItemsSeparator { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Window is not { TotalMilliseconds: >= _minWindow and <= _maxWindow })
        {
            yield return new(
                $"The specified period needs to be between {_minWindow} and {_maxWindow} milliseconds, inclusive.",
                [nameof(Window)]
            );
        }
    }
}
