using Microsoft.AspNetCore.Mvc;
using ReportsApi.Interfaces;
using ReportsApi.Models;

namespace ReportsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _service;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(IReportService service, ILogger<ReportsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new incident report.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReportResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateReportRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var created = await _service.CreateAsync(request, cancellationToken);

            return CreatedAtRoute("GetReportById", new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid payload when creating report");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lists all incident reports.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ReportResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        var reports = await _service.GetAllAsync(cancellationToken);
        return Ok(reports);
    }

    /// <summary>
    /// Lists all incident reports for a given crime genre.
    /// </summary>
    [HttpGet("crime-genre/{crimeGenre}")]
    [ProducesResponseType(typeof(IEnumerable<ReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByCrimeGenreAsync(string crimeGenre, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(crimeGenre))
        {
            return BadRequest(new { error = "The crimeGenre value is required." });
        }

        var reports = await _service.GetByCrimeGenreAsync(crimeGenre, cancellationToken);
        return Ok(reports);
    }

    /// <summary>
    /// Lists all incident reports for a given crime type.
    /// </summary>
    [HttpGet("crime-type/{crimeType}")]
    [ProducesResponseType(typeof(IEnumerable<ReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByCrimeTypeAsync(string crimeType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(crimeType))
        {
            return BadRequest(new { error = "The crimeType value is required." });
        }

        var reports = await _service.GetByCrimeTypeAsync(crimeType, cancellationToken);
        return Ok(reports);
    }

    /// <summary>
    /// Gets an incident report by its identifier.
    /// </summary>
    [HttpGet("{id}", Name = "GetReportById")]
    [ProducesResponseType(typeof(ReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var report = await _service.GetByIdAsync(id, cancellationToken);
        if (report is null)
        {
            return NotFound();
        }

        return Ok(report);
    }

    /// <summary>
    /// Updates an incident report.
    /// </summary>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(ReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] UpdateReportRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var updated = await _service.UpdateAsync(id, request, cancellationToken);
            if (updated is null)
            {
                return NotFound();
            }

            return Ok(updated);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid payload when updating report {ReportId}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when updating report {ReportId}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deletes an incident report by its identifier.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
