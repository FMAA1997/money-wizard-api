using Domain.Abstractions.Repositories;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public sealed class ExpenseRepository(MoneyWizardContext context) : IExpenseRepository
{
    public async Task<ExpenseSeries?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.ExpenseSeries
            .Include(s => s.Category)
            .Include(s => s.Segments).ThenInclude(seg => seg.PaycheckSeries)
            .Include(s => s.Segments).ThenInclude(seg => seg.InvoiceSeries)
            .Include(s => s.Exceptions)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ExpenseSeries>> GetAll(CancellationToken cancellationToken = default)
        => await context.ExpenseSeries
            .Include(s => s.Category)
            .Include(s => s.Segments).ThenInclude(seg => seg.PaycheckSeries)
            .Include(s => s.Segments).ThenInclude(seg => seg.InvoiceSeries)
            .Include(s => s.Exceptions)
            .ToListAsync(cancellationToken);

    public async Task Add(ExpenseSeries entity, CancellationToken cancellationToken = default)
        => await context.ExpenseSeries.AddAsync(entity, cancellationToken);

    public void Update(ExpenseSeries entity)
        => context.ExpenseSeries.Update(entity);

    public void Delete(ExpenseSeries entity)
        => context.ExpenseSeries.Remove(entity);

    public async Task<IReadOnlyList<ExpenseSeries>> GetByUserId(Guid userId, CancellationToken cancellationToken = default)
        => await context.ExpenseSeries
            .Include(s => s.Category)
            .Include(s => s.Segments).ThenInclude(seg => seg.PaycheckSeries)
            .Include(s => s.Segments).ThenInclude(seg => seg.InvoiceSeries)
            .Include(s => s.Exceptions)
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExpenseSeries>> GetByUserIdInRange(Guid userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var all = await GetByUserId(userId, cancellationToken);
        return all.Where(s => SeriesOverlapsRange(s, startDate, endDate)).ToList();
    }

    public async Task AddSegment(ExpenseSegment segment, CancellationToken cancellationToken = default)
        => await context.ExpenseSegments.AddAsync(segment, cancellationToken);

    public void UpdateSegment(ExpenseSegment segment)
        => context.ExpenseSegments.Update(segment);

    public void DeleteSegment(ExpenseSegment segment)
        => context.ExpenseSegments.Remove(segment);

    public async Task AddException(ExpenseException exception, CancellationToken cancellationToken = default)
        => await context.ExpenseExceptions.AddAsync(exception, cancellationToken);

    public void UpdateException(ExpenseException exception)
        => context.ExpenseExceptions.Update(exception);

    public void DeleteException(ExpenseException exception)
        => context.ExpenseExceptions.Remove(exception);

    public async Task<ExpenseException?> GetException(Guid seriesId, DateOnly date, CancellationToken cancellationToken = default)
        => await context.ExpenseExceptions
            .FirstOrDefaultAsync(
                e => e.SeriesId == seriesId
                    && (e.OriginalDate == date || (e.OriginalDate == null && e.Date == date)),
                cancellationToken);

    public async Task DeleteExceptionsFromDate(Guid seriesId, DateOnly fromDate, CancellationToken cancellationToken = default)
        => await context.ExpenseExceptions
            .Where(e => e.SeriesId == seriesId
                && (e.OriginalDate >= fromDate || (e.OriginalDate == null && e.Date >= fromDate)))
            .ExecuteDeleteAsync(cancellationToken);

    public async Task DeleteSegmentsFromDate(Guid seriesId, DateOnly fromDate, CancellationToken cancellationToken = default)
        => await context.ExpenseSegments
            .Where(s => s.SeriesId == seriesId && s.EffectiveFrom > fromDate)
            .ExecuteDeleteAsync(cancellationToken);

    private static bool SeriesOverlapsRange(ExpenseSeries series, DateOnly start, DateOnly end)
    {
        var sorted = series.Segments.OrderBy(s => s.EffectiveFrom).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            var seg = sorted[i];
            if (seg.RecurrenceRule is null)
            {
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
