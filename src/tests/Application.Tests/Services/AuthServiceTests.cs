using Application.Abstractions;
using Application.Abstractions.Services;
using Application.DTOs.Auth;
using Application.DTOs.Profile;
using Domain.Abstractions;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using Application.Services;
using ErrorOr;
using FluentAssertions;
using Moq;

namespace Application.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<ICurrentUserProvider> _currentUserProviderMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<ICountryProfileRegistry> _registryMock = new();
    private readonly Mock<ICountryProfileHandler> _handlerMock = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var handler = _handlerMock.Object;
        _registryMock
            .Setup(r => r.TryGet("ar", out handler!))
            .Returns(true);
        _registryMock
            .Setup(r => r.TryGet("row", out handler!))
            .Returns(true);

        _handlerMock
            .Setup(h => h.Upsert(It.IsAny<Guid>(), It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);
        _handlerMock
            .Setup(h => h.LoadResponseSlice(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((object?)null);

        var assembler = new UserResponseAssembler(_registryMock.Object);

        _sut = new AuthService(
            _repositoryMock.Object,
            _currentUserProviderMock.Object,
            _registryMock.Object,
            _unitOfWorkMock.Object,
            assembler);
    }

    private static RegisterRequest BuildRequest(
        string name = "John",
        DateOnly? dob = null,
        string country = "row",
        ArgentinaProfilePayload? argentina = null)
        => new(name, dob ?? new DateOnly(1990, 1, 1), country, argentina);

    [Fact]
    public async Task Sync_WhenUserExists_ReturnsUser()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, ExternalId = "ext-123", Name = "John", Email = "john@test.com", Dob = new DateOnly(1990, 1, 1), Country = "row" };
        _currentUserProviderMock.Setup(p => p.UserId).Returns(userId);
        _repositoryMock
            .Setup(r => r.GetById(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _sut.Sync();

        result.IsError.Should().BeFalse();
        result.Value.Email.Should().Be("john@test.com");
        result.Value.Name.Should().Be("John");
    }

    [Fact]
    public async Task Sync_WhenUserDoesNotExist_ReturnsNotFoundError()
    {
        var userId = Guid.NewGuid();
        _currentUserProviderMock.Setup(p => p.UserId).Returns(userId);
        _repositoryMock
            .Setup(r => r.GetById(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await _sut.Sync();

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.UserNotFound);
    }

    [Fact]
    public async Task Register_HappyPath_CreatesAndReturnsUser()
    {
        _repositoryMock
            .Setup(r => r.GetByExternalId("ext-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _repositoryMock
            .Setup(r => r.ExistsByEmail("john@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.Register("ext-123", "john@test.com", BuildRequest());

        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("John");
        result.Value.Email.Should().Be("john@test.com");
        result.Value.DateOfBirth.Should().Be(new DateOnly(1990, 1, 1));
        result.Value.Country.Should().Be("row");
        _repositoryMock.Verify(r => r.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_ArgentinaHappyPath_InvokesCountryHandlerWithUserId()
    {
        _repositoryMock
            .Setup(r => r.GetByExternalId("ext-ar", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _repositoryMock
            .Setup(r => r.ExistsByEmail("ana@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        User? added = null;
        _repositoryMock
            .Setup(r => r.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => added = u);

        var payload = new ArgentinaProfilePayload("monotributo", Guid.NewGuid());
        var request = BuildRequest(name: "Ana", country: "AR", argentina: payload);

        var result = await _sut.Register("ext-ar", "ana@test.com", request);

        result.IsError.Should().BeFalse();
        result.Value.Country.Should().Be("ar");
        added.Should().NotBeNull();
        _handlerMock.Verify(
            h => h.Upsert(added!.Id, It.Is<UpdateProfileRequest>(r => r.Country == "ar" && r.Argentina == payload), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_UnsupportedCountry_ReturnsInvalidCountryError()
    {
        var request = BuildRequest(country: "xx");

        var result = await _sut.Register("ext-123", "john@test.com", request);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(ProfileErrors.InvalidCountry);
        _repositoryMock.Verify(r => r.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Register_NonArgentinaCountryWithArgentinaPayload_ReturnsInvalidCountryPayloadError()
    {
        var request = BuildRequest(country: "row", argentina: new ArgentinaProfilePayload("other", null));

        var result = await _sut.Register("ext-123", "john@test.com", request);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(ProfileErrors.InvalidCountryPayload);
        _repositoryMock.Verify(r => r.Add(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Register_HandlerUpsertFails_ReturnsHandlerError()
    {
        _repositoryMock
            .Setup(r => r.GetByExternalId("ext-ar", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _repositoryMock
            .Setup(r => r.ExistsByEmail("ana@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _handlerMock
            .Setup(h => h.Upsert(It.IsAny<Guid>(), It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileErrors.MonotributoCategoryNotFound);

        var request = BuildRequest(country: "ar", argentina: new ArgentinaProfilePayload("monotributo", Guid.NewGuid()));

        var result = await _sut.Register("ext-ar", "ana@test.com", request);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(ProfileErrors.MonotributoCategoryNotFound);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Register_DuplicateExternalId_ReturnsConflictError()
    {
        var existingUser = new User { ExternalId = "ext-123", Name = "John", Email = "john@test.com", Dob = new DateOnly(1990, 1, 1), Country = "row" };
        _repositoryMock
            .Setup(r => r.GetByExternalId("ext-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var result = await _sut.Register("ext-123", "other@test.com", BuildRequest(name: "Jane", dob: new DateOnly(1995, 5, 5)));

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.UserAlreadyExists);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflictError()
    {
        _repositoryMock
            .Setup(r => r.GetByExternalId("ext-456", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _repositoryMock
            .Setup(r => r.ExistsByEmail("john@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.Register("ext-456", "john@test.com", BuildRequest(name: "Jane", dob: new DateOnly(1995, 5, 5)));

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.EmailAlreadyInUse);
    }

    [Fact]
    public async Task Register_MissingExternalId_ReturnsUnauthorizedError()
    {
        var result = await _sut.Register(null, "john@test.com", BuildRequest());

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.MissingExternalId);
    }

    [Fact]
    public async Task Register_MissingEmail_ReturnsUnauthorizedError()
    {
        var result = await _sut.Register("ext-123", null, BuildRequest());

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(AuthErrors.MissingEmail);
    }
}
