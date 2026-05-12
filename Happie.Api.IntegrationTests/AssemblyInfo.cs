using Xunit;

// Disable parallel test execution for integration tests because they share Azure Table Storage tables.
// Running tests in parallel causes cross-test contamination when multiple test class constructors
// truncate and write to the same tables simultaneously.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
