using Domain.Abstractions.Repositories;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public sealed class PaycheckRepository(MoneyWizardContext context) : IPaycheckRepository
{
    public async Task<PaycheckSeries?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.PaycheckSeries
            .Include(s => s.Segments)
            .Include(s => s.Exceptions)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PaycheckSeries>> GetAll(CancellationToken cancellationToken = default)
        => await context.PaycheckSeries
            .Include(s => s.Segments)
            .Include(s => s.Exceptions)
            .ToListAsync(cancellationToken);

    public async Task Add(PaycheckSeries entity, CancellationToken cancellationToken = default)
        => await context.PaycheckSeries.AddAsync(entity, cancellationToken);

    public void Update(PaycheckSeries entity)
        => context.PaycheckSeries.Update(entity);

    public void Delete(PaycheckSeries entity)
        => context.PaycheckSeries.Remove(entity);

    public async Task<IReadOnlyList<PaycheckSeries>> GetByUserId(Guid userId, CancellationToken cancellationToken = default)
        => await context.PaycheckSeries
            .Include(s => s.Segments)
            .Include(s => s.Exceptions)
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaycheckSeries>> GetByUserIdInRange(Guid userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var all = await GetByUserId(userId, cancellationToken);
        return all.Where(s => SeriesOverlapsRange(s, startDate, endDate)).ToList();
    }

    public async Task AddSegment(PaycheckSegment segment, CancellationToken cancellationToken = default)
        => await context.PaycheckSegments.AddAsync(segment, cancellationToken);

    public void UpdateSegment(PaycheckSegment segment)
        => context.PaycheckSegments.Update(segment);

    public void DeleteSegment(PaycheckSegment segment)
        => context.PaycheckSegments.Remove(segment);

    public async Task AddException(PaycheckException exception, CancellationToken cancellationToken = default)
        => await context.PaycheckExceptions.AddAsync(exception, cancellationToken);

    public void UpdateException(PaycheckException exception)
        => context.PaycheckExceptions.Update(exception);

    public void DeleteException(PaycheckException exception)
        => context.PaycheckExceptions.Remove(exception);

    public async Task<PaycheckException?> GetException(Guid seriesId, DateOnly date, CancellationToken cancellationToken = default)
        => await context.PaycheckExceptions
            .FirstOrDefaultAsync(
                e => e.SeriesId == seriesId
                    && (e.OriginalDate == date || (e.OriginalDate == null && e.Date == date)),
                cancellationToken);

    public async Task DeleteExceptionsFromDate(Guid seriesId, DateOnly fromDate, CancellationToken cancellationToken = default)
        => await context.PaycheckExceptions
            .Where(e => e.SeriesId == seriesId
                && (e.OriginalDate >= fromDate || (e.OriginalDate == null && e.Date >= fromDate)))
            .ExecuteDeleteAsync(cancellationToken);

    public async Task DeleteSegmentsFromDate(Guid seriesId, DateOnly fromDate, CancellationToken cancellationToken = default)
        => await context.PaycheckSegments
            .Where(s => s.SeriesId == seriesId && s.EffectiveFrom >= fromDate)
            .ExecuteDeleteAsync(cancellationToken);

    private static bool SeriesOverlapsRange(PaycheckSeries series, DateOnly start, DateOnly end)
    {
        var sorted = series.Segments.OrderBy(s => s.EffectiveFrom).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            var seg = sorted[i];
            if (seg.RecurrenceRule is null)
            {
                // Non-recurring segments produce a single occurrence at EffectiveFrom.
                if (seg.EffectiveFrom >= start && seg.EffectiveFrom <= end)
                    return true;
                continue;
            }

            var nextBoundary = i + 1 < sorted.Count
                ? sorted[i + 1].EffectiveFrom.AddDays(-1)
                : DateOnly.MaxValue;
            var ruleEnd = seg.RecurrenceRule.EndDate ?? DateOnly.MaxValue;
            var segEnd = nextBoundary < ruleEnd ? nextBoundary : ruleEnd;
            if (seg.EffectiveFrom <= end && segEnd >= start)
                return true;
        }
        return false;
    }
}
