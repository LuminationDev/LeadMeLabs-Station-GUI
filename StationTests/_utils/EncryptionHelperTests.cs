using System;
using Xunit;
using LeadMeLabsLibrary;

namespace StationTests._utils;

public class EncryptionHelperTests
{
    /// <summary>
    /// Checks whether the Encrypt method returns a non-null, non-empty string of 
    /// type string when given a valid plainText and passPhrase.
    /// </summary>
    [Fact]
    public void Encrypt_Should_Return_Encrypted_String()
    {
        // Arrange
        string plainText = "Hello, World!";
        string passPhrase = "mySecretKey123";

        // Act
        string encrypted = EncryptionHelper.UnicodeEncrypt(plainText, passPhrase);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEmpty(encrypted);
        Assert.IsType<string>(encrypted);
    }

    /// <summary>
    /// Check how the method handles empty input strings for plainText and passPhrase.
    /// </summary>
    [Fact]
    public void Encrypt_Should_Throw_Exception_For_Empty_Inputs()
    {
        // Arrange
        string emptyPlainText = "";
        string passPhrase = "mySecretKey123";

        // Act + Assert
        Assert.Throws<ArgumentException>(() => EncryptionHelper.UnicodeEncrypt(emptyPlainText, passPhrase));

        string plainText = "Hello, World!";
        string emptyPassPhrase = "";

        // Act + Assert
        Assert.Throws<ArgumentException>(() => EncryptionHelper.UnicodeEncrypt(plainText, emptyPassPhrase));
    }

    /// <summary>
    /// Test how the method handles long input strings for plainText.
    /// </summary>
    [Fact]
    public void Encrypt_Should_Encrypt_Long_PlainText_Correctly()
    {
        // Arrange
        string passPhrase = "mySecretKey123";
        string longPlainText = new string('A', 1024); // 1024 'A's

        // Act
        string encrypted = EncryptionHelper.UnicodeEncrypt(longPlainText, passPhrase);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEmpty(encrypted);
        Assert.IsType<string>(encrypted);

        // Decrypt and verify the original plainText
        string decrypted = EncryptionHelper.UnicodeDecrypt(encrypted, passPhrase);
        Assert.Equal(longPlainText, decrypted);
    }

    /// <summary>
    /// Test how the method handles different passPhrase values. Test different lengths, 
    /// characters, and combinations of passphrases.
    /// </summary>
    [Fact]
    public void Encrypt_Should_Encrypt_Different_PassPhrases_Correctly()
    {
        // Arrange
        string plainText = "Hello, World!";
        string passPhrase1 = "mySecretKey123";
        string passPhrase2 = "anotherSecret456";

        // Act
        string encrypted1 = EncryptionHelper.UnicodeEncrypt(plainText, passPhrase1);
        string encrypted2 = EncryptionHelper.UnicodeEncrypt(plainText, passPhrase2);

        // Assert
        Assert.NotNull(encrypted1);
        Assert.NotEmpty(encrypted1);
        Assert.IsType<string>(encrypted1);

        Assert.NotNull(encrypted2);
        Assert.NotEmpty(encrypted2);
        Assert.IsType<string>(encrypted2);

        // Decrypt and verify the original plainText
        string decrypted1 = EncryptionHelper.UnicodeDecrypt(encrypted1, passPhrase1);
        Assert.Equal(plainText, decrypted1);

        string decrypted2 = EncryptionHelper.UnicodeDecrypt(encrypted2, passPhrase2);
        Assert.Equal(plainText, decrypted2);
    }

    /// <summary>
    /// Checks whether the Decrypt method throws an ArgumentException when the input is null or empty.
    /// </summary>
    /// <param name="cipherText"></param>
    /// <param name="passPhrase"></param>
    [Theory]
    [InlineData("", "test")] // empty cipherText
    [InlineData("test", "")] // empty passPhrase
    [InlineData(null, "test")] // null cipherText
    [InlineData("test", null)] // null passPhrase
    public void Decrypt_Should_Throw_ArgumentException_When_Input_Is_Invalid(string cipherText, string passPhrase)
    {
        // Act and Assert
        Assert.Throws<ArgumentException>(() => EncryptionHelper.UnicodeDecrypt(cipherText, passPhrase));
    }

    /// <summary>
    /// Checks whether the Decrypt method returns the decrypted text when the input is valid. In this 
    /// example, we assume that the input cipherText is a base64-encoded string of an encrypted "secret text" 
    /// using the Encrypt method with the same passPhrase.
    /// </summary>
    [Fact]
    public void Decrypt_Should_Return_Decrypted_Text_When_Input_Is_Valid()
    {
        // Arrange
        string cipherText = "z4T+xvC3t//SZ2+8jPf0tNUIVxL3DxMOa0yvV6/xT9YaLwGiFvQSSyQVaXsJDne1s3rK1B7iIUL7Hl4WTbvjjfW+pJAkbeI14OWvQWHeRzM=";
        string passPhrase = "passphrase";

        // Act
        string decryptedText = EncryptionHelper.UnicodeDecrypt(cipherText, passPhrase);

        // Assert
        Assert.NotNull(decryptedText);
        Assert.NotEmpty(decryptedText);
        Assert.IsType<string>(decryptedText);
        Assert.Equal("secret text_____________________", decryptedText);
    }
}
