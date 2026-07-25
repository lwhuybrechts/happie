namespace Happie.Migration;

/// <summary>Tracks the totals for a migration run.</summary>
public class MigrationResult
{
    /// <summary>Number of records successfully migrated to the new format.</summary>
    public int Migrated { get; set; }

    /// <summary>Number of records skipped because the new-format record already existed.</summary>
    public int Skipped { get; set; }

    /// <summary>Number of records that failed during migration.</summary>
    public int Failed { get; set; }
}
