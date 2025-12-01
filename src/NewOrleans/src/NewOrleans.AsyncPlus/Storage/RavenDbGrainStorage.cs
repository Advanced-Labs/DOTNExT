using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Runtime;
using Orleans.Storage;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Session;
using Raven.Client.Exceptions;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

namespace NewOrleans.AsyncPlus.Storage;

/// <summary>
/// RavenDB-based grain storage provider for Orleans.
/// Provides durable persistence for grain state using RavenDB.
/// </summary>
public class RavenDbGrainStorage : IGrainStorage, ILifecycleParticipant<ISiloLifecycle>, IDisposable
{
    private readonly string _name;
    private readonly string _serviceId;
    private readonly RavenDbStorageOptions _options;
    private readonly IGrainStorageSerializer _grainStorageSerializer;
    private readonly ILogger<RavenDbGrainStorage> _logger;
    private IDocumentStore? _documentStore;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of RavenDbGrainStorage.
    /// </summary>
    public RavenDbGrainStorage(
        string name,
        RavenDbStorageOptions options,
        IGrainStorageSerializer grainStorageSerializer,
        IOptions<ClusterOptions> clusterOptions,
        ILogger<RavenDbGrainStorage> logger)
    {
        _name = name;
        _options = options;
        _grainStorageSerializer = options.GrainStorageSerializer ?? grainStorageSerializer;
        _serviceId = clusterOptions.Value.ServiceId;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Participate(ISiloLifecycle lifecycle)
    {
        var name = $"RavenDbGrainStorage-{_name}";
        lifecycle.Subscribe(name, _options.InitStage, Init, Close);
    }

    private async Task Init(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Initializing RavenDB grain storage '{Name}' for service '{ServiceId}'",
                _name, _serviceId);

            // Configure certificate if provided (for secured RavenDB connections)
            // Certificate is init-only in RavenDB 6.x, so must be set in initializer
            System.Security.Cryptography.X509Certificates.X509Certificate2? certificate = null;
            if (!string.IsNullOrEmpty(_options.CertificatePath))
            {
                var certFlags = System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.EphemeralKeySet;
                certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                    _options.CertificatePath,
                    _options.CertificatePassword,
                    certFlags);
            }

            _documentStore = new DocumentStore
            {
                Urls = _options.Urls,
                Database = _options.DatabaseName,
                Certificate = certificate
            };

            _documentStore.Initialize();

            // Create database if needed
            if (_options.CreateDatabaseIfNotExists)
            {
                await EnsureDatabaseExistsAsync(cancellationToken);
            }

            _logger.LogInformation(
                "RavenDB grain storage '{Name}' initialized in {ElapsedMs}ms. Database: {Database}, URLs: {Urls}",
                _name, sw.ElapsedMilliseconds, _options.DatabaseName, string.Join(", ", _options.Urls));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to initialize RavenDB grain storage '{Name}' after {ElapsedMs}ms",
                _name, sw.ElapsedMilliseconds);
            throw;
        }
    }

    private async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Check if database exists
            var operation = new GetDatabaseRecordOperation(_options.DatabaseName);
            var record = await _documentStore!.Maintenance.Server.SendAsync(operation, cancellationToken);

            if (record == null)
            {
                _logger.LogInformation("Creating RavenDB database '{Database}'", _options.DatabaseName);

                var createOp = new CreateDatabaseOperation(new DatabaseRecord(_options.DatabaseName));
                await _documentStore.Maintenance.Server.SendAsync(createOp, cancellationToken);
            }
        }
        catch (ConcurrencyException)
        {
            // Database already exists (race condition)
            _logger.LogDebug("Database '{Database}' already exists", _options.DatabaseName);
        }
    }

    private Task Close(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        EnsureInitialized();

        var documentId = GetDocumentId(stateName, grainId);

        try
        {
            using var session = _documentStore!.OpenAsyncSession();

            var doc = await session.LoadAsync<GrainStateDocument>(documentId);

            if (doc == null)
            {
                // Document doesn't exist
                _logger.LogDebug("[DEBUG] ReadState: Document NOT FOUND for {GrainId}, using default state", grainId);
                grainState.State = Activator.CreateInstance<T>();
                grainState.ETag = null;
                grainState.RecordExists = false;
                return;
            }

            if (doc.StateData == null)
            {
                // Document exists but StateData is null (was cleared)
                _logger.LogDebug("[DEBUG] ReadState: Document EXISTS but StateData is NULL for {GrainId}, using default state", grainId);
                grainState.State = Activator.CreateInstance<T>();
                grainState.ETag = session.Advanced.GetChangeVectorFor(doc);
                grainState.RecordExists = false;
                return;
            }

            // Deserialize state
            var stateData = new BinaryData(doc.StateData);
            grainState.State = _grainStorageSerializer.Deserialize<T>(stateData);
            grainState.ETag = session.Advanced.GetChangeVectorFor(doc);
            grainState.RecordExists = true;

            _logger.LogDebug("[DEBUG] ReadState: Document FOUND with {ByteCount} bytes StateData for {GrainId}", doc.StateData.Length, grainId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read state for grain {GrainId}", grainId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        EnsureInitialized();

        var documentId = GetDocumentId(stateName, grainId);

        // Debug: Log what we're about to write and where from
        if (grainState.State is AsyncStatePersistenceGrainState asyncState)
        {
            var stackTrace = new System.Diagnostics.StackTrace(true);
            var callerInfo = stackTrace.ToString().Split('\n').Take(8).Aggregate((a, b) => a + " | " + b);
            _logger.LogDebug(
                "[DEBUG] WriteState: GrainId={GrainId}, StateNumber={StateNumber}, IsCompleted={IsCompleted}, IsFaulted={IsFaulted}, HasSerializedData={HasData}, CalledFrom={CallerInfo}",
                grainId, asyncState.StateNumber, asyncState.IsCompleted, asyncState.IsFaulted, asyncState.SerializedStateMachine != null, callerInfo);
        }

        try
        {
            using var session = _documentStore!.OpenAsyncSession();

            // Serialize state
            var stateData = _grainStorageSerializer.Serialize(grainState.State);

            var doc = new GrainStateDocument
            {
                Id = documentId,
                GrainType = stateName,
                GrainId = grainId.ToString(),
                StateData = stateData.ToArray(),
                ServiceId = _serviceId,
                LastModifiedUtc = DateTime.UtcNow
            };

            // Use optimistic concurrency if we have an ETag
            if (!string.IsNullOrEmpty(grainState.ETag))
            {
                session.Advanced.UseOptimisticConcurrency = true;
                await session.StoreAsync(doc, grainState.ETag, documentId);
            }
            else
            {
                await session.StoreAsync(doc, documentId);
            }

            await session.SaveChangesAsync();

            // Update ETag with new change vector
            grainState.ETag = session.Advanced.GetChangeVectorFor(doc);
            grainState.RecordExists = true;

            _logger.LogDebug("Wrote state for grain {GrainId}, document {DocumentId}", grainId, documentId);
        }
        catch (ConcurrencyException ex)
        {
            throw new InconsistentStateException(
                $"Concurrency conflict writing grain state: {ex.Message}",
                grainState.ETag ?? "null",
                ex.ActualChangeVector ?? "unknown",
                ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write state for grain {GrainId}", grainId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ClearStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        EnsureInitialized();

        var documentId = GetDocumentId(stateName, grainId);

        try
        {
            using var session = _documentStore!.OpenAsyncSession();

            if (_options.DeleteStateOnClear)
            {
                _logger.LogDebug("[DEBUG] ClearState: DELETING document {DocumentId} for {GrainId}", documentId, grainId);
                session.Delete(documentId);
            }
            else
            {
                // Mark as cleared but keep document
                var doc = await session.LoadAsync<GrainStateDocument>(documentId);
                if (doc != null)
                {
                    _logger.LogDebug("[DEBUG] ClearState: Setting StateData=NULL for {DocumentId} (document exists)", documentId);
                    doc.StateData = null;
                    doc.LastModifiedUtc = DateTime.UtcNow;
                }
                else
                {
                    _logger.LogDebug("[DEBUG] ClearState: Document {DocumentId} does not exist, nothing to clear", documentId);
                }
            }

            await session.SaveChangesAsync();

            grainState.ETag = null;
            grainState.RecordExists = false;

            _logger.LogDebug("Cleared state for grain {GrainId}, document {DocumentId}", grainId, documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear state for grain {GrainId}", grainId);
            throw;
        }
    }

    private string GetDocumentId(string stateName, GrainId grainId)
    {
        // Create a document ID that's unique per service, state name, and grain
        // Format: orleans/{serviceId}/grains/{stateName}/{grainIdKey}
        var grainIdKey = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(grainId.ToString()))
            .Replace('/', '_')
            .Replace('+', '-');

        return $"orleans/{_serviceId}/grains/{stateName}/{grainIdKey}";
    }

    private void EnsureInitialized()
    {
        if (_documentStore == null)
        {
            throw new InvalidOperationException(
                $"RavenDB grain storage '{_name}' has not been initialized");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        _documentStore?.Dispose();
        _disposed = true;
    }
}
