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

// Seed SavedDishes table.
var savedDishesClient = new TableClient(ConnectionString, "SavedDishes");
savedDishesClient.CreateIfNotExists();

var savedDish1 = new TableEntity(HouseholdId, "00000000-0000-0000-0000-000000000010")
{
    ["Description"] = "Pasta",
    ["IsDeleted"] = false
};
savedDishesClient.UpsertEntity(savedDish1);
Console.WriteLine("Upserted saved dish: Pasta");

var savedDish2 = new TableEntity(HouseholdId, "00000000-0000-0000-0000-000000000011")
{
    ["Description"] = "Pizza",
    ["IsDeleted"] = false
};
savedDishesClient.UpsertEntity(savedDish2);
Console.WriteLine("Upserted saved dish: Pizza");

var savedDish3 = new TableEntity(HouseholdId, "00000000-0000-0000-0000-000000000012")
{
    ["Description"] = "Stamppot",
    ["IsDeleted"] = false
};
savedDishesClient.UpsertEntity(savedDish3);
Console.WriteLine("Upserted saved dish: Stamppot");

// Seed DayPlanDishLinks table with some dishes on recent dates.
var linksClient = new TableClient(ConnectionString, "DayPlanDishLinks");
linksClient.CreateIfNotExists();

const string PastaId = "00000000-0000-0000-0000-000000000010";
const string PizzaId = "00000000-0000-0000-0000-000000000011";
const string StamppotId = "00000000-0000-0000-0000-000000000012";
const string AliceId = "00000000-0000-0000-0000-000000000002";
const string BobId = "00000000-0000-0000-0000-000000000003";

// Generate dates: today minus 1..30 days.
var today = DateTime.Today;
var dates = Enumerable.Range(1, 30).Select(x => today.AddDays(-x).ToString("yyyy-MM-dd")).ToList();

// Link dishes to days: Pasta on days 1-12, Pizza on days 5-20, Stamppot on days 15-28.
void SeedLink(string date, string dishId, int sortOrder)
{
    var entity = new TableEntity(HouseholdId, $"{date}_{dishId}") { ["SortOrder"] = sortOrder };
    linksClient.UpsertEntity(entity);
}

for (int i = 0; i < 12; i++) SeedLink(dates[i], PastaId, 0);
for (int i = 4; i < 20; i++) SeedLink(dates[i], PizzaId, i < 12 ? 1 : 0);
for (int i = 14; i < 28; i++) SeedLink(dates[i], StamppotId, i < 20 ? 1 : 0);
Console.WriteLine("Seeded DayPlanDishLinks (Pasta: 12 days, Pizza: 16 days, Stamppot: 14 days)");

// Seed AttendanceRecords table with attendance and chef assignments.
var attendanceClient = new TableClient(ConnectionString, "AttendanceRecords");
attendanceClient.CreateIfNotExists();

void SeedAttendance(string date, string housemateId, string status, bool isChef)
{
    var entity = new TableEntity(HouseholdId, $"{date}_{housemateId}")
    {
        ["HousemateId"] = Guid.Parse(housemateId),
        ["Status"] = status,
        ["IsChef"] = isChef,
        ["LastModified"] = DateTimeOffset.UtcNow
    };
    attendanceClient.UpsertEntity(entity);
}

// Alice: eating in most days (1-25), chef on days 1-8 and 15-20.
for (int i = 0; i < 25; i++)
{
    bool isChef = i < 8 || (i >= 14 && i < 20);
    SeedAttendance(dates[i], AliceId, "EatingIn", isChef);
}
// Alice not eating in on days 26-28.
for (int i = 25; i < 28; i++)
    SeedAttendance(dates[i], AliceId, "NotEatingIn", false);

// Bob: eating in on days 1-20, not eating in 21-30. Chef on days 3-6, 9-14, 22-24.
for (int i = 0; i < 20; i++)
{
    bool isChef = (i >= 2 && i < 6) || (i >= 8 && i < 14);
    SeedAttendance(dates[i], BobId, "EatingIn", isChef);
}
for (int i = 20; i < 30; i++)
{
    bool isChef = i >= 21 && i < 24;
    SeedAttendance(dates[i], BobId, "NotEatingIn", isChef);
}

Console.WriteLine("Seeded AttendanceRecords (Alice: 14 chef days, Bob: 13 chef days)");

Console.WriteLine();
Console.WriteLine("Done. Login with password: happie");
