#r "nuget: Azure.Data.Tables, 12.9.0"

using Azure.Data.Tables;

const string ConnectionString = "UseDevelopmentStorage=true";
const string HouseholdId = "00000000-0000-0000-0000-000000000001";

// Seed Households table.
var householdsClient = new TableClient(ConnectionString, "Households");
householdsClient.CreateIfNotExists();

var household = new TableEntity("households", HouseholdId)
{
    // bcrypt hash of "happie" (cost 11).
    ["Name"] = "Test Household",
    ["PasswordHash"] = "$2a$11$qa7dtLgVeLxVbxunMy2n2OIQXU8mZx5K8C4okHgF8LdbejOpXRboi"
};
householdsClient.UpsertEntity(household);
Console.WriteLine($"Upserted household: Test Household ({HouseholdId})");

// Seed Housemates table.
var housematesClient = new TableClient(ConnectionString, "Housemates");
housematesClient.CreateIfNotExists();

var alice = new TableEntity(HouseholdId, "00000000-0000-0000-0000-000000000002")
{
    ["Name"] = "Alice",
    ["Color"] = "#EF5350",
    ["IsDeleted"] = false
};
housematesClient.UpsertEntity(alice);
Console.WriteLine("Upserted housemate: Alice (#EF5350)");

var bob = new TableEntity(HouseholdId, "00000000-0000-0000-0000-000000000003")
{
    ["Name"] = "Bob",
    ["Color"] = "#1E88E5",
    ["IsDeleted"] = false
};
housematesClient.UpsertEntity(bob);
Console.WriteLine("Upserted housemate: Bob (#1E88E5)");

Console.WriteLine();
Console.WriteLine("Done. Login with password: happie");
