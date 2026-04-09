using ErrorOr;

namespace Domain.Errors;

public static class RecurrenceErrors
{
    public static readonly Error NotRecurring = Error.Validation(
        "Recurrence.NotRecurring", "This entity does not have a recurrence rule.");

    public static readonly Error InvalidOccurrenceDate = Error.Validation(
        "Recurrence.InvalidOccurrenceDate", "The specified date is not a valid occurrence of this recurring entity.");

    public static readonly Error InvalidInterval = Error.Validation(
        "Recurrence.InvalidInterval", "Recurrence interval must be at least 1.");

    public static readonly Error EndDateBeforeStart = Error.Validation(
        "Recurrence.EndDateBeforeStart", "Recurrence end date cannot be before the start date.");

    public static readonly Error InvalidDateRange = Error.Validation(
        "Recurrence.InvalidDateRange", "startDate must be before or equal to endDate.");
}
