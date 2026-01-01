using Agencies.Core.DTO;
using FluentValidation;

namespace Agencies.API.Validators
{
    public class CreatePropertyRequestValidator : AbstractValidator<CreatePropertyRequest>
    {
        public CreatePropertyRequestValidator()
        {
            RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(10000).WithMessage("Цена должна быть не менее 10 000")
            .LessThanOrEqualTo(1000000000).WithMessage("Цена не должна превышать 1 000 000 000");

            RuleFor(x => x.Area)
                .GreaterThanOrEqualTo(1).WithMessage("Площадь должна быть не менее 1 м²")
                .LessThanOrEqualTo(10000).WithMessage("Площадь не должна превышать 10 000 м²");
        }

        protected void ConfigureRules()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Название обязательно")
                .MaximumLength(200).WithMessage("Название не должно превышать 200 символов")
                .MinimumLength(5).WithMessage("Название должно содержать минимум 5 символов");

            RuleFor(x => x.Address)
                .NotEmpty().WithMessage("Адрес обязателен")
                .MaximumLength(500).WithMessage("Адрес не должен превышать 500 символов");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Цена должна быть больше 0")
                .LessThanOrEqualTo(1000000000).WithMessage("Цена не должна превышать 1 000 000 000");

            RuleFor(x => x.Area)
                .GreaterThan(0).WithMessage("Площадь должна быть больше 0")
                .LessThanOrEqualTo(10000).WithMessage("Площадь не должна превышать 10 000 м²");

            RuleFor(x => x.Type)
                .NotEmpty().WithMessage("Тип недвижимости обязателен")
                .Must(BeValidPropertyType).WithMessage("Недопустимый тип недвижимости");

            RuleFor(x => x.Rooms)
                .InclusiveBetween(0, 100).WithMessage("Количество комнат должно быть от 0 до 100");

            RuleFor(x => x.Description)
                .MaximumLength(2000).WithMessage("Описание не должно превышать 2000 символов");
        }

        private bool BeValidPropertyType(string type)
        {
            var validTypes = new[] { "Apartment", "House", "Commercial", "Квартира", "Дом", "Коммерческая недвижимость" };
            return validTypes.Contains(type);
        }
    }

    public class UpdatePropertyRequestValidator : AbstractValidator<UpdatePropertyRequest>
    {
        public UpdatePropertyRequestValidator()
        {
            RuleFor(x => x.Title)
            .MinimumLength(2).When(x => !string.IsNullOrEmpty(x.Title))
            .WithMessage("Название должно содержать минимум 2 символа")
            .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Title))
            .WithMessage("Название не должно превышать 200 символов");

            RuleFor(x => x.Address)
                .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Address))
                .WithMessage("Адрес не должен превышать 500 символов");

            // Цена: от 0 до 1 млрд, 0 = не обновлять
            RuleFor(x => x.Price)
                .InclusiveBetween(0, 1000000000).WithMessage("Цена должна быть от 0 до 1 000 000 000");

            // Площадь: от 0 до 10000, 0 = не обновлять
            RuleFor(x => x.Area)
                .InclusiveBetween(0, 10000).WithMessage("Площадь должна быть от 0 до 10 000 м²");

            RuleFor(x => x.Type)
                .Must(BeValidPropertyType).When(x => !string.IsNullOrEmpty(x.Type))
                .WithMessage("Недопустимый тип недвижимости");

            // Комнаты: от -1 до 100, -1 = не обновлять
            RuleFor(x => x.Rooms)
                .InclusiveBetween(-1, 100).WithMessage("Количество комнат должно быть от -1 до 100");

            RuleFor(x => x.Description)
                .MaximumLength(2000).When(x => !string.IsNullOrEmpty(x.Description))
                .WithMessage("Описание не должно превышать 2000 символов");
        }

        private bool BeValidPropertyType(string type)
        {
            // Используйте тот же список, что и в Create
            var validTypes = new[] { "Apartment", "House", "Commercial", "Квартира", "Дом", "Коммерческая недвижимость" };
            return validTypes.Contains(type);
        }
    }
}