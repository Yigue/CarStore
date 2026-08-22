using Xunit;

namespace WebApiTests.Postgres;

[CollectionDefinition("PostgresCollection")]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}
