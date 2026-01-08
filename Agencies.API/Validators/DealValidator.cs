using Agencies.Core.DTO;
using FluentValidation;
using System;

namespace Agencies.API.Validators
{
    public class CreateDealRequestValidator : AbstractValidator<CreateDealRequest>
    {
        public CreateDealRequestValidator()
        {
            RuleFor(x => x.PropertyId)
                .GreaterThan(0).WithMessage("ID объекта недвижимости обязательно");

            RuleFor(x => x.ClientId)
                .GreaterThan(0).WithMessage("ID клиента обязательно");

            RuleFor(x => x.DealAmount)
                .GreaterThan(0).WithMessage("Сумма сделки должна быть больше 0")
                .LessThanOrEqualTo(1000000000).WithMessage("Сумма сделки не должна превышать 1 000 000 000");

            RuleFor(x => x.DealDate)
                .NotEmpty().WithMessage("Дата сделки обязательна")
                .LessThanOrEqualTo(DateTime.Now).WithMessage("Дата сделки не может быть в будущем")
                .GreaterThanOrEqualTo(DateTime.Now.AddYears(-10)).WithMessage("Дата сделки не может быть старше 10 лет");

            RuleFor(x => x.Status)
                .NotEmpty().WithMessage("Статус сделки обязателен")
                .Must(BeValidDealStatus).WithMessage("Недопустимый статус сделки");

            RuleFor(x => x.AgentId)
                .GreaterThan(0).WithMessage("ID агента обязательно");
        }

        private bool BeValidDealStatus(string status)
        {
            var validStatuses = new[] { "В ожидании", "Завершено", "Отменено"};
            return validStatuses.Contains(status);
        }
    }

    public class UpdateDealRequestValidator : AbstractValidator<UpdateDealRequest>
    {
        public UpdateDealRequestValidator()
        {
            Include(new CreateDealRequestValidator());
        }
    }
}