namespace Ascend.Api.Services;

public static class Passwords
{
    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 10);

    public static bool Verify(string password, string stored)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, stored);
        }
        catch
        {
            return false;
        }
    }
}
