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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // snippet:ConcurrencySyncUniqueKey
        // One result per source row, enforced by the database: even a second writer can't create a duplicate.
        modelBuilder.Entity<SyncedItem>().HasIndex(x => new { x.Feed, x.ExternalCode }).IsUnique();
        // endsnippet
    }
}

public enum SyncRunStatus { Completed, Cancelled, AlreadyRunning }

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

        // One run per feed at a time: two runs would read the same Pending rows and write every result twice.
        await using var runLock = await SyncRunLock.TryAcquireAsync(db, feed, cancellationToken);
        if (runLock is null)
        {
            result.Status = SyncRunStatus.AlreadyRunning;
            result.Remaining = await CountPendingAsync(feed);
            return result;
        }

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
                    // Not the caller's token: a cancel mid-write would roll the batch back, and a time budget shorter than
                    // one batch would then lose that batch on every run (the incident again). The run stops at the next
                    // ThrowIfCancellationRequested instead.
                    await db.SaveChangesAsync(CancellationToken.None);
                    Count(batch, result);
                }
                catch (DbUpdateException e) when (PermanentReason(e) is not null)
                {
                    // A row the database refuses would fail this batch on every run: find it, one row at a time.
                    // Any other error (a deadlock, a timeout, a lost connection) is not caught: the batch rolls back,
                    // its rows stay Pending, and the next run tries them again.
                    await CommitRowByRowAsync(batch.Select(x => x.ID).ToList(), result, cancellationToken);
                }
            }

            result.Status = SyncRunStatus.Completed;
        }
        catch (Exception e) when (cancellationToken.IsCancellationRequested
                                  && (e is OperationCanceledException or SqlException
                                      || e.InnerException is OperationCanceledException or SqlException))
        {
            // Stopped between batches or during a read ("Operation cancelled by user" when a read was in flight). No
            // write was cut short, and the next run starts at the first row still Pending.
            result.Status = SyncRunStatus.Cancelled;
        }

        result.Remaining = await CountPendingAsync(feed);
        return result;
    }

    // Only an error the row itself causes settles it as Failed, with a short reason: the result goes back to callers,
    // and SQL Server's own message names tables and columns. Any other error stops the run, so a constraint the row
    // breaks (547) counts as the row's too: left Pending, it would stop every run at the same place.
    private static string? PermanentReason(DbUpdateException e)
        => e.InnerException is SqlException sql ? sql.Number switch
        {
            2627 or 2601 => "Duplicate: another row already has this key.",
            547 => "The row breaks a database constraint.",
            515 => "A required value is missing.",
            8152 or 2628 => "A value is too long for its column.",
            _ => null,
        } : null;
    // endsnippet

    private async Task<int> CountPendingAsync(string feed)
    {
        db.ChangeTracker.Clear();
        return await db.Inbox.CountAsync(
            x => x.Feed == feed && x.Status == SyncRowStatus.Pending,
            CancellationToken.None); // a cancelled run must still be able to say what is left
    }

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
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (DbUpdateException e) when (PermanentReason(e) is { } reason)
            {
                db.ChangeTracker.Clear();
                row = await db.Inbox.SingleAsync(x => x.ID == id, CancellationToken.None);
                row.Status = SyncRowStatus.Failed;
                row.Error = reason;
                await db.SaveChangesAsync(CancellationToken.None);
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

// snippet:ConcurrencySyncRunLock
// A SQL Server application lock held on the run's connection. It works across processes and servers, and the server
// drops it with the connection, so a crashed run can't leave the feed locked.
public sealed class SyncRunLock : IAsyncDisposable
{
    private readonly SyncDb db;
    private readonly string resource;

    private SyncRunLock(SyncDb db, string resource) => (this.db, this.resource) = (db, resource);

    public static async Task<SyncRunLock?> TryAcquireAsync(SyncDb db, string feed, CancellationToken cancellationToken)
    {
        var resource = "sync:" + feed;

        // Keep ONE connection open for the whole run: the lock lives on it, and EF reuses it for every batch.
        await db.Database.OpenConnectionAsync(cancellationToken);

        // Don't wait (timeout 0): if another run holds the feed, this one answers AlreadyRunning at once.
        var code = await CallAsync(db, "sp_getapplock", resource, cancellationToken,
            new SqlParameter("@LockMode", "Exclusive"),
            new SqlParameter("@LockTimeout", System.Data.SqlDbType.Int) { Value = 0 }); // not ("@LockTimeout", 0): that 0 is read as a SqlDbType

        if (code >= 0)
            return new SyncRunLock(db, resource);

        await db.Database.CloseConnectionAsync();
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        await CallAsync(db, "sp_releaseapplock", resource, CancellationToken.None);
        await db.Database.CloseConnectionAsync();
    }

    private static async Task<int> CallAsync(SyncDb db, string procedure, string resource,
        CancellationToken cancellationToken, params SqlParameter[] extra)
    {
        await using var command = (SqlCommand)db.Database.GetDbConnection().CreateCommand();
        command.CommandText = procedure;
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.Parameters.AddWithValue("@Resource", resource);
        command.Parameters.AddWithValue("@LockOwner", "Session");
        command.Parameters.AddRange(extra);
        var returnCode = command.Parameters.Add(new SqlParameter { Direction = System.Data.ParameterDirection.ReturnValue });

        await command.ExecuteNonQueryAsync(cancellationToken);
        return (int)returnCode.Value;
    }
}
// endsnippet

public static class SyncEndpoints
{
    public static void MapSync(IEndpointRouteBuilder app)
    {
        // snippet:ConcurrencySyncEndpoint
        app.MapPost("api/sync/{feed}", async (string feed, int? maxSeconds, ResumableSync sync, CancellationToken requestAborted) =>
        {
            // A time budget shorter than any gateway or command timeout, ALWAYS: a caller that sends none gets the
            // default, and none can ask for more than the maximum. The run stops in time and ANSWERS
            // (Status = Cancelled, Remaining = n) instead of being killed. The caller calls again to continue.
            const int DefaultSeconds = 60, MaxSeconds = 120; // e.g. Azure App Service ends a request after 230 s
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            budget.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(maxSeconds ?? DefaultSeconds, 1, MaxSeconds)));

            return Results.Ok(await sync.RunAsync(feed, batchSize: 500, budget.Token));
        }).RequireAuthorization();
        // endsnippet
    }
}
