using Agencies.Core.DTO;
using FluentValidation;

namespace Agencies.API.Validators
{
    public class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
    {
        public CreateClientRequestValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("Имя обязательно")
                .MaximumLength(100).WithMessage("Имя не должно превышать 100 символов")
                .Matches(@"^[a-zA-Zа-яА-ЯёЁ\s-]+$").WithMessage("Имя может содержать только буквы, пробелы и дефисы");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Фамилия обязательна")
                .MaximumLength(100).WithMessage("Фамилия не должна превышать 100 символов")
                .Matches(@"^[a-zA-Zа-яА-ЯёЁ\s-]+$").WithMessage("Фамилия может содержать только буквы, пробелы и дефисы");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email обязателен")
                .EmailAddress().WithMessage("Неверный формат email")
                .MaximumLength(200).WithMessage("Email не должен превышать 200 символов");

            RuleFor(x => x.Phone)
                .NotEmpty().WithMessage("Телефон обязателен")
                .MaximumLength(20).WithMessage("Телефон не должен превышать 20 символов")
                .Matches(@"^[\d\s\(\)\-\+]+$").WithMessage("Неверный формат телефона");

            RuleFor(x => x.Budget)
                .GreaterThanOrEqualTo(0).WithMessage("Бюджет не может быть отрицательным")
                .LessThanOrEqualTo(1000000000).WithMessage("Бюджет не должен превышать 1 000 000 000")
                .When(x => x.Budget.HasValue);

            RuleFor(x => x.Requirements)
                .MaximumLength(1000).WithMessage("Требования не должны превышать 1000 символов");
        }
    }

    public class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
    {
        public UpdateClientRequestValidator()
        {
            Include(new CreateClientRequestValidator());
        }
    }
}