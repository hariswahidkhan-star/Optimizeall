using Cronos;
using OptimizeAll.Application.Abstractions.Platform;
using OptimizeAll.SharedKernel.Results;

namespace OptimizeAll.Infrastructure.Scheduling;

/// <summary>
/// Cron evaluation with IANA timezone and daylight-saving handling.
/// <para>
/// Timezone correctness is the whole reason this is a dedicated port. "Every weekday at 09:00
/// Europe/London" is a different UTC instant in January and July, and a scheduler that stores an
/// offset instead of a zone silently shifts every schedule by an hour twice a year — a class of bug
/// that is reported as "the report arrived late" months after it starts.
/// </para>
/// </summary>
public sealed class CronosEvaluator : ICronEvaluator
{
    public Result<DateTimeOffset?> NextOccurrence(string cronExpression, string timeZoneId, DateTimeOffset after)
    {
        Result<(CronExpression Expression, TimeZoneInfo Zone)> parsed = Parse(cronExpression, timeZoneId);

        if (parsed.IsFailure)
        {
            return Result.Failure<DateTimeOffset?>(parsed.Error);
        }

        DateTimeOffset? next = parsed.Value.Expression.GetNextOccurrence(after, parsed.Value.Zone);
        return Result.Success(next);
    }

    public Result ValidateExpression(string cronExpression, string timeZoneId)
    {
        Result<(CronExpression, TimeZoneInfo)> parsed = Parse(cronExpression, timeZoneId);
        return parsed.IsFailure ? Result.Failure(parsed.Error) : Result.Success();
    }

    public Result<IReadOnlyList<DateTimeOffset>> OccurrencesBetween(
        string cronExpression,
        string timeZoneId,
        DateTimeOffset from,
        DateTimeOffset to,
        int limit)
    {
        Result<(CronExpression Expression, TimeZoneInfo Zone)> parsed = Parse(cronExpression, timeZoneId);

        if (parsed.IsFailure)
        {
            return Result.Failure<IReadOnlyList<DateTimeOffset>>(parsed.Error);
        }

        if (to <= from)
        {
            return Result.Success<IReadOnlyList<DateTimeOffset>>([]);
        }

        List<DateTimeOffset> occurrences =
        [
            .. parsed.Value.Expression
                .GetOccurrences(from, to, parsed.Value.Zone, fromInclusive: false, toInclusive: true)
                .Take(limit),
        ];

        return Result.Success<IReadOnlyList<DateTimeOffset>>(occurrences);
    }

    private static Result<(CronExpression Expression, TimeZoneInfo Zone)> Parse(
        string cronExpression,
        string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
        {
            return Result.Failure<(CronExpression, TimeZoneInfo)>(Error.Validation(
                "schedule.cron_required", "A cron expression is required."));
        }

        CronExpression expression;

        try
        {
            // Six fields when seconds are supplied, five otherwise. Chosen by field count rather
            // than by configuration so an operator can paste either form and have it work.
            int fieldCount = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            expression = CronExpression.Parse(
                cronExpression,
                fieldCount >= 6 ? CronFormat.IncludeSeconds : CronFormat.Standard);
        }
        catch (CronFormatException exception)
        {
            return Result.Failure<(CronExpression, TimeZoneInfo)>(Error.Validation(
                "schedule.invalid_cron",
                $"'{cronExpression}' is not a valid cron expression: {exception.Message}"));
        }

        TimeZoneInfo zone;

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return Result.Failure<(CronExpression, TimeZoneInfo)>(Error.Validation(
                "schedule.invalid_timezone",
                $"'{timeZoneId}' is not a recognised IANA timezone identifier."));
        }

        return Result.Success((expression, zone));
    }
}
