using Happie.Api.Infrastructure.Repositories;
using Happie.Api.Models;
using Happie.Shared.Domain;

namespace Happie.Api.Handlers;

/// <summary>Handles housemate management operations.</summary>
public class HousemateHandler : IHousemateHandler
{
    private readonly IHousemateRepository _housemateRepository;
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly ICommentRepository _commentRepository;

    /// <summary>Initializes a new instance of <see cref="HousemateHandler"/>.</summary>
    public HousemateHandler(
        IHousemateRepository housemateRepository,
        IAttendanceRepository attendanceRepository,
        ICommentRepository commentRepository)
    {
        _housemateRepository = housemateRepository;
        _attendanceRepository = attendanceRepository;
        _commentRepository = commentRepository;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<HousemateDto>> GetActiveHousematesAsync(Guid householdId, CancellationToken ct = default)
    {
        var housemates = await _housemateRepository.GetAllAsync(householdId, ct);

        return housemates
            .Where(x => !x.IsDeleted)
            .Select(x => new HousemateDto(x.Id, x.Name, x.Color))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<HousemateDto?> AddHousemateAsync(Guid householdId, string name, CancellationToken ct = default)
    {
        var trimmed = name.Trim();

        if (trimmed.Length == 0 || trimmed.Length > 50)
            return null;

        var existing = await _housemateRepository.GetAllAsync(householdId, ct);
        var usedColors = existing
            .Where(x => !x.IsDeleted)
            .Select(x => x.Color)
            .ToHashSet();

        var color = HousemateColors.Palette.FirstOrDefault(x => !usedColors.Contains(x))
            ?? HousemateColors.Palette[0];

        var housemate = new Housemate(Guid.NewGuid(), householdId, trimmed, color, false);

        await _housemateRepository.UpsertAsync(housemate, ct);

        return new HousemateDto(housemate.Id, housemate.Name, housemate.Color);
    }

    /// <inheritdoc/>
    public async Task<UpdateHousemateResult> UpdateHousemateAsync(Guid householdId, Guid housemateId, string? name, string? color, CancellationToken ct = default)
    {
        // Validate that at least one field is being updated.
        if (name is null && color is null)
            return new UpdateHousemateResult(UpdateHousemateOutcome.ValidationError, ErrorMessage: "At least one of name or color must be provided.");

        // Validate name if provided.
        string? trimmedName = null;
        if (name is not null)
        {
            trimmedName = name.Trim();
            if (trimmedName.Length == 0 || trimmedName.Length > 50)
                return new UpdateHousemateResult(UpdateHousemateOutcome.ValidationError, ErrorMessage: "Name must be between 1 and 50 characters.");
        }

        // Validate color if provided.
        if (color is not null && !HousemateColors.Palette.Contains(color))
            return new UpdateHousemateResult(UpdateHousemateOutcome.ValidationError, ErrorMessage: "Color must be a value from the predefined palette.");

        // Fetch the housemate to update.
        var housemate = await _housemateRepository.GetAsync(householdId, housemateId, ct);

        if (housemate is null || housemate.IsDeleted)
            return new UpdateHousemateResult(UpdateHousemateOutcome.NotFound);

        // Check color uniqueness if a new color is requested.
        if (color is not null && color != housemate.Color)
        {
            var allHousemates = await _housemateRepository.GetAllAsync(householdId, ct);
            var colorInUse = allHousemates
                .Any(x => !x.IsDeleted && x.Id != housemateId && x.Color == color);

            if (colorInUse)
                return new UpdateHousemateResult(UpdateHousemateOutcome.ColorConflict, ErrorMessage: "This color is already in use by another housemate.");
        }

        // Apply updates.
        var updatedName = trimmedName ?? housemate.Name;
        var updatedColor = color ?? housemate.Color;
        var updated = housemate with { Name = updatedName, Color = updatedColor };

        await _housemateRepository.UpsertAsync(updated, ct);

        return new UpdateHousemateResult(UpdateHousemateOutcome.Success, Housemate: new HousemateDto(updated.Id, updated.Name, updated.Color));
    }

    /// <inheritdoc/>
    public async Task<DeleteHousemateOutcome> DeleteHousemateAsync(Guid householdId, Guid housemateId, CancellationToken ct = default)
    {
        var housemate = await _housemateRepository.GetAsync(householdId, housemateId, ct);

        if (housemate is null || housemate.IsDeleted)
            return DeleteHousemateOutcome.NotFound;

        // Check for linked attendance records or comments to decide between hard and soft delete.
        var attendanceRecords = await _attendanceRepository.GetAllByHouseholdAsync(householdId, ct);
        var hasAttendance = attendanceRecords.Any(x => x.HousemateId == housemateId);

        if (!hasAttendance)
        {
            var comments = await _commentRepository.GetAllByHouseholdAsync(householdId, ct);
            var hasComments = comments.Any(x => x.HousemateId == housemateId);

            if (!hasComments)
            {
                // No linked records — hard delete.
                await _housemateRepository.DeleteAsync(householdId, housemateId, ct);
                return DeleteHousemateOutcome.Success;
            }
        }

        // At least one linked record exists — soft delete.
        var softDeleted = housemate with { IsDeleted = true };
        await _housemateRepository.UpsertAsync(softDeleted, ct);

        return DeleteHousemateOutcome.Success;
    }
}
