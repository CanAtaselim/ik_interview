using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.InterviewTemplates.CreateInterviewTemplate;
using Recruitment.Application.Features.InterviewTemplates.GetInterviewTemplateDetail;
using Recruitment.Application.Features.InterviewTemplates.GetInterviewTemplates;
using Recruitment.Application.Features.InterviewTemplates.SetInterviewTemplateActive;
using Recruitment.Application.Features.InterviewTemplates.UpdateInterviewTemplate;
using Recruitment.Application.Features.Questions.CreateQuestion;
using Recruitment.Application.Features.Questions.DeleteQuestion;
using Recruitment.Application.Features.Questions.GetQuestionDetail;
using Recruitment.Application.Features.Questions.GetQuestions;
using Recruitment.Application.Features.Questions.SetQuestionActive;
using Recruitment.Application.Features.Questions.UpdateQuestion;
using Recruitment.Domain.Questions;

namespace Recruitment.Api.Controllers;

/// <summary>
/// Soru bankası ve mülakat şablonları. İK tüm birimleri, takım lideri yalnızca kendi birimini
/// yönetir (handler'da uygulanır).
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize(Policy = Policies.CanManageQuestions)]
public sealed class QuestionsController(IDispatcher dispatcher) : ControllerBase
{
    public sealed record ActiveRequest(bool IsActive);

    [HttpGet("questions")]
    public async Task<IActionResult> GetQuestions(
        [FromQuery] string? search,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? competencyId,
        [FromQuery] Guid? levelId,
        [FromQuery] QuestionType? type,
        [FromQuery] int? difficulty,
        [FromQuery] bool? isActive,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize)
    {
        var query = new GetQuestionsQuery(search, departmentId, competencyId, levelId, type, difficulty, isActive, page, pageSize);
        return (await dispatcher.Query(query, ct)).ToActionResult(this);
    }

    [HttpGet("questions/{id:guid}")]
    public async Task<IActionResult> GetQuestion(Guid id, CancellationToken ct) =>
        (await dispatcher.Query(new GetQuestionDetailQuery(id), ct)).ToActionResult(this);

    [HttpPost("questions")]
    public async Task<IActionResult> CreateQuestion(CreateQuestionCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command, ct)).ToCreatedResult(this);

    [HttpPut("questions/{id:guid}")]
    public async Task<IActionResult> UpdateQuestion(Guid id, UpdateQuestionCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command with { Id = id }, ct)).ToActionResult(this);

    [HttpPost("questions/{id:guid}/active")]
    public async Task<IActionResult> SetQuestionActive(Guid id, ActiveRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new SetQuestionActiveCommand(id, request.IsActive), ct)).ToActionResult(this);

    [HttpDelete("questions/{id:guid}")]
    public async Task<IActionResult> DeleteQuestion(Guid id, CancellationToken ct) =>
        (await dispatcher.Send(new DeleteQuestionCommand(id), ct)).ToActionResult(this);

    [HttpGet("interview-templates")]
    public async Task<IActionResult> GetTemplates(
        [FromQuery] Guid? departmentId, [FromQuery] bool? isActive, [FromQuery] string? search, CancellationToken ct) =>
        (await dispatcher.Query(new GetInterviewTemplatesQuery(departmentId, isActive, search), ct)).ToActionResult(this);

    [HttpGet("interview-templates/{id:guid}")]
    public async Task<IActionResult> GetTemplate(Guid id, CancellationToken ct) =>
        (await dispatcher.Query(new GetInterviewTemplateDetailQuery(id), ct)).ToActionResult(this);

    [HttpPost("interview-templates")]
    public async Task<IActionResult> CreateTemplate(CreateInterviewTemplateCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command, ct)).ToCreatedResult(this);

    [HttpPut("interview-templates/{id:guid}")]
    public async Task<IActionResult> UpdateTemplate(Guid id, UpdateInterviewTemplateCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command with { Id = id }, ct)).ToActionResult(this);

    [HttpPost("interview-templates/{id:guid}/active")]
    public async Task<IActionResult> SetTemplateActive(Guid id, ActiveRequest request, CancellationToken ct) =>
        (await dispatcher.Send(new SetInterviewTemplateActiveCommand(id, request.IsActive), ct)).ToActionResult(this);
}
