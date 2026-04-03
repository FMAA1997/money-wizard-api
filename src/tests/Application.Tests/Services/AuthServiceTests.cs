using Domain.Abstractions;
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
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_repositoryMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Sync_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var user = new User { ExternalId = "ext-123", Name = "John", Email = "john@test.com", Dob = new DateOnly(1990, 1, 1) };
        _repositoryMock
            .Setup(r => r.GetByExternalId("ext-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _sut.Sync("ext-123");

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Email.Should().Be("john@test.com");
        result.Value.Name.Should().Be("John");
    }

    [Fact]
    public async Task Sync_WhenUserDoesNotExist_ReturnsNotFoundError()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByExternalId("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _sut.Sync("unknown");

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.UserNotFound);
    }

    [Fact]
    public async Task Sync_WhenExternalIdIsEmpty_ReturnsUnauthorizedError()
    {
        // Act
        var result = await _sut.Sync("");

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.MissingExternalId);
    }

    [Fact]
    public async Task Register_HappyPath_CreatesAndReturnsUser()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByExternalId("ext-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _repositoryMock
            .Setup(r => r.ExistsByEmail("john@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.Register("ext-123", "john@test.com", "John", new DateOnly(1990, 1, 1));

        // Assert
        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("John");
        result.Value.Email.Should().Be("john@test.com");
        result.Value.DateOfBirth.Should().Be(new DateOnly(1990, 1, 1));
        _repositoryMock.Verify(r => r.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_DuplicateExternalId_ReturnsConflictError()
    {
        // Arrange
        var existingUser = new User { ExternalId = "ext-123", Name = "John", Email = "john@test.com", Dob = new DateOnly(1990, 1, 1) };
        _repositoryMock
            .Setup(r => r.GetByExternalId("ext-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _sut.Register("ext-123", "other@test.com", "Jane", new DateOnly(1995, 5, 5));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.UserAlreadyExists);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflictError()
    {
        // Arrange
        _repositoryMock
            .Setup(r => r.GetByExternalId("ext-456", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _repositoryMock
            .Setup(r => r.ExistsByEmail("john@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.Register("ext-456", "john@test.com", "Jane", new DateOnly(1995, 5, 5));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.EmailAlreadyInUse);
    }

    [Fact]
    public async Task Register_MissingExternalId_ReturnsUnauthorizedError()
    {
        // Act
        var result = await _sut.Register(null, "john@test.com", "John", new DateOnly(1990, 1, 1));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.MissingExternalId);
    }

    [Fact]
    public async Task Register_MissingEmail_ReturnsUnauthorizedError()
    {
        // Act
        var result = await _sut.Register("ext-123", null, "John", new DateOnly(1990, 1, 1));

        // Assert
        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.MissingEmail);
    }
}
