namespace Domain.Users;

/// <summary>What an emailed <see cref="UserActionToken"/> authorises (sfc design C16).</summary>
public enum UserActionPurpose
{
    EmailVerification = 0,
    Invitation = 1,
    OwnerTransfer = 2
}
