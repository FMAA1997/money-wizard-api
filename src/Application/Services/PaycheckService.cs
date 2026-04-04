using Application.Abstractions.Services;
using Domain.Abstractions;
using Domain.Abstractions.Repositories;
using Domain.Errors;
using Domain.Models;
using Domain.Requests;
using ErrorOr;

namespace Application.Services;

public sealed class PaycheckService(
    IPaycheckRepository paycheckRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork) : IPaycheckService
{
    public async Task<ErrorOr<IReadOnlyList<Paycheck>>> GetAll(string? externalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var paychecks = await paycheckRepository.GetByUserId(user.Id, cancellationToken);
        return paychecks.ToList();
    }

    public async Task<ErrorOr<Paycheck>> GetById(string? externalId, Guid id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var paycheck = await paycheckRepository.GetById(id, cancellationToken);
        if (paycheck is null || paycheck.UserId != user.Id)
            return PaycheckErrors.NotFound;

        return paycheck;
    }

    public async Task<ErrorOr<Paycheck>> Create(string? externalId, CreatePaycheckRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var paycheck = new Paycheck
        {
            UserId = user.Id,
            Date = request.Date,
            Amount = request.Amount,
            Description = request.Description
        };

        await paycheckRepository.Add(paycheck, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return paycheck;
    }

    public async Task<ErrorOr<Paycheck>> Update(string? externalId, Guid id, UpdatePaycheckRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var paycheck = await paycheckRepository.GetById(id, cancellationToken);
        if (paycheck is null || paycheck.UserId != user.Id)
            return PaycheckErrors.NotFound;

        paycheck.Date = request.Date;
        paycheck.Amount = request.Amount;
        paycheck.Description = request.Description;

        paycheckRepository.Update(paycheck);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return paycheck;
    }

    public async Task<ErrorOr<Deleted>> Delete(string? externalId, Guid id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(externalId))
            return AuthErrors.MissingExternalId;

        var user = await userRepository.GetByExternalId(externalId, cancellationToken);
        if (user is null)
            return AuthErrors.UserNotFound;

        var paycheck = await paycheckRepository.GetById(id, cancellationToken);
        if (paycheck is null || paycheck.UserId != user.Id)
            return PaycheckErrors.NotFound;

        paycheckRepository.Delete(paycheck);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Deleted;
    }
}
