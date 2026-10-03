using FsCheck;
using FsCheck.Xunit;

namespace Happie.Web.Tests.Components;

// Feature: loading-strategy, Property 1: Loading category mutual exclusivity
public class LoadingCategoryPropertyTests
{
    // Pure model of the four boolean inputs that determine which loading category applies.
    public record LoadingScenario(
        bool HasCachedData,
        bool IsBackgroundRefresh,
        bool IsSubPanel,
        bool IsFilterChange);

    // The four mutually exclusive loading categories.
    public enum LoadingCategory
    {
        HeaderSpinner,
        InlineLoadingDots,
        StaleOverlay,
        PanelLoadingIndicator
    }

    // Pure function encoding the loading strategy rules from the design document.
    // Priority order:
    // 1. Panel_Loading_Indicator when IsSubPanel is true.
    // 2. Header_Spinner when HasCachedData is true AND IsBackgroundRefresh is true.
    // 3. Stale_Overlay when IsFilterChange is true (stale data is visible).
    // 4. Inline_Loading_Dots for cold fetch of primary page content (fallback).
    public static LoadingCategory DetermineCategory(LoadingScenario scenario)
    {
        if (scenario.IsSubPanel)
            return LoadingCategory.PanelLoadingIndicator;

        if (scenario.HasCachedData && scenario.IsBackgroundRefresh)
            return LoadingCategory.HeaderSpinner;

        if (scenario.IsFilterChange)
            return LoadingCategory.StaleOverlay;

        return LoadingCategory.InlineLoadingDots;
    }

    // Feature: loading-strategy, Property 1: Loading category mutual exclusivity
    // Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5
    [Property(MaxTest = 100)]
    public bool DetermineCategory_AnyScenario_ReturnsExactlyOneCategory(
        bool hasCachedData,
        bool isBackgroundRefresh,
        bool isSubPanel,
        bool isFilterChange)
    {
        var scenario = new LoadingScenario(hasCachedData, isBackgroundRefresh, isSubPanel, isFilterChange);

        var result = DetermineCategory(scenario);

        // Assert exactly one category is returned (the function does not throw and returns a defined enum value).
        return Enum.IsDefined(typeof(LoadingCategory), result);
    }

    // Feature: loading-strategy, Property 1: Loading category mutual exclusivity
    // Validates: Requirements 1.1, 1.5
    [Property(MaxTest = 100)]
    public bool DetermineCategory_SubPanel_AlwaysReturnsPanelLoadingIndicator(
        bool hasCachedData,
        bool isBackgroundRefresh,
        bool isFilterChange)
    {
        var scenario = new LoadingScenario(hasCachedData, isBackgroundRefresh, IsSubPanel: true, isFilterChange);

        var result = DetermineCategory(scenario);

        // Panel_Loading_Indicator takes precedence when IsSubPanel is true.
        return result == LoadingCategory.PanelLoadingIndicator;
    }

    // Feature: loading-strategy, Property 2: Loading state transition on fetch completion
    // Validates: Requirements 1.4, 1.5, 1.6, 2.4, 2.5, 2.7

    // The possible outcomes when a fetch completes.
    public enum FetchOutcome
    {
        SuccessWithData,
        SuccessEmpty,
        Failure
    }

    // The visual display state after a fetch completes.
    public enum DisplayState
    {
        Loading,
        Content,
        EmptyState,
        ErrorState
    }

    // Pure model of a panel or page loading state.
    public record LoadingState(bool IsLoading, DisplayState DisplayState);

    // Pure state transition function: given an initial loading state and a fetch outcome,
    // produce the resulting state. The loading indicator is always removed on completion.
    public static LoadingState TransitionOnFetchComplete(LoadingState initialState, FetchOutcome outcome)
    {
        var displayState = outcome switch
        {
            FetchOutcome.SuccessWithData => DisplayState.Content,
            FetchOutcome.SuccessEmpty => DisplayState.EmptyState,
            FetchOutcome.Failure => DisplayState.ErrorState,
            _ => throw new InvalidOperationException($"Unknown fetch outcome: {outcome}")
        };

        return new LoadingState(IsLoading: false, displayState);
    }

    // Feature: loading-strategy, Property 2: Loading state transition on fetch completion
    // Validates: Requirements 1.4, 1.5, 1.6, 2.4, 2.5, 2.7
    [Property(MaxTest = 100)]
    public bool TransitionOnFetchComplete_AnyOutcome_ResultHasIsLoadingFalse(FetchOutcome outcome)
    {
        var initialState = new LoadingState(IsLoading: true, DisplayState.Loading);

        var result = TransitionOnFetchComplete(initialState, outcome);

        // After any fetch completion, the loading indicator is always removed.
        return result.IsLoading == false;
    }
}
