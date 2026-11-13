using Prometeus.Core.Data.Entities;
using Prometeus.Core.Data.Repositories;
using Prometeus.Core.Generators;
using Prometeus.Core.Models;
using Prometeus.Core.Utils;
using Prometeus.Services.Models.DTOs;
using Prometeus.Services.Models.Requests;
using Prometeus.Services.Tests.Utils;

namespace Prometeus.Services.Tests;

public class AccountServiceTests(DepedencyFixture depedencyFixture) : IClassFixture<DepedencyFixture>
{
    private readonly AccountService accountService = new(depedencyFixture.DatabaseContext, DepedencyFixture.BankDetails);
    private readonly ClientRepository clientRepository = depedencyFixture.DatabaseContext.Get<ClientRepository>();
    private readonly AccountRepository accountRepository = depedencyFixture.DatabaseContext.Get<AccountRepository>();

    #region GetByNumberAsync
    [Fact]
    public async Task GetByNumberAsync_WithInvalidNumber()
    {
        var res = await accountService.GetByNumberAsync("00000000000000000000");
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task GetByNumberAsync_WithNotExistsAccount()
    {
        var number = AccountNumberGenerator.Generate(408, 02, 643, DepedencyFixture.BankDetails.BIK);
        var res = await accountService.GetByNumberAsync(number);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task GetByNumberAsync_WithExistsAccount()
    {
        var account = await CreateAccountAsync();
        var res = await accountService.GetByNumberAsync(account.Number);
        Assert.True(res is SuccessResponse<AccountDTO>);
        Assert.Equal(account.Number, ((SuccessResponse<AccountDTO>)res).Value.Number);
    }

    [Fact]
    public async Task GetByNumberAsync_WithCards()
    {
        var account = await CreateAccountAsync();
        await CreateCardAsync(account.Number);
        var res = await accountService.GetByNumberAsync(account.Number);
        Assert.True(res is SuccessResponse<AccountDTO>);
        Assert.NotEmpty(((SuccessResponse<AccountDTO>)res).Value.Cards);
    }
    #endregion

    #region GetAccountDetails
    [Fact]
    public async Task GetAccountDetails_WithInvalidNumber()
    {
        var client = await CreateClientAsync();
        var res = await accountService.GetAccountDetails("00000000000000000000", client.Id);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task GetAccountDetails_WithNotExistsAccount()
    {
        var client = await CreateClientAsync();
        var number = AccountNumberGenerator.Generate(408, 02, 643, DepedencyFixture.BankDetails.BIK);

        var res = await accountService.GetAccountDetails(number, client.Id);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task GetAccountDetails_WithAnotherClientAccount()
    {
        var client1 = await CreateClientAsync();
        var account1 = await CreateAccountAsync(client1.Id);

        var client2 = await CreateClientAsync();

        var res = await accountService.GetAccountDetails(account1.Number, client2.Id);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task GetAccountDetails_WithValidData()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);

        var res = await accountService.GetAccountDetails(account.Number, client.Id);

        Assert.True(res is SuccessResponse<AccountDetails>);
        var details = ((SuccessResponse<AccountDetails>)res).Value;

        Assert.Equal(DepedencyFixture.BankDetails.Name, details.Bank);
        Assert.Equal(DepedencyFixture.BankDetails.BIK, details.BIK);
        Assert.Equal(account.Number, details.ClientAccountNumber);
        Assert.False(string.IsNullOrEmpty(details.CorrespondentAccountNumber));
        Assert.Contains(client.Surname, details.ClientFullName);
        Assert.Contains(client.Name, details.ClientFullName);
    }
    #endregion

    #region GetAllForClientAsync
    [Fact]
    public async Task GetAllForClientAsync_WithNotExistsClient()
    {
        var res = await accountService.GetAllForClientAsync(Guid.NewGuid());
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task GetAllForClientAsync_WithoutAccounts()
    {
        var client = await CreateClientAsync();
        var res = await accountService.GetAllForClientAsync(client.Id);
        Assert.True(res is SuccessResponse<List<AccountDTO>>);
        Assert.Empty(((SuccessResponse<List<AccountDTO>>)res).Value);
    }

    [Fact]
    public async Task GetAllForClientAsync_WithAccounts()
    {
        var client = await CreateClientAsync();
        for (int i = 0; i < 3; i++)
            await CreateAccountAsync(client.Id);

        var res = await accountService.GetAllForClientAsync(client.Id);
        Assert.True(res is SuccessResponse<List<AccountDTO>>);
        Assert.Equal(3, ((SuccessResponse<List<AccountDTO>>)res).Value.Count);
    }
    #endregion

    #region OpenAsync
    [Fact]
    public async Task OpenAsync_WithNotExistsClient()
    {
        var request = new OpenAccountRequest
        {
            Name = "Test account",
            CurrencyCode = "RUB",
            BranchCode = 0
        };
        var res = await accountService.OpenAsync(Guid.NewGuid(), request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task OpenAsync_WithUnsupportedCurrency()
    {
        var client = await CreateClientAsync();
        var request = new OpenAccountRequest
        {
            Name = "Test account",
            CurrencyCode = "GBP",
            BranchCode = 0
        };
        var res = await accountService.OpenAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task OpenAsync_WithIncorrectCurrencyCode()
    {
        var client = await CreateClientAsync();
        var request = new OpenAccountRequest
        {
            Name = "Test account",
            CurrencyCode = "usd",
            BranchCode = 0
        };
        var (isValid, _) = ValidationHelper.Validate(request);
        Assert.False(isValid);
    }

    [Fact]
    public async Task OpenAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var request = new OpenAccountRequest
        {
            Name = "Test account",
            CurrencyCode = "RUB",
            BranchCode = 0
        };
        var res = await accountService.OpenAsync(client.Id, request);
        Assert.True(res is SuccessResponse<AccountDTO>);
        Assert.Equal("Test account", ((SuccessResponse<AccountDTO>)res).Value.Name);
    }

    [Fact]
    public async Task OpenAsync_WithUsdCurrency()
    {
        var client = await CreateClientAsync();
        var request = new OpenAccountRequest
        {
            Name = "USD account",
            CurrencyCode = "USD",
            BranchCode = 0
        };
        var res = await accountService.OpenAsync(client.Id, request);
        Assert.True(res is SuccessResponse<AccountDTO>);
    }

    [Fact]
    public async Task OpenAsync_WithBranchCode()
    {
        var client = await CreateClientAsync();
        var request = new OpenAccountRequest
        {
            Name = "Test account",
            CurrencyCode = "RUB",
            BranchCode = 5
        };
        var res = await accountService.OpenAsync(client.Id, request);
        Assert.True(res is SuccessResponse<AccountDTO>);
    }
    #endregion

    #region CloseAsync
    [Fact]
    public async Task CloseAsync_WithInvalidNumber()
    {
        var client = await CreateClientAsync();
        var res = await accountService.CloseAsync(client.Id, "00000000000000000000");
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task CloseAsync_WithNotExistsAccount()
    {
        var client = await CreateClientAsync();
        var number = AccountNumberGenerator.Generate(408, 02, 643, DepedencyFixture.BankDetails.BIK);
        var res = await accountService.CloseAsync(client.Id, number);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task CloseAsync_WithAccountNotBelongsToClient()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync();
        var res = await accountService.CloseAsync(client.Id, account.Number);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task CloseAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        var res = await accountService.CloseAsync(client.Id, account.Number);
        Assert.True(res is SuccessResponse);
    }

    [Fact]
    public async Task CloseAsync_WithNonZeroBalance()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        await accountRepository.CreditAsync(account.Number, 1000);

        var res = await accountService.CloseAsync(client.Id, account.Number);
        Assert.True(res is ErrorResponse);
    }
    #endregion

    #region UpdateAsync
    [Fact]
    public async Task UpdateAsync_WithInvalidNumber()
    {
        var client = await CreateClientAsync();
        var request = new UpdateAccountRequest
        {
            Number = "00000000000000000000",
            Name = "New name"
        };
        var res = await accountService.UpdateAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Theory]
    [InlineData("4080200001")]
    [InlineData("4080200001ABZ")]
    [InlineData("QWE00000000000000000")]
    public async Task UpdateAsync_WithInvalidNumber2(string number)
    {
        var request = new UpdateAccountRequest
        {
            Number = number,
            Name = "New name"
        };
        var (isValid, _) = ValidationHelper.Validate(request);
        Assert.False(isValid);
    }

    [Fact]
    public async Task UpdateAsync_WithNotExistsAccount()
    {
        var client = await CreateClientAsync();
        var number = AccountNumberGenerator.Generate(408, 02, 643, DepedencyFixture.BankDetails.BIK);
        var request = new UpdateAccountRequest
        {
            Number = number,
            Name = "New name"
        };
        var res = await accountService.UpdateAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task UpdateAsync_WithAccountNotBelongsToClient()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync();
        var request = new UpdateAccountRequest
        {
            Number = account.Number,
            Name = "New name"
        };
        var res = await accountService.UpdateAsync(client.Id, request);
        Assert.True(res is ErrorResponse);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData()
    {
        var client = await CreateClientAsync();
        var account = await CreateAccountAsync(client.Id);
        var request = new UpdateAccountRequest
        {
            Number = account.Number,
            Name = "New name"
        };
        var res = await accountService.UpdateAsync(client.Id, request);
        Assert.True(res is SuccessResponse);
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

    private async Task<Account> CreateAccountAsync(Guid? clientId = null)
    {
        clientId ??= (await CreateClientAsync()).Id;
        var account = DataGenerators.CreateRandomAccount(clientId.Value);
        var opened = await accountRepository.OpenAsync(account);
        Assert.NotNull(opened);
        return opened;
    }

    private async Task<Card> CreateCardAsync(string accountNumber)
    {
        var cardRepository = depedencyFixture.DatabaseContext.Get<CardRepository>();
        var card = DataGenerators.CreateRandomCard(accountNumber);
        var pin = Random.Shared.Next(1000, 9000);
        var cvc = Random.Shared.Next(100, 900);
        card.PinHash = Hasher.Hash(pin.ToString());
        card.CvcKeyId = await depedencyFixture.KeyStore.WriteAsync(cvc.ToString());
        var created = await cardRepository.OpenAsync(card);
        Assert.NotNull(created);
        return created;
    }
    #endregion
}