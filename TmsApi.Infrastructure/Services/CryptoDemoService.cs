namespace TmsApi.Infrastructure.Services;

public class CryptoDemoService
{
    // =========================
    // HASH PASSWORD
    // =========================
    public string HashUserPassword(string plainText)
    {
        return BCrypt.Net.BCrypt.HashPassword(
            plainText,
            workFactor: 12);
    }

    // =========================
    // VERIFY PASSWORD
    // =========================
    public bool VerifyUserPassword(
        string plainText,
        string hashedDbPassword)
    {
        return BCrypt.Net.BCrypt.Verify(
            plainText,
            hashedDbPassword);
    }
}