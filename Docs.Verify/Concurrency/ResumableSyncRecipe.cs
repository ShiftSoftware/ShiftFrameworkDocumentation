using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Docs.Verify.Concurrency;

// Compile-checked recipe for the "Concurrency in Shift Framework" guide (Incident A: a long sync that gets cancelled).
// Distilled from the StockPlusPlus sample's StockSyncService, whose behaviour is proven against SQL Server by
// StockSyncTests: a run cancelled after 2,000 of 10,000 rows keeps them, reports 8,000 left, and the next run
// processes exactly those 8,000.

// snippet:ConcurrencyResumableInbox
public class SyncInboxRow
{
    public long ID { get; set; }

    [MaxLength(100)]
    public string Feed { get; set; } = default!;

    [MaxLength(100)]
    public string ExternalCode { get; set; } = default!;

    public string? Name { get; set; }

    // The sync's progress lives on the row, and is written in the same transaction as the row's result.
    public SyncRowStatus Status { get; set; } = SyncRowStatus.Pending;

    public string? Error { get; set; }
}

public enum SyncRowStatus { Pending, Done, Failed }
// endsnippet

public class SyncedItem
{
    public long ID { get; set; }

    [MaxLength(100)]
    public string Feed { get; set; } = default!;

    [MaxLength(100)]
    public string ExternalCode { get; set; } = default!;

    [MaxLength(100)]
    public string Name { get; set; } = default!;
}

public class SyncDb(DbContextOptions<SyncDb> options) : DbContext(options)
{
    public DbSet<SyncInboxRow> Inbox => Set<SyncInboxRow>();
    public DbSet<SyncedItem> Items => Set<SyncedItem>();
}

public enum SyncRunStatus { Completed, Cancelled }

public class SyncResult
{
    public string Feed { get; set; } = default!;
    public SyncRunStatus Status { get; set; }
    public int ProcessedThisRun { get; set; }
    public int FailedThisRun { get; set; }
    public int Remaining { get; set; }
}

public class ResumableSync(SyncDb db)
{
    // snippet:ConcurrencyResumableSyncLoop
    public async Task<SyncResult> RunAsync(string feed, int batchSize, CancellationToken cancellationToken)
    {
        var result = new SyncResult { Feed = feed };

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested(); // between batches: stopping costs nothing

                db.ChangeTracker.Clear();
                var batch = await db.Inbox
                    .Where(x => x.Feed == feed && x.Status == SyncRowStatus.Pending)
                    .OrderBy(x => x.ID)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);

                if (batch.Count == 0)
                    break;

                var items = await LoadItemsAsync(feed, batch, cancellationToken); // one query per batch, not per row
                foreach (var row in batch)
                    Process(row, items); // writes the result and marks the row Done, or marks it Failed with a reason

                try
                {
                    // The batch's results and its rows' marks: ONE transaction. Kept whole or rolled back whole.
                    await db.SaveChangesAsync(cancellationToken);
                    Count(batch, result);
                }
                catch (DbUpdateException) when (!cancellationToken.IsCancellationRequested)
                {
                    // A row the database refuses would fail this batch on every run: find it, one row at a time.
                    await CommitRowByRowAsync(batch.Select(x => x.ID).ToList(), result, cancellationToken);
                }
            }

            result.Status = SyncRunStatus.Completed;
        }
        catch (Exception e) when (cancellationToken.IsCancellationRequested
                                  && (e is OperationCanceledException or SqlException
                                      || e.InnerException is OperationCanceledException or SqlException))
        {
            // "Operation cancelled by user": only the batch in flight rolled back. The next run starts at the
            // first row still Pending, so nothing has to start again from zero.
            result.Status = SyncRunStatus.Cancelled;
        }

        db.ChangeTracker.Clear();
        result.Remaining = await db.Inbox.CountAsync(
            x => x.Feed == feed && x.Status == SyncRowStatus.Pending,
            CancellationToken.None); // a cancelled run must still be able to say what is left
        return result;
    }
    // endsnippet

    private async Task<Dictionary<string, SyncedItem>> LoadItemsAsync(string feed, List<SyncInboxRow> rows, CancellationToken cancellationToken)
    {
        var codes = rows.Select(x => x.ExternalCode).Distinct().ToList();
        return await db.Items
            .Where(x => x.Feed == feed && codes.Contains(x.ExternalCode))
            .ToDictionaryAsync(x => x.ExternalCode, cancellationToken);
    }

    private void Process(SyncInboxRow row, Dictionary<string, SyncedItem> items)
    {
        if (string.IsNullOrWhiteSpace(row.Name))
        {
            row.Status = SyncRowStatus.Failed; // can never be processed: settled, not retried
            row.Error = "Name is required.";
            return;
        }

        if (!items.TryGetValue(row.ExternalCode, out var item))
        {
            item = new SyncedItem { Feed = row.Feed, ExternalCode = row.ExternalCode };
            db.Items.Add(item);
            items[row.ExternalCode] = item;
        }

        item.Name = row.Name;
        row.Status = SyncRowStatus.Done;
        row.Error = null;
    }

    private async Task CommitRowByRowAsync(List<long> rowIds, SyncResult result, CancellationToken cancellationToken)
    {
        foreach (var id in rowIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            db.ChangeTracker.Clear();
            var row = await db.Inbox.SingleAsync(x => x.ID == id, cancellationToken);
            Process(row, await LoadItemsAsync(row.Feed, [row], cancellationToken));

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (!cancellationToken.IsCancellationRequested)
            {
                db.ChangeTracker.Clear();
                row = await db.Inbox.SingleAsync(x => x.ID == id, cancellationToken);
                row.Status = SyncRowStatus.Failed;
                row.Error = "Rejected by the database: " + (e.InnerException ?? e).Message;
                await db.SaveChangesAsync(cancellationToken);
            }

            Count([row], result);
        }
    }

    private static void Count(IEnumerable<SyncInboxRow> committed, SyncResult result)
    {
        foreach (var row in committed)
        {
            if (row.Status == SyncRowStatus.Done)
                result.ProcessedThisRun++;
            else if (row.Status == SyncRowStatus.Failed)
                result.FailedThisRun++;
        }
    }
}

public static class SyncEndpoints
{
    public static void MapSync(IEndpointRouteBuilder app)
    {
        // snippet:ConcurrencySyncEndpoint
        app.MapPost("api/sync/{feed}", async (string feed, int? maxSeconds, ResumableSync sync, CancellationToken requestAborted) =>
        {
            // A time budget shorter than any gateway or command timeout: the run stops in time and ANSWERS
            // (Status = Cancelled, Remaining = n) instead of being killed. The caller calls again to continue.
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            if (maxSeconds is > 0)
                budget.CancelAfter(TimeSpan.FromSeconds(maxSeconds.Value));

            return Results.Ok(await sync.RunAsync(feed, batchSize: 500, budget.Token));
        }).RequireAuthorization();
        // endsnippet
    }
}
