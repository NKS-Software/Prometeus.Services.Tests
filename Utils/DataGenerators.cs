using Prometeus.Core.Data.Entities;
using Prometeus.Core.Generators;
using System;
using System.Collections.Generic;
using System.Text;

namespace Prometeus.Services.Tests.Utils
{
    internal static class DataGenerators
    {
        public static Client CreateRandomClient(string name = "TestName", string surname = "TestSurname", string emailPrefix = "test", string phonePrefix = "+7906157") => new()
        {
            Id = Guid.Empty,
            Name = name,
            Surname = surname,
            BirthDate = DateOnly.ParseExact($"{Random.Shared.Next(1980, 2027)}-{Random.Shared.Next(1, 13).ToString().PadLeft(2, '0')}-{Random.Shared.Next(1, 28).ToString().PadLeft(2, '0')}", "yyyy-MM-dd"),
            Email = $"{emailPrefix}{Random.Shared.Next(1000, 9999)}@gmail.com",
            Phone = $"{phonePrefix}{Random.Shared.Next(1000, 9999)}"
        };

        public static Account CreateRandomAccount(Guid clientId, string name = "Payment", int currencyCode = 810) => new()
        {
            ClientId = clientId,
            Number = AccountNumberGenerator.Generate(408, 02, currencyCode, DepedencyFixture.BankDetails.BIK, Random.Shared.Next(0, 1000)),
            OpenedAt = DateTime.UtcNow,
            Name = name
        };

        public static Card CreateRandomCard(string accountNumber) => new()
        {
            Number = CardNumberGenerator.Generate(DepedencyFixture.BankDetails.BINs["Mir"][0], 16),
            AccountNumber = accountNumber,
            ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(3),
        };
    }
}
