//using Agencies.API.Validators;
//using Agencies.Core.DTO;
//using FluentValidation.TestHelper;
//using System.Collections.Generic;
//using Xunit;

//namespace Agencies.Tests.UnitTests
//{
//    public class ValidationTests
//    {
//        private readonly CreatePropertyRequestValidator _propertyValidator;
//        private readonly CreateClientRequestValidator _clientValidator;
//        private readonly CreateDealRequestValidator _dealValidator;

//        public ValidationTests()
//        {
//            _propertyValidator = new CreatePropertyRequestValidator();
//            _clientValidator = new CreateClientRequestValidator();
//            _dealValidator = new CreateDealRequestValidator();
//        }

//        [Theory]
//        [InlineData("", false)]
//        [InlineData("Test", false)]
//        [InlineData("Valid Property Title", true)]
//        [InlineData("A".PadLeft(201, 'A'), false)]
//        public void PropertyTitle_Validation(string title, bool shouldBeValid)
//        {
//            var request = new CreatePropertyRequest { Title = title };
//            var result = _propertyValidator.TestValidate(request);

//            if (shouldBeValid)
//                result.ShouldNotHaveValidationErrorFor(x => x.Title);
//            else
//                result.ShouldHaveValidationErrorFor(x => x.Title);
//        }

//        public static IEnumerable<object[]> PropertyPriceTestData =>
//            new List<object[]>
//            {
//                new object[] { 0m, false },
//                new object[] { -100m, false },
//                new object[] { 100000m, true },
//                new object[] { 1000000001m, false }
//            };

//        [Theory]
//        [MemberData(nameof(PropertyPriceTestData))]
//        public void PropertyPrice_Validation(decimal price, bool shouldBeValid)
//        {
//            var request = new CreatePropertyRequest { Price = price };
//            var result = _propertyValidator.TestValidate(request);

//            if (shouldBeValid)
//                result.ShouldNotHaveValidationErrorFor(x => x.Price);
//            else
//                result.ShouldHaveValidationErrorFor(x => x.Price);
//        }

//        [Theory]
//        [InlineData("Apartment", true)]
//        [InlineData("House", true)]
//        [InlineData("Commercial", true)]
//        [InlineData("InvalidType", false)]
//        [InlineData("", false)]
//        public void PropertyType_Validation(string type, bool shouldBeValid)
//        {
//            var request = new CreatePropertyRequest { Type = type };
//            var result = _propertyValidator.TestValidate(request);

//            if (shouldBeValid)
//                result.ShouldNotHaveValidationErrorFor(x => x.Type);
//            else
//                result.ShouldHaveValidationErrorFor(x => x.Type);
//        }

//        [Theory]
//        [InlineData("", false)]
//        [InlineData("test@example.com", true)]
//        [InlineData("invalid-email", false)]
//        [InlineData("test", false)]
//        public void ClientEmail_Validation(string email, bool shouldBeValid)
//        {
//            var request = new CreateClientRequest { Email = email };
//            var result = _clientValidator.TestValidate(request);

//            if (shouldBeValid)
//                result.ShouldNotHaveValidationErrorFor(x => x.Email);
//            else
//                result.ShouldHaveValidationErrorFor(x => x.Email);
//        }

//        [Theory]
//        [InlineData("1234567890", true)]
//        [InlineData("+7 (123) 456-78-90", true)]
//        [InlineData("", false)]
//        [InlineData("abc", false)]
//        [InlineData("A".PadLeft(21, 'A'), false)]
//        public void ClientPhone_Validation(string phone, bool shouldBeValid)
//        {
//            var request = new CreateClientRequest { Phone = phone };
//            var result = _clientValidator.TestValidate(request);

//            if (shouldBeValid)
//                result.ShouldNotHaveValidationErrorFor(x => x.Phone);
//            else
//                result.ShouldHaveValidationErrorFor(x => x.Phone);
//        }

//        public static IEnumerable<object[]> DealAmountTestData =>
//            new List<object[]>
//            {
//                new object[] { 0m, false },
//                new object[] { 1000000m, true },
//                new object[] { 1000000001m, false }
//            };

//        [Theory]
//        [MemberData(nameof(DealAmountTestData))]
//        public void DealAmount_Validation(decimal amount, bool shouldBeValid)
//        {
//            var request = new CreateDealRequest { DealAmount = amount };
//            var result = _dealValidator.TestValidate(request);

//            if (shouldBeValid)
//                result.ShouldNotHaveValidationErrorFor(x => x.DealAmount);
//            else
//                result.ShouldHaveValidationErrorFor(x => x.DealAmount);
//        }

//        [Fact]
//        public void CompletePropertyRequest_ValidData_PassesValidation()
//        {
//            var request = new CreatePropertyRequest
//            {
//                Title = "Beautiful Apartment in Center",
//                Address = "Central Street 123, Moscow",
//                Price = 7500000m,
//                Area = 85.5,
//                Type = "Apartment",
//                Rooms = 3,
//                IsAvailable = true,
//                Description = "Spacious apartment with great view"
//            };

//            var result = _propertyValidator.TestValidate(request);
//            result.ShouldNotHaveAnyValidationErrors();
//        }

//        [Fact]
//        public void CompletePropertyRequest_InvalidData_FailsValidation()
//        {
//            var request = new CreatePropertyRequest
//            {
//                Title = "",
//                Address = "",
//                Price = -100m,
//                Area = 0.0,
//                Type = "InvalidType",
//                Rooms = -1
//            };

//            var result = _propertyValidator.TestValidate(request);
//            result.ShouldHaveValidationErrorFor(x => x.Title);
//            result.ShouldHaveValidationErrorFor(x => x.Address);
//            result.ShouldHaveValidationErrorFor(x => x.Price);
//            result.ShouldHaveValidationErrorFor(x => x.Area);
//            result.ShouldHaveValidationErrorFor(x => x.Type);
//            result.ShouldHaveValidationErrorFor(x => x.Rooms);
//        }

//        public static IEnumerable<object[]> PropertyAreaTestData =>
//            new List<object[]>
//            {
//                new object[] { 0.0, false },
//                new object[] { 10.5, true },
//                new object[] { 1000.1, true },
//                new object[] { -10.0, false }
//            };

//        [Theory]
//        [MemberData(nameof(PropertyAreaTestData))]
//        public void PropertyArea_Validation(double area, bool shouldBeValid)
//        {
//            var request = new CreatePropertyRequest { Area = area };
//            var result = _propertyValidator.TestValidate(request);

//            if (shouldBeValid)
//                result.ShouldNotHaveValidationErrorFor(x => x.Area);
//            else
//                result.ShouldHaveValidationErrorFor(x => x.Area);
//        }

//        public static IEnumerable<object[]> PropertyRoomsTestData =>
//            new List<object[]>
//            {
//                new object[] { 0, true },
//                new object[] { 1, true },
//                new object[] { 5, true },
//                new object[] { -1, false },
//                new object[] { 21, false }
//            };

//        [Theory]
//        [MemberData(nameof(PropertyRoomsTestData))]
//        public void PropertyRooms_Validation(int rooms, bool shouldBeValid)
//        {
//            var request = new CreatePropertyRequest { Rooms = rooms };
//            var result = _propertyValidator.TestValidate(request);

//            if (shouldBeValid)
//                result.ShouldNotHaveValidationErrorFor(x => x.Rooms);
//            else
//                result.ShouldHaveValidationErrorFor(x => x.Rooms);
//        }
//    }
//}