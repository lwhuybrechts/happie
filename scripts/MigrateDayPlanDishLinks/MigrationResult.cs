namespace MigrateDayPlanDishLinks;

/// <summary>Summary totals reported at the end of the migration run.</summary>
public record MigrationResult(int Migrated, int Skipped, int Failed, int TotalScanned);
