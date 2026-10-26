using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Common;
using Shouldly;

namespace Recruitment.Application.Tests.Common;

public sealed class DispatcherTests
{
    private readonly IAppDbContext _db = Substitute.For<IAppDbContext>();
    private readonly HandlerCalls _calls = new();
    private readonly IDispatcher _dispatcher;

    public DispatcherTests()
    {
        _db.ExecuteInTransaction(Arg.Any<Func<Task<Result<string>>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<Task<Result<string>>>>()());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(_db);
        services.AddSingleton(_calls);
        services.AddScoped<ICommandHandler<RenameCommand, string>, RenameCommandHandler>();
        services.AddScoped<IValidator<RenameCommand>, RenameCommandValidator>();
        services.AddScoped<IQueryHandler<GetNameQuery, string>, GetNameQueryHandler>();

        _dispatcher = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IDispatcher>();
    }

    [Fact]
    public async Task Send_WithValidCommand_RunsHandlerInsideTransaction()
    {
        var result = await _dispatcher.Send(new RenameCommand("Yeni Ad"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Yeni Ad");
        await _db.Received(1)
            .ExecuteInTransaction(Arg.Any<Func<Task<Result<string>>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_WithInvalidCommand_ReturnsFieldErrorsAndSkipsHandler()
    {
        var result = await _dispatcher.Send(new RenameCommand(""));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.FieldErrors.ShouldNotBeNull();
        result.Error.FieldErrors["newName"].ShouldBe(["Ad boş olamaz."]);
        _calls.Count.ShouldBe(0);
        await _db.DidNotReceive()
            .ExecuteInTransaction(Arg.Any<Func<Task<Result<string>>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Send_WhenHandlerThrowsDomainException_ReturnsBusinessRuleError()
    {
        var result = await _dispatcher.Send(new RenameCommand("Yeni Ad", BreakRule: true));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.BusinessRule);
        result.Error.Message.ShouldBe("Bu ad kullanılamaz.");
    }

    [Fact]
    public async Task Send_WhenHandlerThrowsUnexpectedException_Propagates()
    {
        await Should.ThrowAsync<InvalidOperationException>(() =>
            _dispatcher.Send(new RenameCommand("Yeni Ad", Crash: true)));
    }

    [Fact]
    public async Task Query_WithQuery_RunsHandlerWithoutTransaction()
    {
        var result = await _dispatcher.Query(new GetNameQuery());

        result.Value.ShouldBe("Mevcut Ad");
        await _db.DidNotReceive()
            .ExecuteInTransaction(Arg.Any<Func<Task<Result<string>>>>(), Arg.Any<CancellationToken>());
    }

    private sealed class HandlerCalls
    {
        public int Count { get; set; }
    }

    private sealed record RenameCommand(string NewName, bool BreakRule = false, bool Crash = false) : ICommand<string>;

    private sealed class RenameCommandValidator : AbstractValidator<RenameCommand>
    {
        public RenameCommandValidator() => RuleFor(c => c.NewName).NotEmpty().WithMessage("Ad boş olamaz.");
    }

    private sealed class RenameCommandHandler(HandlerCalls calls) : ICommandHandler<RenameCommand, string>
    {
        public Task<Result<string>> Handle(RenameCommand command, CancellationToken ct)
        {
            calls.Count++;

            if (command.BreakRule)
                throw new DomainException("Bu ad kullanılamaz.");
            if (command.Crash)
                throw new InvalidOperationException("Beklenmeyen durum.");

            return Task.FromResult<Result<string>>(command.NewName);
        }
    }

    private sealed record GetNameQuery : IQuery<string>;

    private sealed class GetNameQueryHandler : IQueryHandler<GetNameQuery, string>
    {
        public Task<Result<string>> Handle(GetNameQuery query, CancellationToken ct) =>
            Task.FromResult<Result<string>>("Mevcut Ad");
    }
}
