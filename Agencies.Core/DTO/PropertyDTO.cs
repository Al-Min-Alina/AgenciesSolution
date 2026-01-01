using System.ComponentModel.DataAnnotations;

namespace Agencies.Core.DTO
{
    public class CreatePropertyRequest
    {
        [Required(ErrorMessage = "Название обязательно")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "Название должно быть от 2 до 200 символов")]
        public string Title { get; set; }

        [StringLength(2000, ErrorMessage = "Описание не должно превышать 2000 символов")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Адрес обязателен")]
        [StringLength(500, ErrorMessage = "Адрес не должен превышать 500 символов")]
        public string Address { get; set; }

        [Range(10000, 1000000000, ErrorMessage = "Цена должна быть от 10 000 до 1 000 000 000")]
        public double Price { get; set; }

        [Range(1, 10000, ErrorMessage = "Площадь должна быть от 1 до 10 000 м²")]
        public double Area { get; set; }

        [Required(ErrorMessage = "Тип недвижимости обязателен")]
        public string Type { get; set; }

        [Range(0, 100, ErrorMessage = "Количество комнат должно быть от 0 до 100")]
        public int Rooms { get; set; }

        public bool IsAvailable { get; set; }
    }

    public class PropertyDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public double Price { get; set; }
        public double Area { get; set; }
        public string Type { get; set; }
        public int Rooms { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CreatedByUserId { get; set; }
    }

    public class UpdatePropertyRequest
    {
        [StringLength(200, MinimumLength = 2, ErrorMessage = "Название должно быть от 2 до 200 символов")]
        public string Title { get; set; }

        [StringLength(2000, ErrorMessage = "Описание не должно превышать 2000 символов")]
        public string Description { get; set; }

        [StringLength(500, ErrorMessage = "Адрес не должен превышать 500 символов")]
        public string Address { get; set; }

        [Range(0, 1000000000, ErrorMessage = "Цена должна быть от 0 до 1 000 000 000")]
        public double Price { get; set; } // 0 = не обновлять

        [Range(0, 10000, ErrorMessage = "Площадь должна быть от 0 до 10 000 м²")]
        public double Area { get; set; } // 0 = не обновлять

        [RegularExpression("^(Apartment|House|Commercial|Квартира|Дом|Коммерческая)$",
            ErrorMessage = "Тип должен быть: Apartment/House/Commercial или Квартира/Дом/Коммерческая")]
        public string Type { get; set; } // null/empty = не обновлять

        [Range(-1, 100, ErrorMessage = "Количество комнат должно быть от -1 до 100")]
        public int Rooms { get; set; } = -1; // -1 = не обновлять (по умолчанию)

        public bool? IsAvailable { get; set; } // Nullable = не обновлять
    }
}