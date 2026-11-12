using Xunit;

namespace WebApiTests.Postgres;

[CollectionDefinition("PostgresCollection", DisableParallelization = true)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}
