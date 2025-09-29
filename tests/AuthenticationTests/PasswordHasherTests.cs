using Application.Abstractions.Authentication;
using Infrastructure.Authentication;

namespace AuthenticationTests;

public class PasswordHasherTests
{
    private readonly IPasswordHasher _passwordHasher = new PasswordHasher();

    [Fact]
    public void Hash_ProducesUniqueHashes()
    {
        const string password = "P@ssw0rd";

        string hash1 = _passwordHasher.Hash(password);
        string hash2 = _passwordHasher.Hash(password);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        const string password = "P@ssw0rd";
        string hash = _passwordHasher.Hash(password);

        bool result = _passwordHasher.Verify(password, hash);

        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForIncorrectPassword()
    {
        const string password = "P@ssw0rd";
        string hash = _passwordHasher.Hash(password);

        bool result = _passwordHasher.Verify("wrong", hash);

        result.Should().BeFalse();
    }
}
