using System.Net;
using System.Linq;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using ReportsApi.Configuration;
using ReportsApi.Interfaces;
using ReportsApi.Models;

namespace ReportsApi.Services;

public class ReportService : IReportService
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosOptions _options;
    private readonly ILogger<ReportService> _logger;
    private Container? _container;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    public ReportService(CosmosClient cosmosClient, IOptions<CosmosOptions> options, ILogger<ReportService> logger)
    {
        _cosmosClient = cosmosClient;
        _options = options.Value;
        _logger = logger;
    }

    private static DateTime NormalizeDateTime(DateTime dateTime) => dateTime.Kind switch
    {
        DateTimeKind.Local => dateTime.ToUniversalTime(),
        DateTimeKind.Utc => dateTime,
        _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
    };

    private async Task<Container> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (_container is not null)
        {
            _logger.LogDebug("Cosmos container already initialized");
            return _container;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_container is null)
            {
                _logger.LogInformation("Initializing Cosmos database {DatabaseId} and container {ContainerId}", _options.DatabaseId, _options.ContainerId);

                try
                {
                    var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_options.DatabaseId, cancellationToken: cancellationToken);
                    var containerResponse = await databaseResponse.Database.CreateContainerIfNotExistsAsync(new ContainerProperties
                    {
                        Id = _options.ContainerId,
                        PartitionKeyPath = "/id"
                    }, cancellationToken: cancellationToken);

                    _container = containerResponse.Container;
                    _logger.LogInformation("Cosmos container {ContainerId} ready", _options.ContainerId);
                }
                catch (CosmosException ex)
                {
                    _logger.LogCritical(ex, "Failed to initialize Cosmos resources {DatabaseId}/{ContainerId}", _options.DatabaseId, _options.ContainerId);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "Unexpected error while initializing Cosmos resources {DatabaseId}/{ContainerId}", _options.DatabaseId, _options.ContainerId);
                    throw;
                }
            }
        }
        finally
        {
            _initializationLock.Release();
        }

        return _container;
    }

    public async Task<ReportResponse> CreateAsync(CreateReportRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating new report");
        var container = await GetContainerAsync(cancellationToken);

        var crimeGenre = request.CrimeGenre ?? throw new ArgumentException("The crimeGenre field is required.", nameof(request.CrimeGenre));
        var crimeType = request.CrimeType ?? throw new ArgumentException("The crimeType field is required.", nameof(request.CrimeType));
        var description = request.Description ?? throw new ArgumentException("The description field is required.", nameof(request.Description));
        var location = request.Location ?? throw new ArgumentException("The location field is required.", nameof(request.Location));
        var crimeDate = request.CrimeDate ?? throw new ArgumentException("The crimeDate field is required.", nameof(request.CrimeDate));
        var resolved = request.Resolved ?? throw new ArgumentException("The resolved field is required.", nameof(request.Resolved));

        ReporterDetails? reporterDetails = null;
        if (request.ReporterDetails is not null)
        {
            var details = request.ReporterDetails;
            reporterDetails = new ReporterDetails
            {
                AgeGroup = details.AgeGroup?.Trim(),
                Ethnicity = details.Ethnicity?.Trim(),
                GenderIdentity = details.GenderIdentity?.Trim(),
                SexualOrientation = details.SexualOrientation?.Trim()
            };
        }

        var report = new Report
        {
            Id = Guid.NewGuid().ToString(),
            CrimeGenre = crimeGenre.Trim(),
            CrimeType = crimeType.Trim(),
            Description = description.Trim(),
            Location = location.Trim(),
            CrimeDate = NormalizeDateTime(crimeDate),
            ReporterDetails = reporterDetails,
            CreatedDate = DateTime.UtcNow,
            Resolved = resolved
        };

        var itemRequestOptions = new ItemRequestOptions
        {
            EnableContentResponseOnWrite = _options.EnableContentResponseOnWrite
        };

        try
        {
            _logger.LogDebug("Persisting report {ReportId} in Cosmos", report.Id);
            await container.CreateItemAsync(report, new PartitionKey(report.PartitionKey), itemRequestOptions, cancellationToken);
            _logger.LogInformation("Report {ReportId} created successfully", report.Id);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to create report in Cosmos DB");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure while creating report {ReportId}", report.Id);
            throw;
        }

        return ReportResponse.FromModel(report);
    }

    public async Task<IReadOnlyCollection<ReportResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching all reports");
        var container = await GetContainerAsync(cancellationToken);
        var results = new List<ReportResponse>();

        var queryIterator = container.GetItemQueryIterator<Report>(requestOptions: new QueryRequestOptions
        {
            MaxBufferedItemCount = 100,
            MaxConcurrency = -1
        });

        while (queryIterator.HasMoreResults)
        {
            FeedResponse<Report> response;
            try
            {
                response = await queryIterator.ReadNextAsync(cancellationToken);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Failed to fetch reports from Cosmos DB");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching all reports");
                throw;
            }

            _logger.LogDebug("Fetched {Count} reports batch", response.Count);
            results.AddRange(response.Resource.Select(ReportResponse.FromModel));
        }

        _logger.LogInformation("Returning {Count} reports", results.Count);
        return results;
    }

    public async Task<IReadOnlyCollection<ReportResponse>> GetByCrimeGenreAsync(string crimeGenre, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(crimeGenre))
        {
            throw new ArgumentException("The crimeGenre value is required.", nameof(crimeGenre));
        }

        _logger.LogInformation("Fetching reports by crime genre {CrimeGenre}", crimeGenre);
        var container = await GetContainerAsync(cancellationToken);
        var results = new List<ReportResponse>();

        var normalizedCrimeGenre = crimeGenre.Trim();

        var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.crimeGenre = @crimeGenre")
            .WithParameter("@crimeGenre", normalizedCrimeGenre);

        var queryIterator = container.GetItemQueryIterator<Report>(
            queryDefinition,
            requestOptions: new QueryRequestOptions
            {
                MaxBufferedItemCount = 100,
                MaxConcurrency = -1
            });

        while (queryIterator.HasMoreResults)
        {
            FeedResponse<Report> response;
            try
            {
                response = await queryIterator.ReadNextAsync(cancellationToken);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Failed to fetch reports by crime genre {CrimeGenre} in Cosmos DB", normalizedCrimeGenre);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching reports by crime genre {CrimeGenre}", normalizedCrimeGenre);
                throw;
            }

            _logger.LogDebug("Fetched {Count} reports batch for genre {CrimeGenre}", response.Count, normalizedCrimeGenre);
            results.AddRange(response.Resource.Select(ReportResponse.FromModel));
        }

        _logger.LogInformation("Returning {Count} reports for crime genre {CrimeGenre}", results.Count, normalizedCrimeGenre);
        return results;
    }

    public async Task<IReadOnlyCollection<ReportResponse>> GetByCrimeTypeAsync(string crimeType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(crimeType))
        {
            throw new ArgumentException("The crimeType value is required.", nameof(crimeType));
        }

        _logger.LogInformation("Fetching reports by crime type {CrimeType}", crimeType);
        var container = await GetContainerAsync(cancellationToken);
        var results = new List<ReportResponse>();

        var normalizedCrimeType = crimeType.Trim();

        var queryDefinition = new QueryDefinition("SELECT * FROM c WHERE c.crimeType = @crimeType")
            .WithParameter("@crimeType", normalizedCrimeType);

        var queryIterator = container.GetItemQueryIterator<Report>(
            queryDefinition,
            requestOptions: new QueryRequestOptions
            {
                MaxBufferedItemCount = 100,
                MaxConcurrency = -1
            });

        while (queryIterator.HasMoreResults)
        {
            FeedResponse<Report> response;
            try
            {
                response = await queryIterator.ReadNextAsync(cancellationToken);
            }
            catch (CosmosException ex)
            {
                _logger.LogError(ex, "Failed to fetch reports by crime type {CrimeType} in Cosmos DB", normalizedCrimeType);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching reports by crime type {CrimeType}", normalizedCrimeType);
                throw;
            }

            _logger.LogDebug("Fetched {Count} reports batch for crime type {CrimeType}", response.Count, normalizedCrimeType);
            results.AddRange(response.Resource.Select(ReportResponse.FromModel));
        }

        _logger.LogInformation("Returning {Count} reports for crime type {CrimeType}", results.Count, normalizedCrimeType);
        return results;
    }

    public async Task<ReportResponse?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("The report id is required.", nameof(id));
        }

        _logger.LogInformation("Fetching report {ReportId}", id);
        var container = await GetContainerAsync(cancellationToken);
        try
        {
            var response = await container.ReadItemAsync<Report>(id, new PartitionKey(id), cancellationToken: cancellationToken);
            _logger.LogDebug("Report {ReportId} found", id);
            return ReportResponse.FromModel(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation("Report {ReportId} not found", id);
            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to fetch report {ReportId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching report {ReportId}", id);
            throw;
        }
    }

    public async Task<ReportResponse?> UpdateAsync(string id, UpdateReportRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("The report id is required.", nameof(id));
        }

        _logger.LogInformation("Updating report {ReportId}", id);
        var container = await GetContainerAsync(cancellationToken);
        Report existing;
        try
        {
            var readResponse = await container.ReadItemAsync<Report>(id, new PartitionKey(id), cancellationToken: cancellationToken);
            existing = readResponse.Resource;
            _logger.LogDebug("Loaded existing report {ReportId} for update", id);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation("Report {ReportId} not found for update", id);
            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to fetch report for update {ReportId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while preparing update for report {ReportId}", id);
            throw;
        }

        var operations = new List<PatchOperation>();

        string? updatedCrimeGenre = null;
        if (!string.IsNullOrWhiteSpace(request.CrimeGenre))
        {
            var normalized = request.CrimeGenre.Trim();
            if (!string.Equals(existing.CrimeGenre, normalized, StringComparison.Ordinal))
            {
                operations.Add(PatchOperation.Set("/crimeGenre", normalized));
                updatedCrimeGenre = normalized;
            }
        }

        string? updatedCrimeType = null;
        if (!string.IsNullOrWhiteSpace(request.CrimeType))
        {
            var normalized = request.CrimeType.Trim();
            if (!string.Equals(existing.CrimeType, normalized, StringComparison.Ordinal))
            {
                operations.Add(PatchOperation.Set("/crimeType", normalized));
                updatedCrimeType = normalized;
            }
        }

        string? updatedDescription = null;
        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            var normalized = request.Description.Trim();
            if (!string.Equals(existing.Description, normalized, StringComparison.Ordinal))
            {
                operations.Add(PatchOperation.Set("/description", normalized));
                updatedDescription = normalized;
            }
        }

        string? updatedLocation = null;
        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            var normalized = request.Location.Trim();
            if (!string.Equals(existing.Location, normalized, StringComparison.Ordinal))
            {
                operations.Add(PatchOperation.Set("/location", normalized));
                updatedLocation = normalized;
            }
        }

        DateTime? updatedCrimeDate = null;
        if (request.CrimeDate.HasValue)
        {
            var normalizedDate = NormalizeDateTime(request.CrimeDate.Value);
            if (existing.CrimeDate != normalizedDate)
            {
                operations.Add(PatchOperation.Set("/crimeDate", normalizedDate));
                updatedCrimeDate = normalizedDate;
            }
        }

        bool? updatedResolved = null;
        if (request.Resolved.HasValue && existing.Resolved != request.Resolved.Value)
        {
            operations.Add(PatchOperation.Set("/resolved", request.Resolved.Value));
            updatedResolved = request.Resolved.Value;
        }

        ReporterDetails? updatedReporterDetails = null;
        if (request.ReporterDetails is not null)
        {
            var detailsRequest = request.ReporterDetails;
            var normalizedDetails = new ReporterDetails
            {
                AgeGroup = detailsRequest.AgeGroup?.Trim(),
                Ethnicity = detailsRequest.Ethnicity?.Trim(),
                GenderIdentity = detailsRequest.GenderIdentity?.Trim(),
                SexualOrientation = detailsRequest.SexualOrientation?.Trim()
            };

            var currentDetails = existing.ReporterDetails;

            static bool EqualsOrdinal(string? left, string? right) => string.Equals(left, right, StringComparison.Ordinal);

            var detailsChanged = currentDetails is null
                ? normalizedDetails.AgeGroup is not null
                    || normalizedDetails.Ethnicity is not null
                    || normalizedDetails.GenderIdentity is not null
                    || normalizedDetails.SexualOrientation is not null
                : !EqualsOrdinal(currentDetails.AgeGroup, normalizedDetails.AgeGroup)
                    || !EqualsOrdinal(currentDetails.Ethnicity, normalizedDetails.Ethnicity)
                    || !EqualsOrdinal(currentDetails.GenderIdentity, normalizedDetails.GenderIdentity)
                    || !EqualsOrdinal(currentDetails.SexualOrientation, normalizedDetails.SexualOrientation);

            if (detailsChanged)
            {
                operations.Add(PatchOperation.Set("/reporterDetails", normalizedDetails));
                updatedReporterDetails = normalizedDetails;
            }
        }

        if (operations.Count == 0)
        {
            return ReportResponse.FromModel(existing);
        }

        PatchItemRequestOptions? requestOptions = null;
        if (_options.EnableContentResponseOnWrite)
        {
            requestOptions = new PatchItemRequestOptions
            {
                EnableContentResponseOnWrite = true
            };
        }

        try
        {
            var response = await container.PatchItemAsync<Report>(
                id,
                new PartitionKey(existing.PartitionKey),
                operations,
                requestOptions,
                cancellationToken: cancellationToken);

            var updatedResource = response.Resource;
            if (updatedResource is null)
            {
                if (updatedCrimeGenre is not null)
                {
                    existing.CrimeGenre = updatedCrimeGenre;
                }

                if (updatedCrimeType is not null)
                {
                    existing.CrimeType = updatedCrimeType;
                }

                if (updatedDescription is not null)
                {
                    existing.Description = updatedDescription;
                }

                if (updatedLocation is not null)
                {
                    existing.Location = updatedLocation;
                }

                if (updatedCrimeDate.HasValue)
                {
                    existing.CrimeDate = updatedCrimeDate.Value;
                }

                if (updatedResolved.HasValue)
                {
                    existing.Resolved = updatedResolved.Value;
                }

                if (updatedReporterDetails is not null)
                {
                    existing.ReporterDetails = updatedReporterDetails;
                }

                updatedResource = existing;
            }

            _logger.LogInformation("Report {ReportId} updated successfully", id);
            return ReportResponse.FromModel(updatedResource);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to update report {ReportId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating report {ReportId}", id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        _logger.LogInformation("Deleting report {ReportId}", id);
        var container = await GetContainerAsync(cancellationToken);

        try
        {
            await container.DeleteItemAsync<Report>(id, new PartitionKey(id), cancellationToken: cancellationToken);
            _logger.LogInformation("Report {ReportId} deleted successfully", id);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInformation("Report {ReportId} not found for deletion", id);
            return false;
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to delete report {ReportId}", id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while deleting report {ReportId}", id);
            throw;
        }
    }
}
