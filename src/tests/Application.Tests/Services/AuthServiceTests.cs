using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using Application.Services;
using FluentAssertions;
using Moq;

namespace Application.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_repositoryMock.Object);
    }

    [Fact]
    public async Task SyncUserAsync_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var user = new User { ExternalId = "ext-123", Name = "John", Email = "john@test.com", Dob = new DateOnly(1990, 1, 1) };
        _repositoryMock
            .Setup(r => r.GetByExternalIdAsync("ext-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.SyncUserAsync("ext-123");

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Email.Should().Be("john@test.com");
        result.Value.Name.Should().Be("John");
    }

    [Fact]
    public async Task SyncUserAsync_WhenUserDoesNotExist_ReturnsNotFoundError()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByExternalIdAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.SyncUserAsync("unknown");

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.UserNotFound);
    }

    [Fact]
    public async Task SyncUserAsync_WhenExternalIdIsEmpty_ReturnsUnauthorizedError()
    {
        // Act
        var result = await _sut.SyncUserAsync("");

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.MissingExternalId);
    }

    [Fact]
    public async Task RegisterUserAsync_HappyPath_CreatesAndReturnsUser()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByExternalIdAsync("ext-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync("john@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.RegisterUserAsync("ext-123", "john@test.com", "John", new DateOnly(1990, 1, 1));

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("John");
        result.Value.Email.Should().Be("john@test.com");
        result.Value.DateOfBirth.Should().Be(new DateOnly(1990, 1, 1));
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterUserAsync_DuplicateExternalId_ReturnsConflictError()
    {
        // Arrange
        var existingUser = new User { ExternalId = "ext-123", Name = "John", Email = "john@test.com", Dob = new DateOnly(1990, 1, 1) };
        _repositoryMock
            .Setup(r => r.GetByExternalIdAsync("ext-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _sut.RegisterUserAsync("ext-123", "other@test.com", "Jane", new DateOnly(1995, 5, 5));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.UserAlreadyExists);
    }

    [Fact]
    public async Task RegisterUserAsync_DuplicateEmail_ReturnsConflictError()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByExternalIdAsync("ext-456", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync("john@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.RegisterUserAsync("ext-456", "john@test.com", "Jane", new DateOnly(1995, 5, 5));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.EmailAlreadyInUse);
    }

    [Fact]
    public async Task RegisterUserAsync_MissingExternalId_ReturnsUnauthorizedError()
    {
        // Act
        var result = await _sut.RegisterUserAsync(null, "john@test.com", "John", new DateOnly(1990, 1, 1));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.MissingExternalId);
    }

    [Fact]
    public async Task RegisterUserAsync_MissingEmail_ReturnsUnauthorizedError()
    {
        // Act
        var result = await _sut.RegisterUserAsync("ext-123", null, "John", new DateOnly(1990, 1, 1));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.MissingEmail);
    }
}
