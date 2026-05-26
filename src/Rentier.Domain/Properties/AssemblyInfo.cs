using System.Runtime.CompilerServices;

// Allows the unit test project to access internal members such as Filing's internal
// constructor, which is designed for EF Core hydration and controlled test seeding.
[assembly: InternalsVisibleTo("Rentier.UnitTests")]
