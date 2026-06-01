Console.Write("Household name: ");
var householdName = Console.ReadLine()?.Trim();
if (string.IsNullOrWhiteSpace(householdName))
{
    Console.Error.WriteLine("Household name cannot be empty.");
    return 1;
}

Console.Write("Household password: ");
var password = Console.ReadLine()?.Trim();
if (string.IsNullOrWhiteSpace(password))
{
    Console.Error.WriteLine("Password cannot be empty.");
    return 1;
}

Console.Write("Housemate name: ");
var housemateName = Console.ReadLine()?.Trim();
if (string.IsNullOrWhiteSpace(housemateName))
{
    Console.Error.WriteLine("Housemate name cannot be empty.");
    return 1;
}

var householdId = Guid.NewGuid();
var housemateId = Guid.NewGuid();
var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

Console.WriteLine();
Console.WriteLine("=== Households table ===");
Console.WriteLine($"  PartitionKey: households");
Console.WriteLine($"  RowKey:       {householdId}");
Console.WriteLine($"  Name:         {householdName}");
Console.WriteLine($"  PasswordHash: {passwordHash}");
Console.WriteLine();
Console.WriteLine("=== Housemates table ===");
Console.WriteLine($"  PartitionKey: {householdId}");
Console.WriteLine($"  RowKey:       {housemateId}");
Console.WriteLine($"  Name:         {housemateName}");
Console.WriteLine($"  Color:        #FF0000");
Console.WriteLine($"  IsDeleted:    false");
Console.WriteLine($"  SortOrder:    0");

return 0;
