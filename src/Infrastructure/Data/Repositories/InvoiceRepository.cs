using Domain.Abstractions.Repositories;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public sealed class InvoiceRepository(MoneyWizardContext context) : IInvoiceRepository
{
    public async Task<InvoiceSeries?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.InvoiceSeries
            .Include(s => s.Segments).ThenInclude(seg => seg.PaycheckSeries)
            .Include(s => s.Exceptions)
            .Include(s => s.ParentException)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<InvoiceSeries>> GetAll(CancellationToken cancellationToken = default)
        => await context.InvoiceSeries
            .Include(s => s.Segments).ThenInclude(seg => seg.PaycheckSeries)
            .Include(s => s.Exceptions)
            .Include(s => s.ParentException)
            .ToListAsync(cancellationToken);

    public async Task Add(InvoiceSeries entity, CancellationToken cancellationToken = default)
        => await context.InvoiceSeries.AddAsync(entity, cancellationToken);

    public void Update(InvoiceSeries entity)
        => context.InvoiceSeries.Update(entity);

    public void Delete(InvoiceSeries entity)
        => context.InvoiceSeries.Remove(entity);

    public async Task<IReadOnlyList<InvoiceSeries>> GetByUserId(Guid userId, CancellationToken cancellationToken = default)
        => await context.InvoiceSeries
            .Include(s => s.Segments).ThenInclude(seg => seg.PaycheckSeries)
            .Include(s => s.Exceptions)
            .Include(s => s.ParentException)
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InvoiceSeries>> GetByUserIdInRange(Guid userId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var all = await GetByUserId(userId, cancellationToken);
        return all.Where(s => SeriesOverlapsRange(s, startDate, endDate)).ToList();
    }

    public Task<bool> AnyForUser(Guid userId, CancellationToken cancellationToken = default)
        => context.InvoiceSeries.AnyAsync(s => s.UserId == userId, cancellationToken);

    public async Task AddSegment(InvoiceSegment segment, CancellationToken cancellationToken = default)
        => await context.InvoiceSegments.AddAsync(segment, cancellationToken);

    public void UpdateSegment(InvoiceSegment segment)
        => context.InvoiceSegments.Update(segment);

    public void DeleteSegment(InvoiceSegment segment)
        => context.InvoiceSegments.Remove(segment);

    public async Task AddException(InvoiceException exception, CancellationToken cancellationToken = default)
        => await context.InvoiceExceptions.AddAsync(exception, cancellationToken);

    public void UpdateException(InvoiceException exception)
        => context.InvoiceExceptions.Update(exception);

    public async Task<InvoiceException?> GetException(Guid seriesId, DateOnly originalDate, CancellationToken cancellationToken = default)
        => await context.InvoiceExceptions
            .FirstOrDefaultAsync(e => e.SeriesId == seriesId && e.OriginalDate == originalDate, cancellationToken);

    public async Task DeleteExceptionsFromDate(Guid seriesId, DateOnly fromDate, CancellationToken cancellationToken = default)
        => await context.InvoiceExceptions
            .Where(e => e.SeriesId == seriesId && e.OriginalDate >= fromDate)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task DeleteSegmentsFromDate(Guid seriesId, DateOnly fromDate, CancellationToken cancellationToken = default)
        => await context.InvoiceSegments
            .Where(s => s.SeriesId == seriesId && s.EffectiveFrom > fromDate)
            .ExecuteDeleteAsync(cancellationToken);

    private static bool SeriesOverlapsRange(InvoiceSeries series, DateOnly start, DateOnly end)
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
