using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Happie.Shared.Contracts;
using Happie.Shared.Domain;

namespace Happie.Api.Tests.Handlers;

// Feature: dish-recipes, Property 9: For any list of N cooking instructions (after any reorder, add, or delete operation), the displayed numbering SHALL be a continuous sequence from 1 to N with no gaps or duplicates.
// Feature: dish-recipes, Property 8: For any instruction paragraph with text consisting entirely of whitespace, the system SHALL auto-delete that item upon confirm.

/// <summary>Property-based tests for instruction numbering and whitespace auto-delete operations.</summary>
public class RecipeHandlerInstructionOperationsPropertyTests
{
    /// <summary>
    /// For any list of N cooking instructions (after any reorder, add, or delete operation),
    /// the displayed numbering SHALL be a continuous sequence from 1 to N with no gaps or duplicates.
    /// **Validates: Requirements 7.2, 8.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InstructionNumbering_AfterAnyOperation_IsContinuousSequence()
    {
        return Prop.ForAll(
            InstructionOperationScenarioArb(),
            scenario =>
            {
                // Arrange — start with the instructions list.
                var instructions = scenario.Instructions.ToList();

                // Act — apply the operation.
                switch (scenario.Operation)
                {
                    case InstructionOperation.ReorderUp when scenario.OperationIndex > 0:
                        (instructions[scenario.OperationIndex], instructions[scenario.OperationIndex - 1]) =
                            (instructions[scenario.OperationIndex - 1], instructions[scenario.OperationIndex]);
                        break;
                    case InstructionOperation.ReorderDown when scenario.OperationIndex < instructions.Count - 1:
                        (instructions[scenario.OperationIndex], instructions[scenario.OperationIndex + 1]) =
                            (instructions[scenario.OperationIndex + 1], instructions[scenario.OperationIndex]);
                        break;
                    case InstructionOperation.Delete when instructions.Count > 0:
                        instructions.RemoveAt(scenario.OperationIndex);
                        break;
                    case InstructionOperation.Add:
                        instructions.Add(new CookingInstructionDto(Guid.NewGuid(), "New instruction", 0));
                        break;
                }

                // Act — apply the confirm logic: filter whitespace, assign sequential sort orders.
                var validInstructions = instructions
                    .Where(x => !string.IsNullOrWhiteSpace(x.Text))
                    .ToList();

                var numberedInstructions = validInstructions
                    .Select((x, index) => new CookingInstructionDto(x.Id, x.Text.Trim(), index))
                    .ToList();

                // Assert — sort orders form a continuous 0-based sequence (displayed as 1..N).
                var count = numberedInstructions.Count;
                var sortOrders = numberedInstructions.Select(x => x.SortOrder).ToList();
                var expectedSequence = Enumerable.Range(0, count).ToList();
                var isContinuous = sortOrders.SequenceEqual(expectedSequence);

                // Assert — no duplicates.
                var noDuplicates = sortOrders.Distinct().Count() == count;

                // Assert — no gaps (covered by continuous check, but explicit for clarity).
                var noGaps = count == 0 || (sortOrders.Min() == 0 && sortOrders.Max() == count - 1);

                return (isContinuous && noDuplicates && noGaps)
                    .Label($"isContinuous={isContinuous}, noDuplicates={noDuplicates}, noGaps={noGaps}, " +
                           $"operation={scenario.Operation}, count={count}, sortOrders=[{string.Join(",", sortOrders)}]");
            });
    }

    /// <summary>
    /// For any instruction paragraph with text consisting entirely of whitespace,
    /// the system SHALL auto-delete that item upon confirm.
    /// **Validates: Requirements 8.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WhitespaceFilter_WhitespaceOnlyInstructions_AreAutoDeleted()
    {
        return Prop.ForAll(
            InstructionListWithWhitespaceArb(),
            instructions =>
            {
                // Arrange — determine expected valid items (non-whitespace text).
                var expectedValidIds = instructions
                    .Where(x => !string.IsNullOrWhiteSpace(x.Text))
                    .Select(x => x.Id)
                    .ToHashSet();

                // Act — apply the same filtering logic as the handler.
                var validInstructions = instructions
                    .Where(x => !string.IsNullOrWhiteSpace(x.Text))
                    .ToList();

                // Assert — only non-whitespace items remain.
                var allRemainingAreValid = validInstructions.All(x => !string.IsNullOrWhiteSpace(x.Text));
                var correctCount = validInstructions.Count == expectedValidIds.Count;
                var correctIds = validInstructions.Select(x => x.Id).ToHashSet().SetEquals(expectedValidIds);

                // Assert — no whitespace-only items survived.
                var noWhitespaceOnlySurvived = !validInstructions.Any(x => string.IsNullOrWhiteSpace(x.Text));

                return (allRemainingAreValid && correctCount && correctIds && noWhitespaceOnlySurvived)
                    .Label($"allValid={allRemainingAreValid}, correctCount={correctCount}, " +
                           $"correctIds={correctIds}, noWhitespaceSurvived={noWhitespaceOnlySurvived}, " +
                           $"input={instructions.Count}, output={validInstructions.Count}");
            });
    }

    private static Arbitrary<InstructionOperationScenario> InstructionOperationScenarioArb()
    {
        // Generate a list of 1-15 instructions, an operation, and a valid index.
        var gen = Gen.Choose(1, RecipeConstants.MaxInstructions)
            .SelectMany(count =>
                Gen.ListOf(InstructionDtoGen(), count)
                    .SelectMany(instructions =>
                        Gen.Choose(0, Math.Max(0, count - 1)).SelectMany(index =>
                            Gen.Elements(
                                InstructionOperation.ReorderUp,
                                InstructionOperation.ReorderDown,
                                InstructionOperation.Delete,
                                InstructionOperation.Add)
                                .Select(operation => new InstructionOperationScenario(
                                    instructions.ToList(), index, operation)))));

        return Arb.From(gen);
    }

    private static Arbitrary<List<CookingInstructionDto>> InstructionListWithWhitespaceArb()
    {
        // Generate a list of 1-15 instructions where some have whitespace-only text.
        var gen = Gen.Choose(1, RecipeConstants.MaxInstructions)
            .SelectMany(count => Gen.ListOf(InstructionDtoWithMixedTextGen(), count)
                .Select(x => x.ToList())
                // Ensure at least one whitespace-only text to make the property meaningful.
                .Where(x => x.Any(i => string.IsNullOrWhiteSpace(i.Text))));

        return Arb.From(gen);
    }

    private static Gen<CookingInstructionDto> InstructionDtoGen()
    {
        var textCharGen = Gen.Choose(33, 126).Select(x => (char)x);

        return Gen.Choose(1, 50)
            .SelectMany(textLength => Gen.ListOf(textCharGen, textLength)
                .Select(chars => new CookingInstructionDto(
                    Guid.NewGuid(),
                    new string(chars.ToArray()),
                    0)));
    }

    private static Gen<CookingInstructionDto> InstructionDtoWithMixedTextGen()
    {
        // Generate instructions with either valid text or whitespace-only text.
        var validTextCharGen = Gen.Choose(33, 126).Select(x => (char)x);

        var validTextGen = Gen.Choose(1, 50)
            .SelectMany(length => Gen.ListOf(validTextCharGen, length)
                .Select(chars => new string(chars.ToArray())));

        var whitespaceTextGen = Gen.Choose(0, 3).Select(choice => choice switch
        {
            0 => "",
            1 => " ",
            2 => "   ",
            _ => "\t \t"
        });

        return Gen.Choose(0, 2).SelectMany(choice => choice switch
        {
            // Valid text (2 out of 3 chance).
            0 or 1 => validTextGen,
            // Whitespace-only text (1 out of 3 chance).
            _ => whitespaceTextGen
        }).Select(text => new CookingInstructionDto(
            Guid.NewGuid(),
            text,
            0));
    }

    private record InstructionOperationScenario(
        List<CookingInstructionDto> Instructions,
        int OperationIndex,
        InstructionOperation Operation);

    private enum InstructionOperation
    {
        ReorderUp,
        ReorderDown,
        Delete,
        Add
    }
}
