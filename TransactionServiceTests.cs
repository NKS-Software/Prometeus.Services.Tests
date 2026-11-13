using Prometeus.Core.Data.Entities;
using Prometeus.Core.Data.Repositories;
using Prometeus.Core.Generators;
using Prometeus.Core.Models;
using Prometeus.Services.Models.DTOs;
using Prometeus.Services.Models.Requests;
using Prometeus.Services.Tests.Utils;

namespace Prometeus.Services.Tests;

public class TransactionServiceTests(DepedencyFixture depedencyFixture) : IClassFixture<DepedencyFixture>
{
    private readonly TransactionService transactionService = new(
        depedencyFixture.DatabaseContext,
        DepedencyFixture.BankDetails);

    private readonly ClientRepository clientRepository = depedencyFixture.DatabaseContext.Get<ClientRepository>();
    private readonly AccountRepository accountRepository = depedencyFixture.DatabaseContext.Get<AccountRepository>();
    private readonly CardRepository cardRepository = depedencyFixture.DatabaseContext.Get<CardRepository>();

    #region GetAllAsync
    [Fact]
    public async Task GetAllAsync_WithInvalidCount()
    {
        var client = await CreateClientAsync();
        var res = await transactionService.GetAllAsync(client.Id, count: 0);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task GetAllAsync_WithInvalidPage()
    {
        var client = await CreateClientAsync();
        var res = await transactionService.GetAllAsync(client.Id, page: -1);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task GetAllAsync_WithAnotherClientAccount()
    {
        var client1 = await CreateClientAsync();
        var account1 = await CreateAccountAsync(client1.Id);

        var client2 = await CreateClientAsync();
        var res = await transactionService.GetAllAsync(client2.Id, account1.Number);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task GetAllAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var res = await transactionService.GetAllAsync(client.Id);
        Assert.True(res is SuccessResponse<List<TransactionDTO>>);
    }
    #endregion

    #region SendToAccountAsync
    [Fact]
    public async Task SendToAccountAsync_WithNotExistsSenderAccount()
    {
        var client = await CreateClientAsync();
        var request = new SendToAccountRequest
        {
            FromAccount = AccountNumberGenerator.Generate(408, 02, 643, DepedencyFixture.BankDetails.BIK),
            ToAccount = AccountNumberGenerator.Generate(408, 02, 643, DepedencyFixture.BankDetails.BIK),
            Amount = 100
        };
        var res = await transactionService.SendToAccountAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task SendToAccountAsync_WithAnotherClientAccount()
    {
        var client1 = await CreateClientAsync();
        var account1 = await CreateAccountAsync(client1.Id);

        var client2 = await CreateClientAsync();
        var account2 = await CreateAccountAsync(client2.Id);

        var request = new SendToAccountRequest
        {
            FromAccount = account1.Number,
            ToAccount = account2.Number,
            Amount = 100
        };
        var res = await transactionService.SendToAccountAsync(client2.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task SendToAccountAsync_WithInsufficientFunds()
    {
        var client = await CreateClientAsync();
        var account1 = await CreateAccountAsync(client.Id);
        var account2 = await CreateAccountAsync(client.Id);

        var request = new SendToAccountRequest
        {
            FromAccount = account1.Number,
            ToAccount = account2.Number,
            Amount = 1000
        };
        var res = await transactionService.SendToAccountAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task SendToAccountAsync_WithNotExistsRecipientAccount()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        await accountRepository.CreditAsync(account.Number, 1000);

        var request = new SendToAccountRequest
        {
            FromAccount = account.Number,
            ToAccount = AccountNumberGenerator.Generate(408, 02, 643, DepedencyFixture.BankDetails.BIK),
            Amount = 100
        };
        var res = await transactionService.SendToAccountAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task SendToAccountAsync_WithDifferentCurrencies()
    {
        var client = await CreateClientAsync();
        var accountRub = await CreateAccountAsync(client.Id, "RUB");
        var accountUsd = await CreateAccountAsync(client.Id, "USD");
        await accountRepository.CreditAsync(accountRub.Number, 1000);

        var request = new SendToAccountRequest
        {
            FromAccount = accountRub.Number,
            ToAccount = accountUsd.Number,
            Amount = 100
        };
        var res = await transactionService.SendToAccountAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task SendToAccountAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var fromAccount = await CreateAccountAsync(client.Id);
        var toAccount = await CreateAccountAsync(client.Id);
        await accountRepository.CreditAsync(fromAccount.Number, 1000);

        var request = new SendToAccountRequest
        {
            FromAccount = fromAccount.Number,
            ToAccount = toAccount.Number,
            Amount = 500
        };
        var res = await transactionService.SendToAccountAsync(client.Id, request);

        Assert.True(res is SuccessResponse<TransactionDTO>);

        var updatedFrom = await accountRepository.GetByNumberAsync(fromAccount.Number);
        var updatedTo = await accountRepository.GetByNumberAsync(toAccount.Number);
        Assert.Equal(500, updatedFrom!.Balance);
        Assert.Equal(500, updatedTo!.Balance);
    }
    #endregion

    #region SendToCardAsync
    [Fact]
    public async Task SendToCardAsync_WithNotExistsSenderAccount()
    {
        var client = await CreateClientAsync();
        var request = new SendToCardRequest
        {
            FromAccount = AccountNumberGenerator.Generate(408, 02, 643, DepedencyFixture.BankDetails.BIK),
            ToCard = CardNumberGenerator.Generate("440000"),
            Amount = 100
        };
        var res = await transactionService.SendToCardAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task SendToCardAsync_WithAnotherClientAccount()
    {
        var client1 = await CreateClientAsync();
        var account1 = await CreateAccountAsync(client1.Id);

        var client2 = await CreateClientAsync();
        var request = new SendToCardRequest
        {
            FromAccount = account1.Number,
            ToCard = CardNumberGenerator.Generate("440000"),
            Amount = 100
        };
        var res = await transactionService.SendToCardAsync(client2.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task SendToCardAsync_WithInsufficientFunds()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        var card = await CreateCardAsync(account.Number);

        var request = new SendToCardRequest
        {
            FromAccount = account.Number,
            ToCard = card.Number,
            Amount = 1000
        };
        var res = await transactionService.SendToCardAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task SendToCardAsync_WithUnsupportedCard()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        await accountRepository.CreditAsync(account.Number, 1000);

        var request = new SendToCardRequest
        {
            FromAccount = account.Number,
            ToCard = CardNumberGenerator.Generate("400000"), // ← чужой BIN
            Amount = 100
        };
        var res = await transactionService.SendToCardAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task SendToCardAsync_WithNotExistsCard()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        await accountRepository.CreditAsync(account.Number, 1000);

        var request = new SendToCardRequest
        {
            FromAccount = account.Number,
            ToCard = CardNumberGenerator.Generate("440000"),
            Amount = 100
        };
        var res = await transactionService.SendToCardAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task SendToCardAsync_WithFrozenCard()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        await accountRepository.CreditAsync(account.Number, 1000);

        var card = await CreateCardAsync(account.Number);
        await cardRepository.FreezeAsync(card.Number); // ← замораживаем

        var request = new SendToCardRequest
        {
            FromAccount = account.Number,
            ToCard = card.Number,
            Amount = 100
        };
        var res = await transactionService.SendToCardAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task SendToCardAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var fromAccount = await CreateAccountAsync(client.Id);
        var toAccount = await CreateAccountAsync(client.Id);
        await accountRepository.CreditAsync(fromAccount.Number, 1000);

        var card = await CreateCardAsync(toAccount.Number);

        var request = new SendToCardRequest
        {
            FromAccount = fromAccount.Number,
            ToCard = card.Number,
            Amount = 500
        };
        var res = await transactionService.SendToCardAsync(client.Id, request);

        Assert.True(res is SuccessResponse<TransactionDTO>);

        var updatedFrom = await accountRepository.GetByNumberAsync(fromAccount.Number);
        var updatedTo = await accountRepository.GetByNumberAsync(toAccount.Number);
        Assert.Equal(500, updatedFrom!.Balance);
        Assert.Equal(500, updatedTo!.Balance);
    }
    #endregion

    #region Utils
    private async Task<Client> CreateClientAsync()
    {
        var client = DataGenerators.CreateRandomClient();
        var added = await clientRepository.AddAsync(client);
        Assert.NotNull(added);
        return added;
    }

    private async Task<Account> CreateAccountAsync(Guid clientId, string currency = "RUB")
    {
        var currencyCode = DepedencyFixture.BankDetails.CurrencyCodes[currency];
        var number = AccountNumberGenerator.Generate(408, 02, currencyCode, DepedencyFixture.BankDetails.BIK);
        var account = new Account
        {
            OpenedAt = DateTime.UtcNow,
            ClientId = clientId,
            Number = number,
            Name = "Test account"
        };
        var opened = await accountRepository.OpenAsync(account);
        Assert.NotNull(opened);
        return opened;
    }

    private async Task<Card> CreateCardAsync(string accountNumber)
    {
        var bin = DepedencyFixture.BankDetails.BINs.Values.First()[0];
        var cvc = "123";
        var cvcKeyId = await depedencyFixture.KeyStore.WriteAsync(cvc);

        var card = new Card
        {
            AccountNumber = accountNumber,
            Number = CardNumberGenerator.Generate(bin),
            CardHolderName = "TEST CLIENT",
            ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(3),
            PinHash = "hash",
            CvcKeyId = cvcKeyId
        };
        var created = await cardRepository.OpenAsync(card);
        Assert.NotNull(created);
        return created;
    }
    #endregion
}