using Application.Users.ChangePassword;
using Application.Users.Commands.CreateUser;
using Application.Users.Register;
using Application.Users.ResetPassword;
using FluentValidation.Results;

namespace ApplicationTests.Users;

/// <summary>
/// CU-13: one password policy (min 10 chars + upper + lower + digit) shared by
/// register, create-user, reset and change-password.
/// </summary>
public class PasswordRulesTests
{
    private const string Strong = "Abcdef1234";

    public static TheoryData<string> WeakPasswords => new()
    {
        "abc12345",     // 8 chars, no uppercase
        "Abcdef123",    // 9 chars
        "abcdefghij1",  // no uppercase
        "ABCDEFGHIJ1",  // no lowercase
        "Abcdefghijk",  // no digit
        "",
    };

    private static ValidationResult Register(string password) =>
        new RegisterUserCommandValidator().Validate(new RegisterUserCommand("u@mail.com", "First", "Last", password));

    private static ValidationResult Create(string password) =>
        new CreateUserValidator().Validate(
            new CreateUserCommand("u@mail.com", password, "First", "Last", null, Guid.NewGuid()));

    private static ValidationResult Reset(string password) =>
        new ResetPasswordCommandValidator().Validate(new ResetPasswordCommand("token", password));

    private static ValidationResult Change(string password) =>
        new ChangePasswordCommandValidator().Validate(new ChangePasswordCommand("Current-Password-1", password));

    [Theory]
    [MemberData(nameof(WeakPasswords))]
    public void Register_ShouldReject_WeakPassword(string password) =>
        Register(password).Errors.Should().Contain(e => e.PropertyName == "Password");

    [Theory]
    [MemberData(nameof(WeakPasswords))]
    public void CreateUser_ShouldReject_WeakPassword(string password) =>
        Create(password).Errors.Should().Contain(e => e.PropertyName == "Password");

    [Theory]
    [MemberData(nameof(WeakPasswords))]
    public void ResetPassword_ShouldReject_WeakPassword(string password) =>
        Reset(password).Errors.Should().Contain(e => e.PropertyName == "NewPassword");

    [Theory]
    [MemberData(nameof(WeakPasswords))]
    public void ChangePassword_ShouldReject_WeakPassword(string password) =>
        Change(password).Errors.Should().Contain(e => e.PropertyName == "NewPassword");

    [Fact]
    public void AllFlows_ShouldAccept_StrongPasswordWithoutSpecialCharacter()
    {
        Register(Strong).IsValid.Should().BeTrue();
        Create(Strong).IsValid.Should().BeTrue();
        Reset(Strong).IsValid.Should().BeTrue();
        Change(Strong).IsValid.Should().BeTrue();
    }
}
