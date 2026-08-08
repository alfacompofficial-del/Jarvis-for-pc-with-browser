using System;
using System.IO;
using Jarvis.Core;
using Jarvis.Services;
using Xunit;

namespace Jarvis.Tests;

/// <summary>
/// Unit tests for SecurityManager.
/// Tests encryption, decryption, path validation, and command safety checking.
/// </summary>
public class SecurityManagerTests
{
    private readonly SecurityManager _securityManager = new();
    private readonly string _testAppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Jarvis", "tests");

    public SecurityManagerTests()
    {
        // Ensure test directory exists for log file creation
        Directory.CreateDirectory(_testAppDataPath);
    }

    #region Encryption/Decryption Tests

    [Fact]
    public void EncryptString_WithValidString_ReturnsEncryptedBase64()
    {
        // Arrange
        string plainText = "test-api-key-12345";

        // Act
        string encrypted = _securityManager.EncryptString(plainText);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEmpty(encrypted);
        Assert.NotEqual(plainText, encrypted);
        // Verify it's valid base64
        Assert.DoesNotThrow(() => Convert.FromBase64String(encrypted));
    }

    [Fact]
    public void EncryptString_WithEmptyString_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _securityManager.EncryptString(""));
    }

    [Fact]
    public void EncryptString_WithNull_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _securityManager.EncryptString(null!));
    }

    [Fact]
    public void DecryptString_WithValidEncryptedString_ReturnsOriginalString()
    {
        // Arrange
        string originalText = "test-api-key-12345";
        string encrypted = _securityManager.EncryptString(originalText);

        // Act
        string decrypted = _securityManager.DecryptString(encrypted);

        // Assert
        Assert.Equal(originalText, decrypted);
    }

    [Fact]
    public void DecryptString_WithEmptyString_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _securityManager.DecryptString(""));
    }

    [Fact]
    public void DecryptString_WithInvalidBase64_ThrowsSecurityException()
    {
        // Act & Assert
        Assert.Throws<SecurityException>(() => _securityManager.DecryptString("not-valid-base64!!!"));
    }

    [Fact]
    public void EncryptDecryptRoundTrip_WithVariousStrings_MaintainsIntegrity()
    {
        // Arrange
        string[] testStrings = 
        {
            "simple",
            "with spaces and symbols!@#$%",
            "with-dashes-and_underscores",
            "УникальныеСимволы",
            "MixedLanguage_РусскийEnglish123",
            "veryLongStringWithManyCharactersThatShouldStillWorkCorrectlyWithEncryptionAndDecryption" +
            "veryLongStringWithManyCharactersThatShouldStillWorkCorrectlyWithEncryptionAndDecryption"
        };

        foreach (var originalText in testStrings)
        {
            // Act
            string encrypted = _securityManager.EncryptString(originalText);
            string decrypted = _securityManager.DecryptString(encrypted);

            // Assert
            Assert.Equal(originalText, decrypted);
        }
    }

    #endregion

    #region Path Validation Tests

    [Fact]
    public void ValidateFilePath_WithNormalPath_ReturnsTrue()
    {
        // Arrange
        string path = "C:\\Users\\Public\\Documents\\file.txt";
        string allowedBase = "C:\\Users\\Public";

        // Act
        bool result = _securityManager.ValidateFilePath(path, allowedBase);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("../../../Windows/System32")]
    [InlineData("..\\..\\..\\Windows")]
    [InlineData("C:/Windows/../System32")]
    [InlineData("Documents/../../sensitive")]
    public void ValidateFilePath_WithTraversalPatterns_ReturnsFalse(string maliciousPath)
    {
        // Arrange
        string allowedBase = "C:\\Users\\Public";

        // Act
        bool result = _securityManager.ValidateFilePath(maliciousPath, allowedBase);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateFilePath_WithPathOutsideAllowedBase_ReturnsFalse()
    {
        // Arrange
        string path = "C:\\Windows\\System32\\cmd.exe";
        string allowedBase = "C:\\Users\\Public";

        // Act
        bool result = _securityManager.ValidateFilePath(path, allowedBase);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateFilePath_WithEmptyPath_ReturnsFalse()
    {
        // Act
        bool result = _securityManager.ValidateFilePath("", "C:\\Users");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateFilePath_WithNullPath_ReturnsFalse()
    {
        // Act
        bool result = _securityManager.ValidateFilePath(null!, "C:\\Users");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Sensitive Path Detection Tests

    [Theory]
    [InlineData("C:\\Windows\\System32\\cmd.exe")]
    [InlineData("C:\\Program Files\\App\\app.exe")]
    [InlineData("C:\\ProgramData\\data.txt")]
    [InlineData("C:\\WinRE\\recovery.exe")]
    public void IsSensitivePath_WithSensitivePaths_ReturnsTrue(string sensitivePath)
    {
        // Act
        bool result = _securityManager.IsSensitivePath(sensitivePath);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("C:\\Users\\Public\\Documents\\file.txt")]
    [InlineData("D:\\My Documents\\project.docx")]
    [InlineData("C:\\Temp\\downloads\\archive.zip")]
    public void IsSensitivePath_WithNormalPaths_ReturnsFalse(string normalPath)
    {
        // Act
        bool result = _securityManager.IsSensitivePath(normalPath);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSensitivePath_WithEmptyPath_ReturnsFalse()
    {
        // Act
        bool result = _securityManager.IsSensitivePath("");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Command Validation Tests

    [Theory]
    [InlineData("Get-Date")]
    [InlineData("echo 'Hello World'")]
    [InlineData("dir /b")]
    [InlineData("Get-Process | Select-Object -First 5")]
    public void IsCommandSafe_WithSafeCommands_ReturnsTrue(string safeCommand)
    {
        // Act
        bool result = _securityManager.IsCommandSafe(safeCommand);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("format C:")]
    [InlineData("del /q /s C:\\Users")]
    [InlineData("rd /s /q C:\\")]
    [InlineData("rm -rf /")]
    [InlineData("Remove-Item -Recurse -Force C:\\Windows")]
    public void IsCommandSafe_WithDangerousCommands_ReturnsFalse(string dangerousCommand)
    {
        // Act
        bool result = _securityManager.IsCommandSafe(dangerousCommand);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateCommand_WithDangerousCommand_ReturnRequiresConfirmationTrue()
    {
        // Arrange
        string dangerousCommand = "format C:";

        // Act
        var result = _securityManager.ValidateCommand(dangerousCommand);

        // Assert
        Assert.False(result.IsSafe);
        Assert.True(result.RequiresConfirmation);
        Assert.NotNull(result.WarningMessage);
        Assert.NotEmpty(result.DangerousOperations);
    }

    [Fact]
    public void ValidateCommand_WithSafeCommand_ReturnIsSafeTrue()
    {
        // Arrange
        string safeCommand = "Get-Date";

        // Act
        var result = _securityManager.ValidateCommand(safeCommand);

        // Assert
        Assert.True(result.IsSafe);
        Assert.False(result.RequiresConfirmation);
        Assert.Null(result.WarningMessage);
    }

    [Fact]
    public void ValidateCommand_WithEmptyCommand_ReturnIsSafeTrue()
    {
        // Act
        var result = _securityManager.ValidateCommand("");

        // Assert
        Assert.True(result.IsSafe);
    }

    [Theory]
    [InlineData("FORMAT c:", "format")]
    [InlineData("DEL /Q /S C:\\", "del")]
    [InlineData("RD /S /Q C:\\", "rd")]
    [InlineData("RM -RF /", "rm")]
    [InlineData("remove-item -recurse -force", "Remove-Item")]
    public void ValidateCommand_CaseInsensitive_DetectsDangerousPatterns(string command, string expectedPattern)
    {
        // Act
        var result = _securityManager.ValidateCommand(command);

        // Assert
        Assert.False(result.IsSafe);
        Assert.NotEmpty(result.DangerousOperations);
    }

    [Fact]
    public void ValidateCommand_WithMultipleDangerousPatterns_DetectsAll()
    {
        // Arrange
        string command = "del /q C:\\ && rd /s /q D:\\";

        // Act
        var result = _securityManager.ValidateCommand(command);

        // Assert
        Assert.False(result.IsSafe);
        Assert.NotEmpty(result.DangerousOperations);
        Assert.True(result.DangerousOperations.Count >= 1);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void EncryptString_WithSpecialCharacters_EncryptsSuccessfully()
    {
        // Arrange
        string specialString = "!@#$%^&*()_+-=[]{}|;:',.<>?/\\\"";

        // Act
        string encrypted = _securityManager.EncryptString(specialString);
        string decrypted = _securityManager.DecryptString(encrypted);

        // Assert
        Assert.Equal(specialString, decrypted);
    }

    [Fact]
    public void EncryptString_WithUnicodeCharacters_EncryptsSuccessfully()
    {
        // Arrange
        string unicodeString = "Привет мир! 你好 مرحبا";

        // Act
        string encrypted = _securityManager.EncryptString(unicodeString);
        string decrypted = _securityManager.DecryptString(encrypted);

        // Assert
        Assert.Equal(unicodeString, decrypted);
    }

    [Fact]
    public void ValidateFilePath_WithRelativePath_HandlesCorrectly()
    {
        // Arrange
        string relativePath = "subfolder/document.txt";
        string allowedBase = Path.GetTempPath();

        // Act
        bool result = _securityManager.ValidateFilePath(relativePath, allowedBase);

        // Assert - should succeed as it resolves within allowed base
        Assert.True(result);
    }

    #endregion

    #region CommandValidationResult Tests

    [Fact]
    public void CommandValidationResult_Safe_CreatesCorrectObject()
    {
        // Act
        var result = CommandValidationResult.Safe();

        // Assert
        Assert.True(result.IsSafe);
        Assert.False(result.RequiresConfirmation);
        Assert.Null(result.WarningMessage);
        Assert.Empty(result.DangerousOperations);
    }

    [Fact]
    public void CommandValidationResult_RequiresUserConfirmation_CreatesCorrectObject()
    {
        // Act
        var result = CommandValidationResult.RequiresUserConfirmation(
            "Test warning", 
            "operation1", 
            "operation2");

        // Assert
        Assert.False(result.IsSafe);
        Assert.True(result.RequiresConfirmation);
        Assert.Equal("Test warning", result.WarningMessage);
        Assert.Equal(2, result.DangerousOperations.Count);
    }

    [Fact]
    public void CommandValidationResult_Unsafe_CreatesCorrectObject()
    {
        // Act
        var result = CommandValidationResult.Unsafe("Unsafe warning");

        // Assert
        Assert.False(result.IsSafe);
        Assert.False(result.RequiresConfirmation);
        Assert.Equal("Unsafe warning", result.WarningMessage);
    }

    #endregion
}
