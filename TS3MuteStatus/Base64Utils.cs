using System;
using System.Text;

#nullable enable

public static class Base64Utils
{
    public static string Encode(string plainText)
    {
        if (plainText == null)
        {
            throw new ArgumentNullException(nameof(plainText), "Input string cannot be null.");
        }

        byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }

    public static string Decode(string base64Text)
    {
        if (base64Text == null)
        {
            throw new ArgumentNullException(nameof(base64Text), "Input string cannot be null.");
        }

        byte[] base64Bytes = Convert.FromBase64String(base64Text);
        return Encoding.UTF8.GetString(base64Bytes);
    }
}
